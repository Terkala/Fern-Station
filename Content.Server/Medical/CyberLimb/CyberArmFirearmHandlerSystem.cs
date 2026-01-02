// SPDX-FileCopyrightText: 2025 terkala <appleorange64@gmail.com>
//
// SPDX-License-Identifier: MIT

using Content.Shared.Body.Components;
using Content.Shared.Body.Part;
using Content.Shared.Body.Systems;
using Content.Shared.Containers.ItemSlots;
using Content.Shared.Hands.Components;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Interaction;
using Content.Shared.Medical.CyberLimb;
using Content.Shared.Medical.CyberLimb.Modules;
using Content.Shared.Storage;
using Content.Shared.Storage.EntitySystems;
using Content.Shared.Weapons.Ranged;
using Content.Shared.Weapons.Ranged.Components;
using Content.Shared.Weapons.Ranged.Events;
using Content.Shared.Weapons.Ranged.Systems;
using Content.Shared.Whitelist;
using Robust.Shared.Containers;
using Robust.Shared.Network;
using Robust.Shared.Timing;
using System.Linq;

namespace Content.Server.Medical.CyberLimb;

/// <summary>
/// System that handles automatic firearm operations for cyber-arms.
/// Automatically loads bullets into chamber and cocks firearms when they're active items.
/// Also handles auto-reload from cyber arms with auto-reload modules.
/// </summary>
public sealed class CyberArmFirearmHandlerSystem : EntitySystem
{
    [Dependency] private readonly SharedGunSystem _gunSystem = default!;
    [Dependency] private readonly IGameTiming _gameTiming = default!;
    [Dependency] private readonly SharedBodySystem _body = default!;
    [Dependency] private readonly SharedHandsSystem _hands = default!;
    [Dependency] private readonly ItemSlotsSystem _itemSlots = default!;
    [Dependency] private readonly EntityWhitelistSystem _whitelist = default!;
    [Dependency] private readonly SharedStorageSystem _storage = default!;
    [Dependency] private readonly SharedContainerSystem _containers = default!;
    [Dependency] private readonly INetManager _netManager = default!;

    private const float FirearmCheckInterval = 0.5f; // Check every 0.5 seconds

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<CyberArmActiveItemComponent, ComponentStartup>(OnStartup);
        SubscribeLocalEvent<CyberArmActiveItemComponent, ComponentShutdown>(OnShutdown);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var currentTime = _gameTiming.CurTime;

        // Handle active item firearm operations
        var query = EntityQueryEnumerator<CyberArmActiveItemComponent>();
        while (query.MoveNext(out var uid, out var component))
        {
            // Only process if there's an active item
            if (component.ActiveItem == null)
                continue;

            var activeItem = component.ActiveItem.Value;

            // Check if the active item is a firearm with chamber/magazine
            if (!TryComp<ChamberMagazineAmmoProviderComponent>(activeItem, out var ammoProvider))
                continue;

            // Check if enough time has passed since last check
            if (component.LastFirearmCheckTime != null)
            {
                var timeSinceLastCheck = (currentTime - component.LastFirearmCheckTime.Value).TotalSeconds;
                if (timeSinceLastCheck < FirearmCheckInterval)
                    continue;
            }

            component.LastFirearmCheckTime = currentTime;
            Dirty(uid, component);

            // Handle firearm operations
            HandleFirearmOperations(activeItem, ammoProvider);
        }

