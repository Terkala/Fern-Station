// SPDX-FileCopyrightText: 2025 terkala <appleorange64@gmail.com>
//
// SPDX-License-Identifier: MIT

using Robust.Shared.GameStates;

namespace Content.Shared.Medical.Cyber.Components;

/// <summary>
/// Slot component for Diona cybernetic stomach (species-specific).
/// Tracks maintenance state and service time for Diona cybernetic stomach.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class CyberneticDionaStomachComponent : CyberneticSlotComponent
{
}
