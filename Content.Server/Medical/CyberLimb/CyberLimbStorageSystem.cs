// SPDX-FileCopyrightText: 2025 terkala <appleorange64@gmail.com>
//
// SPDX-License-Identifier: MIT

using Content.Shared.Body.Organ;
using Content.Shared.Body.Part;
using Content.Shared.Medical.CyberLimb;
using Content.Shared.Medical.CyberLimb.Modules;
using Content.Shared.Medical.CyberOrgan;
using Content.Shared.Storage;
using Content.Shared.Storage.EntitySystems;
using Content.Shared.Stacks;
using Content.Shared.UserInterface;
using Content.Server.Stack;
using Content.Server.Medical.CyberOrgan;
using Content.Shared.Containers;
using Robust.Shared.Containers;
using Content.Shared.Popups;
using Content.Server.Popups;
using Content.Shared.Tag;
using Robust.Shared.Prototypes;

namespace Content.Server.Medical.CyberLimb;

/// <summary>
/// System that handles cyber limb storage with non-stacking behavior and module count caching.
/// Also handles cyber-organ module counting and efficiency calculation.
/// </summary>
public sealed class CyberLimbStorageSystem : EntitySystem
{
    [Dependency] private readonly SharedStorageSystem _storage = default!;
    [Dependency] private readonly SharedContainerSystem _container = default!;
    [Dependency] private readonly StackSystem _stack = default!;
    [Dependency] private readonly CyberOrganEfficiencySystem _organEfficiency = default!;
    [Dependency] private readonly CyberLimbStatsSystem _stats = default!;
    [Dependency] private readonly CyberLimbModuleSystem _moduleSystem = default!;
    [Dependency] private readonly CyberneticsUpkeepSystem _upkeep = default!;
    [Dependency] private readonly PopupSystem _popup = default!;
    [Dependency] private readonly TagSystem _tag = default!;

