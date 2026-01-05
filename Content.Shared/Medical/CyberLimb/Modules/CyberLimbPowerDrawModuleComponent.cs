// SPDX-FileCopyrightText: 2025 terkala <appleorange64@gmail.com>
//
// SPDX-License-Identifier: MIT

using Robust.Shared.GameStates;

namespace Content.Shared.Medical.CyberLimb.Modules;

/// <summary>
/// Component for modules that draw power from the body's battery.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class CyberLimbPowerDrawModuleComponent : Component
{
    /// <summary>
    /// How much power this module draws in watts (joules per second).
    /// </summary>
    [DataField(required: true), ViewVariables, AutoNetworkedField]
    public float PowerDrawWatts = 0f;
}
