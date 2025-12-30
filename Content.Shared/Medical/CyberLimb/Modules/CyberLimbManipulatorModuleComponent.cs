// SPDX-FileCopyrightText: 2025 terkala <appleorange64@gmail.com>
//
// SPDX-License-Identifier: MIT

using Robust.Shared.GameStates;

namespace Content.Shared.Medical.CyberLimb.Modules;

/// <summary>
/// Component for manipulator modules that increase cyber limb efficiency.
/// First manipulator provides 100% efficiency, each additional provides +10%.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class CyberLimbManipulatorModuleComponent : Component
{
}

