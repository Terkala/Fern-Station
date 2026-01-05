// SPDX-FileCopyrightText: 2025 terkala <appleorange64@gmail.com>
//
// SPDX-License-Identifier: MIT

using Robust.Shared.GameStates;

namespace Content.Shared.Medical.CyberLimb;

/// <summary>
/// Component for implants that provide battery capacity to the body.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class CyberImplantBatteryComponent : Component
{
    /// <summary>
    /// Maximum charge capacity of this battery in joules.
    /// </summary>
    [DataField(required: true), ViewVariables, AutoNetworkedField]
    public float MaxCharge = 0f;
}
