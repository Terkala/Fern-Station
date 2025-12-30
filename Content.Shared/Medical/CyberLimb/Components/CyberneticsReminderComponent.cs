// SPDX-FileCopyrightText: 2025 terkala <appleorange64@gmail.com>
//
// SPDX-License-Identifier: MIT

using Robust.Shared.GameStates;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom;

namespace Content.Shared.Medical.CyberLimb;

/// <summary>
/// Component that tracks reminder timers for cybernetics maintenance warnings.
/// Only added to bodies that have cybernetics, for performance.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class CyberneticsReminderComponent : Component
{
    /// <summary>
    /// Last time a reminder was sent. Used to throttle reminders to every 30 seconds.
    /// </summary>
    [DataField(customTypeSerializer: typeof(TimeOffsetSerializer)), ViewVariables]
    public TimeSpan LastReminderTime = TimeSpan.Zero;

    /// <summary>
    /// Interval between reminders (30 seconds).
    /// </summary>
    [DataField, ViewVariables]
    public TimeSpan ReminderInterval = TimeSpan.FromSeconds(30);
}

