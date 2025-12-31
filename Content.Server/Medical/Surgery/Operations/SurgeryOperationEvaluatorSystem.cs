// SPDX-FileCopyrightText: 2025 terkala <appleorange64@gmail.com>
//
// SPDX-License-Identifier: MIT

using Content.Shared.Hands.Components;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Medical.Surgery.Operations;
using Content.Shared.Weapons.Melee;
using Content.Shared.FixedPoint;
using Robust.Shared.GameObjects;
using Robust.Shared.Containers;
using System.Linq;

namespace Content.Server.Medical.Surgery.Operations;

/// <summary>
/// System that evaluates whether improvised/secondary methods can be used for surgery operations.
/// </summary>
public sealed class SurgeryOperationEvaluatorSystem : EntitySystem
{
    [Dependency] private readonly SharedHandsSystem _hands = default!;

    /// <summary>
    /// Evaluates a secondary method for a surgery operation.
    /// </summary>
    public SurgeryOperationEvaluationResult EvaluateSecondaryMethod(
        EntityUid user,
        string evaluatorName,
        List<ComponentRegistry>? tools = null)
    {
        return evaluatorName switch
        {
            "CheckBluntDamage" => EvaluateBluntDamage(user),
            "CheckSlashDamage" => EvaluateSlashDamage(user),
            "CheckHeatDamage" => EvaluateHeatDamage(user),
            "CheckToolList" => EvaluateToolList(user, tools),
            _ => SurgeryOperationEvaluationResult.Invalid()
        };
    }

    /// <summary>
    /// Evaluates multiple evaluators with OR logic.
    /// </summary>
    public SurgeryOperationEvaluationResult EvaluateMultiEvaluator(
        EntityUid user,
        List<SurgeryOperationEvaluatorConfig> evaluators)
    {
        foreach (var config in evaluators)
        {
            var result = EvaluateSecondaryMethod(user, config.Evaluator, config.Tools);
            if (result.IsValid)
                return result;
        }

        return SurgeryOperationEvaluationResult.Invalid();
    }

    /// <summary>
    /// Checks if user has a melee weapon with blunt damage.
    /// Speed modifier is based on blunt damage (10 blunt = 1.0 speed).
    /// </summary>
    private SurgeryOperationEvaluationResult EvaluateBluntDamage(EntityUid user)
    {
        if (!TryComp<HandsComponent>(user, out var hands))
            return SurgeryOperationEvaluationResult.Invalid();

        foreach (var heldItem in _hands.EnumerateHeld(user, hands))
        {
            if (!TryComp<MeleeWeaponComponent>(heldItem, out var melee))
                continue;

            if (melee.Damage.DamageDict.TryGetValue("Blunt", out var bluntDamage) && bluntDamage > 0)
            {
                // 10 blunt = average speed (1.0), scale accordingly
                var speed = (float)bluntDamage / 10.0f;
                if (speed < 0.1f) speed = 0.1f; // Minimum speed
                if (speed > 3.0f) speed = 3.0f; // Maximum speed

                return SurgeryOperationEvaluationResult.Valid(speed, heldItem);
            }
        }

        return SurgeryOperationEvaluationResult.Invalid();
    }

    /// <summary>
    /// Checks if user has a melee weapon with slash damage.
    /// Speed modifier is based on slash damage (10 slash = 1.0 speed).
    /// </summary>
    private SurgeryOperationEvaluationResult EvaluateSlashDamage(EntityUid user)
    {
        if (!TryComp<HandsComponent>(user, out var hands))
            return SurgeryOperationEvaluationResult.Invalid();

        foreach (var heldItem in _hands.EnumerateHeld(user, hands))
        {
            if (!TryComp<MeleeWeaponComponent>(heldItem, out var melee))
                continue;

            if (melee.Damage.DamageDict.TryGetValue("Slash", out var slashDamage) && slashDamage > 0)
            {
                // 10 slash = average speed (1.0), scale accordingly
                var speed = (float)slashDamage / 10.0f;
                if (speed < 0.1f) speed = 0.1f; // Minimum speed
                if (speed > 3.0f) speed = 3.0f; // Maximum speed

                return SurgeryOperationEvaluationResult.Valid(speed, heldItem);
            }
        }

        return SurgeryOperationEvaluationResult.Invalid();
    }

    /// <summary>
    /// Checks if user has a melee weapon with heat damage.
    /// Speed modifier is based on heat damage (5 heat = 1.0 speed).
    /// </summary>
    private SurgeryOperationEvaluationResult EvaluateHeatDamage(EntityUid user)
    {
        if (!TryComp<HandsComponent>(user, out var hands))
            return SurgeryOperationEvaluationResult.Invalid();

        foreach (var heldItem in _hands.EnumerateHeld(user, hands))
        {
            if (!TryComp<MeleeWeaponComponent>(heldItem, out var melee))
                continue;

            if (melee.Damage.DamageDict.TryGetValue("Heat", out var heatDamage) && heatDamage > 0)
            {
                // 5 heat = average speed (1.0), scale accordingly
                var speed = (float)heatDamage / 5.0f;
                if (speed < 0.1f) speed = 0.1f; // Minimum speed
                if (speed > 3.0f) speed = 3.0f; // Maximum speed

                return SurgeryOperationEvaluationResult.Valid(speed, heldItem);
            }
        }

        return SurgeryOperationEvaluationResult.Invalid();
    }

    /// <summary>
    /// Checks if user has any of the specified tool components.
    /// </summary>
    private SurgeryOperationEvaluationResult EvaluateToolList(EntityUid user, List<ComponentRegistry>? tools)
    {
        if (tools == null || tools.Count == 0)
            return SurgeryOperationEvaluationResult.Invalid();

        if (!TryComp<HandsComponent>(user, out var hands))
            return SurgeryOperationEvaluationResult.Invalid();

        foreach (var heldItem in _hands.EnumerateHeld(user, hands))
        {
            foreach (var toolReg in tools)
            {
                if (HasComp(heldItem, toolReg.Component.GetType()))
                {
                    // Tool found, return with normal speed
                    return SurgeryOperationEvaluationResult.Valid(1.0f, heldItem);
                }
            }
        }

        return SurgeryOperationEvaluationResult.Invalid();
    }
}
