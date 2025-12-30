// SPDX-FileCopyrightText: 2025 terkala <appleorange64@gmail.com>
//
// SPDX-License-Identifier: MIT

using Robust.Shared.GameStates;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom;

namespace Content.Shared.Medical.CyberLimb;

/// <summary>
/// Component that extends StorageComponent with cyber-limb specific behavior.
/// Prevents stacking and caches module counts for performance.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class CyberLimbStorageComponent : Component
{
    /// <summary>
    /// Cached count of battery modules. Updated when storage changes.
    /// </summary>
    [DataField, ViewVariables, AutoNetworkedField]
    public int CachedBatteryCount = 0;

    /// <summary>
    /// Cached total battery capacity (sum of all battery MaxCharge).
    /// </summary>
    [DataField, ViewVariables, AutoNetworkedField]
    public float CachedBatteryCapacity = 0f;

    /// <summary>
    /// Cached count of matter bin modules.
    /// </summary>
    [DataField, ViewVariables, AutoNetworkedField]
    public int CachedMatterBinCount = 0;

    /// <summary>
    /// Cached count of manipulator modules.
    /// </summary>
    [DataField, ViewVariables, AutoNetworkedField]
    public int CachedManipulatorCount = 0;

    /// <summary>
    /// Cached efficiency value. Recalculated only when modules change.
    /// Base: 100% for first manipulator, +10% for each additional.
    /// </summary>
    [DataField, ViewVariables, AutoNetworkedField]
    public float CachedEfficiency = 1.0f;

    /// <summary>
    /// Whether module counts need to be recalculated.
    /// </summary>
    [DataField, ViewVariables]
    public bool NeedsRecalculation = true;

    /// <summary>
    /// Service time remaining in seconds for this specific limb.
    /// Calculated from matter bin modules: 10 minutes (600 seconds) per matter bin.
    /// </summary>
    [DataField, ViewVariables, AutoNetworkedField]
    public float ServiceTimeRemaining = 0f;

    /// <summary>
    /// Maximum service time for this limb (calculated from matter bins).
    /// </summary>
    [DataField, ViewVariables, AutoNetworkedField]
    public float MaxServiceTime = 0f;

    /// <summary>
    /// Whether service time needs to be updated this tick.
    /// </summary>
    [DataField, ViewVariables]
    public bool NeedsServiceTimeUpdate = false;

    /// <summary>
    /// Whether this limb's service time is expired (affects efficiency).
    /// </summary>
    [DataField, ViewVariables, AutoNetworkedField]
    public bool IsServiceTimeExpired = false;

    /// <summary>
    /// Last time service time was updated. Used for infrequent updates.
    /// </summary>
    [DataField(customTypeSerializer: typeof(TimeOffsetSerializer)), ViewVariables]
    public TimeSpan LastServiceTimeUpdate = TimeSpan.Zero;
}

