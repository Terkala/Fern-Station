// SPDX-FileCopyrightText: 2025 terkala <appleorange64@gmail.com>
//
// SPDX-License-Identifier: MIT

using Content.Shared.FixedPoint;
using Robust.Shared.GameStates;

namespace Content.Shared.Medical.Surgery;

/// <summary>
/// Component that tracks unsanitary conditions penalty for a patient.
/// This penalty is applied when surgery goes below skin level in an unsanitary environment.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class UnsanitaryConditionsComponent : Component
{
    /// <summary>
    /// Current unsanitary conditions penalty (0-3).
    /// This contributes to bio-rejection damage.
    /// </summary>
    [DataField, ViewVariables, AutoNetworkedField]
    public FixedPoint2 Penalty = FixedPoint2.Zero;

    /// <summary>
    /// Whether the penalty has been applied (i.e., surgery has gone below skin level).
    /// </summary>
    [DataField, ViewVariables, AutoNetworkedField]
    public bool PenaltyApplied = false;
}

