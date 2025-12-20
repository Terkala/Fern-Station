// SPDX-FileCopyrightText: 2025 terkala <appleorange64@gmail.com>
//
// SPDX-License-Identifier: MIT

using Content.Shared.Arcade;
using Robust.Shared.GameObjects;

namespace Content.Server.Arcade.UI;

/// <summary>
/// Server-side bound user interface for VR Pod tutorial selection.
/// </summary>
public sealed class VRPodBoundUserInterface : BoundUserInterface
{
    public VRPodBoundUserInterface(EntityUid owner, Enum uiKey) : base(owner, uiKey)
    {
    }
}

