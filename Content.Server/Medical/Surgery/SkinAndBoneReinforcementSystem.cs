// SPDX-FileCopyrightText: 2025 terkala <appleorange64@gmail.com>
//
// SPDX-License-Identifier: MIT

using Content.Shared.Damage;
using Content.Shared.Damage.Systems;
using Content.Shared.Medical.Surgery.Components;

namespace Content.Server.Medical.Surgery;

/// <summary>
/// System that applies damage resistance from all forms of skin and bone reinforcement.
/// Handles plasteel bone plating, dermal plasteel weave, durathread woven skin, and other reinforcement types.
/// </summary>
public sealed class SkinAndBoneReinforcementSystem : EntitySystem
{
    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<PlasteelBonePlatingComponent, DamageModifyEvent>(OnBonePlatingDamageModify);
        SubscribeLocalEvent<DermalPlasteelWeaveComponent, DamageModifyEvent>(OnDermalWeaveDamageModify);
    }

    /// <summary>
    /// Applies damage resistance from plasteel bone plating.
    /// Bone plating: 10% blunt, 5% slash, 5% pierce, 5% heat resistance.
    /// </summary>
    private void OnBonePlatingDamageModify(Entity<PlasteelBonePlatingComponent> ent, ref DamageModifyEvent args)
    {
        // Apply resistances: 10% blunt, 5% slash, 5% pierce, 5% heat
        if (args.Damage.DamageDict.TryGetValue("Blunt", out var blunt))
            args.Damage.DamageDict["Blunt"] = blunt * 0.9f; // 10% reduction

        if (args.Damage.DamageDict.TryGetValue("Slash", out var slash))
            args.Damage.DamageDict["Slash"] = slash * 0.95f; // 5% reduction

        if (args.Damage.DamageDict.TryGetValue("Pierce", out var pierce))
            args.Damage.DamageDict["Pierce"] = pierce * 0.95f; // 5% reduction

        if (args.Damage.DamageDict.TryGetValue("Heat", out var heat))
            args.Damage.DamageDict["Heat"] = heat * 0.95f; // 5% reduction
    }

    /// <summary>
    /// Applies damage resistance from dermal reinforcement.
    /// Dermal reinforcement: 5% blunt, 5% slash, 5% pierce, 5% heat resistance.
    /// </summary>
    private void OnDermalWeaveDamageModify(Entity<DermalPlasteelWeaveComponent> ent, ref DamageModifyEvent args)
    {
        // Apply resistances: 5% blunt, 5% slash, 5% pierce, 5% heat
        if (args.Damage.DamageDict.TryGetValue("Blunt", out var blunt))
            args.Damage.DamageDict["Blunt"] = blunt * 0.95f; // 5% reduction

        if (args.Damage.DamageDict.TryGetValue("Slash", out var slash))
            args.Damage.DamageDict["Slash"] = slash * 0.95f; // 5% reduction

        if (args.Damage.DamageDict.TryGetValue("Pierce", out var pierce))
            args.Damage.DamageDict["Pierce"] = pierce * 0.95f; // 5% reduction

        if (args.Damage.DamageDict.TryGetValue("Heat", out var heat))
            args.Damage.DamageDict["Heat"] = heat * 0.95f; // 5% reduction
    }
}

