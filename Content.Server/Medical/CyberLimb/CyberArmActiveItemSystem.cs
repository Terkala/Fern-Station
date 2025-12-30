// SPDX-FileCopyrightText: 2025 terkala <appleorange64@gmail.com>
//
// SPDX-License-Identifier: MIT

using Content.Shared.Body.Components;
using Content.Shared.Body.Part;
using Content.Shared.Body.Systems;
using Content.Shared.Hands;
using Content.Shared.Hands.Components;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Interaction;
using Content.Shared.Interaction.Events;
using Content.Shared.Inventory.VirtualItem;
using Content.Shared.Medical.CyberLimb;
using Content.Shared.Medical.CyberLimb.Modules;
using Content.Shared.Storage;
using Content.Shared.Storage.EntitySystems;
using Robust.Shared.Containers;
using System.Diagnostics.CodeAnalysis;

namespace Content.Server.Medical.CyberLimb;

/// <summary>
/// System that handles dynamic cyber arm item selection.
/// When the use key is pressed on an empty hand, it checks if there's a corresponding cyber arm
/// and cycles through items in the cyber arm's storage (excluding modules and batteries).
/// </summary>
public sealed class CyberArmActiveItemSystem : EntitySystem
{
    [Dependency] private readonly SharedHandsSystem _hands = default!;
    [Dependency] private readonly SharedStorageSystem _storage = default!;
    [Dependency] private readonly SharedVirtualItemSystem _virtualItem = default!;
    [Dependency] private readonly SharedBodySystem _body = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<CyberArmActiveItemComponent, UseInHandEvent>(OnUseInHand);
        SubscribeLocalEvent<CyberArmActiveItemComponent, ActivateInWorldEvent>(OnActivateInWorld, before: new[] { typeof(SharedInteractionSystem) });
        SubscribeLocalEvent<CyberArmActiveItemComponent, EntRemovedFromContainerMessage>(OnItemRemovedFromStorage);
        
        // Intercept use key press when hand is empty to check for cyber arms
        SubscribeLocalEvent<HandsComponent, UseInHandEvent>(OnHandUseInHand, before: new[] { typeof(SharedHandsSystem) });
        
        // Handle GetUsedEntityEvent to resolve virtual items to real items in cyber arm storage
        SubscribeLocalEvent<VirtualItemComponent, GetUsedEntityEvent>(OnVirtualItemGetUsedEntity);
        
        // Automatically add component to cyber arms with storage
        SubscribeLocalEvent<CyberLimbStorageComponent, ComponentStartup>(OnCyberLimbStorageStartup);
        
