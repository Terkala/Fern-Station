// SPDX-FileCopyrightText: 2025 terkala <appleorange64@gmail.com>
//
// SPDX-License-Identifier: MIT

using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared.Medical.Biosynthetic;

/// <summary>
/// Component that marks an organ as biosynthetic (from bioprinter).
/// Biosynthetic organs have reduced or no integrity cost for matching species.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class BiosyntheticOrganComponent : Component
{
    /// <summary>
    /// Integrity cost multiplier for biosynthetic organs when implanted into matching species.
    /// Default is 0.5x (half cost), or 0.0 for no cost.
    /// </summary>
    [DataField, AutoNetworkedField]
    public float MatchingSpeciesCostMultiplier = 0.5f;

    /// <summary>
    /// Species this biosynthetic organ is designed for.
    /// If null, it adapts to the recipient's species.
    /// </summary>
    [DataField, AutoNetworkedField]
    public ProtoId<EntityPrototype>? TargetSpecies;
}


