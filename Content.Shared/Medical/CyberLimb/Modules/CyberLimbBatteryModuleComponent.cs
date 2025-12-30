// SPDX-FileCopyrightText: 2025 terkala <appleorange64@gmail.com>
//
// SPDX-License-Identifier: MIT

using Robust.Shared.GameStates;

namespace Content.Shared.Medical.CyberLimb.Modules;

/// <summary>
/// Component for battery modules that provide power to cyber limbs.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class CyberLimbBatteryModuleComponent : Component
{
    /// <summary>
    /// Maximum charge capacity of this battery in joules.
    /// </summary>
    [DataField(required: true), ViewVariables, AutoNetworkedField]
    public float MaxCharge = 1000f;
}

