// SPDX-FileCopyrightText: 2025 terkala <appleorange64@gmail.com>
//
// SPDX-License-Identifier: MIT

using Robust.Shared.GameStates;

namespace Content.Shared.Medical.CyberOrgan.Modules;

/// <summary>
/// Component for poison filter modules that allow cyber-stomachs to digest poisonous foods.
/// Grants the same organ processing flag that allows rats to eat poisonous foods.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class CyberStomachPoisonFilterModuleComponent : Component
{
}

