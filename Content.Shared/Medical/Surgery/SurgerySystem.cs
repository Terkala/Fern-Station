// SPDX-FileCopyrightText: 2025 terkala <appleorange64@gmail.com>
//
// SPDX-License-Identifier: MIT

using Content.Shared.Body.Components;
using Content.Shared.Body.Systems;
using Content.Shared.DoAfter;
using Content.Shared.FixedPoint;
using Content.Shared.Medical.Surgery.Skill;
using Content.Shared.Medical.Surgery.Equipment;
using Content.Shared.Medical.Integrity;
using Content.Shared.Medical.Compatibility;
using Content.Shared.Medical.Biosynthetic;
using Content.Shared.Tag;
using Content.Shared.Popups;
using Content.Shared.Prototypes;
using Robust.Shared.GameObjects;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;

namespace Content.Shared.Medical.Surgery;

/// <summary>
/// Base system for surgery operations.
/// Handles surgery execution, step validation, and integrity cost calculation.
/// </summary>
public abstract partial class SharedSurgerySystem : EntitySystem
{
    [Dependency] protected readonly SharedBodySystem Body = default!;
    [Dependency] protected readonly SharedDoAfterSystem DoAfter = default!;
    [Dependency] protected readonly IPrototypeManager Prototypes = default!;
    [Dependency] protected readonly IRobustRandom Random = default!;
    [Dependency] protected readonly SharedPopupSystem Popup = default!;
    [Dependency] protected readonly TagSystem Tags = default!;
    [Dependency] protected readonly SharedIntegritySystem Integrity = default!;

    private static readonly ProtoId<TagPrototype> ImprovisedSurgeryToolTag = "ImprovisedSurgeryTool";

    /// <summary>
    /// Calculates the final integrity cost for an organ/limb/cybernetic installation.
    /// Takes into account base cost, tool quality, equipment quality, and compatibility.
    /// Compatible donors (same species as recipient) have 0 integrity cost.
    /// 
    /// Calculation order:
    /// 1. Donor species compatibility check (returns 0 if same species)
    /// 2. Base cost from integrity component (first found: Organ, Limb, or Cybernetic)
    /// 3. Improvised tool tag (1.5x multiplier if present)
    /// 4. Quality multipliers (tool * table, multiplicative stacking)
    /// 5. Compatibility multiplier (if incompatible species)
    /// 6. Biosynthetic multiplier (if matching species)
    /// 
    /// Edge cases:
    /// - Multiple integrity components: Uses first found (Organ > Limb > Cybernetic)
    /// - Compatibility and Biosynthetic: Compatibility checked first, can coexist (compatibility has priority for zero cost)
    /// - Quality multipliers: Both tool and table multipliers apply multiplicatively (toolQuality * tableQuality)
    /// </summary>
    public FixedPoint2 CalculateIntegrityCost(
        EntityUid item,
        EntityUid body,
        EntityUid? tool = null,
        EntityUid? operatingTable = null)
    {
        // Check if donor species matches recipient species - if so, no integrity cost
        var bodySpecies = GetBodySpecies(body);
        if (bodySpecies != null && TryComp<DonorSpeciesComponent>(item, out var donorSpecies))
        {
            if (donorSpecies.DonorSpecies == bodySpecies.Value)
            {
                // Compatible donor - no integrity cost
                return FixedPoint2.Zero;
            }
        }

        FixedPoint2 baseCost = FixedPoint2.Zero;

        // Get base cost from item (check in priority order: Organ > Limb > Cybernetic)
        // Edge case: If item has multiple integrity components, use first found
        if (TryComp<OrganIntegrityComponent>(item, out var organIntegrity) && organIntegrity != null)
        {
            baseCost = organIntegrity.BaseIntegrityCost;
        }
        else if (TryComp<LimbIntegrityComponent>(item, out var limbIntegrity) && limbIntegrity != null)
        {
            baseCost = limbIntegrity.BaseIntegrityCost;
        }
        else if (TryComp<CyberneticIntegrityComponent>(item, out var cyberIntegrity) && cyberIntegrity != null)
        {
            baseCost = cyberIntegrity.BaseIntegrityCost;
        }
        else
        {
            // Failsafe: If there's a compatibility component but no integrity component,
            // default to 1.0 so compatibility multipliers can still apply
            if (HasComp<OrganCompatibilityComponent>(item))
                baseCost = FixedPoint2.New(1.0);
            else
                return FixedPoint2.Zero; // No integrity cost if item doesn't have a cost component
        }

        float multiplier = 1.0f;

        // Apply tool quality modifier (improvised tools increase cost)
        if (tool != null && Tags.HasTag(tool.Value, ImprovisedSurgeryToolTag))
            multiplier *= 1.5f; // Configurable

        // Apply surgical quality from all items being used
        // Quality multipliers stack multiplicatively: toolQuality * tableQuality
        // Check tool quality
        if (tool != null && TryComp<SurgicalQualityComponent>(tool.Value, out var toolQuality) && toolQuality != null)
            multiplier *= toolQuality.QualityMultiplier;

        // Check operating table quality
        if (operatingTable != null && TryComp<SurgicalQualityComponent>(operatingTable, out var tableQuality) && tableQuality != null)
            multiplier *= tableQuality.QualityMultiplier;

        // Apply compatibility modifier
        // Note: Compatibility and Biosynthetic can coexist - compatibility is checked first
        // If compatible (same species), returns early with 0 cost before reaching here
        if (TryComp<OrganCompatibilityComponent>(item, out var compatibility))
        {
            // Check if body's species is compatible
            if (bodySpecies != null && !compatibility.CompatibleSpecies.Contains(bodySpecies.Value))
                multiplier *= compatibility.IncompatibleCostMultiplier;
        }

        // Apply biosynthetic modifier
        // Only applies if compatibility check didn't already return 0 cost
        if (TryComp<BiosyntheticOrganComponent>(item, out var biosynthetic))
        {
            if (bodySpecies != null && (biosynthetic.TargetSpecies == null || biosynthetic.TargetSpecies == bodySpecies))
                multiplier *= biosynthetic.MatchingSpeciesCostMultiplier;
        }

        return baseCost * FixedPoint2.New(multiplier);
    }

    /// <summary>
    /// Gets the species prototype ID from a body entity.
    /// </summary>
    public ProtoId<EntityPrototype>? GetBodySpecies(EntityUid body)
    {
        // Get entity prototype ID from metadata
        if (TryComp(body, out MetaDataComponent? metadata) && metadata.EntityPrototype != null)
        {
            return new ProtoId<EntityPrototype>(metadata.EntityPrototype.ID);
        }
        return null;
    }

    /// <summary>
    /// Checks if an entity has medical surgery skill.
    /// </summary>
    protected bool HasMedicalSkill(EntityUid uid)
    {
        return HasComp<MedicalSurgerySkillComponent>(uid);
    }

}

