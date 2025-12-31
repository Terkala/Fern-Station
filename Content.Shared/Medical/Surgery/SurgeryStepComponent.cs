// SPDX-FileCopyrightText: 2025 terkala <appleorange64@gmail.com>
//
// SPDX-License-Identifier: MIT

using Content.Shared.Body.Part;
using Content.Shared.Medical.Surgery.Operations;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared.Medical.Surgery;

/// <summary>
/// Component that defines a single step in a surgery procedure.
/// Each step can have conditions, effects, and debilitations.
/// </summary>
[RegisterComponent, NetworkedComponent]
[Prototype("SurgerySteps")]
public sealed partial class SurgeryStepComponent : Component
{
    /// <summary>
    /// Duration of this step in seconds.
    /// </summary>
    [DataField]
    public float Duration = 2f;

    /// <summary>
    /// Required tool component types for this step.
    /// If null, no specific tool is required.
    /// </summary>
    [DataField]
    public ComponentRegistry? Tool;

    /// <summary>
    /// Components to add to the body part when this step completes.
    /// </summary>
    [DataField]
    public ComponentRegistry? Add;

    /// <summary>
    /// Components to remove from the body part when this step completes.
    /// </summary>
    [DataField]
    public ComponentRegistry? Remove;

    /// <summary>
    /// Components to add to the body when this step completes.
    /// </summary>
    [DataField]
    public ComponentRegistry? BodyAdd;

    /// <summary>
    /// Components to remove from the body when this step completes.
    /// </summary>
    [DataField]
    public ComponentRegistry? BodyRemove;


    /// <summary>
    /// Which surgery layer this step belongs to.
    /// </summary>
    [DataField]
    public SurgeryLayer Layer = SurgeryLayer.Skin;

    /// <summary>
    /// Body part types this step can be performed on.
    /// If empty, can be performed on any part.
    /// </summary>
    [DataField]
    public List<BodyPartType> ValidPartTypes = new();

    /// <summary>
    /// Required surgery layer state before this step can be performed.
    /// For example, tissue steps require skin to be retracted.
    /// </summary>
    [DataField]
    public SurgeryLayerRequirements? Requirements;

    /// <summary>
    /// For organ layer steps: the organ slot ID this step targets (e.g., "heart", "lungs", "stomach").
    /// If specified, this step will only be shown if the body part has this organ slot defined.
    /// This ensures species-specific surgeries (e.g., heart surgery) don't appear for species
    /// that don't have that organ slot (e.g., Diona has no heart slot, so heart surgery won't appear).
    /// If null, the step is generic and applies to all organs that exist on the body part.
    /// </summary>
    [DataField]
    public string? TargetOrganSlot;

    /// <summary>
    /// Optional reference to a surgery operation prototype.
    /// If set, this step will use operation-based tool validation with primary and secondary methods.
    /// If null, the step uses the legacy Tool field for backward compatibility.
    /// </summary>
    [DataField]
    public ProtoId<SurgeryOperationPrototype>? OperationId;
}

/// <summary>
/// Requirements for a surgery step based on layer state.
/// </summary>
[DataRecord]
public sealed record SurgeryLayerRequirements
{
    /// <summary>
    /// Whether skin must be retracted before this step can be performed.
    /// </summary>
    [DataField]
    public bool RequiresSkinRetracted = false;

    /// <summary>
    /// Whether tissue must be retracted before this step can be performed.
    /// </summary>
    [DataField]
    public bool RequiresTissueRetracted = false;

    /// <summary>
    /// Whether bones must be sawed before this step can be performed.
    /// </summary>
    [DataField]
    public bool RequiresBonesSawed = false;
}


