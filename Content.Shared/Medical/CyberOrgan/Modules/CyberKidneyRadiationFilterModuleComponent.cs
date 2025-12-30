// SPDX-FileCopyrightText: 2025 terkala <appleorange64@gmail.com>
//
// SPDX-License-Identifier: MIT

using Robust.Shared.GameStates;

namespace Content.Shared.Medical.CyberOrgan.Modules;

/// <summary>
/// Component for radiation filter modules that allow cyber-kidneys to remove radiation.
/// Removes radiation at a rate of 0.01u/second per 10% efficiency above 0%.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class CyberKidneyRadiationFilterModuleComponent : Component
{
}

