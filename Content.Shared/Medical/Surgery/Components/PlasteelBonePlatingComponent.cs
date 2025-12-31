// SPDX-FileCopyrightText: 2025 terkala <appleorange64@gmail.com>
//
// SPDX-License-Identifier: MIT

using Robust.Shared.GameStates;

namespace Content.Shared.Medical.Surgery.Components;

/// <summary>
/// Component that tracks plasteel bone plating on a body part.
/// Provides damage resistance when applied.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class PlasteelBonePlatingComponent : Component
{
}

