// SPDX-FileCopyrightText: 2025 terkala <appleorange64@gmail.com>
//
// SPDX-License-Identifier: MIT

using Robust.Shared.GameStates;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom;

namespace Content.Shared.Medical.CyberOrgan;

/// <summary>
/// Component that tracks the last update time for cyber-kidney effects.
/// Used to throttle updates to 1-second intervals for performance.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class CyberKidneyUpdateTrackerComponent : Component
{
    /// <summary>
    /// Last time kidney effects were processed for this body.
    /// </summary>
    [DataField(customTypeSerializer: typeof(TimeOffsetSerializer))]
    public TimeSpan LastUpdate = TimeSpan.Zero;
}

