// SPDX-FileCopyrightText: 2025 terkala <appleorange64@gmail.com>
//
// SPDX-License-Identifier: MIT

using Content.Shared.Body.Components;
using Content.Shared.Body.Part;
using Content.Shared.Body.Systems;
using Content.Shared.Cuffs;
using Content.Shared.Cuffs.Components;
using Content.Shared.Hands;
using Content.Shared.Hands.Components;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.IdentityManagement;
using Content.Shared.Interaction;
using Content.Shared.Interaction.Events;
using Content.Shared.Inventory.VirtualItem;
using Content.Shared.Item;
using Content.Shared.Medical.CyberLimb;
using Content.Shared.Medical.CyberLimb.Modules;
using Content.Shared.Storage;
using Content.Shared.Storage.EntitySystems;
using Content.Shared.UserInterface;
using Content.Shared.Wieldable;
using Content.Shared.Wieldable.Components;
using Robust.Shared.Containers;
using Robust.Shared.Prototypes;
using System.Diagnostics.CodeAnalysis;

namespace Content.Server.Medical.CyberLimb;

/// <summary>
/// System that handles dynamic cyber arm item selection.
/// When the use key is pressed on an empty hand, it checks if there's a corresponding cyber arm
/// and opens a radial menu to select items from the cyber arm's storage (excluding modules and batteries).
/// </summary>
public sealed class CyberArmActiveItemSystem : EntitySystem
{
    [Dependency] private readonly SharedHandsSystem _hands = default!;
    [Dependency] private readonly SharedVirtualItemSystem _virtualItem = default!;
    [Dependency] private readonly SharedBodySystem _body = default!;
    [Dependency] private readonly SharedUserInterfaceSystem _uiSystem = default!;
    [Dependency] private readonly SharedCuffableSystem _cuffable = default!;
    [Dependency] private readonly SharedWieldableSystem _wieldable = default!;
    [Dependency] private readonly IPrototypeManager _prototypeManager = default!;

