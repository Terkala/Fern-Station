// SPDX-FileCopyrightText: 2025 terkala <appleorange64@gmail.com>
//
// SPDX-License-Identifier: MIT

using Robust.Shared.GameStates;

namespace Content.Shared.Medical.CyberLimb;

/// <summary>
/// Component that provides damage resistance to military cyber-limbs.
/// Applies 10% damage reduction multiplicatively (stacks with other sources).
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class CyberLimbDamageResistanceComponent : Component
{
    /// <summary>
    /// Damage resistance coefficient (0.9 = 10% reduction).
    /// Applied multiplicatively to all damage types.
    /// </summary>
    [DataField]
    public float ResistanceCoefficient = 0.9f;
}

