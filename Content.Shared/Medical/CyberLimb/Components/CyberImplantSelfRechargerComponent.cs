// SPDX-FileCopyrightText: 2025 terkala <appleorange64@gmail.com>
//
// SPDX-License-Identifier: MIT

using Robust.Shared.GameStates;

namespace Content.Shared.Medical.CyberLimb;

/// <summary>
/// Component for implants that self-recharge the body's battery (like microreactors).
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class CyberImplantSelfRechargerComponent : Component
{
    /// <summary>
    /// Rate at which to recharge the body's battery in joules per second.
    /// </summary>
    [DataField(required: true), ViewVariables, AutoNetworkedField]
    public float AutoRechargeRate = 0f;

    /// <summary>
    /// Whether to pause recharging when battery is used.
    /// </summary>
    [DataField, ViewVariables, AutoNetworkedField]
    public bool AutoRechargePause = false;

    /// <summary>
    /// How long to pause after battery use in seconds.
    /// </summary>
    [DataField, ViewVariables, AutoNetworkedField]
    public float AutoRechargePauseTime = 0f;
}
