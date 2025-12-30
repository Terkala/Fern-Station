// SPDX-FileCopyrightText: 2025 terkala <appleorange64@gmail.com>
//
// SPDX-License-Identifier: MIT

using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared.Medical.Compatibility;

/// <summary>
/// Component that tracks the species of the donor for an organ, limb, or cybernetic.
/// Compatible donors (same species as recipient) don't consume integrity points.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class DonorSpeciesComponent : Component
{
    /// <summary>
    /// The species prototype ID of the donor.
    /// If this matches the recipient's species, integrity cost is 0.
    /// </summary>
    [DataField, AutoNetworkedField]
    public ProtoId<EntityPrototype>? DonorSpecies;
}

