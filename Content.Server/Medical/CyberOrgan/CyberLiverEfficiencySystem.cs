// SPDX-FileCopyrightText: 2025 terkala <appleorange64@gmail.com>
//
// SPDX-License-Identifier: MIT

using Content.Shared.Body.Organ;
using Content.Shared.Medical.CyberOrgan;
using Content.Shared._Shitmed.Body.Organ;
using Content.Server.Body.Components;
using Content.Server.EntityEffects.Effects;
using Content.Shared.EntityEffects;
using Content.Shared.Chemistry.Reagent;
using Content.Shared.Drunk;
using Content.Shared.Body.Systems;

namespace Content.Server.Medical.CyberOrgan;

/// <summary>
/// System that handles cyber-liver efficiency effects: inverse alcohol processing and drunk threshold multiplier.
/// </summary>
public sealed class CyberLiverEfficiencySystem : EntitySystem
{
    [Dependency] private readonly CyberOrganEfficiencySystem _organEfficiency = default!;
    [Dependency] private readonly SharedBodySystem _body = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<EntityEffectReagentArgs>(OnReagentEffect, before: new[] { typeof(HealthChange) });
        SubscribeLocalEvent<DrunkComponent, ComponentStartup>(OnDrunkStartup);
    }

    private void OnDrunkStartup(EntityUid uid, DrunkComponent component, ComponentStartup args)
    {
        // Apply drunk threshold multiplier when drunk effect is applied
        ApplyDrunkThresholdMultiplier(uid);
    }

    /// <summary>
    /// Intercepts reagent effects to apply inverse alcohol multiplier.
    /// </summary>
    private void OnReagentEffect(ref EntityEffectReagentArgs args)
    {
        // Check if this is being processed by a liver
        if (args.OrganEntity == null || !TryComp<MetabolizerComponent>(args.OrganEntity, out var metabolizer))
            return;

        if (!HasComp<LiverComponent>(args.OrganEntity))
            return;

        // Get liver efficiency
        if (!TryComp<CyberOrganEfficiencyComponent>(args.OrganEntity, out var efficiency))
            return;

        var finalEfficiency = _organEfficiency.GetFinalEfficiency(args.OrganEntity.Value, efficiency);

        // Check if this is alcohol
        var reagent = args.Reagent;
        if (reagent == null)
            return;

        bool isAlcohol = reagent.Metabolisms?.ContainsKey("Alcohol") ?? false;

        if (isAlcohol)
        {
            // Inverse multiplier: higher efficiency = less alcohol added
            // Example: 150% efficiency, 5u ethanol → 2.5u added (5 / 1.5)
            args.Scale /= finalEfficiency;
        }
    }

    /// <summary>
    /// Applies drunk threshold multiplier based on liver efficiency.
    /// Higher efficiency = harder to get drunk (need more alcohol to reach same level).
    /// </summary>
    private void ApplyDrunkThresholdMultiplier(EntityUid body)
    {
        // Find the liver organ
        var liverOrgans = _body.GetBodyOrganEntityComps<LiverComponent>(body);
        if (liverOrgans.Count == 0)
            return;

        var liver = liverOrgans[0].Owner;
        if (!TryComp<CyberOrganEfficiencyComponent>(liver, out var efficiency))
            return;

        var finalEfficiency = _organEfficiency.GetFinalEfficiency(liver, efficiency);

        // The drunk threshold is effectively multiplied by efficiency
        // This means you need more alcohol to get drunk (the effect is reduced)
        // We apply this by modifying the drunk component's time scaling
        // Since we can't directly modify the threshold, we'll reduce the boozePower when applying
        // This is handled in OnReagentEffect by dividing the scale
    }

    /// <summary>
    /// Gets the drunk threshold multiplier based on liver efficiency.
    /// Higher efficiency = harder to get drunk.
    /// </summary>
    public float GetDrunkThresholdMultiplier(EntityUid body)
    {
        var liverOrgans = _body.GetBodyOrganEntityComps<LiverComponent>(body);
        if (liverOrgans.Count == 0)
            return 1.0f;

        var liver = liverOrgans[0].Owner;
        if (!TryComp<CyberOrganEfficiencyComponent>(liver, out var efficiency))
            return 1.0f;

        var finalEfficiency = _organEfficiency.GetFinalEfficiency(liver, efficiency);
        return finalEfficiency;
    }
}

