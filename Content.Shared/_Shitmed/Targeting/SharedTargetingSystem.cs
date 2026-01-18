// SPDX-FileCopyrightText: 2025 terkala <appleorange64@gmail.com>
//
// SPDX-License-Identifier: MIT

using Robust.Shared.GameObjects;

namespace Content.Shared._Shitmed.Targeting;

/// <summary>
/// Stub system for SharedTargetingSystem - kept for compatibility with _Shitmed files.
/// The actual targeting system has been removed and replaced with the new system.
/// This system does nothing and is only present to prevent compilation errors.
/// </summary>
public abstract class SharedTargetingSystem : EntitySystem
{
    /// <summary>
    /// Stub method - returns empty list.
    /// </summary>
    public static List<TargetBodyPart> GetValidParts()
    {
        return new List<TargetBodyPart>();
    }
}
