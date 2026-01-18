// SPDX-FileCopyrightText: 2025 terkala <appleorange64@gmail.com>
//
// SPDX-License-Identifier: MIT

namespace Content.Shared.Medical.Limb.Targeting;

/// <summary>
/// Represents the simplified body parts that can be targeted.
/// Arms and hands are treated as one unit, legs and feet are treated as one unit.
/// No left/right distinction in targeting - the system intelligently chooses which side based on facing and damage source.
/// </summary>
[Flags]
public enum TargetBodyPart : ushort
{
    /// <summary>
    /// Head target - includes the entire head.
    /// </summary>
    Head = 1,

    /// <summary>
    /// Torso target - includes the torso and core body.
    /// </summary>
    Torso = 1 << 1,

    /// <summary>
    /// Arms target - includes both arms and hands as a single unit.
    /// When targeted, the system intelligently chooses left or right based on facing and damage source direction.
    /// </summary>
    Arms = 1 << 2,

    /// <summary>
    /// Legs target - includes both legs and feet as a single unit.
    /// When targeted, the system intelligently chooses left or right based on facing and damage source direction.
    /// </summary>
    Legs = 1 << 3,

    /// <summary>
    /// All body parts - used for area damage or when no specific target is selected.
    /// </summary>
    All = Head | Torso | Arms | Legs,
}
