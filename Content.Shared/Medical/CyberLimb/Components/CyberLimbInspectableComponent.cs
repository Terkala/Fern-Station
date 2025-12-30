// SPDX-FileCopyrightText: 2025 terkala <appleorange64@gmail.com>
//
// SPDX-License-Identifier: MIT

using Robust.Shared.GameStates;

namespace Content.Shared.Medical.CyberLimb;

/// <summary>
/// Component that marks a cyber limb as inspectable with diagnostic goggles.
/// Shows stats: battery lifespan, service time, efficiency, installed modules.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class CyberLimbInspectableComponent : Component
{
}

