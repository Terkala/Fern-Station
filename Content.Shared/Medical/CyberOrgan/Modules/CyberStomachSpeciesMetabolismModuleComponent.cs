// SPDX-FileCopyrightText: 2025 terkala <appleorange64@gmail.com>
//
// SPDX-License-Identifier: MIT

using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared.Medical.CyberOrgan.Modules;

/// <summary>
/// Component for species metabolism modules that allow cyber-stomachs to count as a specific species.
/// Adds that species tag to the entity for the purposes of eating.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class CyberStomachSpeciesMetabolismModuleComponent : Component
{
    /// <summary>
    /// The species tag to add to the entity for food digestion purposes.
    /// </summary>
    [DataField, ViewVariables]
    public ProtoId<EntityPrototype>? TargetSpecies;
}