    public override void Initialize()
    {
        base.Initialize();

        // Both UseInHandEvent subscriptions need the same ordering constraints
        SubscribeLocalEvent<CyberArmActiveItemComponent, UseInHandEvent>(OnUseInHand, before: new[] { typeof(SharedHandsSystem) });
        SubscribeLocalEvent<CyberArmActiveItemComponent, ActivateInWorldEvent>(OnActivateInWorld, before: new[] { typeof(SharedInteractionSystem) });
        
        // Intercept UseInHandEvent on all entities to prevent self-use when used via cyber arm virtual items
        SubscribeLocalEvent<UseInHandEvent>(OnAnyItemUseInHand, before: new[] { typeof(SharedHandsSystem) });
        SubscribeLocalEvent<CyberArmActiveItemComponent, EntRemovedFromContainerMessage>(OnItemRemovedFromStorage);
        
        // Intercept use key press when hand is empty to check for cyber arms
        SubscribeLocalEvent<HandsComponent, UseInHandEvent>(OnHandUseInHand, before: new[] { typeof(SharedHandsSystem) });
        
        // Handle UI messages
        SubscribeLocalEvent<CyberArmActiveItemComponent, CyberArmSelectItemMessage>(OnSelectItemMessage);
        SubscribeLocalEvent<CyberArmActiveItemComponent, CyberArmOpenHandMessage>(OnOpenHandMessage);
        SubscribeLocalEvent<CyberArmActiveItemComponent, ActivatableUIOpenAttemptEvent>(OnUIOpenAttempt);
        
        // Note: GetUsedEntityEvent subscription handled by VirtualItemSystem override to avoid duplicates
        
        // Note: ComponentStartup subscription moved to CyberLimbStorageSystem to avoid duplicates
        
        // Handle items being removed from special module storage
        // Subscribe to CyberLimbSpecialModuleComponent instead of StorageComponent to avoid duplicate subscriptions
        SubscribeLocalEvent<CyberLimbSpecialModuleComponent, EntRemovedFromContainerMessage>(OnItemRemovedFromModuleStorage);
        
        // Clear active item when player is cuffed
        SubscribeLocalEvent<CuffableComponent, EntInsertedIntoContainerMessage>(OnCuffsAdded);
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

        // Block if player is cuffed
        if (TryComp<CuffableComponent>(ent.Owner, out var cuffable) && _cuffable.IsCuffed((ent.Owner, cuffable)))
            return;

        // Find the corresponding cyber arm for this hand
        if (!TryFindCyberArmForHand(ent.Owner, ent.Comp.ActiveHand, out var cyberArm) || cyberArm == null)
            return;

        if (!TryComp<CyberArmActiveItemComponent>(cyberArm.Value, out var activeItem))
            return;

        // Open the radial menu UI
        if (_uiSystem.TryOpenUi(cyberArm.Value, CyberArmRadialMenuUiKey.Key, ent.Owner))
        {
            UpdateUIState(cyberArm.Value, activeItem);
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
    /// If the hand has a virtual item, open the radial menu to select a different item.
    /// </summary>
    private void OnUseInHand(Entity<CyberArmActiveItemComponent> ent, ref UseInHandEvent args)
    {
        // Block if player is cuffed
        if (TryComp<CuffableComponent>(args.User, out var cuffable) && _cuffable.IsCuffed((args.User, cuffable)))
            return;

        // If there's a virtual item, open the menu to select a different item
        if (ent.Comp.VirtualItem != null)
        {
            if (_uiSystem.TryOpenUi(ent.Owner, CyberArmRadialMenuUiKey.Key, args.User))
            {
                UpdateUIState(ent.Owner, ent.Comp);
                args.Handled = true;
            }
        }
    }

    /// <summary>
    /// Intercepts ActivateInWorldEvent to prevent normal use interactions when the item is from a cyber arm.
    /// This prevents items like bottles from being drunk when used, only when clicking on self.
    /// Also handles UseInHandEvent by checking if the real item is being used via a cyber arm virtual item.
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
    /// Intercepts UseInHandEvent on all entities to prevent self-use when used via cyber arm virtual items.
    /// This prevents items like food from being eaten when pressing use, but allows clicking on self.
    /// </summary>
    private void OnAnyItemUseInHand(ref UseInHandEvent args)
    {
        // Check if the user is holding a virtual item
        if (!TryComp<HandsComponent>(args.User, out var hands) || hands.ActiveHandEntity == null)
            return;

        if (!TryComp<VirtualItemComponent>(hands.ActiveHandEntity, out var virtualItem))
            return;

        // Check if this virtual item belongs to a cyber arm
        var query = EntityQueryEnumerator<CyberArmActiveItemComponent>();
        while (query.MoveNext(out var cyberArmUid, out var activeItem))
        {
            if (activeItem.ActiveItem == virtualItem.BlockingEntity && activeItem.VirtualItem == hands.ActiveHandEntity)
            {
                // This is a cyber arm item being used - prevent self-use
                // The item can still be used for interactions (clicking on things), but not self-use
                // Note: The event is raised on the real item (via GetUsedEntityEvent resolution),
                // so we need to check if the real item matches the blocking entity
                // We can't directly check which entity the event is raised on in this handler,
                // but we can check if the user is holding a cyber arm virtual item and prevent all UseInHandEvents
                // This is safe because the virtual item system will still allow interactions via GetUsedEntityEvent
                args.Handled = true;
                return;
            }
        }
    }


    /// <summary>
    /// When an item is removed from storage, clear the active item if it was removed.
    /// </summary>
    private void OnItemRemovedFromStorage(Entity<CyberArmActiveItemComponent> ent, ref EntRemovedFromContainerMessage args)
    {
        if (ent.Comp.ActiveItem == args.Entity)
        {
            // Clear active item when it's removed - use the cyber arm owner as user
            ClearActiveItem(ent, ent.Owner);
        }
    }

    /// <summary>
    /// Gets the first available item from cyber arm storage (excluding modules and batteries).
    /// Special modules are included if they contain items inside them.
    /// </summary>
    private bool TryGetFirstAvailableItem(EntityUid cyberArm, [NotNullWhen(true)] out EntityUid? item, out EntityUid? module)
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
                if (TryGetItemFromSpecialModule(storedItem, out var moduleItem) && moduleItem != null)
                {
                    items.Add((moduleItem.Value, storedItem));
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
                
                // Auto-wield guns that require 2 hands
                if (TryComp<WieldableComponent>(item, out var wieldable) && 
                    !wieldable.Wielded && 
                    wieldable.FreeHandsRequired > 0)
                {
                    // Try to wield the real item (not the virtual item)
                    _wieldable.TryWield(item, wieldable, user);
                }
            }
        }

        // Update UI state
        UpdateUIState(cyberArm.Owner, cyberArm.Comp);
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

        // Update UI state
        UpdateUIState(cyberArm.Owner, cyberArm.Comp);
    }

