// SPDX-FileCopyrightText: 2025 terkala <appleorange64@gmail.com>
//
// SPDX-License-Identifier: MIT

using Content.Shared.Medical.Surgery.Components;
using Robust.Shared.GameStates;

namespace Content.Shared._Shitmed.Medical.Surgery.Effects.Step;

/// <summary>
/// Effect component for durathread weave surgery step.
/// Applies DermalPlasteelWeaveComponent to the body part and consumes DurathreadWovenSkin item.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class SurgeryDurathreadWeaveEffectComponent : Component
{
}
