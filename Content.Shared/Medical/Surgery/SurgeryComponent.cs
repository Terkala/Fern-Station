// SPDX-FileCopyrightText: 2025 terkala <appleorange64@gmail.com>
//
// SPDX-License-Identifier: MIT

using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared.Medical.Surgery;

/// <summary>
/// Component that defines a surgery procedure with a flowchart of steps.
/// Surgeries are data-driven and execute steps in order (exterior to interior).
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
[Prototype("Surgeries")]
public sealed partial class SurgeryComponent : Component
{
    /// <summary>
    /// Priority of this surgery. Lower numbers are higher priority.
    /// </summary>
    [DataField, AutoNetworkedField]
    public int Priority = 0;

    /// <summary>
    /// Required surgery that must be completed before this one can be performed.
    /// For example, "OpenIncision" must be done before "AccessOrgan".
    /// </summary>
    [DataField, AutoNetworkedField]
    public EntProtoId? Requirement;

    /// <summary>
    /// List of surgery step IDs that make up this surgery procedure.
    /// Steps are executed in order, representing the flowchart from exterior to interior.
    /// </summary>
    [DataField(required: true), AutoNetworkedField]
    public List<EntProtoId> Steps = new();
}


