// SPDX-FileCopyrightText: 2025 terkala <appleorange64@gmail.com>
//
// SPDX-License-Identifier: MIT

using Robust.Shared.GameObjects;

namespace Content.Shared.Medical.Surgery.Operations;

/// <summary>
/// Result of evaluating whether a secondary/improvised method can be used for a surgery operation.
/// </summary>
public sealed record SurgeryOperationEvaluationResult
{
    /// <summary>
    /// Whether the evaluation was successful (tool/method is valid).
    /// </summary>
    public bool IsValid { get; init; }

    /// <summary>
    /// Speed modifier based on tool quality (1.0 = normal speed).
    /// </summary>
    public float SpeedModifier { get; init; } = 1.0f;

    /// <summary>
    /// The tool entity that was used, if any.
    /// </summary>
    public EntityUid? ToolEntity { get; init; }

    public SurgeryOperationEvaluationResult(bool isValid, float speedModifier = 1.0f, EntityUid? toolEntity = null)
    {
        IsValid = isValid;
        SpeedModifier = speedModifier;
        ToolEntity = toolEntity;
    }

    public static SurgeryOperationEvaluationResult Invalid() => new(false);
    public static SurgeryOperationEvaluationResult Valid(float speedModifier = 1.0f, EntityUid? toolEntity = null) => new(true, speedModifier, toolEntity);
}
