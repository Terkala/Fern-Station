// SPDX-FileCopyrightText: 2025 terkala <appleorange64@gmail.com>
//
// SPDX-License-Identifier: MIT

using System.Linq;
using System.Numerics;
using Content.Server.Body.Systems;
using Content.Shared.Body.Components;
using Content.Shared.Body.Part;
using Content.Shared.Body.Systems;
using Content.Shared.Damage;
using Content.Shared.Damage.Components;
using Content.Shared.Damage.Systems;
using Content.Shared.FixedPoint;
using Content.Shared.Medical.Limb.Damage;
using Content.Shared.Medical.Limb.Targeting;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Random;

namespace Content.Server.Medical.Limb.Damage;

/// <summary>
/// Server-side limb damage processing with directional selection, damage inheritance to torso, healing handling, missing limb fallback, chemical distribution, and limb disable/destroy threshold checking.
/// This system is completely original and not based on any _Shitmed implementations.
/// </summary>
public sealed class LimbDamageSystem : SharedLimbDamageSystem
{
    [Dependency] private readonly BodySystem _body = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;

    private const float FacingAngleThreshold = MathF.PI / 4f; // 45 degrees

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<BodyComponent, BeforeDamageChangedEvent>(OnBodyDamage);
    }

    private void OnBodyDamage(EntityUid uid, BodyComponent body, ref BeforeDamageChangedEvent args)
    {
        // Check if this is chemical damage (reagent-based) - if so, distribute to all body parts
        if (IsChemicalDamage(args.Damage))
        {
            DistributeChemicalDamage(uid, args.Damage, args.Origin);
            args.Cancelled = true; // Cancel original damage, we've handled it
            return;
        }

        // Get target from LimbTargetingComponent, default to Torso if not present
        var target = GetTargetBodyPart(uid);
        
        // Route damage based on target
        RouteDamage(uid, args.Damage, target, args.Origin);
        
        // Cancel original damage since we're routing it ourselves
        args.Cancelled = true;
    }

    /// <summary>
    /// Gets the target body part from LimbTargetingComponent, or defaults to Torso.
    /// </summary>
    private TargetBodyPart GetTargetBodyPart(EntityUid uid)
    {
        if (TryComp<LimbTargetingComponent>(uid, out var targeting))
            return targeting.SelectedTarget;
        
        return TargetBodyPart.Torso;
    }

    /// <summary>
    /// Routes damage to the appropriate body part based on target and directional logic.
    /// </summary>
    private void RouteDamage(EntityUid body, DamageSpecifier damage, TargetBodyPart target, EntityUid? origin)
    {
        switch (target)
        {
            case TargetBodyPart.Head:
                ApplyDamageToTarget(body, damage, BodyPartType.Head, null, origin);
                break;
            
            case TargetBodyPart.Torso:
                ApplyDamageToTarget(body, damage, BodyPartType.Torso, null, origin);
                break;
            
            case TargetBodyPart.Arms:
                ApplyDamageToArms(body, damage, origin);
                break;
            
            case TargetBodyPart.Legs:
                ApplyDamageToLegs(body, damage, origin);
                break;
        }
    }

    /// <summary>
    /// Applies damage to arms, choosing left or right based on facing and damage source direction.
    /// </summary>
    private void ApplyDamageToArms(EntityUid body, DamageSpecifier damage, EntityUid? origin)
    {
        // Check if body has arms
        var arms = _body.GetBodyChildrenOfType(body, BodyPartType.Arm).ToList();
        if (arms.Count == 0)
        {
            // Missing limb fallback: 50% damage to torso
            ApplyMissingLimbFallback(body, damage);
            return;
        }

        // Choose which arm to damage based on directional logic
        var chosenSymmetry = ChooseLimbSide(body, origin, BodyPartType.Arm);
        
        // Find the chosen arm
        var chosenArm = arms.FirstOrDefault(a => a.Component.Symmetry == chosenSymmetry);
        if (chosenArm.Id == EntityUid.Invalid)
        {
            // Fallback to first available arm
            chosenArm = arms[0];
        }

        // Apply damage to arm and hand (if exists)
        ApplyDamageToLimbUnit(body, chosenArm.Id, damage, BodyPartType.Arm, chosenSymmetry, origin);
    }

    /// <summary>
    /// Applies damage to legs, choosing left or right based on facing and damage source direction.
    /// </summary>
    private void ApplyDamageToLegs(EntityUid body, DamageSpecifier damage, EntityUid? origin)
    {
        // Check if body has legs
        var legs = _body.GetBodyChildrenOfType(body, BodyPartType.Leg).ToList();
        if (legs.Count == 0)
        {
            // Missing limb fallback: 50% damage to torso
            ApplyMissingLimbFallback(body, damage);
            return;
        }

        // Choose which leg to damage based on directional logic
        var chosenSymmetry = ChooseLimbSide(body, origin, BodyPartType.Leg);
        
        // Find the chosen leg
        var chosenLeg = legs.FirstOrDefault(l => l.Component.Symmetry == chosenSymmetry);
        if (chosenLeg.Id == EntityUid.Invalid)
        {
            // Fallback to first available leg
            chosenLeg = legs[0];
        }

        // Apply damage to leg and foot (if exists)
        ApplyDamageToLimbUnit(body, chosenLeg.Id, damage, BodyPartType.Leg, chosenSymmetry, origin);
    }

    /// <summary>
    /// Chooses which side (left or right) of a limb to damage based on facing and damage source direction.
    /// </summary>
    private BodyPartSymmetry ChooseLimbSide(EntityUid body, EntityUid? origin, BodyPartType limbType)
    {
        if (origin == null)
        {
            // No origin, random choice
            return _random.Prob(0.5f) ? BodyPartSymmetry.Left : BodyPartSymmetry.Right;
        }

        var bodyXform = Transform(body);
        var originXform = Transform(origin.Value);
        
        var bodyPos = _transform.GetWorldPosition(bodyXform);
        var originPos = _transform.GetWorldPosition(originXform);
        var bodyRotation = _transform.GetWorldRotation(bodyXform);

        // Calculate direction from body to origin
        var toOrigin = originPos - bodyPos;
        if (toOrigin.LengthSquared() < 0.01f)
        {
            // Too close, random choice
            return _random.Prob(0.5f) ? BodyPartSymmetry.Left : BodyPartSymmetry.Right;
        }

        var toOriginAngle = Angle.FromWorldVec(toOrigin);
        var facingAngle = bodyRotation;

        // Calculate angle difference
        var angleDiff = Angle.ShortestDistance(facingAngle, toOriginAngle).Theta;

        // Check if target is facing the source (within 45 degrees)
        if (Math.Abs(angleDiff) < FacingAngleThreshold)
        {
            // Facing source: random choice
            return _random.Prob(0.5f) ? BodyPartSymmetry.Left : BodyPartSymmetry.Right;
        }

        // Not facing source: geometry-based choice
        // Determine which side the origin is on relative to facing direction
        // Positive angle = origin is to the right, negative = to the left
        // In SS13, facing south (0 degrees) means right is east (positive), left is west (negative)
        return angleDiff > 0 ? BodyPartSymmetry.Right : BodyPartSymmetry.Left;
    }

    /// <summary>
    /// Applies damage to a limb unit (arm+hand or leg+foot).
    /// </summary>
    private void ApplyDamageToLimbUnit(EntityUid body, EntityUid mainPart, DamageSpecifier damage, BodyPartType mainType, BodyPartSymmetry symmetry, EntityUid? origin)
    {
        // Apply damage to main part (arm or leg)
        var mainDamage = Damageable.TryChangeDamage(mainPart, damage, origin: origin);
        
        // Apply damage to child part (hand or foot) if it exists
        var childType = mainType == BodyPartType.Arm ? BodyPartType.Hand : BodyPartType.Foot;
        var childParts = _body.GetBodyPartChildren(mainPart).Where(p => 
            p.Component.PartType == childType && p.Component.Symmetry == symmetry).ToList();
        
        foreach (var childPart in childParts)
        {
            Damageable.TryChangeDamage(childPart.Id, damage, origin: origin);
        }

        // Apply damage inheritance to torso
        if (mainDamage != null && mainDamage.GetTotal() > FixedPoint2.Zero)
        {
            ApplyDamageInheritance(body, TargetBodyPart.Arms, mainDamage);
        }

        // Check thresholds
        CheckLimbThresholds(mainPart);
    }

    /// <summary>
    /// Applies damage to a specific target body part.
    /// </summary>
    private void ApplyDamageToTarget(EntityUid body, DamageSpecifier damage, BodyPartType type, BodyPartSymmetry? symmetry, EntityUid? origin)
    {
        var parts = _body.GetBodyChildrenOfType(body, type, symmetry: symmetry).ToList();
        if (parts.Count == 0)
        {
            // Missing part fallback
            if (type == BodyPartType.Head)
            {
                ApplyMissingLimbFallback(body, damage);
            }
            // Torso always exists (it's the root), so no fallback needed
            return;
        }

        foreach (var part in parts)
        {
            var result = Damageable.TryChangeDamage(part.Id, damage, origin: origin);
            
            // Apply damage inheritance to torso (except for torso itself)
            if (type != BodyPartType.Torso && result != null && result.GetTotal() > FixedPoint2.Zero)
            {
                var target = type == BodyPartType.Head ? TargetBodyPart.Head : TargetBodyPart.Torso;
                ApplyDamageInheritance(body, target, result);
            }

            // Check thresholds for non-torso parts
            if (type != BodyPartType.Torso)
            {
                CheckLimbThresholds(part.Id);
            }
        }
    }

    /// <summary>
    /// Applies missing limb fallback: 50% damage to torso.
    /// </summary>
    private void ApplyMissingLimbFallback(EntityUid body, DamageSpecifier damage)
    {
        var fallbackDamage = damage * 0.5f;
        var torso = _body.GetBodyChildrenOfType(body, BodyPartType.Torso).FirstOrDefault();
        if (torso.Id != EntityUid.Invalid)
        {
            Damageable.TryChangeDamage(torso.Id, fallbackDamage);
        }
    }

    /// <summary>
    /// Applies damage inheritance from a limb/head to the torso.
    /// </summary>
    private void ApplyDamageInheritance(EntityUid body, TargetBodyPart sourceTarget, DamageSpecifier sourceDamage)
    {
        if (!TryComp<DamageInheritanceComponent>(body, out var inheritance))
            return;

        var inheritancePercent = inheritance.GetInheritancePercentage(sourceTarget);
        if (inheritancePercent <= 0f)
            return;

        var inheritedDamage = sourceDamage * inheritancePercent;
        var torso = _body.GetBodyChildrenOfType(body, BodyPartType.Torso).FirstOrDefault();
        if (torso.Id != EntityUid.Invalid)
        {
            Damageable.TryChangeDamage(torso.Id, inheritedDamage);
        }
    }

    /// <summary>
    /// Checks if damage is from chemicals (reagents).
    /// </summary>
    private bool IsChemicalDamage(DamageSpecifier damage)
    {
        // Check if damage has specific chemical damage types
        // This is a simplified check - in practice, you'd check for specific damage types
        // For now, we'll use a heuristic: if damage has "Poison" or "Toxin" types
        return damage.DamageDict.Keys.Any(k => k.Contains("Poison", StringComparison.OrdinalIgnoreCase) ||
                                                k.Contains("Toxin", StringComparison.OrdinalIgnoreCase) ||
                                                k.Contains("Radiation", StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// Distributes chemical damage/healing to all body parts equally.
    /// </summary>
    private void DistributeChemicalDamage(EntityUid body, DamageSpecifier damage, EntityUid? origin)
    {
        // Apply full damage to head
        ApplyDamageToTarget(body, damage, BodyPartType.Head, null, origin);
        
        // Apply full damage to both arms
        var arms = _body.GetBodyChildrenOfType(body, BodyPartType.Arm).ToList();
        foreach (var arm in arms)
        {
            ApplyDamageToLimbUnit(body, arm.Id, damage, BodyPartType.Arm, arm.Component.Symmetry, origin);
        }
        
        // Apply full damage to both legs
        var legs = _body.GetBodyChildrenOfType(body, BodyPartType.Leg).ToList();
        foreach (var leg in legs)
        {
            ApplyDamageToLimbUnit(body, leg.Id, damage, BodyPartType.Leg, leg.Component.Symmetry, origin);
        }
        
        // Apply full damage to torso
        ApplyDamageToTarget(body, damage, BodyPartType.Torso, null, origin);
    }

    /// <summary>
    /// Checks limb damage thresholds and updates limb state (disabled/destroyed).
    /// </summary>
    private void CheckLimbThresholds(EntityUid part)
    {
        if (!TryComp<DamageableComponent>(part, out var damageable))
            return;

        if (!TryComp<LimbDamageComponent>(part, out var limbDamage))
            limbDamage = EnsureComp<LimbDamageComponent>(part);

        var totalDamage = damageable.TotalDamage;
        
        // Check destroy threshold (200 damage)
        if (totalDamage >= limbDamage.DestroyThreshold && !limbDamage.IsDestroyed)
        {
            limbDamage.IsDestroyed = true;
            limbDamage.IsDisabled = true;
            Dirty(part, limbDamage);
            
            // Sever the limb (drop it)
            SeverLimb(part);
        }
        // Check disable threshold (100 damage)
        else if (totalDamage >= limbDamage.DisableThreshold && !limbDamage.IsDisabled)
        {
            limbDamage.IsDisabled = true;
            Dirty(part, limbDamage);
        }
        // Check if limb is healed below disable threshold
        else if (totalDamage < limbDamage.DisableThreshold && limbDamage.IsDisabled && !limbDamage.IsDestroyed)
        {
            limbDamage.IsDisabled = false;
            Dirty(part, limbDamage);
        }
    }

    /// <summary>
    /// Severes a limb (removes it from body and drops it).
    /// </summary>
    private void SeverLimb(EntityUid part)
    {
        // This will be implemented to actually remove the limb from the body
        // For now, this is a placeholder
        // In practice, you'd use BodySystem to remove the part
    }

    /// <summary>
    /// Heals a target body part. For Arms/Legs, heals both limbs simultaneously.
    /// </summary>
    public void HealTarget(EntityUid body, TargetBodyPart target, DamageSpecifier healing)
    {
        switch (target)
        {
            case TargetBodyPart.Head:
                HealBodyPart(body, BodyPartType.Head, null, healing);
                break;
            
            case TargetBodyPart.Torso:
                HealBodyPart(body, BodyPartType.Torso, null, healing);
                break;
            
            case TargetBodyPart.Arms:
                HealArms(body, healing);
                break;
            
            case TargetBodyPart.Legs:
                HealLegs(body, healing);
                break;
        }
    }

    /// <summary>
    /// Heals both arms simultaneously.
    /// </summary>
    private void HealArms(EntityUid body, DamageSpecifier healing)
    {
        var arms = _body.GetBodyChildrenOfType(body, BodyPartType.Arm).ToList();
        foreach (var arm in arms)
        {
            HealLimbUnit(body, arm.Id, healing, BodyPartType.Arm, arm.Component.Symmetry);
        }
    }

    /// <summary>
    /// Heals both legs simultaneously.
    /// </summary>
    private void HealLegs(EntityUid body, DamageSpecifier healing)
    {
        var legs = _body.GetBodyChildrenOfType(body, BodyPartType.Leg).ToList();
        foreach (var leg in legs)
        {
            HealLimbUnit(body, leg.Id, healing, BodyPartType.Leg, leg.Component.Symmetry);
        }
    }

    /// <summary>
    /// Heals a limb unit (arm+hand or leg+foot).
    /// </summary>
    private void HealLimbUnit(EntityUid body, EntityUid mainPart, DamageSpecifier healing, BodyPartType mainType, BodyPartSymmetry symmetry)
    {
        // Heal main part (arm or leg)
        Damageable.TryChangeDamage(mainPart, healing, ignoreResistances: true);
        
        // Heal child part (hand or foot) if it exists
        var childType = mainType == BodyPartType.Arm ? BodyPartType.Hand : BodyPartType.Foot;
        var childParts = _body.GetBodyPartChildren(mainPart).Where(p => 
            p.Component.PartType == childType && p.Component.Symmetry == symmetry).ToList();
        
        foreach (var childPart in childParts)
        {
            Damageable.TryChangeDamage(childPart.Id, healing, ignoreResistances: true);
        }

        // Check thresholds after healing
        CheckLimbThresholds(mainPart);
    }

    /// <summary>
    /// Heals a specific body part.
    /// </summary>
    private void HealBodyPart(EntityUid body, BodyPartType type, BodyPartSymmetry? symmetry, DamageSpecifier healing)
    {
        var parts = _body.GetBodyChildrenOfType(body, type, symmetry: symmetry).ToList();
        foreach (var part in parts)
        {
            Damageable.TryChangeDamage(part.Id, healing, ignoreResistances: true);
            
            // Check thresholds after healing
            if (type != BodyPartType.Torso)
            {
                CheckLimbThresholds(part.Id);
            }
        }
    }

    /// <summary>
    /// Distributes chemical healing to all body parts equally.
    /// </summary>
    public void DistributeChemicalHealing(EntityUid body, DamageSpecifier healing)
    {
        // Apply full healing to head
        HealBodyPart(body, BodyPartType.Head, null, healing);
        
        // Apply full healing to both arms
        HealArms(body, healing);
        
        // Apply full healing to both legs
        HealLegs(body, healing);
        
        // Apply full healing to torso
        HealBodyPart(body, BodyPartType.Torso, null, healing);
    }
}
