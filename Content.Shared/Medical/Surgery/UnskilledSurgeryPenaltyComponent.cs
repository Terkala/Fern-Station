// SPDX-FileCopyrightText: 2025 terkala <appleorange64@gmail.com>
//
// SPDX-License-Identifier: MIT

using Content.Shared.FixedPoint;
using Robust.Shared.GameStates;

namespace Content.Shared.Medical.Surgery;

/// <summary>
/// Component that tracks unskilled surgery penalties on a body part.
/// Applied when non-medical personnel perform surgery, causing +2 bio-rejection.
/// Can be removed by medical personnel.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class UnskilledSurgeryPenaltyComponent : Component
{
    /// <summary>
    /// The bio-rejection penalty from unskilled surgery (+2).
    /// </summary>
    [DataField, AutoNetworkedField]
    public FixedPoint2 Penalty = FixedPoint2.New(2);
}

