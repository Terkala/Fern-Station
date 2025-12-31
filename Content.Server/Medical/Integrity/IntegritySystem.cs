// SPDX-FileCopyrightText: 2025 terkala <appleorange64@gmail.com>
//
// SPDX-License-Identifier: MIT

using Content.Server.Body.Systems;
using Content.Server.Medical.CyberLimb;
using Content.Shared.Body.Components;
using Content.Shared.Body.Part;
using Content.Shared.FixedPoint;
using Content.Shared.Medical.Integrity;
using Content.Shared.Medical.Surgery;
using Content.Server.Medical.Surgery;
using Content.Shared.Humanoid;
using Content.Shared.Humanoid.Prototypes;
using Content.Shared.Damage;
using Content.Shared.Damage.Systems;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Server.Medical.Integrity;

/// <summary>
/// Server-side integrity system that handles max health reduction and initialization.
/// </summary>
public sealed class IntegritySystem : SharedIntegritySystem
{
    [Dependency] private readonly BodySystem _body = default!;
    [Dependency] private readonly IPrototypeManager _prototypes = default!;
    [Dependency] private readonly DamageableSystem _damageable = default!;
    [Dependency] private readonly CyberLimbStatsSystem _cyberLimbStats = default!;
    [Dependency] private readonly IGameTiming _timing = default!;

    private const float BioRejectionChangeRate = 0.2f; // Per tick

    public override void Initialize()
    {
        base.Initialize();
        // Note: BodyComponent, ComponentStartup subscription moved to LimbCapabilitiesSystem to avoid duplicates
        SubscribeLocalEvent<IntegrityComponent, IntegrityUsageChangedEvent>(OnIntegrityUsageChanged);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        // Gradually adjust bio-rejection toward target
        // Only process entities that need updates (NeedsUpdate flag)
        var query = EntityQueryEnumerator<IntegrityComponent, DamageableComponent>();
        while (query.MoveNext(out var uid, out var integrity, out var damageable))
        {
            // Early exit if no update needed
            if (!integrity.NeedsUpdate)
                continue;

            UpdateBioRejection(uid, integrity, damageable);
        }

        // Gradually adjust surgery penalties toward target
        var penaltyQuery = EntityQueryEnumerator<SurgeryPenaltyComponent>();
        while (penaltyQuery.MoveNext(out var uid, out var penalty))
        {
            UpdateSurgeryPenalty(uid, penalty);
        }

        // Check for cyber-limb service time expirations
        var curTime = _timing.CurTime;
        var integrityQuery = EntityQueryEnumerator<IntegrityComponent>();
        while (integrityQuery.MoveNext(out var uid, out var integrity))
        {
            // Check if we've reached the next service time expiration
            if (integrity.NextServiceTimeExpiration.HasValue &&
                integrity.NextServiceTimeExpirationTime != TimeSpan.Zero &&
                curTime >= integrity.NextServiceTimeExpirationTime)
            {
                // Check all limbs for expired service times and apply penalties
                _cyberLimbStats.CheckAndApplyServiceTimeExpirations(uid);
            }
        }
    }

    /// <summary>
    /// Called by LimbCapabilitiesSystem when a body component starts up.
    /// </summary>
    public void OnBodyStartup(EntityUid uid, BodyComponent component, ComponentStartup args)
    {
        // Initialize integrity component if not present
        if (!HasComp<IntegrityComponent>(uid))
        {
            var integrity = EnsureComp<IntegrityComponent>(uid);
            
            // Set max integrity based on body prototype
            // Default is 6, dwarves get 8
            if (component.Prototype != null)
            {
                integrity.MaxIntegrity = component.Prototype.Value == "Dwarf" ? 8 : 6;
            }
            else
            {
                integrity.MaxIntegrity = 6;
            }

            // Initialize cached surgery penalty
            UpdateCachedSurgeryPenalty(uid, integrity);

            Dirty(uid, integrity);
        }
        else
        {
            // Update cached surgery penalty for existing integrity component
            if (TryComp<IntegrityComponent>(uid, out var integrity))
            {
                UpdateCachedSurgeryPenalty(uid, integrity);
            }
        }
    }

    private void OnIntegrityUsageChanged(EntityUid uid, IntegrityComponent component, ref IntegrityUsageChangedEvent args)
    {
        UpdateTargetBioRejection(uid, component);
    }

    /// <summary>
    /// Updates the target bio-rejection based on current integrity usage.
    /// The actual bio-rejection will gradually adjust toward this target.
    /// </summary>
    private void UpdateTargetBioRejection(EntityUid uid, IntegrityComponent component)
    {
        // Calculate effective max integrity (base + temporary bonus from immunosuppressants)
        var effectiveMaxIntegrity = FixedPoint2.New(component.MaxIntegrity) + component.TemporaryIntegrityBonus;
        
        // Calculate over limit
        var overLimit = component.UsedIntegrity - effectiveMaxIntegrity;
        if (overLimit < 0)
            overLimit = FixedPoint2.Zero;

        // Target bio-rejection = (used - effectiveMax) * bioRejectionPerPoint
        var targetBioRejection = overLimit * component.BioRejectionPerPoint;

        component.TargetBioRejection = targetBioRejection;
        Dirty(uid, component);
    }

