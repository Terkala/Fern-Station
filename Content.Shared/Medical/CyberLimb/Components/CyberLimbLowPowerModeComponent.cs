// SPDX-FileCopyrightText: 2025 terkala <appleorange64@gmail.com>
//
// SPDX-License-Identifier: MIT

using Robust.Shared.GameStates;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom;

namespace Content.Shared.Medical.CyberLimb;

/// <summary>
/// Component that tracks player inactivity for cyberlimb low power mode.
/// When inactive for 10 seconds (no movement or actions, except speaking),
/// cyberlimbs enter low power mode
/// This is so AFK players don't come back to all their cyberwear being broken.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class CyberLimbLowPowerModeComponent : Component
{
    /// <summary>
    /// Time when the player last performed an action (movement, interaction, etc.)
    /// Speaking does not count as activity.
    /// </summary>
    [DataField, ViewVariables, AutoNetworkedField]
    public TimeSpan LastActivityTime = TimeSpan.Zero;

    /// <summary>
    /// Whether the cyberlimbs are currently in low power mode.
    /// </summary>
    [DataField, ViewVariables, AutoNetworkedField]
    public bool IsLowPowerMode = false;

    /// <summary>
    /// Time required inactive before entering low power mode (10 seconds).
    /// </summary>
    [DataField]
    public float InactivityThreshold = 10.0f;
}

