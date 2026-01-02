// SPDX-FileCopyrightText: 2025 terkala <appleorange64@gmail.com>
//
// SPDX-License-Identifier: MIT

using Robust.Shared.GameStates;

namespace Content.Shared.Medical.CyberLimb.Modules;

/// <summary>
/// Component for auto-reload modules that automatically reload firearms from stored ammunition.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class CyberLimbAutoReloadModuleComponent : Component
{
}
