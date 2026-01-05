// SPDX-FileCopyrightText: 2025 terkala <appleorange64@gmail.com>
//
// SPDX-License-Identifier: MIT

using Robust.Shared.Prototypes;

namespace Content.Shared.Medical.Surgery.Prototypes;

/// <summary>
/// Prototype that defines a bidirectional surgery step sequence.
/// Enables operations like Retract Skin ↔ Close Skin where steps can be reversed mid-sequence.
/// </summary>
[Prototype("surgerySequence")]
public sealed partial class SurgerySequencePrototype : IPrototype
{
    /// <inheritdoc/>
    [IdDataField]
    public string ID { get; private set; } = default!;

    /// <summary>
    /// Which surgery layer this sequence belongs to.
    /// </summary>
    [DataField(required: true)]
    public SurgeryLayer Layer { get; private set; }

    /// <summary>
    /// List of step IDs for the forward direction (e.g., retracting skin).
    /// Steps are executed in order.
    /// </summary>
    [DataField(required: true)]
    public List<EntProtoId> ForwardSteps { get; private set; } = new();

    /// <summary>
    /// List of step IDs for the reverse direction (e.g., closing skin).
    /// Steps are executed in reverse order of forward steps.
    /// </summary>
    [DataField(required: true)]
    public List<EntProtoId> ReverseSteps { get; private set; } = new();

    /// <summary>
    /// ID of the paired sequence (the reverse sequence).
    /// For example, "RetractSkinSequence" pairs with "CloseSkinSequence".
    /// </summary>
    [DataField]
    public ProtoId<SurgerySequencePrototype>? PairedSequence;
}
