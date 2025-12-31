// SPDX-FileCopyrightText: 2025 terkala <appleorange64@gmail.com>
//
// SPDX-License-Identifier: MIT

using Content.Server.Body.Components;
using Content.Server.Body.Systems;
using Content.Shared.Body.Components;
using Content.Shared.Body.Organ;
using Content.Shared.Body.Systems;
using Content.Shared.Medical.CyberOrgan;
using Content.Shared.Medical.CyberOrgan.Modules;
using Content.Shared.Storage;
using Robust.Shared.Containers;
using Robust.Shared.Prototypes;

namespace Content.Server.Medical.CyberOrgan;

/// <summary>
/// System that handles cyber-stomach efficiency effects: chemical multiplier, size scaling, poison filter, and species metabolism.
/// </summary>
public sealed class CyberStomachEfficiencySystem : EntitySystem
{
    [Dependency] private readonly CyberOrganEfficiencySystem _organEfficiency = default!;

    public override void Initialize()
    {
        base.Initialize();

        // Note: ComponentStartup and container event subscriptions moved to CyberOrganEfficiencySystem to avoid duplicates
    }

    /// <summary>
    /// Called by CyberOrganEfficiencySystem when a cyber organ with stomach starts up.
    /// </summary>
    public void OnStomachEfficiencyStartup(EntityUid uid, CyberOrganEfficiencyComponent component)
    {
        UpdateStomachEfficiency(uid, component);
    }

    /// <summary>
    /// Called by CyberOrganEfficiencySystem when storage changes for a cyber organ with stomach.
    /// </summary>
    public void OnStomachStorageChanged(EntityUid uid, CyberOrganEfficiencyComponent component)
    {
        if (!HasComp<StomachComponent>(uid))
            return;

        UpdateStomachEfficiency(uid, component);
    }

    /// <summary>
    /// Updates stomach efficiency effects: size scaling and module effects.
    /// </summary>
    private void UpdateStomachEfficiency(EntityUid stomachUid, CyberOrganEfficiencyComponent efficiency)
    {
        if (!TryComp<OrganComponent>(stomachUid, out var organ) || organ.Body == null)
            return;

        var body = organ.Body.Value;
        var finalEfficiency = _organEfficiency.GetFinalEfficiency(stomachUid, efficiency);

        // Apply stomach size scaling
        ApplyStomachSizeScaling(stomachUid, finalEfficiency);

        // Update poison filter and species metabolism
        UpdateStomachModules(stomachUid, body);
    }

    /// <summary>
    /// Applies stomach size scaling based on efficiency.
    /// </summary>
    private void ApplyStomachSizeScaling(EntityUid stomachUid, float efficiency)
    {
        if (!TryComp<StomachComponent>(stomachUid, out var stomach))
            return;

        // Store base max volume if not already stored
        // For now, we'll scale the current max volume
        // The base max volume should be stored in a component or calculated from prototype
        // This is a simplified implementation
        // TODO: Access solution max volume if needed for scaling
        // MaxVolume is stored in the solution, not the stomach component
    }

    /// <summary>
    /// Updates stomach module effects: poison filter and species metabolism.
    /// </summary>
    private void UpdateStomachModules(EntityUid stomachUid, EntityUid body)
    {
        if (!TryComp<StorageComponent>(stomachUid, out var storage))
            return;

        ProtoId<EntityPrototype>? targetSpecies = null;

        foreach (var item in storage.Container.ContainedEntities)
        {
            if (TryComp<CyberStomachSpeciesMetabolismModuleComponent>(item, out var speciesModule))
            {
                targetSpecies = speciesModule.TargetSpecies;
            }
        }

        // Apply poison filter (grants SpecialDigestible whitelist like rats)
        // TODO: Implement SpecialDigestible component/flag

        // Apply species metabolism (adds species tag for food digestion)
        // TODO: Implement species tag addition for food digestion
    }

    /// <summary>
    /// Gets the chemical multiplier for food digestion based on stomach efficiency.
    /// </summary>
    public float GetChemicalMultiplier(EntityUid stomachUid)
    {
        if (!TryComp<CyberOrganEfficiencyComponent>(stomachUid, out var efficiency))
            return 1.0f;

        var finalEfficiency = _organEfficiency.GetFinalEfficiency(stomachUid, efficiency);
        
        // Only multiply if efficiency > 100%
        return finalEfficiency > 1.0f ? finalEfficiency : 1.0f;
    }

    /// <summary>
    /// Checks if stomach has poison filter module.
    /// </summary>
    public bool HasPoisonFilter(EntityUid stomachUid)
    {
        if (!TryComp<StorageComponent>(stomachUid, out var storage))
            return false;

        foreach (var item in storage.Container.ContainedEntities)
        {
            if (HasComp<CyberStomachPoisonFilterModuleComponent>(item))
                return true;
        }

        return false;
    }

    /// <summary>
    /// Gets the target species for metabolism if species metabolism module is present.
    /// </summary>
    public ProtoId<EntityPrototype>? GetTargetSpecies(EntityUid stomachUid)
    {
        if (!TryComp<StorageComponent>(stomachUid, out var storage))
            return null;

        foreach (var item in storage.Container.ContainedEntities)
        {
            if (TryComp<CyberStomachSpeciesMetabolismModuleComponent>(item, out var speciesModule))
            {
                return speciesModule.TargetSpecies;
            }
        }

        return null;
    }
}

