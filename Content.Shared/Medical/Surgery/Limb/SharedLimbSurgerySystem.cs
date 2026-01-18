// SPDX-FileCopyrightText: 2025 terkala <appleorange64@gmail.com>
//
// SPDX-License-Identifier: MIT

using Content.Shared.Body.Components;
using Content.Shared.Body.Part;
using Content.Shared.Body.Systems;
using Content.Shared.Medical.Limb.Targeting;
using Content.Shared.Medical.Surgery.Integrity;

namespace Content.Shared.Medical.Surgery.Limb;

/// <summary>
/// Base surgery system for limbs.
/// Extends SharedSurgerySystem to provide limb-specific surgery operations.
/// This system is completely original and not based on any _Shitmed implementations.
/// </summary>
public abstract class SharedLimbSurgerySystem : SharedSurgerySystem
{
    /// <summary>
    /// Gets the body part type and symmetry for a target body part.
    /// </summary>
    protected (BodyPartType Type, BodyPartSymmetry? Symmetry) GetBodyPartTypeFromTarget(TargetBodyPart target)
    {
        return target switch
        {
            TargetBodyPart.Head => (BodyPartType.Head, null),
            TargetBodyPart.Torso => (BodyPartType.Torso, null),
            TargetBodyPart.Arms => (BodyPartType.Arm, null), // Will be determined by surgery
            TargetBodyPart.Legs => (BodyPartType.Leg, null), // Will be determined by surgery
            _ => (BodyPartType.Torso, null)
        };
    }

    /// <summary>
    /// Checks if a body has a specific target body part available for surgery.
    /// </summary>
    protected bool HasTargetBodyPart(EntityUid body, TargetBodyPart target, BodyComponent? bodyComp = null)
    {
        if (!Resolve(body, ref bodyComp))
            return false;

        var (type, _) = GetBodyPartTypeFromTarget(target);
        return Body.BodyHasPartType(body, type, bodyComp);
    }
}
