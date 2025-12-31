// SPDX-FileCopyrightText: 2024 AJCM-git <60196617+AJCM-git@users.noreply.github.com>
// SPDX-FileCopyrightText: 2025 taydeo <td12233a@gmail.com>
//
// SPDX-License-Identifier: MIT

using Content.Shared.Hands;
using Content.Shared.Inventory.VirtualItem;
using Content.Shared.Interaction;
using Content.Server.Medical.CyberLimb;

namespace Content.Server.Inventory;

public sealed class VirtualItemSystem : SharedVirtualItemSystem
{
    [Dependency] private readonly CyberArmActiveItemSystem _cyberArmActiveItem = default!;

    protected override void OnGetUsedEntity(Entity<VirtualItemComponent> ent, ref GetUsedEntityEvent args)
    {
        // First, try the base implementation (checks if user is holding the real item)
        base.OnGetUsedEntity(ent, ref args);
        
        // If base didn't handle it, check for cyber arm virtual items
        if (args.Handled)
            return;

        _cyberArmActiveItem.OnVirtualItemGetUsedEntity(ent, ref args);
    }
}
