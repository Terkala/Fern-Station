// SPDX-FileCopyrightText: 2025 terkala <appleorange64@gmail.com>
//
// SPDX-License-Identifier: MIT

using Robust.Shared.GameStates;

namespace Content.Shared.Medical.CyberOrgan;

/// <summary>
/// Unified component for tracking efficiency on all cyber-organs (eyes, heart, lungs, stomach, liver, kidneys).
/// Efficiency is now a flat value based on quality level (Rudimentary=80%, Basic=100%, Advanced=120%, Experimental=140%).
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class CyberOrganEfficiencyComponent : Component
{
    /// <summary>
    /// Base efficiency value set in prototypes. Rudimentary=0.8, Basic=1.0, Advanced=1.2, Experimental=1.4.
    /// </summary>
    [DataField, ViewVariables, AutoNetworkedField]
    public float BaseEfficiency = 1.0f;

    /// <summary>
    /// Cached efficiency value. Set directly from BaseEfficiency, no longer calculated from modules.
    /// </summary>
    [DataField, ViewVariables, AutoNetworkedField]
    public float CachedEfficiency = 1.0f;
}