    /// <summary>
    /// Gradually adjusts current bio-rejection toward target at 0.2 per tick.
    /// </summary>
    private void UpdateBioRejection(EntityUid uid, IntegrityComponent integrity, DamageableComponent damageable)
    {
        var target = integrity.TargetBioRejection;
        var current = integrity.CurrentBioRejection;

        // If already at target, no change needed - mark as not needing updates
        if (current == target)
        {
            integrity.NeedsUpdate = false;
            return;
        }

        // Calculate change needed
        var difference = target - current;
        var change = FixedPoint2.New(Math.Sign((float)difference) * Math.Min(Math.Abs((float)difference), BioRejectionChangeRate));

        if (change == FixedPoint2.Zero)
        {
            integrity.NeedsUpdate = false;
            return;
        }

        // Apply bio-rejection damage change
        var damageSpec = new DamageSpecifier();
        damageSpec.DamageDict["BioRejection"] = change;

        _damageable.TryChangeDamage(uid, damageSpec, ignoreResistances: true);

        // Update current bio-rejection
        integrity.CurrentBioRejection += change;
        integrity.NeedsUpdate = true; // Keep updating until at target
        Dirty(uid, integrity);
    }

    /// <summary>
    /// Gradually adjusts surgery penalty toward target at 0.2 per tick.
    /// Surgery penalties contribute directly to bio-rejection damage.
    /// </summary>
    private void UpdateSurgeryPenalty(EntityUid bodyPart, SurgeryPenaltyComponent penalty)
    {
        var target = penalty.TargetPenalty;
        var current = penalty.CurrentPenalty;

        // If already at target, no change needed - early exit
        // Don't call expensive operations when nothing changed
        if (current == target)
            return;

        // Calculate change needed
        var difference = target - current;
        var change = FixedPoint2.New(Math.Sign((float)difference) * Math.Min(Math.Abs((float)difference), BioRejectionChangeRate));

        if (change == FixedPoint2.Zero)
            return;

        // Update current penalty (this contributes directly to bio-rejection)
        penalty.CurrentPenalty += change;
        Dirty(bodyPart, penalty);

        // Recalculate bio-rejection for the body (surgery penalty is included in the calculation)
        // Update cached penalty and mark integrity as needing update
        if (TryComp<BodyPartComponent>(bodyPart, out var part) && part.Body != null)
        {
            if (TryComp<IntegrityComponent>(part.Body.Value, out var integrity))
            {
                // Update cached surgery penalty instead of recalculating every time
                UpdateCachedSurgeryPenalty(part.Body.Value, integrity);
                integrity.NeedsUpdate = true; // Mark as needing update
                RecalculateTargetBioRejection(part.Body.Value, integrity);
            }
        }
    }

    /// <summary>
    /// Gets the total surgery penalty from all body parts.
    /// Uses cached value if available, otherwise calculates and caches.
    /// </summary>
    public new FixedPoint2 GetTotalSurgeryPenalty(EntityUid body)
    {
        if (!TryComp<IntegrityComponent>(body, out var integrity))
            return FixedPoint2.Zero;

        // Return cached value - avoids expensive iteration
        return integrity.CachedSurgeryPenalty;
    }

    /// <summary>
    /// Updates the cached surgery penalty total for a body.
    /// Call this when surgery penalties change.
    /// </summary>
    public void UpdateCachedSurgeryPenalty(EntityUid body, IntegrityComponent? integrity = null)
    {
        if (!Resolve(body, ref integrity, logMissing: false))
            return;

        if (!TryComp<BodyComponent>(body, out var bodyComp))
        {
            integrity.CachedSurgeryPenalty = FixedPoint2.Zero;
            return;
        }

        FixedPoint2 totalPenalty = FixedPoint2.Zero;

        // Get all body parts and sum their penalties
        if (bodyComp.RootContainer.ContainedEntity != null)
        {
            var parts = _body.GetBodyPartChildren(bodyComp.RootContainer.ContainedEntity.Value);
            foreach (var (partUid, _) in parts)
            {
                if (TryComp<SurgeryPenaltyComponent>(partUid, out var penalty))
                {
                    totalPenalty += penalty.CurrentPenalty;
                }
                
                // Include unskilled surgery penalties
                if (TryComp<UnskilledSurgeryPenaltyComponent>(partUid, out var unskilledPenalty))
                {
                    totalPenalty += unskilledPenalty.Penalty;
                }
                
                // Include unskilled technician penalties
                if (TryComp<UnskilledTechnicianPenaltyComponent>(partUid, out var unskilledTechPenalty))
                {
                    totalPenalty += unskilledTechPenalty.Penalty;
                }
            }
        }

        // Include unsanitary conditions penalty from the body
        if (TryComp<UnsanitaryConditionsComponent>(body, out var unsanitary) && unsanitary.PenaltyApplied)
        {
            totalPenalty += unsanitary.Penalty;
        }

        integrity.CachedSurgeryPenalty = totalPenalty;
        Dirty(body, integrity);
    }
}

