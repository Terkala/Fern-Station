// SPDX-FileCopyrightText: 2025 terkala <appleorange64@gmail.com>
//
// SPDX-License-Identifier: MIT

namespace Content.Shared.Medical.Cyber;

/// <summary>
/// Base interface for all cybernetic slot components.
/// Each slot component represents a specific cybernetic location (arm, heart, lung, etc.)
/// and tracks maintenance state and service time for that location.
/// </summary>
public interface ICyberneticSlotComponent
{
    /// <summary>
    /// Service time remaining in seconds.
    /// </summary>
    float ServiceTimeRemaining { get; set; }

    /// <summary>
    /// Maximum service time in seconds.
    /// </summary>
    float MaxServiceTime { get; set; }

    /// <summary>
    /// Whether the service time has expired.
    /// </summary>
    bool IsServiceTimeExpired { get; }

    /// <summary>
    /// Whether the maintenance panel is currently unscrewed (open).
    /// </summary>
    bool IsPanelUnscrewed { get; set; }

    /// <summary>
    /// Whether bolts have been adjusted (maintenance step).
    /// </summary>
    bool BoltsAdjusted { get; set; }

    /// <summary>
    /// Whether wiring has been replaced (maintenance step).
    /// </summary>
    bool WiringReplaced { get; set; }
}
