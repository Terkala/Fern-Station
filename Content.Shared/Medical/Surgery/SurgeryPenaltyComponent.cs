// SPDX-FileCopyrightText: 2025 terkala <appleorange64@gmail.com>
//
// SPDX-License-Identifier: MIT

using Content.Shared.FixedPoint;
using Robust.Shared.GameStates;

namespace Content.Shared.Medical.Surgery;

/// <summary>
/// Component that tracks temporary integrity penalty from incomplete surgeries.
/// This penalty is applied when the body is opened (e.g., bones sawed through)
/// and is removed when the surgery is closed.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class SurgeryPenaltyComponent : Component
{
    /// <summary>
    /// The target integrity penalty amount.
    /// This gradually applies over time.
    /// </summary>
    [DataField, AutoNetworkedField]
    public FixedPoint2 TargetPenalty = FixedPoint2.Zero;

    /// <summary>
    /// Current integrity penalty being applied.
    /// Gradually adjusts toward TargetPenalty.
    /// </summary>
    [ViewVariables, AutoNetworkedField]
    public FixedPoint2 CurrentPenalty = FixedPoint2.Zero;
}

