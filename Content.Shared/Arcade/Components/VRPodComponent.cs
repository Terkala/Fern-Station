// SPDX-FileCopyrightText: 2025 terkala <appleorange64@gmail.com>
//
// SPDX-License-Identifier: MIT

using Content.Shared.Storage;
using Robust.Shared.GameStates;
using Robust.Shared.Map;

namespace Content.Shared.Arcade.Components;

/// <summary>
/// Component for VR Pods that allow players to access tutorials.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class VRPodComponent : Component
{
    /// <summary>
    /// The tutorial body entity that the player's mind is visiting.
    /// </summary>
    [DataField, AutoNetworkedField]
    public EntityUid? ActiveTutorial;

    /// <summary>
    /// The map ID of the loaded tutorial map.
    /// </summary>
    [DataField, AutoNetworkedField]
    public MapId? TutorialMapId;

    /// <summary>
    /// The original body entity stored in the pod.
    /// </summary>
    [DataField, AutoNetworkedField]
    public EntityUid? OriginalBody;

    /// <summary>
    /// The ID of the selected tutorial prototype.
    /// </summary>
    [DataField, AutoNetworkedField]
    public string? SelectedTutorial;

    /// <summary>
    /// When the tutorial started, for enforcing the 5-minute time limit.
    /// </summary>
    [DataField, AutoNetworkedField]
    public TimeSpan? TutorialStartTime;
}


