// SPDX-FileCopyrightText: 2025 terkala <appleorange64@gmail.com>
//
// SPDX-License-Identifier: MIT

using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared.Medical.Compatibility;

/// <summary>
/// Component that defines species compatibility for organs.
/// Incompatible organs have higher integrity costs but are not rejected.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class OrganCompatibilityComponent : Component
{
    /// <summary>
    /// List of species IDs that this organ is compatible with.
    /// If the recipient's species is not in this list, the integrity cost multiplier is applied.
    /// </summary>
    [DataField, AutoNetworkedField]
    public HashSet<ProtoId<EntityPrototype>> CompatibleSpecies = new();

    /// <summary>
    /// Integrity cost multiplier for incompatible species.
    /// Default is 2.0x (incompatible organs cost twice as much integrity).
    /// </summary>
    [DataField, AutoNetworkedField]
    public float IncompatibleCostMultiplier = 2.0f;
}


