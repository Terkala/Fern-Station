// SPDX-FileCopyrightText: 2025 terkala <appleorange64@gmail.com>
//
// SPDX-License-Identifier: MIT

using Content.Shared.Damage;
using Content.Shared.FixedPoint;
using Robust.Shared.GameStates;

namespace Content.Shared.Body.Components;

/// <summary>
/// Component on the mob that stores aggregated capabilities from all attached limbs.
/// This is calculated from LimbCapabilitiesComponent on body parts.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class AggregatedLimbCapabilitiesComponent : Component
{
    /// <summary>
    /// Whether the mob can pry (from any limb).
    /// </summary>
    [ViewVariables, AutoNetworkedField]
    public bool CanPry = false;

    /// <summary>
    /// Whether the mob can pry powered doors (from any limb).
    /// </summary>
    [ViewVariables, AutoNetworkedField]
    public bool CanPryPowered = false;

    /// <summary>
    /// Whether the mob can force pry (from any limb).
    /// </summary>
    [ViewVariables, AutoNetworkedField]
    public bool CanPryForce = false;

    /// <summary>
    /// Best prying speed modifier from all limbs.
    /// </summary>
    [ViewVariables, AutoNetworkedField]
    public float BestPrySpeedModifier = 1.0f;

    /// <summary>
    /// Melee damage from the best arm (arm with highest damage).
    /// </summary>
    [ViewVariables, AutoNetworkedField]
    public DamageSpecifier TotalMeleeDamage = new();

    /// <summary>
    /// Attack rate modifier from the best arm (arm with highest damage).
    /// </summary>
    [ViewVariables, AutoNetworkedField]
    public float CombinedAttackRateModifier = 1.0f;

    /// <summary>
    /// Base melee damage from the mob's original MeleeWeaponComponent (if it existed).
    /// Stored separately to avoid double-counting when recalculating.
    /// </summary>
    [ViewVariables]
    public DamageSpecifier BaseMeleeDamage = new();

    /// <summary>
    /// Base attack rate from the mob's original MeleeWeaponComponent (if it existed).
    /// </summary>
    [ViewVariables]
    public float BaseAttackRate = 1.0f;

    /// <summary>
    /// Whether base melee damage has been stored (to avoid overwriting on recalculation).
    /// </summary>
    [ViewVariables]
    public bool BaseMeleeDamageStored = false;

    /// <summary>
    /// Whether capabilities need to be recalculated.
    /// </summary>
    [ViewVariables]
    public bool NeedsRecalculation = true;
}

