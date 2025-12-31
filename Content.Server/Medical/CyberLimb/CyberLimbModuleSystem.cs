// SPDX-FileCopyrightText: 2025 terkala <appleorange64@gmail.com>
//
// SPDX-License-Identifier: MIT

using Content.Shared.Body.Components;
using Content.Shared.Medical.CyberLimb;
using Content.Shared.Medical.CyberLimb.Modules;
using Content.Shared.Storage;
using Content.Server.Body.Systems;
using Robust.Shared.Containers;

namespace Content.Server.Medical.CyberLimb;

/// <summary>
/// System that handles special module effects (Jaws of Life, bio-battery, etc.).
/// </summary>
public sealed class CyberLimbModuleSystem : EntitySystem
{
    [Dependency] private readonly LimbCapabilitiesSystem _limbCapabilities = default!;

    public override void Initialize()
    {
        base.Initialize();

        // Note: Container event subscriptions moved to CyberLimbStorageSystem to avoid duplicates
    }

    /// <summary>
    /// Called by CyberLimbStorageSystem when items are inserted.
    /// </summary>
    public void OnModuleInserted(EntityUid uid, CyberLimbStorageComponent component, ref EntInsertedIntoContainerMessage args)
    {
        // Check if this is the storage container
        if (!TryComp<StorageComponent>(uid, out var storage) || args.Container.ID != storage.Container.ID)
            return;

        // Check if the inserted item is a special module
        if (!TryComp<CyberLimbSpecialModuleComponent>(args.Entity, out var specialModule))
            return;

        ApplySpecialModule(uid, args.Entity, specialModule.ModuleType);
    }

    /// <summary>
    /// Called by CyberLimbStorageSystem when items are removed.
    /// </summary>
    public void OnModuleRemoved(EntityUid uid, CyberLimbStorageComponent component, ref EntRemovedFromContainerMessage args)
    {
        // Check if this is the storage container
        if (!TryComp<StorageComponent>(uid, out var storage) || args.Container.ID != storage.Container.ID)
            return;

        // Check if the removed item is a special module
        if (!TryComp<CyberLimbSpecialModuleComponent>(args.Entity, out var specialModule))
            return;

        RemoveSpecialModule(uid, specialModule.ModuleType);
    }

    /// <summary>
    /// Applies a special module's effects to a cyber limb.
    /// </summary>
    private void ApplySpecialModule(EntityUid limb, EntityUid module, string moduleType)
    {
        switch (moduleType)
        {
            case CyberLimbModuleIds.JawsOfLife:
                // Add prying capability to limb via LimbCapabilitiesComponent
                var caps = EnsureComp<LimbCapabilitiesComponent>(limb);
                caps.ProvidesPrying = true;
                caps.PryPowered = true;
                caps.PryForce = true;
                caps.PrySpeedModifier = 1.0f; // Default speed
                Dirty(limb, caps);
                
                // Trigger recalculation of mob capabilities
                _limbCapabilities.OnLimbCapabilitiesChanged(limb);
                break;

            case CyberLimbModuleIds.BioBattery:
                // Bio-battery effects are handled in a separate update loop
                // This will be implemented when efficiency penalties are applied
                break;
        }
    }

    /// <summary>
    /// Removes a special module's effects from a cyber limb.
    /// </summary>
    private void RemoveSpecialModule(EntityUid limb, string moduleType)
    {
        switch (moduleType)
        {
            case CyberLimbModuleIds.JawsOfLife:
                // Remove prying capability from limb
                if (TryComp<LimbCapabilitiesComponent>(limb, out var caps))
                {
                    caps.ProvidesPrying = false;
                    caps.PryPowered = false;
                    caps.PryForce = false;
                    Dirty(limb, caps);
                    
                    // Trigger recalculation of mob capabilities
                    _limbCapabilities.OnLimbCapabilitiesChanged(limb);
                }
                break;

            case CyberLimbModuleIds.BioBattery:
                // Bio-battery effects cleanup
                break;
        }
    }
}

