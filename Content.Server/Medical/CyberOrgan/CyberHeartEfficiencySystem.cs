// SPDX-FileCopyrightText: 2025 terkala <appleorange64@gmail.com>
//
// SPDX-License-Identifier: MIT

using Content.Shared.Body.Organ;
using Content.Shared.Damage.Components;
using Content.Shared.Damage.Systems;
using Content.Shared.Medical.CyberOrgan;
using Content.Shared._Shitmed.Body.Organ;
using Content.Server.Body.Components;
using Content.Server.Body.Systems;
using Content.Server.EntityEffects.Effects;
using Content.Shared.EntityEffects;
using Content.Shared.Chemistry.Reagent;
using Robust.Shared.Containers;

namespace Content.Server.Medical.CyberOrgan;

/// <summary>
/// System that handles cyber-heart efficiency effects: healing multipliers and stamina regeneration.
/// </summary>
public sealed class CyberHeartEfficiencySystem : EntitySystem
{
    [Dependency] private readonly CyberOrganEfficiencySystem _organEfficiency = default!;

    public override void Initialize()
    {
        base.Initialize();

        // Note: ComponentStartup and container event subscriptions moved to CyberOrganEfficiencySystem to avoid duplicates
        SubscribeLocalEvent<EntityEffectReagentArgs>(OnReagentEffect, before: new[] { typeof(HealthChange) });
    }

    /// <summary>
    /// Called by CyberOrganEfficiencySystem when a cyber organ with heart starts up.
    /// </summary>
    public void OnHeartEfficiencyStartup(EntityUid uid, CyberOrganEfficiencyComponent component)
    {
        UpdateHeartEfficiency(uid, component);
    }

    /// <summary>
    /// Called by CyberOrganEfficiencySystem when storage changes for a cyber organ with heart.
    /// </summary>
    public void OnHeartStorageChanged(EntityUid uid, CyberOrganEfficiencyComponent component)
    {
        if (!HasComp<HeartComponent>(uid))
            return;

        UpdateHeartEfficiency(uid, component);
    }

    /// <summary>
    /// Updates heart efficiency effects: stamina regeneration.
    /// </summary>
    private void UpdateHeartEfficiency(EntityUid heartUid, CyberOrganEfficiencyComponent efficiency)
    {
        if (!TryComp<OrganComponent>(heartUid, out var organ) || organ.Body == null)
            return;

        var body = organ.Body.Value;
        var finalEfficiency = _organEfficiency.GetFinalEfficiency(heartUid, efficiency);

        // Apply stamina regeneration multiplier
        ApplyStaminaRegeneration(body, finalEfficiency);
    }

    /// <summary>
    /// Applies stamina regeneration multiplier based on heart efficiency.
    /// Higher efficiency = faster stamina recovery.
    /// </summary>
    private void ApplyStaminaRegeneration(EntityUid body, float efficiency)
    {
        if (!TryComp<StaminaComponent>(body, out var stamina))
            return;

        // Store base decay in a component if needed, or use a default
        // For now, we'll directly modify Decay based on efficiency
        // The base decay is typically 3.0f, so we'll use that as reference
        const float baseDecay = 3.0f;
        
        // Apply efficiency multiplier to decay
        // If efficiency is 110%, decay becomes 3.3 (faster recovery)
        stamina.Decay = baseDecay * efficiency;
        Dirty(body, stamina);
    }

    /// <summary>
    /// Intercepts reagent effects to apply efficiency multipliers for healing/damage.
    /// </summary>
    private void OnReagentEffect(ref EntityEffectReagentArgs args)
    {
        // Find the heart organ processing this reagent
        var target = args.TargetEntity;
        if (!target.IsValid())
            return;

        // Check if this is being processed by a heart
        if (args.OrganEntity == null || !TryComp<MetabolizerComponent>(args.OrganEntity, out var metabolizer))
            return;

        if (!HasComp<HeartComponent>(args.OrganEntity))
            return;

        // Get heart efficiency
        if (!TryComp<CyberOrganEfficiencyComponent>(args.OrganEntity, out var efficiency))
            return;

        var finalEfficiency = _organEfficiency.GetFinalEfficiency(args.OrganEntity.Value, efficiency);

        // Check if this is a healing or damaging effect
        var reagent = args.Reagent;
        if (reagent == null)
            return;

        // Check reagent groups to determine if it's medicine, poison, or narcotic
        bool isMedicine = reagent.Metabolisms?.ContainsKey("Medicine") ?? false;
        bool isPoison = reagent.Metabolisms?.ContainsKey("Poison") ?? false;
        bool isNarcotic = reagent.Metabolisms?.ContainsKey("Narcotic") ?? false;

        // Apply multipliers
        if (isMedicine && finalEfficiency > 1.0f)
        {
            // Positive healing multiplier: efficiency > 100% increases healing
            args.Scale *= finalEfficiency;
        }
        else if ((isPoison || isNarcotic) && finalEfficiency < 1.0f)
        {
            // Negative healing multiplier: efficiency < 100% increases damage
            var damageMultiplier = 1.0f + (1.0f - finalEfficiency);
            args.Scale *= damageMultiplier;
        }
    }
}

