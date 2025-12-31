// SPDX-FileCopyrightText: 2025 terkala <appleorange64@gmail.com>
//
// SPDX-License-Identifier: MIT

using Content.Shared.Body.Components;
using Content.Shared.Body.Part;
using Content.Shared.Body.Systems;
using Content.Shared.Medical.CyberLimb;
using Content.Shared.Medical.CyberLimb.Modules;
using Content.Shared.Medical.Integrity;
using Content.Shared._Shitmed.Cybernetics;
using Robust.Shared.Containers;
using Robust.Shared.Timing;

namespace Content.Server.Medical.CyberLimb;

/// <summary>
/// System that manages cyber limb stats (battery shared, service time per-limb, efficiency) for bodies.
/// Uses performance optimizations: infrequent updates (1 second intervals), cached calculations.
/// Battery is shared across all limbs, but service time is tracked per-limb.
/// </summary>
public sealed class CyberLimbStatsSystem : EntitySystem
{
    [Dependency] private readonly SharedBodySystem _body = default!;
    [Dependency] private readonly CyberLimbLowPowerModeSystem _lowPowerMode = default!;
    [Dependency] private readonly IGameTiming _timing = default!;

    private const float BatteryUpdateInterval = 1.0f; // Update every 1 second instead of every tick
    private const float ServiceTimeUpdateInterval = 1.0f; // Update every 1 second
    private const float BatteryDrainRatePerSecond = 1.0f / 1200.0f; // 20 minutes = 1200 seconds, so drain 1/1200th per second
    private const float ServiceTimePerMatterBin = 600f; // 10 minutes = 600 seconds

    public override void Initialize()
    {
        base.Initialize();

        // Note: ComponentStartup and container event subscriptions moved to CyberLimbStorageSystem to avoid duplicates
        // BodyComponent ComponentStartup subscription removed to avoid duplicates with IntegritySystem and LimbCapabilitiesSystem
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var curTime = _timing.CurTime;

        // Process body-level battery updates
        var query = EntityQueryEnumerator<CyberLimbStatsComponent, BodyComponent>();
        while (query.MoveNext(out var uid, out var stats, out var bodyComp))
        {
            // Recalculate averaged battery stats if needed
            if (stats.NeedsRecalculation)
            {
                RecalculateAveragedStats(uid, stats, bodyComp);
            }

            // Battery drain - update every 1 second, not every tick
            if (stats.NeedsBatteryUpdate &&
                (curTime - stats.LastBatteryUpdate).TotalSeconds >= BatteryUpdateInterval)
            {
                UpdateBatteryDrain(uid, stats);
                stats.LastBatteryUpdate = curTime;

                // If battery is full or empty and not changing, stop updating
                if (stats.CurrentBatteryCharge <= 0f ||
                    stats.CurrentBatteryCharge >= stats.CachedAverageBatteryCapacity)
                {
                    stats.NeedsBatteryUpdate = false;
                }
            }

            // Update efficiency penalty if battery state changed
            UpdateBatteryEfficiencyPenalty(uid, stats);
        }

        // Service time expiration is now handled via IntegrityComponent tracking
        // We only update service time countdown here, not check for expiration
        var limbQuery = EntityQueryEnumerator<CyberLimbStorageComponent>();
        while (limbQuery.MoveNext(out var uid, out var storage))
        {
            // Update service time countdown every 1 second if needed
            if (storage.NeedsServiceTimeUpdate &&
                storage.MaxServiceTime > 0f &&
                storage.ServiceTimeRemaining > 0f &&
                (curTime - storage.LastServiceTimeUpdate).TotalSeconds >= ServiceTimeUpdateInterval)
            {
                UpdateLimbServiceTime(uid, storage);
                storage.LastServiceTimeUpdate = curTime;
                
                // Update the next expiration time in integrity component
                if (TryComp<BodyPartComponent>(uid, out var part) && part.Body != null)
                {
                    UpdateNextServiceTimeExpiration(part.Body.Value);
                }
            }
        }
    }

