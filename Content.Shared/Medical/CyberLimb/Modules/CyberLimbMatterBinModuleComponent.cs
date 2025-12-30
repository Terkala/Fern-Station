// SPDX-FileCopyrightText: 2025 terkala <appleorange64@gmail.com>
//
// SPDX-License-Identifier: MIT

using Robust.Shared.GameStates;

namespace Content.Shared.Medical.CyberLimb.Modules;

/// <summary>
/// Component for matter bin modules that provide service time to cyber limbs.
/// Each matter bin provides 10 minutes of service time.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class CyberLimbMatterBinModuleComponent : Component
{
}

