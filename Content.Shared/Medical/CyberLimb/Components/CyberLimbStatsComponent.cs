// SPDX-FileCopyrightText: 2025 terkala <appleorange64@gmail.com>
//
// SPDX-License-Identifier: MIT

using Robust.Shared.GameStates;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom;

namespace Content.Shared.Medical.CyberLimb;

/// <summary>
/// Component that tracks battery charge, service time, and efficiency for a body with cyber limbs.
/// This component is added to the body entity, not individual limbs.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class CyberLimbStatsComponent : Component
{
    /// <summary>
    /// Current battery charge (shared across all cyber limbs).
    /// </summary>
    [DataField, ViewVariables, AutoNetworkedField]
    public float CurrentBatteryCharge = 0f;

    /// <summary>
    /// Cached average battery capacity across all cyber limbs on this body.
    /// Updated when any limb's modules change.
    /// </summary>
    [DataField, ViewVariables, AutoNetworkedField]
    public float CachedAverageBatteryCapacity = 0f;

    /// <summary>
    /// Cached efficiency penalty multiplier (0.5 if battery depleted, 1.0 if not).
    /// Note: Service time penalties are tracked per-limb, not on the body.
    /// </summary>
    [DataField, ViewVariables, AutoNetworkedField]
    public float CachedEfficiencyPenalty = 1.0f;

    /// <summary>
    /// Whether averaged stats need to be recalculated.
    /// </summary>
    [DataField, ViewVariables]
    public bool NeedsRecalculation = true;

    /// <summary>
    /// Whether battery drain needs to be processed this tick.
    /// Set to false when battery is full or empty and not changing.
    /// </summary>
    [DataField, ViewVariables]
    public bool NeedsBatteryUpdate = false;

    /// <summary>
    /// Last time battery was updated. Used for infrequent updates.
    /// </summary>
    [DataField(customTypeSerializer: typeof(TimeOffsetSerializer)), ViewVariables]
    public TimeSpan LastBatteryUpdate = TimeSpan.Zero;

    /// <summary>
    /// Whether battery is depleted (affects efficiency).
    /// </summary>
    [DataField, ViewVariables, AutoNetworkedField]
    public bool IsBatteryDepleted = false;
}

