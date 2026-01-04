// SPDX-FileCopyrightText: 2025 terkala <appleorange64@gmail.com>
//
// SPDX-License-Identifier: MIT

using Robust.Shared.GameStates;

namespace Content.Shared.Medical.Cyber.Components;

/// <summary>
/// Slot component for cybernetic eyes.
/// Tracks maintenance state and service time for cybernetic eyes.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class CyberneticEyeComponent : CyberneticSlotComponent
{
}
