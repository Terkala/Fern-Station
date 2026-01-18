// SPDX-FileCopyrightText: 2025 terkala <appleorange64@gmail.com>
//
// SPDX-License-Identifier: MIT

using Content.Client.Medical.Limb.Targeting;
using Content.Shared.Medical.Limb.Targeting;
using Robust.Client.UserInterface;

namespace Content.Client.Medical.Limb.Targeting;

/// <summary>
/// Client-side targeting system that handles UI and network events.
/// </summary>
public sealed class TargetingSystem : SharedTargetingSystem
{
    [Dependency] private readonly IUserInterfaceManager _uiManager = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<LimbTargetingComponent, AfterAutoHandleStateEvent>(OnComponentStateChanged);
    }

    private void OnComponentStateChanged(Entity<LimbTargetingComponent> ent, ref AfterAutoHandleStateEvent args)
    {
        // Notify UI controller of state change
        var controller = _uiManager.GetUIController<TargetingUIController>();
        controller.HandleComponentUpdate(ent.Comp);
    }
}
