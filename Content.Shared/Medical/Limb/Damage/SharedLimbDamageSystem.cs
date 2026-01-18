// SPDX-FileCopyrightText: 2025 terkala <appleorange64@gmail.com>
//
// SPDX-License-Identifier: MIT

using Content.Shared.Body.Components;
using Content.Shared.Body.Part;
using Content.Shared.Body.Systems;
using Content.Shared.Damage;
using Content.Shared.Damage.Components;
using Content.Shared.Damage.Systems;
using Content.Shared.FixedPoint;
using Content.Shared.Medical.Limb.Targeting;
using Robust.Shared.GameObjects;

namespace Content.Shared.Medical.Limb.Damage;

/// <summary>
/// Base system for limb damage calculations with directional routing logic, damage inheritance, healing, missing limb fallback, and chemical distribution.
/// This system is completely original and not based on any _Shitmed implementations.
/// </summary>
public abstract class SharedLimbDamageSystem : EntitySystem
{
    [Dependency] protected readonly SharedBodySystem Body = default!;
    [Dependency] protected readonly DamageableSystem Damageable = default!;

    /// <summary>
    /// Converts TargetBodyPart to BodyPartType and symmetry for finding actual body parts.
    /// </summary>
    protected (BodyPartType Type, BodyPartSymmetry? Symmetry) ConvertTargetToBodyPartType(TargetBodyPart target)
    {
        return target switch
        {
            TargetBodyPart.Head => (BodyPartType.Head, null),
            TargetBodyPart.Torso => (BodyPartType.Torso, null),
            TargetBodyPart.Arms => (BodyPartType.Arm, null), // Will choose left/right based on direction
            TargetBodyPart.Legs => (BodyPartType.Leg, null), // Will choose left/right based on direction
            _ => (BodyPartType.Torso, null)
        };
    }

    /// <summary>
    /// Gets the target body part from a BodyPartComponent.
    /// </summary>
    protected TargetBodyPart? GetTargetFromBodyPart(BodyPartComponent part)
    {
        return part.PartType switch
        {
            BodyPartType.Head => TargetBodyPart.Head,
            BodyPartType.Torso => TargetBodyPart.Torso,
            BodyPartType.Arm => TargetBodyPart.Arms,
            BodyPartType.Hand => TargetBodyPart.Arms, // Hand is part of arm
            BodyPartType.Leg => TargetBodyPart.Legs,
            BodyPartType.Foot => TargetBodyPart.Legs, // Foot is part of leg
            _ => null
        };
    }
}
