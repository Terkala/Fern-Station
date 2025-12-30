// SPDX-FileCopyrightText: 2025 terkala <appleorange64@gmail.com>
//
// SPDX-License-Identifier: MIT

using Content.Shared.Body.Organ;
using Content.Shared.Medical.CyberLimb;
using Content.Shared.Medical.CyberLimb.Components;
using Content.Shared.Medical.CyberLimb.Modules;
using Content.Shared.Medical.CyberOrgan;
using Content.Shared.Storage;
using Content.Shared.Storage.EntitySystems;
using Content.Shared.Stacks;
using Robust.Shared.Containers;

namespace Content.Server.Medical.CyberLimb;

/// <summary>
/// System that handles cyber limb storage with non-stacking behavior and module count caching.
/// Also handles cyber-organ module counting and efficiency calculation.
/// </summary>
public sealed class CyberLimbStorageSystem : EntitySystem
{
    [Dependency] private readonly SharedStorageSystem _storage = default!;
    [Dependency] private readonly StackSystem _stack = default!;
    [Dependency] private readonly CyberOrganEfficiencySystem _organEfficiency = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<CyberLimbStorageComponent, ComponentStartup>(OnCyberLimbStorageStartup);
        SubscribeLocalEvent<CyberLimbStorageComponent, EntInsertedIntoContainerMessage>(OnItemInserted);
        SubscribeLocalEvent<CyberLimbStorageComponent, EntRemovedFromContainerMessage>(OnItemRemoved);
        SubscribeLocalEvent<CyberLimbStorageComponent, StorageInteractAttemptEvent>(OnStorageInteractAttempt);
        SubscribeLocalEvent<CyberLimbStorageComponent, ContainerIsInsertingAttemptEvent>(OnContainerInsertAttempt, before: new[] { typeof(SharedStorageSystem) });
    }

    private void OnCyberLimbStorageStartup(EntityUid uid, CyberLimbStorageComponent component, ComponentStartup args)
    {
        // Ensure upkeep component exists for all cybernetics
        EnsureComp<CyberneticsUpkeepComponent>(uid);
    }

    private void OnContainerInsertAttempt(EntityUid uid, CyberLimbStorageComponent component, ContainerIsInsertingAttemptEvent args)
    {
        // Check if maintenance panel is open - if not, prevent insertion
        if (TryComp<CyberneticsUpkeepComponent>(uid, out var upkeep))
        {
            if (!upkeep.IsPanelUnscrewed)
            {
                args.Cancel();
                return;
            }
        }

        // If inserting a stack with count > 1, we need to split it
        // However, we can't do that here because the insertion hasn't happened yet
        // Instead, we'll handle it in OnItemInserted by checking and splitting there
        // For now, we just ensure stacking is disabled by letting it through
        // The actual stack splitting will happen in a post-insertion check
    }

    private void OnStorageInteractAttempt(EntityUid uid, CyberLimbStorageComponent component, ref StorageInteractAttemptEvent args)
    {
        // Check if maintenance panel is open - if not, prevent access
        if (TryComp<CyberneticsUpkeepComponent>(uid, out var upkeep))
        {
            if (!upkeep.IsPanelUnscrewed)
            {
                args.Cancelled = true;
            }
        }
    }

    private void OnItemInserted(EntityUid uid, CyberLimbStorageComponent component, ref EntInsertedIntoContainerMessage args)
    {
        // Check if this is the storage container
        if (!TryComp<StorageComponent>(uid, out var storage) || args.Container.ID != storage.Container.ID)
            return;

        // Check if the inserted item is a stack with count > 1
        // If so, we need to remove it, split it, and re-insert only 1 item
        if (TryComp<StackComponent>(args.Entity, out var stack) && stack.Count > 1)
        {
            // Remove the stack from storage
            _container.Remove(args.Entity, args.Container);

            // Split the stack: take 1, leave the rest
            var splitItem = _stack.Split(args.Entity, 1, Transform(uid).Coordinates, stack);
            if (splitItem != null)
            {
                // Insert the split item (single item, not a stack)
                _storage.Insert(uid, splitItem.Value, out _, storageComp: storage, stackAutomatically: false);
            }

            // Don't recalculate yet, wait for the split item to be inserted
            return;
        }

        // Recalculate module counts when item is inserted
        component.NeedsRecalculation = true;
        RecalculateModuleCounts(uid, component, storage);
        
        // Also recalculate organ efficiency if this is a cyber-organ
        if (HasComp<OrganComponent>(uid))
        {
            RecalculateOrganEfficiency(uid, storage);
        }
    }

    private void OnItemRemoved(EntityUid uid, CyberLimbStorageComponent component, ref EntRemovedFromContainerMessage args)
    {
        // Check if this is the storage container
        if (!TryComp<StorageComponent>(uid, out var storage) || args.Container.ID != storage.Container.ID)
            return;

        // Recalculate module counts when item is removed
        component.NeedsRecalculation = true;
        RecalculateModuleCounts(uid, component, storage);
        
        // Also recalculate organ efficiency if this is a cyber-organ
        if (HasComp<OrganComponent>(uid))
        {
            RecalculateOrganEfficiency(uid, storage);
        }
    }

    /// <summary>
    /// Handles insertion into cyber limb storage with non-stacking behavior.
    /// If a stack is inserted, splits it to take only 1 item.
    /// This should be called instead of SharedStorageSystem.Insert for cyber limbs.
    /// </summary>
    public bool TryInsertNonStacking(EntityUid limb, EntityUid item, EntityUid? user = null)
    {
        if (!TryComp<StorageComponent>(limb, out var storage))
            return false;

        // Check if maintenance panel is open
        if (TryComp<CyberneticsUpkeepComponent>(limb, out var upkeep) && !upkeep.IsPanelUnscrewed)
            return false;

        if (!TryComp<StackComponent>(item, out var stack))
        {
            // Not a stack, insert normally with stacking disabled
            return _storage.Insert(limb, item, out _, user: user, storageComp: storage, stackAutomatically: false);
        }

        // It's a stack - split off 1 item
        if (stack.Count <= 1)
        {
            // Already 1 or less, insert normally with stacking disabled
            return _storage.Insert(limb, item, out _, user: user, storageComp: storage, stackAutomatically: false);
        }

        // Split the stack: take 1, leave the rest
        var splitItem = _stack.Split(item, 1, Transform(limb).Coordinates, stack);
        if (splitItem == null)
            return false;

        // Insert the split item (single item, not a stack)
        var inserted = _storage.Insert(limb, splitItem.Value, out _, user: user, storageComp: storage, stackAutomatically: false);
        
        if (!inserted)
        {
            // Failed to insert, delete the split item
            Del(splitItem.Value);
            return false;
        }

        return true;
    }

    /// <summary>
    /// Recalculates module counts and efficiency for a cyber limb.
    /// </summary>
    public void RecalculateModuleCounts(EntityUid uid, CyberLimbStorageComponent storage, StorageComponent? storageComp = null)
    {
        if (!Resolve(uid, ref storageComp))
            return;

        storage.CachedBatteryCount = 0;
        storage.CachedBatteryCapacity = 0f;
        storage.CachedMatterBinCount = 0;
        storage.CachedManipulatorCount = 0;

        foreach (var item in storageComp.Container.ContainedEntities)
        {
            if (TryComp<CyberLimbBatteryModuleComponent>(item, out var battery))
            {
                storage.CachedBatteryCount++;
                storage.CachedBatteryCapacity += battery.MaxCharge;
            }
            else if (HasComp<CyberLimbMatterBinModuleComponent>(item))
            {
                storage.CachedMatterBinCount++;
            }
            else if (HasComp<CyberLimbManipulatorModuleComponent>(item))
            {
                storage.CachedManipulatorCount++;
            }
        }

        // Recalculate efficiency
        storage.CachedEfficiency = CalculateEfficiency(storage.CachedManipulatorCount);
        storage.NeedsRecalculation = false;
        Dirty(uid, storage);

        // Trigger service time recalculation (handled by CyberLimbStatsSystem)
        // This will be done via the OnLimbStorageChanged event
    }

    /// <summary>
    /// Calculates efficiency based on manipulator count.
    /// Base: 100% for first manipulator, +10% for each additional.
    /// </summary>
    private float CalculateEfficiency(int manipulatorCount)
    {
        if (manipulatorCount == 0)
            return 0f; // No manipulators = 0% efficiency

        // Base 100% for first manipulator, +10% for each additional
        return 1.0f + (manipulatorCount - 1) * 0.1f;
    }

    /// <summary>
    /// Recalculates organ module counts and efficiency for a cyber-organ.
    /// Uses manipulator modules (same as cyber-limbs) - each manipulator adds 10% efficiency.
    /// </summary>
    public void RecalculateOrganEfficiency(EntityUid uid, CyberLimbStorageComponent storage, StorageComponent? storageComp = null)
    {
        if (!Resolve(uid, ref storageComp))
            return;

        if (!TryComp<CyberOrganEfficiencyComponent>(uid, out var efficiency))
            return;

        // Count manipulator modules (same component used for cyber-limbs)
        int moduleCount = 0;

        foreach (var item in storageComp.Container.ContainedEntities)
        {
            if (HasComp<CyberLimbManipulatorModuleComponent>(item))
            {
                moduleCount++;
            }
        }

        // Calculate and cache efficiency
        efficiency.CachedEfficiency = _organEfficiency.CalculateEfficiency(moduleCount);
        efficiency.CachedModuleCount = moduleCount;
        efficiency.NeedsRecalculation = false;
        Dirty(uid, efficiency);
    }
}

