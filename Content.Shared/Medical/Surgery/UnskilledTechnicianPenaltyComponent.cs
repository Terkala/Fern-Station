// SPDX-FileCopyrightText: 2025 terkala <appleorange64@gmail.com>
//
// SPDX-License-Identifier: MIT

using Content.Shared.FixedPoint;
using Robust.Shared.GameStates;

namespace Content.Shared.Medical.Surgery;

/// <summary>
/// Component that tracks unskilled technician penalties on a body part.
/// Applied when non-technician personnel perform cyberlimb maintenance (adjust bolts or replace wiring),
/// causing +2 bio-rejection. Can be removed by skilled technicians performing the maintenance step.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class UnskilledTechnicianPenaltyComponent : Component
{
    /// <summary>
    /// The bio-rejection penalty from unskilled technician work (+2).
    /// </summary>
    [DataField, AutoNetworkedField]
    public FixedPoint2 Penalty = FixedPoint2.New(2);
}

