// SPDX-FileCopyrightText: 2025 terkala <appleorange64@gmail.com>
//
// SPDX-License-Identifier: MIT

namespace Content.Shared._Shitmed.Targeting;

/// <summary>
/// Stub enum for TargetBodyPart - kept for compatibility with _Shitmed files.
/// The actual targeting system has been removed and replaced with the new system.
/// </summary>
[Flags]
public enum TargetBodyPart : ushort
{
    Head = 1,
    Torso = 1 << 1,
    Groin = 1 << 2,
    LeftArm = 1 << 3,
    LeftHand = 1 << 4,
    RightArm = 1 << 5,
    RightHand = 1 << 6,
    LeftLeg = 1 << 7,
    LeftFoot = 1 << 8,
    RightLeg = 1 << 9,
    RightFoot = 1 << 10,
    All = Head | Torso | Groin | LeftArm | LeftHand | RightArm | RightHand | LeftLeg | LeftFoot | RightLeg | RightFoot
}
