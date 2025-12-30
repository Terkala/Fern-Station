// SPDX-FileCopyrightText: 2025 terkala <appleorange64@gmail.com>
//
// SPDX-License-Identifier: MIT

using Content.Shared.Body.Organ;
using Content.Shared.Medical.CyberOrgan;
using Content.Shared.Medical.CyberOrgan.Modules;
using Content.Shared.Damage;
using Content.Shared.Damage.Components;
using Content.Shared.Damage.Prototypes;
using Content.Server.Body.Systems;
using Robust.Shared.Timing;

namespace Content.Server.Medical.CyberOrgan;

/// <summary>
/// System that handles cyber-kidney efficiency effects: poison cure/accumulation and radiation filtering.
/// </summary>
public sealed class CyberKidneyEfficiencySystem : EntitySystem
{
    [Dependency] private readonly CyberOrganEfficiencySystem _organEfficiency = default!;
    [Dependency] private readonly SharedBodySystem _body = default!;
    [Dependency] private readonly DamageableSystem _damageable = default!;
    [Dependency] private readonly IGameTiming _timing = default!;

    private const float PoisonCureRatePer10Percent = 0.1f; // 0.1 damage/sec per 10% over 100%
    private const float PoisonAccumulationRatePer10Percent = 0.05f; // 0.05 damage/sec per 10% under 100%
    private const float RadiationRemovalRatePer10Percent = 0.01f; // 0.01u/second per 10% efficiency above 0%
    private const float KidneyUpdateInterval = 1.0f; // Update every 1 second instead of every tick

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<CyberOrganEfficiencyComponent, ComponentStartup>(OnEfficiencyStartup);
    }

    private void OnEfficiencyStartup(EntityUid uid, CyberOrganEfficiencyComponent component, ComponentStartup args)
    {
        // Kidneys don't need special startup, they work continuously
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var curTime = _timing.CurTime;

        // Only query organs with CyberOrganEfficiencyComponent (cyber-organs only)
        // This avoids querying all 90 bodies and filtering
        var query = EntityQueryEnumerator<CyberOrganEfficiencyComponent, OrganComponent>();
        var processedBodies = new HashSet<EntityUid>();

        while (query.MoveNext(out var organUid, out var efficiency, out var organ))
        {
            // Only process kidneys
            if (organ.SlotId != "kidneys" || organ.Body == null)
                continue;

            var body = organ.Body.Value;

            // Skip if we've already processed this body this tick
            if (processedBodies.Contains(body))
                continue;

            // Throttle updates to 1-second intervals
            if (!TryComp<CyberKidneyUpdateTrackerComponent>(body, out var tracker))
            {
                tracker = EnsureComp<CyberKidneyUpdateTrackerComponent>(body);
                tracker.LastUpdate = curTime;
            }

            // Check if enough time has passed since last update
            if ((curTime - tracker.LastUpdate).TotalSeconds < KidneyUpdateInterval)
                continue;

            tracker.LastUpdate = curTime;
            processedBodies.Add(body);

            // Get body component for processing
            if (!TryComp<BodyComponent>(body, out var bodyComp))
                continue;

            ProcessKidneyEffects(body, bodyComp, frameTime);
        }
    }

    /// <summary>
    /// Processes kidney efficiency effects: poison cure/accumulation and radiation removal.
    /// </summary>
    private void ProcessKidneyEffects(EntityUid body, BodyComponent bodyComp, float frameTime)
    {
        // Find cyber-kidneys for this body
        var allOrgans = _body.GetBodyOrgans(body, bodyComp);
        var kidneyOrgans = new List<(EntityUid Id, OrganComponent Organ)>();
        foreach (var (organId, organ) in allOrgans)
        {
            if (organ.SlotId == "kidneys" && HasComp<CyberOrganEfficiencyComponent>(organId))
            {
                kidneyOrgans.Add((organId, organ));
            }
        }
        if (kidneyOrgans.Count == 0)
            return;

        // Process each kidney (usually just one, but handle multiple)
        foreach (var (kidneyUid, organ) in kidneyOrgans)
        {
            if (!TryComp<CyberOrganEfficiencyComponent>(kidneyUid, out var efficiency))
                continue;

            var finalEfficiency = _organEfficiency.GetFinalEfficiency(kidneyUid, efficiency);

            // Process poison cure/accumulation (only once per body, not per kidney)
            if (kidneyOrgans.IndexOf((kidneyUid, organ)) == 0)
            {
                ProcessPoisonEffects(body, finalEfficiency, KidneyUpdateInterval); // Use interval (1 second) for per-second rates
            }

            // Process radiation removal if module is present
            if (HasRadiationFilterModule(kidneyUid))
            {
                ProcessRadiationRemoval(body, finalEfficiency, KidneyUpdateInterval); // Use interval (1 second) for per-second rates
            }
        }
    }

    /// <summary>
    /// Processes poison cure (efficiency > 100%) or accumulation (efficiency < 100%).
    /// </summary>
    private void ProcessPoisonEffects(EntityUid body, float efficiency, float frameTime)
    {
        if (!TryComp<DamageableComponent>(body, out var damageable))
            return;

        if (efficiency > 1.0f)
        {
            // Cure poison: 0.1 damage/sec per 10% over 100%
            var over100Percent = (efficiency - 1.0f) / 0.1f; // Convert to "per 10%" units
            var cureRate = PoisonCureRatePer10Percent * over100Percent;
            var cureAmount = cureRate * frameTime;

            // Heal poison damage
            var poisonDamage = new DamageSpecifier();
            poisonDamage.DamageDict.Add("Poison", -cureAmount);
            _damageable.TryChangeDamage(body, poisonDamage, true);
        }
        else if (efficiency < 1.0f)
        {
            // Accumulate poison: 0.05 damage/sec per 10% under 100%
            var under100Percent = (1.0f - efficiency) / 0.1f; // Convert to "per 10%" units
            var accumulationRate = PoisonAccumulationRatePer10Percent * under100Percent;
            var accumulationAmount = accumulationRate * frameTime;

            // Apply poison damage
            var poisonDamage = new DamageSpecifier();
            poisonDamage.DamageDict.Add("Poison", accumulationAmount);
            _damageable.TryChangeDamage(body, poisonDamage, true);
        }
    }

    /// <summary>
    /// Processes radiation removal if radiation filter module is present.
    /// </summary>
    private void ProcessRadiationRemoval(EntityUid body, float efficiency, float frameTime)
    {
        // Check if body has radiation component
        // This would need to integrate with the radiation system
        // For now, we'll just calculate the removal rate
        var removalRate = RadiationRemovalRatePer10Percent * (efficiency / 0.1f); // Per 10% efficiency above 0%
        var removalAmount = removalRate * frameTime;

        // TODO: Integrate with radiation system to actually remove radiation
        // This would require checking what radiation system exists
    }

    /// <summary>
    /// Checks if a kidney has a radiation filter module.
    /// </summary>
    private bool HasRadiationFilterModule(EntityUid kidneyUid)
    {
        if (!TryComp<StorageComponent>(kidneyUid, out var storage))
            return false;

        foreach (var item in storage.Container.ContainedEntities)
        {
            if (HasComp<CyberKidneyRadiationFilterModuleComponent>(item))
                return true;
        }

        return false;
    }
}

