// SPDX-FileCopyrightText: 2025 terkala <appleorange64@gmail.com>
//
// SPDX-License-Identifier: MIT

using Content.Shared.FixedPoint;
using Robust.Shared.GameStates;

namespace Content.Shared.Medical.Limb.Damage;

/// <summary>
/// Component that tracks limb-specific damage and configuration.
/// This component is completely original and not based on any _Shitmed implementations.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class LimbDamageComponent : Component
{
    /// <summary>
    /// Damage threshold at which the limb becomes disabled (non-functional).
    /// Default: 100 damage.
    /// </summary>
    [DataField, AutoNetworkedField]
    public FixedPoint2 DisableThreshold = FixedPoint2.New(100);

    /// <summary>
    /// Damage threshold at which the limb is destroyed (severed/removed).
    /// Default: 200 damage.
    /// </summary>
    [DataField, AutoNetworkedField]
    public FixedPoint2 DestroyThreshold = FixedPoint2.New(200);

    /// <summary>
    /// Whether this limb is currently disabled (non-functional but can be repaired).
    /// </summary>
    [ViewVariables, AutoNetworkedField]
    public bool IsDisabled = false;

    /// <summary>
    /// Whether this limb has been destroyed (severed/removed).
    /// </summary>
    [ViewVariables, AutoNetworkedField]
    public bool IsDestroyed = false;
}
