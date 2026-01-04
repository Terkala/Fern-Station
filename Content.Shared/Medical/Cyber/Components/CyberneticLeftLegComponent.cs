// SPDX-FileCopyrightText: 2025 terkala <appleorange64@gmail.com>
//
// SPDX-License-Identifier: MIT

using Robust.Shared.GameStates;

namespace Content.Shared.Medical.Cyber.Components;

/// <summary>
/// Slot component for the left cybernetic leg (includes foot).
/// Tracks maintenance state and service time for the left leg+foot cybernetic unit.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class CyberneticLeftLegComponent : CyberneticSlotComponent
{
}
