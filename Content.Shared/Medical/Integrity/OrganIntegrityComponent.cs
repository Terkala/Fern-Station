// SPDX-FileCopyrightText: 2025 terkala <appleorange64@gmail.com>
//
// SPDX-License-Identifier: MIT

using Content.Shared.FixedPoint;
using Robust.Shared.GameStates;

namespace Content.Shared.Medical.Integrity;

/// <summary>
/// Component that defines the base integrity cost for an organ.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class OrganIntegrityComponent : Component
{
    /// <summary>
    /// Base integrity cost for this organ.
    /// This will be modified by tool quality, equipment quality, and compatibility.
    /// </summary>
    [DataField, AutoNetworkedField]
    public FixedPoint2 BaseIntegrityCost = FixedPoint2.New(1);
}

