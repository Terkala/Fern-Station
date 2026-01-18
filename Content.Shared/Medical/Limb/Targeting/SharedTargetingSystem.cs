// SPDX-FileCopyrightText: 2025 terkala <appleorange64@gmail.com>
//
// SPDX-License-Identifier: MIT

namespace Content.Shared.Medical.Limb.Targeting;

/// <summary>
/// Base system for targeting operations.
/// Provides shared functionality for both client and server.
/// </summary>
public abstract class SharedTargetingSystem : EntitySystem
{
    /// <summary>
    /// Returns all valid target body parts as an array.
    /// </summary>
    public static TargetBodyPart[] GetValidTargets()
    {
        return new[]
        {
            TargetBodyPart.Head,
            TargetBodyPart.Torso,
            TargetBodyPart.Arms,
            TargetBodyPart.Legs,
        };
    }

    /// <summary>
    /// Checks if a target body part is valid.
    /// </summary>
    public static bool IsValidTarget(TargetBodyPart target)
    {
        return target == TargetBodyPart.Head ||
               target == TargetBodyPart.Torso ||
               target == TargetBodyPart.Arms ||
               target == TargetBodyPart.Legs;
    }
}
