// SPDX-FileCopyrightText: 2025 terkala <appleorange64@gmail.com>
//
// SPDX-License-Identifier: MIT

using Content.Shared.Medical.Limb.Targeting;
using Robust.Shared.GameStates;

namespace Content.Shared.Medical.Limb.Damage;

/// <summary>
/// Component that configures damage inheritance from limbs/head to torso.
/// This component is completely original and not based on any _Shitmed implementations.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class DamageInheritanceComponent : Component
{
    /// <summary>
    /// Percentage of damage from head that is inherited by torso.
    /// Default: 0.5 (50%).
    /// </summary>
    [DataField, AutoNetworkedField]
    public float HeadInheritancePercentage = 0.5f;

    /// <summary>
    /// Percentage of damage from arms that is inherited by torso.
    /// Default: 0.5 (50%).
    /// </summary>
    [DataField, AutoNetworkedField]
    public float ArmsInheritancePercentage = 0.5f;

    /// <summary>
    /// Percentage of damage from legs that is inherited by torso.
    /// Default: 0.5 (50%).
    /// </summary>
    [DataField, AutoNetworkedField]
    public float LegsInheritancePercentage = 0.5f;

    /// <summary>
    /// Gets the inheritance percentage for a specific target body part.
    /// </summary>
    public float GetInheritancePercentage(TargetBodyPart target)
    {
        return target switch
        {
            TargetBodyPart.Head => HeadInheritancePercentage,
            TargetBodyPart.Arms => ArmsInheritancePercentage,
            TargetBodyPart.Legs => LegsInheritancePercentage,
            _ => 0f // Torso doesn't inherit from itself
        };
    }
}
