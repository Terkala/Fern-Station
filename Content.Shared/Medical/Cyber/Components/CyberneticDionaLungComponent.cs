// SPDX-FileCopyrightText: 2025 terkala <appleorange64@gmail.com>
//
// SPDX-License-Identifier: MIT

using Robust.Shared.GameStates;

namespace Content.Shared.Medical.Cyber.Components;

/// <summary>
/// Slot component for Diona cybernetic lungs (species-specific).
/// Tracks maintenance state and service time for Diona cybernetic lungs.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class CyberneticDionaLungComponent : CyberneticSlotComponent
{
}