        // Handle items being removed from special module storage
        SubscribeLocalEvent<StorageComponent, EntRemovedFromContainerMessage>(OnItemRemovedFromModuleStorage);
    }

    /// <summary>
    /// Intercepts use key press when the active hand is empty to check for cyber arms.
    /// </summary>
    private void OnHandUseInHand(Entity<HandsComponent> ent, ref UseInHandEvent args)
    {
        // Only handle if active hand is empty
        if (ent.Comp.ActiveHand?.HeldEntity != null)
            return;

        if (ent.Comp.ActiveHand == null)
            return;

        // Find the corresponding cyber arm for this hand
        if (!TryFindCyberArmForHand(ent.Owner, ent.Comp.ActiveHand, out var cyberArm))
            return;

        if (!TryComp<CyberArmActiveItemComponent>(cyberArm, out var activeItem))
            return;

        // If there's already an active item, cycle to next
        if (activeItem.ActiveItem != null)
        {
            CycleToNextItem((cyberArm, activeItem), ent.Owner);
            args.Handled = true;
            return;
        }

        // Try to activate first item
        if (TryGetFirstAvailableItem(cyberArm, out var firstItem, out var firstModule))
        {
            SetActiveItem((cyberArm, activeItem), firstItem.Value, firstModule, ent.Owner);
            args.Handled = true;
        }
    }

    /// <summary>
    /// Tries to find a cyber arm body part that corresponds to a hand.
    /// </summary>
    private bool TryFindCyberArmForHand(EntityUid user, Hand hand, [NotNullWhen(true)] out EntityUid? cyberArm)
    {
        cyberArm = null;

        if (!TryComp<BodyComponent>(user, out var body))
            return false;

        // Get the hand location (Left, Right, or Middle)
        var handLocation = hand.Location;

        // Find the corresponding arm body part
        // Left hand -> Left arm, Right hand -> Right arm, Middle -> try both
        BodyPartSymmetry targetSymmetry = handLocation switch
        {
            HandLocation.Left => BodyPartSymmetry.Left,
            HandLocation.Right => BodyPartSymmetry.Right,
            HandLocation.Middle => BodyPartSymmetry.Right, // Default to right for middle
            _ => BodyPartSymmetry.None
        };

        // Get all arm body parts
        var arms = _body.GetBodyChildrenOfType(user, BodyPartType.Arm, body);
        foreach (var (armUid, armComp) in arms)
        {
            // Check if this arm matches the hand's location
            if (armComp.Symmetry == targetSymmetry || (handLocation == HandLocation.Middle && armComp.Symmetry == BodyPartSymmetry.Right))
            {
                // Check if this arm is a cyber arm with storage
                if (HasComp<CyberLimbStorageComponent>(armUid))
                {
                    cyberArm = armUid;
                    return true;
                }
            }
        }

        // If middle hand and didn't find right arm, try left arm
        if (handLocation == HandLocation.Middle)
        {
            foreach (var (armUid, armComp) in arms)
            {
                if (armComp.Symmetry == BodyPartSymmetry.Left && HasComp<CyberLimbStorageComponent>(armUid))
                {
                    cyberArm = armUid;
                    return true;
                }
            }
        }

        return false;
    }

    /// <summary>
    /// Handles use key press on a cyber arm.
    /// If the hand has a virtual item, cycles to the next item.
    /// </summary>
    private void OnUseInHand(Entity<CyberArmActiveItemComponent> ent, ref UseInHandEvent args)
    {
        // If there's a virtual item, cycle to next item
        if (ent.Comp.VirtualItem != null)
        {
            CycleToNextItem(ent, args.User);
            args.Handled = true;
        }
    }

    /// <summary>
    /// Intercepts ActivateInWorldEvent to prevent normal use interactions when the item is from a cyber arm.
    /// This prevents items like bottles from being drunk when used, only when clicking on self.
    /// </summary>
    private void OnActivateInWorld(Entity<CyberArmActiveItemComponent> ent, ref ActivateInWorldEvent args)
    {
        // Check if the user is holding a virtual item that represents an item from this cyber arm
        if (!TryComp<HandsComponent>(args.User, out var hands) || hands.ActiveHandEntity == null)
            return;

        if (!TryComp<VirtualItemComponent>(hands.ActiveHandEntity, out var virtualItem))
            return;

        // Check if the virtual item's blocking entity is the active item from this cyber arm
        if (virtualItem.BlockingEntity != ent.Comp.ActiveItem)
            return;

        // This is a cyber arm item being activated - prevent normal use interaction
        // The item can still be used for interactions (clicking on things), but not self-use
        args.Handled = true;
    }

    /// <summary>
    /// When an item is removed from storage, clear the active item if it was removed.
    /// </summary>
    private void OnItemRemovedFromStorage(Entity<CyberArmActiveItemComponent> ent, ref EntRemovedFromContainerMessage args)
    {
        if (ent.Comp.ActiveItem == args.Entity)
        {
            ClearActiveItem(ent, args.User);
        }
    }

    /// <summary>
    /// Gets the first available item from cyber arm storage (excluding modules and batteries).
    /// Special modules are included if they contain items inside them.
    /// </summary>
    private bool TryGetFirstAvailableItem(EntityUid cyberArm, [NotNullWhen(true)] out EntityUid? item, [NotNullWhen(true)] out EntityUid? module)
    {
        item = null;
        module = null;

        if (!TryComp<StorageComponent>(cyberArm, out var storage))
            return false;

        foreach (var storedItem in storage.Container.ContainedEntities)
        {
            // Check if this is a special module with items inside
            if (HasComp<CyberLimbSpecialModuleComponent>(storedItem))
            {
                if (TryGetItemFromSpecialModule(storedItem, out var moduleItem))
                {
                    item = moduleItem;
                    module = storedItem;
                    return true;
                }
                continue; // Skip special modules without items inside
            }

            if (IsValidItem(storedItem))
            {
                item = storedItem;
                module = null;
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Gets all available items from cyber arm storage (excluding modules and batteries).
    /// Special modules are included if they contain items inside them.
    /// Returns a list of tuples: (item, module) where module is null if item is direct.
    /// </summary>
    private List<(EntityUid Item, EntityUid? Module)> GetAvailableItems(EntityUid cyberArm)
    {
        var items = new List<(EntityUid Item, EntityUid? Module)>();

        if (!TryComp<StorageComponent>(cyberArm, out var storage))
            return items;

        foreach (var storedItem in storage.Container.ContainedEntities)
        {
            // Check if this is a special module with items inside
            if (HasComp<CyberLimbSpecialModuleComponent>(storedItem))
            {
                if (TryGetItemFromSpecialModule(storedItem, out var moduleItem))
                {
                    items.Add((moduleItem, storedItem));
                }
                continue; // Skip special modules without items inside
            }

            if (IsValidItem(storedItem))
            {
                items.Add((storedItem, null));
            }
        }

        return items;
    }

    /// <summary>
    /// Checks if an item is valid for cyber arm activation (not a module or battery).
    /// Special modules are handled separately - they're valid if they contain items.
    /// </summary>
    private bool IsValidItem(EntityUid item)
    {
        // Exclude modules and batteries (special modules are handled separately)
        if (HasComp<CyberLimbBatteryModuleComponent>(item) ||
            HasComp<CyberLimbMatterBinModuleComponent>(item) ||
            HasComp<CyberLimbManipulatorModuleComponent>(item) ||
            HasComp<CyberLimbCapacitorModuleComponent>(item))
        {
            return false;
        }

        // Special modules are handled separately - don't include them here
        if (HasComp<CyberLimbSpecialModuleComponent>(item))
        {
            return false;
        }

        return true;
    }

    /// <summary>
    /// Tries to get an item from inside a special module's container.
    /// Returns the first item found in the module's storage.
    /// </summary>
    private bool TryGetItemFromSpecialModule(EntityUid specialModule, [NotNullWhen(true)] out EntityUid? item)
    {
        item = null;

        // Check if the special module has storage
        if (!TryComp<StorageComponent>(specialModule, out var moduleStorage))
            return false;

        // Get the first item from the module's storage
        foreach (var moduleItem in moduleStorage.Container.ContainedEntities)
        {
            item = moduleItem;
            return true;
        }

        return false;
    }

    /// <summary>
    /// Sets the active item for a cyber arm and creates a virtual item in the hand.
    /// </summary>
    private void SetActiveItem(Entity<CyberArmActiveItemComponent> cyberArm, EntityUid item, EntityUid? module, EntityUid user)
    {
        // Clear existing active item if any
        if (cyberArm.Comp.VirtualItem != null)
        {
            ClearActiveItem(cyberArm, user);
        }

        // Set the active item and module
        cyberArm.Comp.ActiveItem = item;
        cyberArm.Comp.ActiveItemModule = module;
        Dirty(cyberArm);

        // Create a virtual item in the empty hand
        if (_hands.TryGetActiveHand(user, out var hand) && hand.IsEmpty)
        {
            if (_virtualItem.TrySpawnVirtualItem(item, user, out var virtualItem))
            {
                // Put virtual item in the empty hand
                _hands.DoPickup(user, hand, virtualItem.Value);
                cyberArm.Comp.VirtualItem = virtualItem.Value;
                Dirty(cyberArm);
            }
        }
    }

    /// <summary>
    /// Cycles to the next available item in the cyber arm storage.
    /// </summary>
    private void CycleToNextItem(Entity<CyberArmActiveItemComponent> cyberArm, EntityUid user)
    {
        var availableItems = GetAvailableItems(cyberArm.Owner);
        if (availableItems.Count == 0)
        {
            ClearActiveItem(cyberArm, user);
            return;
        }

        // Find current item index
        var currentIndex = -1;
        if (cyberArm.Comp.ActiveItem != null)
        {
            for (int i = 0; i < availableItems.Count; i++)
            {
                if (availableItems[i].Item == cyberArm.Comp.ActiveItem.Value)
                {
                    currentIndex = i;
                    break;
                }
            }
        }

        // Get next item (wrap around)
        var nextIndex = (currentIndex + 1) % availableItems.Count;
        var (nextItem, nextModule) = availableItems[nextIndex];

        SetActiveItem(cyberArm, nextItem, nextModule, user);
    }

    /// <summary>
    /// Clears the active item and removes the virtual item from the hand.
    /// </summary>
    private void ClearActiveItem(Entity<CyberArmActiveItemComponent> cyberArm, EntityUid user)
    {
        // Remove virtual item from hand
        if (cyberArm.Comp.VirtualItem != null && !Deleted(cyberArm.Comp.VirtualItem))
        {
            if (_hands.TryGetActiveHand(user, out var hand) && hand.HeldEntity == cyberArm.Comp.VirtualItem)
            {
                _hands.TryDrop(user, hand);
                _virtualItem.DeleteVirtualItem((cyberArm.Comp.VirtualItem.Value, Comp<VirtualItemComponent>(cyberArm.Comp.VirtualItem.Value)), user);
            }
        }

        // Clear active item
        cyberArm.Comp.ActiveItem = null;
        cyberArm.Comp.ActiveItemModule = null;
        cyberArm.Comp.VirtualItem = null;
        Dirty(cyberArm);
    }

    /// <summary>
    /// Handles GetUsedEntityEvent for virtual items from cyber arms.
    /// Resolves the virtual item to the real item in cyber arm storage.
    /// </summary>
    private void OnVirtualItemGetUsedEntity(Entity<VirtualItemComponent> ent, ref GetUsedEntityEvent args)
    {
        if (args.Handled)
            return;

        // Check if this virtual item belongs to a cyber arm
        var blockingEntity = ent.Comp.BlockingEntity;
        if (!blockingEntity.IsValid())
            return;

        // Find the cyber arm that has this item as its active item
        var query = EntityQueryEnumerator<CyberArmActiveItemComponent>();
        while (query.MoveNext(out var cyberArmUid, out var activeItem))
        {
            if (activeItem.ActiveItem == blockingEntity && activeItem.VirtualItem == ent.Owner)
            {
                // This virtual item belongs to this cyber arm - use the real item
                // The real item might be inside a special module, but we use the item itself
                args.Used = blockingEntity;
                args.Handled = true;
                return;
            }
        }
    }

    /// <summary>
    /// Automatically adds CyberArmActiveItemComponent to cyber arms with storage.
    /// </summary>
    private void OnCyberLimbStorageStartup(Entity<CyberLimbStorageComponent> ent, ref ComponentStartup args)
    {
        // Only add to arms (not legs or other body parts)
        if (!TryComp<BodyPartComponent>(ent, out var part) || part.PartType != BodyPartType.Arm)
            return;

        // Add the component if it doesn't exist
        EnsureComp<CyberArmActiveItemComponent>(ent);
    }

    /// <summary>
    /// Handles items being removed from special module storage.
    /// If the removed item was the active item, clear it.
    /// </summary>
    private void OnItemRemovedFromModuleStorage(Entity<StorageComponent> ent, ref EntRemovedFromContainerMessage args)
    {
        // Check if this storage belongs to a special module
        if (!HasComp<CyberLimbSpecialModuleComponent>(ent))
            return;

        // Check if this special module is in a cyber arm by checking if it's in a storage container
        // We'll find the cyber arm by checking all cyber arms' storage

        // Find the cyber arm that contains this module
        var query = EntityQueryEnumerator<CyberArmActiveItemComponent>();
        while (query.MoveNext(out var cyberArmUid, out var activeItem))
        {
            // Check if the removed item was the active item from this module
            if (activeItem.ActiveItem == args.Entity && activeItem.ActiveItemModule == ent.Owner)
            {
                // Find the user (owner of the cyber arm)
                if (TryComp<BodyPartComponent>(cyberArmUid, out var part) && part.Body != null)
                {
                    ClearActiveItem((cyberArmUid, activeItem), part.Body.Value);
                }
                return;
            }
        }
    }
}

