// SPDX-FileCopyrightText: 2025 terkala <appleorange64@gmail.com>
//
// SPDX-License-Identifier: MIT

using Robust.Shared.GameStates;

namespace Content.Shared._Shitmed.Cybernetics;

/// <summary>
/// Internal component that links a spawned subdermal implant back to its cybernetic source.
/// Not for user use - purely internal implementation detail.
/// </summary>
[RegisterComponent]
public sealed partial class LinkedToCyberneticComponent : Component
{
    /// <summary>
    /// The cybernetic implant entity this subdermal implant is linked to.
    /// </summary>
    [DataField, ViewVariables]
    public EntityUid LinkedCybernetic;
}