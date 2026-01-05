// SPDX-FileCopyrightText: 2025 terkala <appleorange64@gmail.com>
//
// SPDX-License-Identifier: MIT

using Robust.Shared.GameStates;
using Robust.Shared.Serialization;

namespace Content.Shared.Medical.Surgery.Components;

/// <summary>
/// Component that tracks individual step completion within surgery sequences.
/// Enables bidirectional operations by tracking which steps have been completed.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class SurgeryStepProgressComponent : Component
{
    /// <summary>
    /// Tracks progress for each sequence. Key: sequence ID (e.g., "RetractSkin"), 
    /// Value: index of the last completed step (0-based, -1 if none completed).
    /// </summary>
    [DataField, AutoNetworkedField]
    public Dictionary<string, int> SequenceProgress = new();

    /// <summary>
    /// List of completed step entity IDs for tracking which specific steps have been done.
    /// </summary>
    [DataField, AutoNetworkedField]
    public List<string> CompletedSteps = new();

    /// <summary>
    /// Maps sequence ID to ordered list of step IDs in that sequence.
    /// Used to determine next available step in forward or reverse direction.
    /// </summary>
    [DataField, AutoNetworkedField]
    public Dictionary<string, List<string>> SequenceSteps = new();
}
