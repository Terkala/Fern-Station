// SPDX-FileCopyrightText: 2025 terkala <appleorange64@gmail.com>
//
// SPDX-License-Identifier: MIT

using Robust.Shared.GameStates;

namespace Content.Shared.Medical.Cyber.Components;

/// <summary>
/// Abstract base class for all cybernetic slot components.
/// Tracks maintenance state and service time for a specific cybernetic slot location.
/// All slot-specific components (CyberneticLeftArmComponent, CyberneticHeartComponent, etc.) inherit from this.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public abstract partial class CyberneticSlotComponent : Component, ICyberneticSlotComponent
{
    /// <summary>
    /// Service time remaining in seconds for this cybernetic slot.
    /// </summary>
    [DataField, AutoNetworkedField]
    public float ServiceTimeRemaining { get; set; } = 0f;

    /// <summary>
    /// Maximum service time in seconds for this cybernetic slot.
    /// Calculated from matter bin count and global capacitor multiplier.
    /// </summary>
    [DataField, AutoNetworkedField]
    public float MaxServiceTime { get; set; } = 0f;

    /// <summary>
    /// Whether the service time has expired for this cybernetic slot.
    /// </summary>
    public bool IsServiceTimeExpired => ServiceTimeRemaining <= 0;

    /// <summary>
    /// Whether the maintenance panel is currently unscrewed (open).
    /// When true, the cybernetic is disabled.
    /// </summary>
    [DataField, AutoNetworkedField]
    public bool IsPanelUnscrewed { get; set; } = false;

    /// <summary>
    /// Whether bolts have been adjusted (maintenance step).
    /// Must be true before panel can be closed.
    /// </summary>
    [DataField, AutoNetworkedField]
    public bool BoltsAdjusted { get; set; } = false;

    /// <summary>
    /// Whether wiring has been replaced (maintenance step).
    /// Must be true before panel can be closed.
    /// </summary>
    [DataField, AutoNetworkedField]
    public bool WiringReplaced { get; set; } = false;
}