    /// <summary>
    /// Called by CyberLimbStorageSystem when a cyber limb storage component starts up.
    /// </summary>
    public void OnCyberLimbStartup(EntityUid uid, CyberLimbStorageComponent component)
    {
        // Initialize service time for this limb when component is first added
        RecalculateLimbServiceTime(uid, component);
        
        // Update next expiration time when a new cyber-limb is added
        if (TryComp<BodyPartComponent>(uid, out var part) && part.Body != null)
        {
            var body = part.Body.Value;
            
            // Initialize stats component on body if not present (lazy initialization)
            if (!HasComp<CyberLimbStatsComponent>(body))
            {
                var stats = EnsureComp<CyberLimbStatsComponent>(body);
                stats.NeedsRecalculation = true;
                Dirty(body, stats);
            }
            
            // Ensure low power mode component exists on the body
            if (!HasComp<CyberLimbLowPowerModeComponent>(body))
            {
                var lowPower = EnsureComp<CyberLimbLowPowerModeComponent>(body);
                lowPower.LastActivityTime = _timing.CurTime; // Start as active
                Dirty(body, lowPower);
            }
            
            UpdateNextServiceTimeExpiration(body);
        }
    }

    /// <summary>
    /// Called by CyberLimbStorageSystem when storage changes.
    /// </summary>
    public void OnLimbStorageChanged(EntityUid uid, CyberLimbStorageComponent component, ref EntInsertedIntoContainerMessage args)
    {
        // When a cyber limb's storage changes:
        // 1. Recalculate body's battery stats
        // 2. Recalculate this limb's service time
        if (TryComp<BodyPartComponent>(uid, out var part) && part.Body != null)
        {
            if (TryComp<CyberLimbStatsComponent>(part.Body.Value, out var stats))
            {
                stats.NeedsRecalculation = true;
                Dirty(part.Body.Value, stats);
            }
        }

        // Recalculate this limb's service time based on matter bins
        RecalculateLimbServiceTime(uid, component);
    }

    /// <summary>
    /// Called by CyberLimbStorageSystem when storage changes.
    /// </summary>
    public void OnLimbStorageChanged(EntityUid uid, CyberLimbStorageComponent component, ref EntRemovedFromContainerMessage args)
    {
        // When a cyber limb's storage changes:
        // 1. Recalculate body's battery stats
        // 2. Recalculate this limb's service time
        if (TryComp<BodyPartComponent>(uid, out var part) && part.Body != null)
        {
            if (TryComp<CyberLimbStatsComponent>(part.Body.Value, out var stats))
            {
                stats.NeedsRecalculation = true;
                Dirty(part.Body.Value, stats);
            }
        }

        // Recalculate this limb's service time based on matter bins
        RecalculateLimbServiceTime(uid, component);
        
        // Update next expiration time when limb storage changes
        if (part != null && part.Body != null)
        {
            UpdateNextServiceTimeExpiration(part.Body.Value);
        }
    }

    /// <summary>
    /// Recalculates averaged battery capacity across all cyber limbs on a body.
    /// Service time is now tracked per-limb, not on the body.
    /// </summary>
    private void RecalculateAveragedStats(EntityUid body, CyberLimbStatsComponent stats, BodyComponent bodyComp)
    {
        var cyberLimbs = new List<(EntityUid, CyberLimbStorageComponent)>();
        float totalBattery = 0f;

        // Find all cyber limbs
        var allParts = _body.GetBodyChildren(body, bodyComp);
        foreach (var (partUid, _) in allParts)
        {
            // Check if this is a cyber limb (has CyberneticsComponent)
            if (!HasComp<CyberneticsComponent>(partUid))
                continue;

            if (TryComp<CyberLimbStorageComponent>(partUid, out var storage))
            {
                cyberLimbs.Add((partUid, storage));
                totalBattery += storage.CachedBatteryCapacity;
            }
        }

        if (cyberLimbs.Count > 0)
        {
            stats.CachedAverageBatteryCapacity = totalBattery / cyberLimbs.Count;

            // Initialize battery charge if not set
            if (stats.CurrentBatteryCharge == 0f && stats.CachedAverageBatteryCapacity > 0f)
            {
                stats.CurrentBatteryCharge = stats.CachedAverageBatteryCapacity;
                stats.NeedsBatteryUpdate = true;
            }
        }
        else
        {
            stats.CachedAverageBatteryCapacity = 0f;
            stats.CurrentBatteryCharge = 0f;
        }

        stats.NeedsRecalculation = false;
        Dirty(body, stats);
    }