        // Handle auto-reload from cyber arms with auto-reload modules
        var autoReloadQuery = EntityQueryEnumerator<CyberLimbAutoReloadModuleComponent>();
        while (autoReloadQuery.MoveNext(out var moduleUid, out _))
        {
            // Find the cyber arm that contains this module
            if (!_containers.TryGetContainingContainer(moduleUid, out var container))
                continue;

            var cyberArm = container.Owner;

            // Get the body that owns this cyber arm
            if (!TryComp<BodyPartComponent>(cyberArm, out var part) || part.Body == null)
                continue;

            var user = part.Body.Value;

            // Check if user has an active hand with a firearm
            if (!TryComp<HandsComponent>(user, out var hands) || hands.ActiveHandEntity == null)
                continue;

            var heldGun = hands.ActiveHandEntity.Value;

            // Check if it's a firearm with magazine
            if (!TryComp<ChamberMagazineAmmoProviderComponent>(heldGun, out var gunAmmoProvider))
                continue;

            // Check if magazine is empty (check the magazine itself, not the gun's total ammo)
            bool needsReload = false;
            if (_containers.TryGetContainer(heldGun, "gun_magazine", out var magContainer) &&
                magContainer is ContainerSlot magSlotContainer &&
                magSlotContainer.ContainedEntity != null)
            {
                var magazine = magSlotContainer.ContainedEntity.Value;
                var magAmmoEv = new GetAmmoCountEvent();
                RaiseLocalEvent(magazine, ref magAmmoEv);
                if (magAmmoEv.Count == 0)
                    needsReload = true; // Magazine is empty, needs reload
            }
            else
            {
                // No magazine loaded, needs reload
                needsReload = true;
            }

            if (!needsReload)
                continue;

            // Try to auto-reload from this cyber arm
            TryAutoReload(cyberArm, heldGun, gunAmmoProvider, user);
        }
    }

    private void OnStartup(Entity<CyberArmActiveItemComponent> ent, ref ComponentStartup args)
    {
        // Initialize last check time
        ent.Comp.LastFirearmCheckTime = _gameTiming.CurTime;
        Dirty(ent);
    }

    private void OnShutdown(Entity<CyberArmActiveItemComponent> ent, ref ComponentShutdown args)
    {
        // Cleanup - nothing needed
    }

    /// <summary>
    /// Handles automatic firearm operations: loading chamber and cocking.
    /// </summary>
    private void HandleFirearmOperations(EntityUid firearm, ChamberMagazineAmmoProviderComponent ammoProvider)
    {
        // First, check if bolt needs to be closed (for shotguns and similar)
        // Closing the bolt will automatically cycle a cartridge if the chamber is empty
        if (ammoProvider.BoltClosed == false && ammoProvider.CanRack)
        {
            // Close the bolt (which will also cycle a cartridge if possible)
            _gunSystem.SetBoltClosed(firearm, ammoProvider, true, user: null);
            return; // SetBoltClosed handles cycling, so we're done
        }

        // If bolt is already closed and auto-cycle is enabled, check if we need to cycle
        // Since we can't directly check the chamber (GetChamberEntity is protected),
        // we'll check ammo count. If there's ammo available, we'll rack to ensure chamber is loaded.
        // Note: This will make a racking sound, but it's necessary to ensure the chamber is loaded.
        if (ammoProvider.BoltClosed == true && ammoProvider.CanRack && ammoProvider.AutoCycle)
        {
            // Check if there's ammo available
            var ammoEv = new GetAmmoCountEvent();
            RaiseLocalEvent(firearm, ref ammoEv);
            
            // If there's ammo available, rack the gun to ensure chamber is loaded
            // This will cycle a round if the chamber is empty
            if (ammoEv.Count > 0)
            {
                // Rack the gun (open and close bolt) - this will cycle a round if chamber is empty
                _gunSystem.ToggleBolt(firearm, ammoProvider, user: null);
                // The toggle opens it, now close it (which cycles if needed)
                _gunSystem.SetBoltClosed(firearm, ammoProvider, true, user: null);
            }
        }
    }

    /// <summary>
    /// Attempts to auto-reload a firearm from a cyber arm with an auto-reload module.
    /// </summary>
    private void TryAutoReload(EntityUid cyberArm, EntityUid firearm, ChamberMagazineAmmoProviderComponent ammoProvider, EntityUid user)
    {
        if (!TryComp<StorageComponent>(cyberArm, out var storage))
            return;

        // Get the gun's magazine slot whitelist to check compatibility
        if (!_itemSlots.TryGetSlot(firearm, "gun_magazine", out var magSlot))
            return;

        // Step 1: Check for compatible magazines in storage
        var compatibleMagazine = FindCompatibleMagazine(cyberArm, storage, magSlot);
        if (compatibleMagazine != null)
        {
            // Get current magazine entity
            if (_containers.TryGetContainer(firearm, "gun_magazine", out var magContainer) &&
                magContainer is ContainerSlot magSlotContainer &&
                magSlotContainer.ContainedEntity != null)
            {
                // Eject current magazine by removing it from the slot
                _itemSlots.TryEject(firearm, "gun_magazine", user, out _, excludeUserAudio: true);
            }
            
            // Insert the new magazine
            if (_itemSlots.TryInsert(firearm, "gun_magazine", compatibleMagazine.Value, user, excludeUserAudio: true))
            {
                return; // Successfully reloaded with magazine
            }
        }

        // Step 2: Check for compatible ammo boxes and add one bullet
        var compatibleAmmoBox = FindCompatibleAmmoBox(cyberArm, storage, firearm);
        if (compatibleAmmoBox != null)
        {
            // Get the current magazine
            if (_containers.TryGetContainer(firearm, "gun_magazine", out var currentMagContainer) &&
                currentMagContainer is ContainerSlot currentMagSlot &&
                currentMagSlot.ContainedEntity != null)
            {
                var currentMag = currentMagSlot.ContainedEntity.Value;
                
                // Try to add one bullet from the ammo box to the magazine
                if (TryAddBulletFromAmmoBox(compatibleAmmoBox.Value, currentMag))
                {
                    // Ammo count will be updated automatically by the ammo system
                    return;
                }
            }
        }
    }

    /// <summary>
    /// Finds a compatible magazine in the cyber arm storage.
    /// </summary>
    private EntityUid? FindCompatibleMagazine(EntityUid cyberArm, StorageComponent storage, ItemSlot magSlot)
    {
        foreach (var item in storage.Container.ContainedEntities)
        {
            if (!item.IsValid())
                continue;

            // Check if this item passes the gun's magazine whitelist
            if (!_whitelist.IsWhitelistFailOrNull(magSlot.Whitelist, item) &&
                !_whitelist.IsBlacklistPass(magSlot.Blacklist, item))
            {
                return item;
            }
        }

        return null;
    }

    /// <summary>
    /// Finds a compatible ammo box in the cyber arm storage.
    /// </summary>
    private EntityUid? FindCompatibleAmmoBox(EntityUid cyberArm, StorageComponent storage, EntityUid firearm)
    {
        // Get the gun's chamber whitelist to determine calibre
        if (!_itemSlots.TryGetSlot(firearm, "gun_chamber", out var chamberSlot))
            return null;

        foreach (var item in storage.Container.ContainedEntities)
        {
            if (!item.IsValid())
                continue;

            // Check if this is an ammo box (has BallisticAmmoProvider)
            if (!TryComp<BallisticAmmoProviderComponent>(item, out var ammoBox))
                continue;

            // Check if the ammo box's whitelist matches the gun's chamber whitelist
            // (i.e., same calibre)
            // We check if the ammo box whitelist would accept items that match the chamber whitelist
            if (ammoBox.Whitelist != null && chamberSlot.Whitelist != null)
            {
                // Check if they have matching tags/components
                if (ammoBox.Whitelist.Tags != null && chamberSlot.Whitelist.Tags != null)
                {
                    // Check if there's any overlap in tags (same calibre)
                    var hasOverlap = ammoBox.Whitelist.Tags.Any(tag => chamberSlot.Whitelist.Tags.Contains(tag));
                    if (hasOverlap)
                        return item;
                }
                
                // Also check components if tags don't match
                if (ammoBox.Whitelist.Components != null && chamberSlot.Whitelist.Components != null)
                {
                    var hasOverlap = ammoBox.Whitelist.Components.Any(comp => chamberSlot.Whitelist.Components.Contains(comp));
                    if (hasOverlap)
                        return item;
                }
            }
        }

        return null;
    }

    /// <summary>
    /// Tries to add one bullet from an ammo box to a magazine.
    /// </summary>
    private bool TryAddBulletFromAmmoBox(EntityUid ammoBox, EntityUid magazine)
    {
        if (!TryComp<BallisticAmmoProviderComponent>(ammoBox, out var ammoBoxComp))
            return false;

        // Check if ammo box has ammo
        var ammoCountEv = new GetAmmoCountEvent();
        RaiseLocalEvent(ammoBox, ref ammoCountEv);
        if (ammoCountEv.Count == 0)
            return false;

        // Check if magazine can accept more ammo
        var magAmmoCountEv = new GetAmmoCountEvent();
        RaiseLocalEvent(magazine, ref magAmmoCountEv);
        if (magAmmoCountEv.Count >= magAmmoCountEv.Capacity)
            return false;

        // Try to take one bullet from the ammo box
        var takeAmmoEv = new TakeAmmoEvent(1, new List<(EntityUid? Entity, IShootable Shootable)>(), Transform(ammoBox).Coordinates, null);
        RaiseLocalEvent(ammoBox, takeAmmoEv);

        if (takeAmmoEv.Ammo.Count == 0)
            return false;

        var bullet = takeAmmoEv.Ammo[0].Entity;
        if (bullet == null)
            return false;

        // Try to insert the bullet into the magazine using InteractUsingEvent
        // This will trigger the magazine's BallisticAmmoProvider to accept the bullet
        var interactEv = new InteractUsingEvent(user: EntityUid.Invalid, used: bullet.Value, target: magazine, clickLocation: Transform(magazine).Coordinates);
        RaiseLocalEvent(magazine, interactEv);

        if (interactEv.Handled)
        {
            // The interaction should have played the loading sound automatically
            return true;
        }

        // If interaction didn't handle it, clean up the bullet
        if (_netManager.IsServer)
            QueueDel(bullet.Value);

        return false;
    }
}
