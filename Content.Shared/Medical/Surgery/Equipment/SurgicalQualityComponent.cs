// SPDX-FileCopyrightText: 2025 terkala <appleorange64@gmail.com>
//
// SPDX-License-Identifier: MIT

using Robust.Shared.GameStates;

namespace Content.Shared.Medical.Surgery.Equipment;

/// <summary>
/// Component that defines the surgical quality of an item.
/// Can be applied to operating tables, tools, equipment, or any item used in surgery.
/// Higher quality items reduce integrity costs for surgeries.
/// Multiple items' quality multipliers stack multiplicatively.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class SurgicalQualityComponent : Component
{
    /// <summary>
    /// Quality multiplier for integrity costs.
    /// 1.0 = no change, 0.9 = 10% reduction, 1.2 = 20% increase.
    /// Multiple items with this component stack multiplicatively.
    /// </summary>
    [DataField, AutoNetworkedField]
    public float QualityMultiplier = 1.0f;
}


