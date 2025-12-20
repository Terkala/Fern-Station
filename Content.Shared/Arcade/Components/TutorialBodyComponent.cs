// SPDX-FileCopyrightText: 2025 terkala <appleorange64@gmail.com>
//
// SPDX-License-Identifier: MIT

using Robust.Shared.GameStates;

namespace Content.Shared.Arcade.Components;

/// <summary>
/// Component on tutorial bodies that tracks the VR Pod they're connected to.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class TutorialBodyComponent : Component
{
    /// <summary>
    /// The VR Pod entity that spawned this tutorial body.
    /// </summary>
    [DataField, AutoNetworkedField]
    public EntityUid? VRPod;
}


