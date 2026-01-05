// SPDX-FileCopyrightText: 2025 terkala <appleorange64@gmail.com>
//
// SPDX-License-Identifier: MIT

using Robust.Shared.GameStates;

namespace Content.Shared.Medical.CyberLimb;

/// <summary>
/// Component for implants that add power draw to the body's battery.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class CyberImplantPowerDrawComponent : Component
{
    /// <summary>
    /// How much power this implant draws in watts (joules per second).
    /// </summary>
    [DataField(required: true), ViewVariables, AutoNetworkedField]
    public float PowerDrawWatts = 0f;
}
