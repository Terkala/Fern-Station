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
using Robust.Shared.Prototypes;
using System.Linq;

namespace Content.Server.Medical.Surgery.Operations;

/// <summary>
/// System that evaluates whether improvised/secondary methods can be used for surgery operations.
/// </summary>
public sealed class SurgeryOperationEvaluatorSystem : EntitySystem
{
    [Dependency] private readonly SharedHandsSystem _hands = default!;

    /// <summary>
    /// Evaluates a secondary/improvised method for a surgery operation.
    /// 
    /// This method checks if the user has tools/methods available for an improvised surgery operation.
    /// Returns a result indicating whether the method can be used and what speed modifier to apply.
    /// 
    /// Supported evaluators:
    /// - "CheckBluntDamage": Checks for melee weapons with blunt damage (for bone removal)
    /// - "CheckSlashDamage": Checks for melee weapons with slash damage (for tissue cutting)
    /// - "CheckHeatDamage": Checks for melee weapons with heat damage (for cauterization)
    /// - "CheckToolList": Checks for specific tool components in hands
    /// 
    /// Speed modifiers are calculated based on damage values or tool quality.
    /// </summary>
    /// <param name="user">The user performing the surgery operation</param>
    /// <param name="evaluatorName">Name of the evaluator to use (e.g., "CheckBluntDamage")</param>
    /// <param name="tools">Optional list of tool component registries for ToolList evaluator</param>
    /// <returns>Evaluation result indicating validity and speed modifier</returns>
    public SurgeryOperationEvaluationResult EvaluateSecondaryMethod(
        EntityUid user,
        string evaluatorName,
        List<Robust.Shared.Prototypes.ComponentRegistry>? tools = null)
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
    /// 
    /// This method checks multiple evaluators and returns the first valid result.
    /// Used for operations that can be performed with multiple different improvised methods.
    /// For example, clamping bleeders can be done with either hemostats OR heat damage.
    /// 
    /// Returns Invalid if none of the evaluators pass.
    /// </summary>
    /// <param name="user">The user performing the surgery operation</param>
    /// <param name="evaluators">List of evaluator configurations to check</param>
    /// <returns>First valid evaluation result, or Invalid if none pass</returns>
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
    /// Checks if user has a melee weapon with blunt damage suitable for improvised bone removal.
    /// 
    /// Speed modifier calculation:
    /// - 10 blunt damage = 1.0x speed (normal speed)
    /// - Higher blunt damage = faster operation
    /// - Lower blunt damage = slower operation
    /// - Speed is clamped between 0.1x and 3.0x
    /// 
    /// Used for operations like bone removal via bashing instead of using a bone saw.
    /// </summary>
    /// <param name="user">The user to check for blunt damage weapons</param>
    /// <returns>Evaluation result with speed modifier, or Invalid if no suitable weapon found</returns>
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
    /// Checks if user has a melee weapon with slash damage suitable for improvised tissue cutting.
    /// 
    /// Speed modifier calculation:
    /// - 10 slash damage = 1.0x speed (normal speed)
    /// - Higher slash damage = faster operation
    /// - Lower slash damage = slower operation
    /// - Speed is clamped between 0.1x and 3.0x
    /// 
    /// Used for operations like cutting tissue or severing blood vessels with a slashing weapon
    /// instead of using a scalpel.
    /// </summary>
    /// <param name="user">The user to check for slash damage weapons</param>
    /// <returns>Evaluation result with speed modifier, or Invalid if no suitable weapon found</returns>
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
    /// Checks if user has a melee weapon with heat damage suitable for improvised cauterization.
    /// 
    /// Speed modifier calculation:
    /// - 5 heat damage = 1.0x speed (normal speed)
    /// - Higher heat damage = faster operation
    /// - Lower heat damage = slower operation
    /// - Speed is clamped between 0.1x and 3.0x
    /// 
    /// Used for operations like cauterizing wounds or clamping bleeders with heat
    /// instead of using a cautery tool.
    /// </summary>
    /// <param name="user">The user to check for heat damage weapons</param>
    /// <returns>Evaluation result with speed modifier, or Invalid if no suitable weapon found</returns>
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
    /// Checks if user has any of the specified tool components in their hands.
    /// 
    /// This evaluator checks for specific component types rather than damage types.
    /// Used for operations that can be performed with specific tools that aren't primary surgical tools.
    /// For example, clamping bleeders can be done with hemostats (primary) or wirecutters (improvised).
    /// 
    /// Returns normal speed (1.0x) if any of the specified tools are found.
    /// </summary>
    /// <param name="user">The user to check for tool components</param>
    /// <param name="tools">List of tool component registries to check for</param>
    /// <returns>Evaluation result with normal speed if tool found, or Invalid if not found</returns>
    private SurgeryOperationEvaluationResult EvaluateToolList(EntityUid user, List<Robust.Shared.Prototypes.ComponentRegistry>? tools)
    {
        if (tools == null || tools.Count == 0)
            return SurgeryOperationEvaluationResult.Invalid();

        if (!TryComp<HandsComponent>(user, out var hands))
            return SurgeryOperationEvaluationResult.Invalid();

        foreach (var heldItem in _hands.EnumerateHeld(user, hands))
        {
            foreach (var toolReg in tools)
            {
                // ComponentRegistry is a Dictionary<string, ComponentRegistryEntry>
                // Get the first component name (key) and use it to get the component type
                var componentName = toolReg.Keys.FirstOrDefault();
                if (componentName != null)
                {
                    var componentFactory = EntityManager.ComponentFactory;
                    if (componentFactory.TryGetRegistration(componentName, out var reg))
                    {
                        if (HasComp(heldItem, reg.Type))
                        {
                            // Tool found, return with normal speed
                            return SurgeryOperationEvaluationResult.Valid(1.0f, heldItem);
                        }
                    }
                }
            }
        }

        return SurgeryOperationEvaluationResult.Invalid();
    }
}
