// SPDX-FileCopyrightText: 2025 terkala <appleorange64@gmail.com>
//
// SPDX-License-Identifier: MIT

using Content.Shared.Atmos.Prototypes;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared.Medical.Cyber.Components;

/// <summary>
/// Slot component for the cybernetic lungs.
/// Tracks maintenance state, service time, and slot-specific properties (like breath gas type).
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class CyberneticLungComponent : CyberneticSlotComponent
{
    /// <summary>
    /// The gas type that the user breathes with these cybernetic lungs.
    /// </summary>
    [DataField, AutoNetworkedField]
    public ProtoId<GasPrototype> BreathGas = "GasNitrogen";
}