    private static readonly ProtoId<TagPrototype> BorgArmTag = "BorgArm";
    private static readonly ProtoId<TagPrototype> BorgLegTag = "BorgLeg";

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<CyberLimbStorageComponent, ComponentStartup>(OnCyberLimbStorageStartup);
        SubscribeLocalEvent<CyberLimbStorageComponent, EntInsertedIntoContainerMessage>(OnItemInserted, after: new[] { typeof(SharedStorageSystem) });
        SubscribeLocalEvent<CyberLimbStorageComponent, EntRemovedFromContainerMessage>(OnItemRemoved);
        SubscribeLocalEvent<CyberLimbStorageComponent, StorageInteractAttemptEvent>(OnStorageInteractAttempt);
        SubscribeLocalEvent<CyberLimbStorageComponent, ContainerIsInsertingAttemptEvent>(OnContainerInsertAttempt, before: new[] { typeof(SharedStorageSystem) });
        SubscribeLocalEvent<CyberLimbStorageComponent, ContainerIsRemovingAttemptEvent>(OnContainerRemoveAttempt, before: new[] { typeof(SharedStorageSystem) });
        SubscribeLocalEvent<CyberLimbStorageComponent, BoundUIOpenedEvent>(OnStorageUIOpened);
    }

    private void OnCyberLimbStorageStartup(EntityUid uid, CyberLimbStorageComponent component, ComponentStartup args)
    {
        // Ensure upkeep component exists for all cybernetics
        var upkeep = EnsureComp<CyberneticsUpkeepComponent>(uid);
        
        // All cyberwear outside of a body should have maintenance panels open
        // This allows access to storage before installation
        if (!IsInBody(uid))
        {
            upkeep.IsPanelUnscrewed = true;
            Dirty(uid, upkeep);
        }
        
        // Initialize stats (service time, low power mode, etc.)
        _stats.OnCyberLimbStartup(uid, component);
        
        // Add CyberArmActiveItemComponent to arms
        if (TryComp<BodyPartComponent>(uid, out var part) && part.PartType == BodyPartType.Arm)
        {
            EnsureComp<CyberArmActiveItemComponent>(uid);
            
            // Ensure UserInterface component exists (should be added via prototype, but ensure it exists)
            EnsureComp<UserInterfaceComponent>(uid);
            
            // Add UI components for radial menu
            var activatable = EnsureComp<ActivatableUIComponent>(uid);
            activatable.Key = CyberArmRadialMenuUiKey.Key;
            activatable.InHandsOnly = false;
            activatable.RequiresComplex = false;
        }
    }

    private void OnContainerInsertAttempt(EntityUid uid, CyberLimbStorageComponent component, ContainerIsInsertingAttemptEvent args)
    {
        // Allow insertion if cybernetic is not in a body (maintenance panel should be open)
        if (!IsInBody(uid))
            return;

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

    private void OnContainerRemoveAttempt(EntityUid uid, CyberLimbStorageComponent component, ContainerIsRemovingAttemptEvent args)
    {
        // Allow removal if cybernetic is not in a body (maintenance panel should be open)
        if (!IsInBody(uid))
            return;

        // Check if maintenance panel is open - if not, prevent removal
        if (TryComp<CyberneticsUpkeepComponent>(uid, out var upkeep))
        {
            if (!upkeep.IsPanelUnscrewed)
            {
                args.Cancel();
                return;
            }
        }
    }

    private void OnStorageInteractAttempt(EntityUid uid, CyberLimbStorageComponent component, ref StorageInteractAttemptEvent args)
    {
        // Allow UI to open - panel check only prevents insertion/removal, not viewing
        // The OnContainerInsertAttempt handler will prevent item insertion when panel is closed
        // Popup will be shown when UI actually opens (in OnStorageUIOpened)
    }

    private void OnStorageUIOpened(EntityUid uid, CyberLimbStorageComponent component, BoundUIOpenedEvent args)
    {
        // Check if this is the storage UI
        if (args.UiKey is not StorageComponent.StorageUiKey.Key)
            return;

        // Only show popup if cybernetic is in a body AND panel is closed
        // Cyberwear outside of a body should have open panels and allow access
        if (IsInBody(uid) && TryComp<CyberneticsUpkeepComponent>(uid, out var upkeep) && !upkeep.IsPanelUnscrewed)
        {
            _popup.PopupEntity("The maintenance panel is closed.", uid, args.Actor, PopupType.MediumCaution);
        }
    }

    /// <summary>
    /// Called by CyberneticsSlotSystem when a cybernetic body part is added to a body.
    /// Auto-closes maintenance panel when cyborg arm/leg is installed.
    /// </summary>
    public void OnBodyPartAdded(Entity<BodyPartComponent> ent, ref BodyPartAddedEvent args)
    {
        // Auto-close maintenance panel when cyborg arm/leg is installed
        if (ent.Comp.PartType == BodyPartType.Arm || ent.Comp.PartType == BodyPartType.Leg)
        {
            // Check if this is a cyborg arm/leg (has BorgArm or BorgLeg tag)
            if (_tag.HasTag(ent, BorgArmTag) || _tag.HasTag(ent, BorgLegTag))
            {
                if (TryComp<CyberneticsUpkeepComponent>(ent, out var upkeep))
                {
                    upkeep.IsPanelUnscrewed = false;
                    Dirty(ent, upkeep);
                }
            }
        }
    }

    private void OnItemInserted(EntityUid uid, CyberLimbStorageComponent component, ref EntInsertedIntoContainerMessage args)
    {
        // Check if this is the storage container
        if (!TryComp<StorageComponent>(uid, out var storage) || args.Container.ID != storage.Container.ID)
            return;

        // Verify the entity is still in the container (SharedStorageSystem may have removed it if no grid space)
        if (!args.Container.Contains(args.Entity))
            return;

        // Check if the inserted item is a stack with count > 1
        // If so, we need to remove it, split it, and re-insert only 1 item
        if (TryComp<StackComponent>(args.Entity, out var stack) && stack.Count > 1)
        {
            // Remove the stack from storage - this will reparent it back to where it was (player's hand or ground)
            _container.Remove(args.Entity, args.Container);

            // Split the stack: take 1, leave the rest
            // The original stack remains with reduced count and should be in the player's hand
            var splitItem = _stack.Split(args.Entity, 1, Transform(uid).Coordinates, stack);
            if (splitItem != null)
            {
                // Try to insert the split item (single item, not a stack)
                // Use Insert with stackAutomatically: false - SharedStorageSystem's OnEntInserted will handle grid space
                // If insertion fails, the item will remain at its current location (accessible to player)
                _storage.Insert(uid, splitItem.Value, out _, storageComp: storage, stackAutomatically: false);
            }

            // Don't recalculate yet, wait for the split item to be inserted (if it succeeded)
            // If insertion fails, SharedStorageSystem will have already removed it in its OnEntInserted handler
            return;
        }

        // Recalculate module counts when item is inserted
        component.NeedsRecalculation = true;
        RecalculateModuleCounts(uid, component, storage);
        
        // Also recalculate organ efficiency if this is a cyber-organ
        if (HasComp<OrganComponent>(uid) && TryComp<CyberLimbStorageComponent>(uid, out var cyberStorage))
        {
            RecalculateOrganEfficiency(uid, cyberStorage, storage);
        }

        // Dispatch to other systems
        _stats.OnLimbStorageChanged(uid, component, ref args);
        _moduleSystem.OnModuleInserted(uid, component, ref args);
        _upkeep.OnBatteryChanged(uid, component, ref args);
    }

    private void OnItemRemoved(EntityUid uid, CyberLimbStorageComponent component, ref EntRemovedFromContainerMessage args)
    {
        // Check if this is the storage container
        if (!TryComp<StorageComponent>(uid, out var storage) || args.Container.ID != storage.Container.ID)
            return;

        // Recalculate module counts when item is removed
        component.NeedsRecalculation = true;
        RecalculateModuleCounts(uid, component, storage);

        // Dispatch to other systems
        _stats.OnLimbStorageChanged(uid, component, ref args);
        _moduleSystem.OnModuleRemoved(uid, component, ref args);
        _upkeep.OnBatteryChanged(uid, component, ref args);
        
        // Also recalculate organ efficiency if this is a cyber-organ
        if (HasComp<OrganComponent>(uid) && TryComp<CyberLimbStorageComponent>(uid, out var cyberStorage))
        {
            RecalculateOrganEfficiency(uid, cyberStorage, storage);
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

        // Allow insertion if cybernetic is not in a body (maintenance panel should be open)
        // Only check panel state if the cybernetic is installed in a body
        if (IsInBody(limb) && TryComp<CyberneticsUpkeepComponent>(limb, out var upkeep) && !upkeep.IsPanelUnscrewed)
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

    /// <summary>
    /// Checks if a cybernetic is currently installed in a body.
    /// </summary>
    private bool IsInBody(EntityUid uid)
    {
        // Check if it's a body part in a body
        if (TryComp<BodyPartComponent>(uid, out var part))
        {
            return part.Body != null;
        }

        // Check if it's an organ in a body
        if (TryComp<OrganComponent>(uid, out var organ))
        {
            return organ.Body != null;
        }

        return false;
    }
}