    /// <summary>
    /// Recalculates service time for a specific limb based on its matter bin modules.
    /// </summary>
    private void RecalculateLimbServiceTime(EntityUid limb, CyberLimbStorageComponent storage)
    {
        var maxServiceTime = storage.CachedMatterBinCount * ServiceTimePerMatterBin;
        var oldMaxServiceTime = storage.MaxServiceTime;
        storage.MaxServiceTime = maxServiceTime;

        // If max service time changed, adjust current service time proportionally
        if (oldMaxServiceTime > 0f && maxServiceTime > 0f && storage.ServiceTimeRemaining > 0f)
        {
            // Scale service time proportionally to new max
            storage.ServiceTimeRemaining = (storage.ServiceTimeRemaining / oldMaxServiceTime) * maxServiceTime;
        }
        // Initialize service time if not set and we have matter bins
        else if (storage.ServiceTimeRemaining == 0f && maxServiceTime > 0f)
        {
            storage.ServiceTimeRemaining = maxServiceTime;
            storage.NeedsServiceTimeUpdate = true;
            storage.LastServiceTimeUpdate = _timing.CurTime;
        }
        else if (maxServiceTime == 0f)
        {
            storage.ServiceTimeRemaining = 0f;
            storage.NeedsServiceTimeUpdate = false;
        }

        Dirty(limb, storage);
        
        // Update next expiration time when service time is recalculated
        if (TryComp<BodyPartComponent>(limb, out var part) && part.Body != null)
        {
            UpdateNextServiceTimeExpiration(part.Body.Value);
        }
    }

    /// <summary>
    /// Updates battery drain. Battery drains at a rate of (average_capacity / 20_minutes) per second.
    /// In low power mode, drains at 50% rate.
    /// </summary>
    private void UpdateBatteryDrain(EntityUid body, CyberLimbStatsComponent stats)
    {
        if (stats.CachedAverageBatteryCapacity <= 0f)
            return;

        // Get power consumption multiplier (0.5 in low power mode, 1.0 otherwise)
        var powerMultiplier = _lowPowerMode.GetPowerConsumptionMultiplier(body);

        // Drain battery: (capacity / 20_minutes) per second, reduced in low power mode
        var drainAmount = stats.CachedAverageBatteryCapacity * BatteryDrainRatePerSecond * BatteryUpdateInterval * powerMultiplier;
        stats.CurrentBatteryCharge = Math.Max(0f, stats.CurrentBatteryCharge - drainAmount);

        Dirty(body, stats);
    }

    /// <summary>
    /// Updates service time countdown for a specific limb.
    /// In low power mode, accumulates at 50% rate.
    /// </summary>
    private void UpdateLimbServiceTime(EntityUid limb, CyberLimbStorageComponent storage)
    {
        if (storage.MaxServiceTime <= 0f)
            return;

        // Get maintenance time multiplier (0.5 in low power mode, 1.0 otherwise)
        float maintenanceMultiplier = 1.0f;
        if (TryComp<BodyPartComponent>(limb, out var part) && part.Body != null)
        {
            maintenanceMultiplier = _lowPowerMode.GetMaintenanceTimeMultiplier(part.Body.Value);
        }

        // Countdown service time, reduced in low power mode
        storage.ServiceTimeRemaining = Math.Max(0f, storage.ServiceTimeRemaining - ServiceTimeUpdateInterval * maintenanceMultiplier);

        Dirty(limb, storage);
    }

    /// <summary>
    /// Updates efficiency penalty based on battery state (shared across all limbs).
    /// </summary>
    private void UpdateBatteryEfficiencyPenalty(EntityUid body, CyberLimbStatsComponent stats)
    {
        bool wasDepleted = stats.IsBatteryDepleted;

        stats.IsBatteryDepleted = stats.CurrentBatteryCharge <= 0f;

        // Only update if state changed
        if (wasDepleted != stats.IsBatteryDepleted)
        {
            stats.CachedEfficiencyPenalty = stats.IsBatteryDepleted ? 0.5f : 1.0f;
            Dirty(body, stats);

            // Recalculate final efficiency for all limbs
            RecalculateAllLimbEfficiency(body);
        }
    }

