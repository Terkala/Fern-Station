// SPDX-FileCopyrightText: 2025 terkala <appleorange64@gmail.com>
//
// SPDX-License-Identifier: MIT

using Content.Shared.Body.Part;
using Robust.Shared.Serialization;

namespace Content.Shared.Medical.Surgery;

[Serializable, NetSerializable]
public enum SurgeryUIKey : byte
{
    Key
}

/// <summary>
/// State for the surgery UI showing available operations by layer.
/// </summary>
[Serializable, NetSerializable]
public sealed class SurgeryBoundUserInterfaceState : BoundUserInterfaceState
{
    /// <summary>
    /// The body part being operated on.
    /// </summary>
    public NetEntity BodyPart;

    /// <summary>
    /// The body part type.
    /// </summary>
    public BodyPartType? PartType;

    /// <summary>
    /// Whether skin layer is retracted.
    /// </summary>
    public bool SkinRetracted;

    /// <summary>
    /// Whether tissue layer is retracted.
    /// </summary>
    public bool TissueRetracted;

    /// <summary>
    /// Whether bones are sawed through.
    /// </summary>
    public bool BonesSawed;

    /// <summary>
    /// Whether bones are smashed (crude surgery).
    /// </summary>
    public bool BonesSmashed;

    /// <summary>
    /// Available surgery steps for the skin layer.
    /// </summary>
    public List<NetEntity> SkinSteps = new();

    /// <summary>
    /// Available surgery steps for the tissue layer.
    /// </summary>
    public List<NetEntity> TissueSteps = new();

    /// <summary>
    /// Available surgery steps for the organ layer.
    /// </summary>
    public List<NetEntity> OrganSteps = new();

    public SurgeryBoundUserInterfaceState(
        NetEntity bodyPart,
        BodyPartType? partType,
        bool skinRetracted,
        bool tissueRetracted,
        bool bonesSawed,
        List<NetEntity> skinSteps,
        List<NetEntity> tissueSteps,
        List<NetEntity> organSteps,
        bool bonesSmashed = false)
    {
        BodyPart = bodyPart;
        PartType = partType;
        SkinRetracted = skinRetracted;
        TissueRetracted = tissueRetracted;
        BonesSawed = bonesSawed;
        BonesSmashed = bonesSmashed;
        SkinSteps = skinSteps;
        TissueSteps = tissueSteps;
        OrganSteps = organSteps;
    }
}

/// <summary>
/// Message sent when a surgery step is selected.
/// </summary>
[Serializable, NetSerializable]
public sealed class SurgeryStepSelectedMessage : BoundUserInterfaceMessage
{
    public NetEntity Step;
    public SurgeryLayer Layer;
    public NetEntity? User;

    public SurgeryStepSelectedMessage(NetEntity step, SurgeryLayer layer, NetEntity? user = null)
    {
        Step = step;
        Layer = layer;
        User = user;
    }
}

/// <summary>
/// Message sent when switching to a different layer tab.
/// </summary>
[Serializable, NetSerializable]
public sealed class SurgeryLayerChangedMessage : BoundUserInterfaceMessage
{
    public SurgeryLayer Layer;

    public SurgeryLayerChangedMessage(SurgeryLayer layer)
    {
        Layer = layer;
    }
}

