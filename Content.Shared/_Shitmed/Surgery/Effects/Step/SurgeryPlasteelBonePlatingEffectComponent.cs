// SPDX-FileCopyrightText: 2025 terkala <appleorange64@gmail.com>
//
// SPDX-License-Identifier: MIT

using Content.Shared.Medical.Surgery.Components;
using Content.Shared.FixedPoint;
using Robust.Shared.GameStates;

namespace Content.Shared._Shitmed.Medical.Surgery.Effects.Step;

/// <summary>
/// Effect component for plasteel bone plating surgery step.
/// Applies PlasteelBonePlatingComponent to the body part and consumes 5 plasteel.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class SurgeryPlasteelBonePlatingEffectComponent : Component
{
}

