// SPDX-FileCopyrightText: 2025 terkala <appleorange64@gmail.com>
//
// SPDX-License-Identifier: MIT

using Content.Shared.Body.Part;
using Robust.Shared.GameStates;

namespace Content.Shared.Medical.Surgery;

/// <summary>
/// Component that tracks which surgery layers have been completed on a body part.
/// This determines which layers are accessible in the surgery UI.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class SurgeryLayerComponent : Component
{
    /// <summary>
    /// Whether the skin layer has been retracted on this body part.
    /// </summary>
    [DataField, AutoNetworkedField]
    public bool SkinRetracted = false;

    /// <summary>
    /// Whether the tissue layer has been retracted on this body part.
    /// </summary>
    [DataField, AutoNetworkedField]
    public bool TissueRetracted = false;

    /// <summary>
    /// Whether bones have been sawed through (for torso) or skull has been sawed through (for head).
    /// </summary>
    [DataField, AutoNetworkedField]
    public bool BonesSawed = false;

    /// <summary>
    /// Whether bones have been smashed (crude surgery) instead of sawed.
    /// Smashed bones require a 5-stage repair process and apply 2x the penalty.
    /// </summary>
    [DataField, AutoNetworkedField]
    public bool BonesSmashed = false;

    /// <summary>
    /// The body part type this layer component is attached to.
    /// Used to determine which operations are available.
    /// </summary>
    [DataField, AutoNetworkedField]
    public BodyPartType? PartType;
}

