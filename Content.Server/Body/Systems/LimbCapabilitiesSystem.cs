// SPDX-FileCopyrightText: 2025 terkala <appleorange64@gmail.com>
//
// SPDX-License-Identifier: MIT

using Content.Shared.Body.Components;
using Content.Shared.Body.Systems;
using Content.Shared.Damage;
using Content.Shared.Prying.Components;
using Content.Shared.Weapons.Melee;
using Robust.Shared.Containers;

namespace Content.Server.Body.Systems;

/// <summary>
/// System that aggregates limb capabilities (prying, melee damage) from body parts to the mob.
/// Updates when limbs are added/removed.
/// </summary>
public sealed class LimbCapabilitiesSystem : EntitySystem
{
    [Dependency] private readonly SharedBodySystem _body = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<BodyComponent, ComponentStartup>(OnBodyStartup);
        SubscribeLocalEvent<BodyComponent, EntInsertedIntoContainerMessage>(OnBodyPartInserted);
        SubscribeLocalEvent<BodyComponent, EntRemovedFromContainerMessage>(OnBodyPartRemoved);
        SubscribeLocalEvent<LimbCapabilitiesComponent, ComponentStartup>(OnLimbCapabilitiesStartup);
        SubscribeLocalEvent<LimbCapabilitiesComponent, ComponentShutdown>(OnLimbCapabilitiesShutdown);
    }

    private void OnBodyStartup(EntityUid uid, BodyComponent component, ComponentStartup args)
    {
        // Initialize aggregated capabilities on body startup
        EnsureComp<AggregatedLimbCapabilitiesComponent>(uid);
        RecalculateCapabilities(uid, component);
    }

    private void OnBodyPartInserted(EntityUid uid, BodyComponent component, ref EntInsertedIntoContainerMessage args)
    {
        // Only handle root container (body parts being added)
        if (args.Container.ID != BodyComponent.BodyRootContainerId)
            return;

        // Recalculate capabilities when a body part is added
        RecalculateCapabilities(uid, component);
    }

    private void OnBodyPartRemoved(EntityUid uid, BodyComponent component, ref EntRemovedFromContainerMessage args)
    {
        // Only handle root container (body parts being removed)
        if (args.Container.ID != BodyComponent.BodyRootContainerId)
            return;

        // Recalculate capabilities when a body part is removed
        RecalculateCapabilities(uid, component);
    }

    private void OnLimbCapabilitiesStartup(EntityUid uid, LimbCapabilitiesComponent component, ComponentStartup args)
    {
        // When a limb's capabilities are added, recalculate mob capabilities
        if (TryComp<BodyPartComponent>(uid, out var part) && part.Body != null)
        {
            if (TryComp<BodyComponent>(part.Body.Value, out var body))
            {
                RecalculateCapabilities(part.Body.Value, body);
            }
        }
    }

    private void OnLimbCapabilitiesShutdown(EntityUid uid, LimbCapabilitiesComponent component, ComponentShutdown args)
    {
        // When a limb's capabilities are removed, recalculate mob capabilities
        if (TryComp<BodyPartComponent>(uid, out var part) && part.Body != null)
        {
            if (TryComp<BodyComponent>(part.Body.Value, out var body))
            {
                RecalculateCapabilities(part.Body.Value, body);
            }
        }
    }

    /// <summary>
    /// Call this when a limb's capabilities change (e.g., from mutations or module changes).
    /// This triggers a recalculation of the mob's aggregated capabilities.
    /// </summary>
    public void OnLimbCapabilitiesChanged(EntityUid limb)
    {
        if (TryComp<BodyPartComponent>(limb, out var part) && part.Body != null)
        {
            if (TryComp<BodyComponent>(part.Body.Value, out var body))
            {
                RecalculateCapabilities(part.Body.Value, body);
            }
        }
    }

    /// <summary>
    /// Recalculates aggregated capabilities from all limbs and applies them to the mob.
    /// </summary>
    public void RecalculateCapabilities(EntityUid body, BodyComponent? bodyComp = null)
    {
        if (!Resolve(body, ref bodyComp, logMissing: false))
            return;

        if (!TryComp<AggregatedLimbCapabilitiesComponent>(body, out var aggregated))
        {
            aggregated = EnsureComp<AggregatedLimbCapabilitiesComponent>(body);
        }

        // Reset aggregated values
        aggregated.CanPry = false;
        aggregated.CanPryPowered = false;
        aggregated.CanPryForce = false;
        aggregated.BestPrySpeedModifier = 1.0f;
        aggregated.TotalMeleeDamage = new DamageSpecifier();
        aggregated.CombinedAttackRateModifier = 1.0f;

        // Find the best arm for melee damage (highest damage)
        EntityUid? bestArm = null;
        FixedPoint2 bestArmDamage = FixedPoint2.Zero;
        float bestArmAttackRate = 1.0f;

        // Aggregate capabilities from all body parts
        var allParts = _body.GetBodyPartChildren(body, bodyComp);
        foreach (var (partUid, partComp) in allParts)
        {
            if (!TryComp<LimbCapabilitiesComponent>(partUid, out var limbCaps))
                continue;

            // Aggregate prying capabilities (if any limb can pry, mob can pry)
            if (limbCaps.ProvidesPrying)
            {
                aggregated.CanPry = true;
                if (limbCaps.PryPowered)
                    aggregated.CanPryPowered = true;
                if (limbCaps.PryForce)
                    aggregated.CanPryForce = true;
                
                // Use best (lowest) speed modifier (faster = lower time = better)
                if (limbCaps.PrySpeedModifier < aggregated.BestPrySpeedModifier)
                    aggregated.BestPrySpeedModifier = limbCaps.PrySpeedModifier;
            }

            // For melee damage, only consider arms and find the one with highest damage
            if (partComp.PartType == BodyPartType.Arm && !limbCaps.MeleeDamage.Empty)
            {
                var armDamage = limbCaps.MeleeDamage.GetTotal();
                if (armDamage > bestArmDamage)
                {
                    bestArm = partUid;
                    bestArmDamage = armDamage;
                    bestArmAttackRate = limbCaps.AttackRateModifier;
                }
            }
        }

        // Use the best arm's damage and attack rate
        if (bestArm != null && TryComp<LimbCapabilitiesComponent>(bestArm.Value, out var bestArmCaps))
        {
            aggregated.TotalMeleeDamage = bestArmCaps.MeleeDamage;
            aggregated.CombinedAttackRateModifier = bestArmCaps.AttackRateModifier;
        }

        aggregated.NeedsRecalculation = false;
        Dirty(body, aggregated);

        // Apply aggregated capabilities to mob components
        ApplyCapabilitiesToMob(body, aggregated);
    }

    /// <summary>
    /// Applies aggregated capabilities to the mob's PryingComponent and MeleeWeaponComponent.
    /// </summary>
    private void ApplyCapabilitiesToMob(EntityUid body, AggregatedLimbCapabilitiesComponent aggregated)
    {
        // Update or create PryingComponent based on aggregated capabilities
        if (aggregated.CanPry)
        {
            var prying = EnsureComp<PryingComponent>(body);
            // Update prying capabilities from limbs
            // Limb capabilities override/add to intrinsic capabilities
            prying.PryPowered = aggregated.CanPryPowered || prying.PryPowered; // If limbs can pry powered, or intrinsic can
            prying.Force = aggregated.CanPryForce || prying.Force; // If limbs can force pry, or intrinsic can
            // Use best (fastest) speed modifier (lower = faster)
            if (aggregated.BestPrySpeedModifier < prying.SpeedModifier)
                prying.SpeedModifier = aggregated.BestPrySpeedModifier;
            prying.Enabled = true;
            Dirty(body, prying);
        }
        // Note: We don't remove PryingComponent if no limbs provide it,
        // as the mob might have intrinsic prying capabilities from its species/prototype.
        // The aggregated capabilities add to/override intrinsic capabilities when limbs provide them.

        // Handle MeleeWeaponComponent
        // We need to store base damage separately to avoid double-counting
        if (!aggregated.BaseMeleeDamageStored)
        {
            // Store base damage on first calculation
            if (TryComp<MeleeWeaponComponent>(body, out var existingMelee))
            {
                aggregated.BaseMeleeDamage = existingMelee.Damage;
                aggregated.BaseAttackRate = existingMelee.AttackRate;
            }
            else
            {
                aggregated.BaseMeleeDamage = new DamageSpecifier();
                aggregated.BaseAttackRate = 1.0f;
            }
            aggregated.BaseMeleeDamageStored = true;
            Dirty(body, aggregated);
        }

        // Calculate final damage: base + limb damage
        var finalDamage = DamageSpecifier.Combine(aggregated.BaseMeleeDamage, aggregated.TotalMeleeDamage);
        var finalAttackRate = aggregated.BaseAttackRate * aggregated.CombinedAttackRateModifier;

        // Only create/update MeleeWeaponComponent if we have damage or if it already exists
        if (!finalDamage.Empty || TryComp<MeleeWeaponComponent>(body, out _))
        {
            var melee = EnsureComp<MeleeWeaponComponent>(body);
            melee.Damage = finalDamage;
            melee.AttackRate = finalAttackRate;
            Dirty(body, melee);
        }
    }
}