    /// <summary>
    /// Handles GetUsedEntityEvent for virtual items from cyber arms.
    /// Resolves the virtual item to the real item in cyber arm storage.
    /// Called by VirtualItemSystem.
    /// </summary>
    public void OnVirtualItemGetUsedEntity(Entity<VirtualItemComponent> ent, ref GetUsedEntityEvent args)
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
                // Handled is automatically true when Used is set (computed property)
                return;
            }
        }
    }


    /// <summary>
    /// Handles items being removed from special module storage.
    /// If the removed item was the active item, clear it.
    /// </summary>
    private void OnItemRemovedFromModuleStorage(Entity<CyberLimbSpecialModuleComponent> ent, ref EntRemovedFromContainerMessage args)
    {
        // Verify this special module has storage (it should, but check to be safe)
        if (!HasComp<StorageComponent>(ent))
            return;

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
                    UpdateUIState(cyberArmUid, activeItem);
                }
                return;
            }
        }
    }

    /// <summary>
    /// Handles UI open attempt - ensures the UI can only be opened by the owner of the cyber arm and not when cuffed.
    /// </summary>
    private void OnUIOpenAttempt(Entity<CyberArmActiveItemComponent> ent, ref ActivatableUIOpenAttemptEvent args)
    {
        // Block if player is cuffed
        if (TryComp<CuffableComponent>(args.User, out var cuffable) && _cuffable.IsCuffed((args.User, cuffable)))
        {
            args.Cancelled = true;
            return;
        }

        // Allow opening if the user owns the body that contains this cyber arm
        if (TryComp<BodyPartComponent>(ent.Owner, out var part) && part.Body == args.User)
            return;

        args.Cancelled = true;
    }

    /// <summary>
    /// Handles item selection message from the UI.
    /// </summary>
    private void OnSelectItemMessage(Entity<CyberArmActiveItemComponent> ent, ref CyberArmSelectItemMessage args)
    {
        // Find the user (owner of the cyber arm)
        if (!TryComp<BodyPartComponent>(ent.Owner, out var part) || part.Body == null)
            return;

        // Block if player is cuffed
        if (TryComp<CuffableComponent>(part.Body.Value, out var cuffable) && _cuffable.IsCuffed((part.Body.Value, cuffable)))
            return;

        var item = GetEntity(args.Item);
        
        // Verify the item is actually in this cyber arm's storage
        var availableItems = GetAvailableItems(ent.Owner);
        EntityUid? module = null;
        bool found = false;
        
        foreach (var (availableItem, availableModule) in availableItems)
        {
            if (availableItem == item)
            {
                module = availableModule;
                found = true;
                break;
            }
        }

        if (!found)
            return;

        SetActiveItem(ent, item, module, part.Body.Value);
        UpdateUIState(ent.Owner, ent.Comp);
    }

    /// <summary>
    /// Handles "open hand" message from the UI.
    /// </summary>
    private void OnOpenHandMessage(Entity<CyberArmActiveItemComponent> ent, ref CyberArmOpenHandMessage args)
    {
        // Find the user (owner of the cyber arm)
        if (!TryComp<BodyPartComponent>(ent.Owner, out var part) || part.Body == null)
            return;

        // Block if player is cuffed
        if (TryComp<CuffableComponent>(part.Body.Value, out var cuffable) && _cuffable.IsCuffed((part.Body.Value, cuffable)))
            return;

        ClearActiveItem(ent, part.Body.Value);
        UpdateUIState(ent.Owner, ent.Comp);
    }

    /// <summary>
    /// Updates the UI state with current available items.
    /// </summary>
    private void UpdateUIState(EntityUid cyberArm, CyberArmActiveItemComponent component)
    {
        var availableItems = GetAvailableItems(cyberArm);
        var itemDataList = new List<CyberArmItemData>();

        foreach (var (item, _) in availableItems)
        {
            var itemName = Identity.Name(item, EntityManager);
            string? spritePath = null;

            // Try to get sprite path from ItemComponent
            if (TryComp<ItemComponent>(item, out var itemComp) && itemComp.RsiPath != null)
            {
                spritePath = itemComp.RsiPath.ToString();
            }

            itemDataList.Add(new CyberArmItemData(
                GetNetEntity(item),
                itemName,
                spritePath
            ));
        }

        var activeItemNet = component.ActiveItem != null ? GetNetEntity(component.ActiveItem.Value) : null;
        var state = new CyberArmRadialMenuState(itemDataList, activeItemNet);
        _uiSystem.SetUiState(cyberArm, CyberArmRadialMenuUiKey.Key, state);
    }

    /// <summary>
    /// Clears active item when player is cuffed.
    /// </summary>
    private void OnCuffsAdded(Entity<CuffableComponent> ent, ref EntInsertedIntoContainerMessage args)
    {
        // Check if this is the cuff container (all cuffs go into this container)
        if (args.Container.ID != ent.Comp.Container.ID)
            return;

        // Check if the entity being inserted is actually a cuff (has HandcuffComponent)
        // This covers handcuffs, zipties, improvised cuffs, etc.
        if (!HasComp<HandcuffComponent>(args.Entity))
            return;

        // Find all cyber arms belonging to this entity and clear their active items
        if (!TryComp<BodyComponent>(ent.Owner, out var body))
            return;

        var arms = _body.GetBodyChildrenOfType(ent.Owner, BodyPartType.Arm, body);
        foreach (var (armUid, _) in arms)
        {
            if (TryComp<CyberArmActiveItemComponent>(armUid, out var activeItem) && activeItem.ActiveItem != null)
            {
                ClearActiveItem((armUid, activeItem), ent.Owner);
            }
        }
    }
}

