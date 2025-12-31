// SPDX-FileCopyrightText: 2025 terkala <appleorange64@gmail.com>
//
// SPDX-License-Identifier: MIT

using Content.Shared.FixedPoint;
using Robust.Shared.Prototypes;

namespace Content.Shared.Medical.Surgery.Operations;

/// <summary>
/// Prototype that defines a surgical operation type with primary tools and optional secondary improvised methods.
/// </summary>
[Prototype("surgeryOperation")]
public sealed partial class SurgeryOperationPrototype : IPrototype
{
    [IdDataField]
    public string ID { get; private set; } = default!;

    /// <summary>
    /// Display name of the operation.
    /// </summary>
    [DataField("name")]
    public string Name { get; private set; } = string.Empty;

    /// <summary>
    /// Primary tool component types that are proper surgical instruments for this operation.
    /// If any of these are available, the operation can be performed as a primary operation.
    /// </summary>
    [DataField("primaryTools")]
    public List<ComponentRegistry> PrimaryTools { get; private set; } = new();

    /// <summary>
    /// Optional secondary method configuration for improvised surgery.
    /// If specified, allows the operation to be performed with alternative tools/methods.
    /// </summary>
    [DataField("secondaryMethod")]
    public SurgeryOperationSecondaryMethod? SecondaryMethod { get; private set; }

    /// <summary>
    /// If this is a repair operation, this links to the operation it repairs.
    /// </summary>
    [DataField("repairOperationFor")]
    public ProtoId<SurgeryOperationPrototype>? RepairOperationFor { get; private set; }

    /// <summary>
    /// For repair operations: the integrity cost that will be removed when this repair is performed.
    /// </summary>
    [DataField("repairIntegrityCost")]
    public FixedPoint2? RepairIntegrityCost { get; private set; }
}

/// <summary>
/// Configuration for secondary/improvised methods of performing an operation.
/// </summary>
[DataRecord]
public sealed partial record SurgeryOperationSecondaryMethod
{
    /// <summary>
    /// Type of secondary method (e.g., "Bashing", "Slashing", "Heat", "ToolList", "MultiEvaluator").
    /// </summary>
    [DataField("type", required: true)]
    public string Type { get; private set; } = string.Empty;

    /// <summary>
    /// Name of the evaluator to call (e.g., "CheckBluntDamage", "CheckSlashDamage", "CheckHeatDamage").
    /// </summary>
    [DataField("evaluator", required: true)]
    public string Evaluator { get; private set; } = string.Empty;

    /// <summary>
    /// For MultiEvaluator type: list of evaluators to check with OR logic.
    /// </summary>
    [DataField("evaluators")]
    public List<SurgeryOperationEvaluatorConfig>? Evaluators { get; private set; }

    /// <summary>
    /// For ToolList evaluator: list of tool component types to check for.
    /// </summary>
    [DataField("tools")]
    public List<ComponentRegistry>? Tools { get; private set; }

    /// <summary>
    /// Flat integrity cost applied when this secondary method is used.
    /// </summary>
    [DataField("integrityCost")]
    public FixedPoint2 IntegrityCost { get; private set; } = FixedPoint2.Zero;

    /// <summary>
    /// Reference to the repair operation that can fix the damage from this improvised method.
    /// </summary>
    [DataField("repairOperation")]
    public ProtoId<SurgeryOperationPrototype>? RepairOperation { get; private set; }
}

/// <summary>
/// Configuration for individual evaluators in a MultiEvaluator.
/// </summary>
[DataRecord]
public sealed partial record SurgeryOperationEvaluatorConfig
{
    /// <summary>
    /// Name of the evaluator to call.
    /// </summary>
    [DataField("evaluator", required: true)]
    public string Evaluator { get; private set; } = string.Empty;

    /// <summary>
    /// For ToolList evaluator: list of tool component types to check for.
    /// </summary>
    [DataField("tools")]
    public List<ComponentRegistry>? Tools { get; private set; }
}
