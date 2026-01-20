// SPDX-FileCopyrightText: 2025 terkala <appleorange64@gmail.com>
//
// SPDX-License-Identifier: MIT

using Content.Shared.Body.Components;
using Content.Shared.Body.Systems;
using Content.Shared.Implants.Components;
using Content.Shared.Medical.CyberLimb;
using Content.Shared.Medical.CyberLimb.Modules;
using Content.Shared.Power.Components;
using Content.Server.Power.Components;
using Content.Shared.Storage;
using Content.Shared.Tag;
using Content.Shared._Shitmed.Cybernetics;
using Robust.Shared.Containers;
using Robust.Shared.Prototypes;

namespace Content.Server.Medical.CyberLimb;

/// <summary>
/// System that evaluates power-drawing modules and disables them when battery is depleted.
/// Implements oscillation prevention by comparing total power draw vs total power generation.
/// </summary>
public sealed class CyberLimbPowerDrawSystem : EntitySystem
{
    [Dependency] private readonly SharedBodySystem _body = default!;
    [Dependency] private readonly TagSystem _tag = default!;
    
    private static readonly ProtoId<TagPrototype> DrawsPowerTag = "DrawsPower";
    private static readonly ProtoId<TagPrototype> PowerDepletedTag = "PowerDepleted";

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<EvaluatePowerDrawModulesEvent>(OnEvaluatePowerDrawModules);
    }

    private void OnEvaluatePowerDrawModules(ref EvaluatePowerDrawModulesEvent ev)
    {
        EvaluateAllPowerDrawModules(ev.Body);
    }

    /// <summary>
    /// Evaluates all power-drawing modules on a body and enables/disables them based on battery state and power draw vs generation.
    /// </summary>
    public void EvaluateAllPowerDrawModules(EntityUid body)
    {
        if (!TryComp<BodyComponent>(body, out var bodyComp))
            return;

        // Calculate total power draw from all modules with DrawsPower tag
        float totalPowerDraw = 0f;
        
        // Calculate total power generation from all self-recharging implants
        float totalPowerGeneration = 0f;

        // Get all cyber limbs on body
        var allParts = _body.GetBodyChildren(body, bodyComp);
        foreach (var (partUid, _) in allParts)
        {
            if (!HasComp<CyberneticsComponent>(partUid))
                continue;

            // Calculate power draw from modules in this limb
            if (TryComp<StorageComponent>(partUid, out var storage))
            {
                foreach (var moduleEntity in storage.Container.ContainedEntities)
                {
                    if (_tag.HasTag(moduleEntity, DrawsPowerTag))
                    {
                        if (TryComp<CyberLimbPowerDrawModuleComponent>(moduleEntity, out var powerDraw))
                        {
                            totalPowerDraw += powerDraw.PowerDrawWatts;
                        }
                    }
                }
            }
        }

        // Calculate total power generation from self-recharging implants
        if (TryComp<ImplantedComponent>(body, out var implanted))
        {
            foreach (var implantEntity in implanted.ImplantContainer.ContainedEntities)
            {
                // Check for CyberImplantSelfRechargerComponent
                if (TryComp<CyberImplantSelfRechargerComponent>(implantEntity, out var recharger))
                {
                    totalPowerGeneration += recharger.AutoRechargeRate;
                }
            }
        }

        // Get battery state
        bool isBatteryDepleted = false;
        if (TryComp<BatteryComponent>(body, out var battery))
        {
            isBatteryDepleted = battery.CurrentCharge <= 0f;
        }

        // Evaluate modules in each limb
        foreach (var (partUid, _) in allParts)
        {
            if (!HasComp<CyberneticsComponent>(partUid))
                continue;

            EvaluateLimbPowerDrawModules(partUid, body, totalPowerDraw, totalPowerGeneration, isBatteryDepleted);
        }
    }

    /// <summary>
    /// Evaluates power-drawing modules in a single limb and enables/disables them based on battery state and draw vs generation.
    /// </summary>
    private void EvaluateLimbPowerDrawModules(EntityUid limb, EntityUid body, float totalPowerDraw, float totalPowerGeneration, bool isBatteryDepleted)
    {
        if (!TryComp<StorageComponent>(limb, out var storage))
            return;

        foreach (var moduleEntity in storage.Container.ContainedEntities)
        {
            if (!_tag.HasTag(moduleEntity, DrawsPowerTag))
                continue;

            // Oscillation Prevention Logic:
            // If battery is depleted AND total draw > total generation: keep disabled
            // Else if battery is depleted AND total draw <= total generation: allow to charge (enable)
            // Else if battery is not depleted: enable
            bool shouldBeDisabled = false;

            if (isBatteryDepleted)
            {
                // If draw > generation, keep modules disabled to prevent oscillation
                if (totalPowerDraw > totalPowerGeneration)
                {
                    shouldBeDisabled = true;
                }
                // If draw <= generation, allow modules to work (they can charge the battery)
                else
                {
                    shouldBeDisabled = false;
                }
            }
            else
            {
                // Battery has charge, modules should be enabled
                shouldBeDisabled = false;
            }

            // Apply PowerDepleted tag based on disabled state
            if (shouldBeDisabled)
            {
                _tag.AddTag(moduleEntity, PowerDepletedTag);
            }
            else
            {
                _tag.RemoveTag(moduleEntity, PowerDepletedTag);
            }
        }
    }
}
