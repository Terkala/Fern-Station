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

    /// <summary>
    /// Operation availability info for each step.
    /// Key: Step entity, Value: Operation info (has primary tools, has secondary method, is repair operation)
    /// </summary>
    public Dictionary<NetEntity, SurgeryStepOperationInfo> StepOperationInfo = new();

    public SurgeryBoundUserInterfaceState(
        NetEntity bodyPart,
        BodyPartType? partType,
        bool skinRetracted,
        bool tissueRetracted,
        bool bonesSawed,
        List<NetEntity> skinSteps,
        List<NetEntity> tissueSteps,
        List<NetEntity> organSteps,
        bool bonesSmashed = false,
        Dictionary<NetEntity, SurgeryStepOperationInfo>? stepOperationInfo = null)
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
        StepOperationInfo = stepOperationInfo ?? new();
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

/// <summary>
/// Information about operation availability for a surgery step.
/// </summary>
[Serializable, NetSerializable]
public sealed class SurgeryStepOperationInfo
{
    /// <summary>
    /// Whether primary tools are available for this step.
    /// </summary>
    public bool HasPrimaryTools { get; init; }

    /// <summary>
    /// Whether secondary/improvised method is available for this step.
    /// </summary>
    public bool HasSecondaryMethod { get; init; }

    /// <summary>
    /// Whether this is a repair operation.
    /// </summary>
    public bool IsRepairOperation { get; init; }

    /// <summary>
    /// Operation name for display.
    /// </summary>
    public string OperationName { get; init; } = string.Empty;

    public SurgeryStepOperationInfo(bool hasPrimaryTools, bool hasSecondaryMethod, bool isRepairOperation, string operationName)
    {
        HasPrimaryTools = hasPrimaryTools;
        HasSecondaryMethod = hasSecondaryMethod;
        IsRepairOperation = isRepairOperation;
        OperationName = operationName;
    }
}

/// <summary>
/// Message sent when user selects primary or improvised method for a step.
/// </summary>
[Serializable, NetSerializable]
public sealed class SurgeryOperationMethodSelectedMessage : BoundUserInterfaceMessage
{
    public NetEntity Step;
    public bool IsImprovised;

    public SurgeryOperationMethodSelectedMessage(NetEntity step, bool isImprovised)
    {
        Step = step;
        IsImprovised = isImprovised;
    }
}