    /// <summary>
    /// Updates the next service time expiration in the integrity component.
    /// This tracks when the next cyber-limb service time will expire.
    /// </summary>
    public void UpdateNextServiceTimeExpiration(EntityUid body)
    {
        if (!TryComp<IntegrityComponent>(body, out var integrity))
            return;

        if (!TryComp<BodyComponent>(body, out var bodyComp))
        {
            integrity.NextServiceTimeExpiration = null;
            integrity.NextServiceTimeExpirationTime = TimeSpan.Zero;
            Dirty(body, integrity);
            return;
        }

        float? nextExpiration = null;
        var curTime = _timing.CurTime;

        // Find the earliest service time expiration from all cyber-limbs
        var allParts = _body.GetBodyChildren(body, bodyComp);
        foreach (var (partUid, _) in allParts)
        {
            // Check if this is a cyber limb
            if (!HasComp<CyberneticsComponent>(partUid))
                continue;

            if (TryComp<CyberLimbStorageComponent>(partUid, out var storage))
            {
                // Only consider limbs that have service time and haven't expired yet
                if (storage.ServiceTimeRemaining > 0f && storage.MaxServiceTime > 0f)
                {
                    // Calculate when this limb's service time will expire
                    // Service time drains at 1 second per second, so expiration is in ServiceTimeRemaining seconds
                    var expirationInSeconds = storage.ServiceTimeRemaining;
                    
                    if (nextExpiration == null || expirationInSeconds < nextExpiration.Value)
                    {
                        nextExpiration = expirationInSeconds;
                    }
                }
            }
        }

        integrity.NextServiceTimeExpiration = nextExpiration;
        integrity.NextServiceTimeExpirationTime = nextExpiration.HasValue 
            ? curTime + TimeSpan.FromSeconds(nextExpiration.Value)
            : TimeSpan.Zero;
        Dirty(body, integrity);
    }

    /// <summary>
    /// Checks all cyber-limbs for expired service times and applies penalties.
    /// Called when the next service time expiration is reached.
    /// </summary>
    public void CheckAndApplyServiceTimeExpirations(EntityUid body)
    {
        if (!TryComp<BodyComponent>(body, out var bodyComp))
            return;

        var allParts = _body.GetBodyChildren(body, bodyComp);
        foreach (var (partUid, _) in allParts)
        {
            // Check if this is a cyber limb
            if (!HasComp<CyberneticsComponent>(partUid))
                continue;

            if (TryComp<CyberLimbStorageComponent>(partUid, out var storage))
            {
                // Check if service time has expired
                bool wasExpired = storage.IsServiceTimeExpired;
                storage.IsServiceTimeExpired = storage.ServiceTimeRemaining <= 0f;

                // If state changed, update efficiency
                if (wasExpired != storage.IsServiceTimeExpired)
                {
                    Dirty(partUid, storage);
                }
            }
        }

        // Recalculate the next expiration time after checking all limbs
        UpdateNextServiceTimeExpiration(body);
    }

    /// <summary>
    /// Recalculates final efficiency for all cyber limbs on a body.
    /// Final efficiency = (base + manipulator_bonus) * penalty_multiplier
    /// </summary>
    private void RecalculateAllLimbEfficiency(EntityUid body)
    {
        if (!TryComp<BodyComponent>(body, out var bodyComp))
            return;

        if (!TryComp<CyberLimbStatsComponent>(body, out var stats))
            return;

        var allParts = _body.GetBodyChildren(body, bodyComp);
        foreach (var (partUid, _) in allParts)
        {
            // Check if this is a cyber limb
            if (!HasComp<CyberneticsComponent>(partUid))
                continue;

            if (TryComp<CyberLimbStorageComponent>(partUid, out var storage))
            {
                // Efficiency is already cached in storage component
                // We just need to apply the penalty multiplier
                // This will be done when efficiency is actually used (in efficiency penalty application system)
            }
        }
    }
}

