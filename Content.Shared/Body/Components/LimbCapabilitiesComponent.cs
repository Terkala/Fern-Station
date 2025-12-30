// SPDX-FileCopyrightText: 2025 terkala <appleorange64@gmail.com>
//
// SPDX-License-Identifier: MIT

using Content.Shared.Damage;
using Content.Shared.FixedPoint;
using Robust.Shared.GameStates;

namespace Content.Shared.Body.Components;

/// <summary>
/// Component that stores capabilities provided by a limb (prying, melee damage, etc.).
/// These values are aggregated to the mob when the limb is attached.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class LimbCapabilitiesComponent : Component
{
    /// <summary>
    /// Whether this limb provides prying capability.
    /// </summary>
    [DataField, ViewVariables, AutoNetworkedField]
    public bool ProvidesPrying = false;

    /// <summary>
    /// Whether this limb can pry powered doors.
    /// </summary>
    [DataField, ViewVariables, AutoNetworkedField]
    public bool PryPowered = false;

    /// <summary>
    /// Whether this limb can force pry (bypass restrictions).
    /// </summary>
    [DataField, ViewVariables, AutoNetworkedField]
    public bool PryForce = false;

    /// <summary>
    /// Prying speed modifier for this limb.
    /// </summary>
    [DataField, ViewVariables, AutoNetworkedField]
    public float PrySpeedModifier = 1.0f;

    /// <summary>
    /// Melee damage provided by this limb.
    /// This is added to the mob's base melee damage.
    /// </summary>
    [DataField, ViewVariables, AutoNetworkedField]
    public DamageSpecifier MeleeDamage = new();

    /// <summary>
    /// Melee attack rate modifier for this limb.
    /// Multiplied with other limbs' rates (typically 1.0, but can be modified).
    /// </summary>
    [DataField, ViewVariables, AutoNetworkedField]
    public float AttackRateModifier = 1.0f;
}

