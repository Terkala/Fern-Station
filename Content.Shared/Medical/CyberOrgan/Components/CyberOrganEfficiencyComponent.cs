// SPDX-FileCopyrightText: 2025 terkala <appleorange64@gmail.com>
//
// SPDX-License-Identifier: MIT

using Robust.Shared.GameStates;

namespace Content.Shared.Medical.CyberOrgan;

/// <summary>
/// Unified component for tracking efficiency on all cyber-organs (eyes, heart, lungs, stomach, liver, kidneys).
/// Efficiency is pre-computed and cached when modules change.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class CyberOrganEfficiencyComponent : Component
{
    /// <summary>
    /// Cached efficiency value. Recalculated only when modules change.
    /// Base: 100% (1.0) for organs with required modules, 0% without.
    /// Each additional module provides +10% efficiency.
    /// </summary>
    [DataField, ViewVariables, AutoNetworkedField]
    public float CachedEfficiency = 1.0f;

    /// <summary>
    /// Cached count of efficiency-boosting modules.
    /// </summary>
    [DataField, ViewVariables, AutoNetworkedField]
    public int CachedModuleCount = 0;

    /// <summary>
    /// Whether module counts need to be recalculated.
    /// </summary>
    [DataField, ViewVariables]
    public bool NeedsRecalculation = true;
}

