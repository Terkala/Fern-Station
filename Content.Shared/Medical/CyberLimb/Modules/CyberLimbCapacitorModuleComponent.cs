// SPDX-FileCopyrightText: 2025 terkala <appleorange64@gmail.com>
//
// SPDX-License-Identifier: MIT

using Robust.Shared.GameStates;

namespace Content.Shared.Medical.CyberLimb.Modules;

/// <summary>
/// Component for capacitor modules that modify power efficiency/storage.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class CyberLimbCapacitorModuleComponent : Component
{
    /// <summary>
    /// Power efficiency modifier (1.0 = no change, 1.5 = 50% more efficient).
    /// </summary>
    [DataField, ViewVariables]
    public float EfficiencyModifier = 1.0f;
}

