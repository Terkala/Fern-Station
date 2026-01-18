// SPDX-FileCopyrightText: 2025 terkala <appleorange64@gmail.com>
//
// SPDX-License-Identifier: MIT

using Content.Shared.Body.Components;
using Content.Shared.Body.Part;
using Content.Shared.Body.Systems;
using Content.Shared.Medical.CyberLimb;
using Content.Shared.Medical.CyberLimb.Modules;
using Content.Shared.Medical.Surgery.Integrity;
using Content.Shared._Shitmed.Cybernetics;
using Content.Shared.Storage;
using Content.Shared.Implants.Components;
using Content.Server.Power.Components;
using Content.Server.Power.EntitySystems;
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
    [Dependency] private readonly BatterySystem _battery = default!;
    [Dependency] private readonly SharedCyberneticsFunctionalitySystem _cyberneticsFunctionality = default!;

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

                // Check if battery is full or empty and not changing, stop updating
                if (TryComp<BatteryComponent>(uid, out var battery))
                {
                    if (battery.CurrentCharge <= 0f || battery.CurrentCharge >= battery.MaxCharge)
                    {
                        stats.NeedsBatteryUpdate = false;
                    }
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
            
            // Ensure BatteryComponent exists on body (only added when cybernetics exist)
            EnsureComp<BatteryComponent>(body);
            
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
    /// Also sums efficiency modifiers from capacitors across all cybernetics for service time multiplier.
    /// Service time is now tracked per-limb, not on the body.
    /// </summary>
    private void RecalculateAveragedStats(EntityUid body, CyberLimbStatsComponent stats, BodyComponent bodyComp)
    {
        var cyberLimbs = new List<(EntityUid, CyberLimbStorageComponent)>();
        float totalBattery = 0f;
        float totalCapacitorEfficiencyModifierSum = 0f;

        // Find all cyber limbs and sum capacitor efficiency modifiers
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

            // Sum efficiency modifiers from capacitors in this cybernetic's storage
            if (TryComp<StorageComponent>(partUid, out var storageComp))
            {
                foreach (var item in storageComp.Container.ContainedEntities)
                {
                    if (TryComp<CyberLimbCapacitorModuleComponent>(item, out var capacitor))
                    {
                        totalCapacitorEfficiencyModifierSum += capacitor.EfficiencyModifier;
                    }
                }
            }
        }

        // Store capacitor efficiency modifier sum
        var oldCapacitorModifierSum = stats.CachedCapacitorEfficiencyModifierSum;
        stats.CachedCapacitorEfficiencyModifierSum = totalCapacitorEfficiencyModifierSum;
        var capacitorModifierSumChanged = Math.Abs(oldCapacitorModifierSum - totalCapacitorEfficiencyModifierSum) > 0.0001f;

        if (cyberLimbs.Count > 0)
        {
            stats.CachedAverageBatteryCapacity = totalBattery / cyberLimbs.Count;

            // Ensure BatteryComponent exists (cybernetics exist)
            var battery = EnsureComp<BatteryComponent>(body);
            
            // Calculate total capacity (sum of all battery modules)
            var totalCapacity = totalBattery;
            
            // Calculate base power draw from limbs: watts = (totalCapacity / BaselineDurationSeconds) * cyberneticsCount
            // Where BaselineDurationSeconds = 20 minutes (1200 seconds)
            var wattsPerCybernetic = totalCapacity / CyberneticsUpkeepComponent.BaselineDurationSeconds;
            var basePowerDraw = wattsPerCybernetic * cyberLimbs.Count;
            
            // Add power draw from cyber-implants
            float implantPowerDraw = 0f;
            if (TryComp<ImplantedComponent>(body, out var implanted))
            {
                foreach (var implantEntity in implanted.ImplantContainer.ContainedEntities)
                {
                    if (TryComp<CyberImplantPowerDrawComponent>(implantEntity, out var powerDraw))
                    {
                        implantPowerDraw += powerDraw.PowerDrawWatts;
                    }
                }
            }
            stats.CachedPowerDrawWatts = basePowerDraw + implantPowerDraw;
            
            // Add battery capacity from cyber-implants
            float implantBatteryCapacity = 0f;
            if (TryComp<ImplantedComponent>(body, out var implantedForBattery))
            {
                foreach (var implantEntity in implantedForBattery.ImplantContainer.ContainedEntities)
                {
                    if (TryComp<CyberImplantBatteryComponent>(implantEntity, out var implantBattery))
                    {
                        implantBatteryCapacity += implantBattery.MaxCharge;
                    }
                }
            }
            var totalCapacityWithImplants = totalCapacity + implantBatteryCapacity;
            
            // Update BatteryComponent max charge (including implant batteries)
            _battery.SetMaxCharge(body, totalCapacityWithImplants, battery);
            
            // After updating battery capacity, trigger re-evaluation of power-drawing modules
            _cyberneticsFunctionality.EvaluateAllCybernetics(body);
            
            // Initialize battery charge if not set and BatteryComponent is empty
            if (battery.CurrentCharge == 0f && totalCapacity > 0f)
            {
                _battery.SetCharge(body, totalCapacity, battery);
                stats.NeedsBatteryUpdate = true;
                stats.LastBatteryUpdate = _timing.CurTime;
            }
            else if (totalCapacity > 0f)
            {
                stats.NeedsBatteryUpdate = true;
                // Initialize LastBatteryUpdate if not set
                if (stats.LastBatteryUpdate == TimeSpan.Zero)
                {
                    stats.LastBatteryUpdate = _timing.CurTime;
                }
            }
        }
        else
        {
            // All cybernetics removed - clean up BatteryComponent
            RemComp<BatteryComponent>(body);
            stats.CachedAverageBatteryCapacity = 0f;
            stats.CachedPowerDrawWatts = 0f;
        }

        stats.NeedsRecalculation = false;
        Dirty(body, stats);

        // If capacitor modifier sum changed, recalculate service time for all cybernetics
        if (capacitorModifierSumChanged)
        {
            RecalculateAllLimbsServiceTime(body, bodyComp);
        }
    }

    /// <summary>
    /// Recalculates service time for a specific limb based on its matter bin modules.
    /// Applies capacitor multiplier from body stats (sum of efficiency modifiers from all capacitors).
    /// </summary>
    private void RecalculateLimbServiceTime(EntityUid limb, CyberLimbStorageComponent storage)
    {
        var baseMaxServiceTime = storage.CachedMatterBinCount * ServiceTimePerMatterBin;
        
        // Apply capacitor multiplier: 1.0 + sum of all capacitor efficiency modifiers
        float capacitorMultiplier = 1.0f;
        if (TryComp<BodyPartComponent>(limb, out var part) && part.Body != null)
        {
            if (TryComp<CyberLimbStatsComponent>(part.Body.Value, out var bodyStats))
            {
                capacitorMultiplier = 1.0f + bodyStats.CachedCapacitorEfficiencyModifierSum;
            }
        }
        
        var maxServiceTime = baseMaxServiceTime * capacitorMultiplier;
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
        if (part != null && part.Body != null)
        {
            UpdateNextServiceTimeExpiration(part.Body.Value);
        }
    }

    /// <summary>
    /// Recalculates service time for all cybernetics on a body.
    /// Called when capacitor count changes.
    /// </summary>
    private void RecalculateAllLimbsServiceTime(EntityUid body, BodyComponent bodyComp)
    {
        var allParts = _body.GetBodyChildren(body, bodyComp);
        foreach (var (partUid, _) in allParts)
        {
            // Check if this is a cybernetic
            if (!HasComp<CyberneticsComponent>(partUid))
                continue;

            if (TryComp<CyberLimbStorageComponent>(partUid, out var storage))
            {
                RecalculateLimbServiceTime(partUid, storage);
            }
        }
    }

    /// <summary>
    /// Updates battery drain using watt-based calculations.
    /// In low power mode, drains at 50% rate.
    /// </summary>
    private void UpdateBatteryDrain(EntityUid body, CyberLimbStatsComponent stats)
    {
        // Check BatteryComponent exists (defensive check)
        if (!TryComp<BatteryComponent>(body, out var battery))
            return;

        if (stats.CachedPowerDrawWatts <= 0f)
            return;

        // Get power consumption multiplier (0.5 in low power mode, 1.0 otherwise)
        var powerMultiplier = _lowPowerMode.GetPowerConsumptionMultiplier(body);

        // Calculate elapsed time since last update
        var elapsedSeconds = (_timing.CurTime - stats.LastBatteryUpdate).TotalSeconds;
        
        // Drain battery: watts * elapsedSeconds * powerMultiplier
        var drainJoules = stats.CachedPowerDrawWatts * (float)elapsedSeconds * powerMultiplier;
        _battery.UseCharge(body, drainJoules, battery);

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
        // Check BatteryComponent exists (defensive check)
        if (!TryComp<BatteryComponent>(body, out var battery))
            return; // No battery = no cybernetics = no penalty needed

        bool wasDepleted = stats.IsBatteryDepleted;

        stats.IsBatteryDepleted = battery.CurrentCharge <= 0f;

        // Only update if state changed
        if (wasDepleted != stats.IsBatteryDepleted)
        {
            stats.CachedEfficiencyPenalty = stats.IsBatteryDepleted ? 0.5f : 1.0f;
            Dirty(body, stats);

            // Recalculate final efficiency for all limbs
            RecalculateAllLimbEfficiency(body);

            // Re-evaluate power-drawing modules when battery depletion state changes
            var powerDrawSystem = EntitySystem.Get<CyberLimbPowerDrawSystem>();
            powerDrawSystem.EvaluateAllPowerDrawModules(body);
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

