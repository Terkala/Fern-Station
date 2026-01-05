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

    // Storage change handler removed - organs no longer have storage

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

        // Organs no longer have storage, so module effects are removed
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

    // UpdateStomachModules removed - organs no longer have storage

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
    /// Organs no longer have storage, so this always returns false.
    /// </summary>
    public bool HasPoisonFilter(EntityUid stomachUid)
    {
        return false;
    }

    /// <summary>
    /// Gets the target species for metabolism if species metabolism module is present.
    /// Organs no longer have storage, so this always returns null.
    /// </summary>
    public ProtoId<EntityPrototype>? GetTargetSpecies(EntityUid stomachUid)
    {
        return null;
    }
}

