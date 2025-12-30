// SPDX-FileCopyrightText: 2025 terkala <appleorange64@gmail.com>
//
// SPDX-License-Identifier: MIT

using Content.Shared.Damage;
using Content.Shared.Damage.Systems;
using Content.Shared.Medical.CyberLimb;

namespace Content.Server.Medical.CyberLimb;

/// <summary>
/// System that applies damage resistance from military cyber-limbs.
/// Subscribes to DamageModifyEvent on body parts to apply multiplicative resistance.
/// </summary>
public sealed class CyberLimbDamageResistanceSystem : EntitySystem
{
    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<CyberLimbDamageResistanceComponent, DamageModifyEvent>(OnDamageModify);
    }

    /// <summary>
    /// Applies damage resistance from military cyber-limbs.
    /// Multiplies all damage by the resistance coefficient (0.9 = 10% reduction).
    /// </summary>
    private void OnDamageModify(Entity<CyberLimbDamageResistanceComponent> ent, ref DamageModifyEvent args)
    {
        // Apply multiplicative resistance to all damage types
        args.Damage = args.Damage * ent.Comp.ResistanceCoefficient;
    }
}

