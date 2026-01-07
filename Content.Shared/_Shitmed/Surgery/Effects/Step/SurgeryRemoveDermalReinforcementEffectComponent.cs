// SPDX-FileCopyrightText: 2025 terkala <appleorange64@gmail.com>
//
// SPDX-License-Identifier: MIT

using Content.Shared.Medical.Surgery.Components;
using Robust.Shared.GameStates;

namespace Content.Shared._Shitmed.Medical.Surgery.Effects.Step;

/// <summary>
/// Effect component for removing dermal reinforcement surgery step.
/// Removes DermalPlasteelWeaveComponent from the body part.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class SurgeryRemoveDermalReinforcementEffectComponent : Component
{
}
