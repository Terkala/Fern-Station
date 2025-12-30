// SPDX-FileCopyrightText: 2025 terkala <appleorange64@gmail.com>
//
// SPDX-License-Identifier: MIT

using Robust.Shared.GameStates;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom;

namespace Content.Shared.Medical.CyberLimb;

/// <summary>
/// Component that handles all cybernetics upkeep: maintenance panel state, battery wattage tracking, and service time.
/// Uses efficient timestamp-based calculations when panel is closed, and detailed wattage calculations when open.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class CyberneticsUpkeepComponent : Component
{
    /// <summary>
    /// Whether the maintenance panel is currently unscrewed (open).
    /// When true, the storage can be accessed via right-click and detailed wattage calculations run.
    /// </summary>
    [DataField, ViewVariables, AutoNetworkedField]
    public bool IsPanelUnscrewed = false;

    /// <summary>
    /// Whether bolts have been adjusted (maintenance step).
    /// </summary>
    [DataField, ViewVariables, AutoNetworkedField]
    public bool BoltsAdjusted = false;

    /// <summary>
    /// Whether wiring has been replaced (maintenance step).
    /// </summary>
    [DataField, ViewVariables, AutoNetworkedField]
    public bool WiringReplaced = false;

    // ===== WATTAGE TRACKING (Only calculated when panel is open) =====

    /// <summary>
    /// Current total wattage across all batteries in all cybernetics (in joules).
    /// Only updated when maintenance panel is open.
    /// </summary>
    [DataField, ViewVariables, AutoNetworkedField]
    public float CurrentTotalWattage = 0f;

    /// <summary>
    /// Maximum total wattage capacity across all batteries in all cybernetics (in joules).
    /// Only updated when maintenance panel is open.
    /// </summary>
    [DataField, ViewVariables, AutoNetworkedField]
    public float MaxTotalWattage = 0f;

    // ===== EFFICIENT TIMESTAMP SYSTEM (Used when panel is closed) =====

    /// <summary>
    /// Timestamp when the batteries are predicted to run out.
    /// Calculated based on current draw rate and remaining capacity.
    /// Only updated when panel is closed or when batteries change.
    /// </summary>
    [DataField(customTypeSerializer: typeof(TimeOffsetSerializer)), ViewVariables, AutoNetworkedField]
    public TimeSpan PredictedBatteryEmptyTime = TimeSpan.Zero;

    /// <summary>
    /// Maximum wattage capacity at the time the prediction was made.
    /// Used to recalculate percentage when panel is closed.
    /// </summary>
    [DataField, ViewVariables, AutoNetworkedField]
    public float PredictedMaxWattage = 0f;

    /// <summary>
    /// Number of cybernetics at the time the prediction was made.
    /// Used to calculate current draw rate.
    /// </summary>
    [DataField, ViewVariables, AutoNetworkedField]
    public int PredictedCyberneticsCount = 0;

    /// <summary>
    /// Baseline: 1 medium battery (2000 joules) = 20 minutes for 1 cybernetic.
    /// This means: 2000 joules / 1200 seconds = ~1.67 joules/second per cybernetic.
    /// Or: 100 joules/minute per cybernetic.
    /// </summary>
    public const float BaselineBatteryCapacity = 2000f; // joules
    public const float BaselineDurationMinutes = 20f; // minutes
    public const float BaselineDurationSeconds = 1200f; // seconds
    public const float JoulesPerSecondPerCybernetics = BaselineBatteryCapacity / BaselineDurationSeconds; // ~1.67 J/s per cybernetic
}

