// SPDX-FileCopyrightText: 2025 terkala <appleorange64@gmail.com>
//
// SPDX-License-Identifier: MIT

using Content.Server.Arcade.Components;
using Content.Shared.Arcade.Components;
using Content.Shared.Destructible;

namespace Content.Server.Arcade.EntitySystems;

/// <summary>
/// System for handling tutorial bodies and their connection to VR Pods.
/// </summary>
public sealed class TutorialBodySystem : EntitySystem
{
    [Dependency] private readonly VRPodSystem _vrPodSystem = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<TutorialBodyComponent, EntityTerminatingEvent>(OnTutorialBodyTerminating);
    }

    private void OnTutorialBodyTerminating(Entity<TutorialBodyComponent> ent, ref EntityTerminatingEvent args)
    {
        // If the tutorial body is being destroyed, trigger emergency return
        if (ent.Comp.VRPod != null && Exists(ent.Comp.VRPod.Value))
        {
            if (TryComp<VRPodComponent>(ent.Comp.VRPod.Value, out var vrPod))
            {
                _vrPodSystem.EmergencyReturn(ent.Comp.VRPod.Value, vrPod);
            }
        }
    }
}


