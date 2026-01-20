// SPDX-FileCopyrightText: 2025 terkala <appleorange64@gmail.com>
//
// SPDX-License-Identifier: MIT

using Content.Server.Atmos.EntitySystems;
using Content.Server.Chemistry.Containers.EntitySystems;
using Content.Server.Fluids.Components;
using Content.Shared.Atmos;
using Content.Shared.BloodCult;
using Content.Shared.Chemistry.Components;
using Content.Shared.Chemistry.EntitySystems;
using Content.Shared.FixedPoint;
using Content.Shared.Fluids.Components;
using Content.Shared.Forensics.Components;
using Content.Server.Medical.Integrity;
using Content.Shared.Medical.Integrity;
using Content.Shared.Medical.Surgery;
using Content.Shared.Medical.Surgery.Equipment;
using Robust.Server.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using System.Numerics;

namespace Content.Server.Medical.Surgery;

/// <summary>
/// System that calculates room cleanliness for surgical procedures.
/// Uses the same airborne radius check as the zombie system to find non-airblocked tiles.
/// </summary>
public sealed class RoomCleanlinessSystem : EntitySystem
{
    [Dependency] private readonly AtmosphereSystem _atmosphereSystem = default!;
    [Dependency] private readonly MapSystem _mapSystem = default!;
    [Dependency] private readonly EntityLookupSystem _entityLookup = default!;
    [Dependency] private readonly SharedSolutionContainerSystem _solutionContainer = default!;
    [Dependency] private readonly TransformSystem _transform = default!;
    [Dependency] private readonly SharedIntegritySystem _integrity = default!;
    [Dependency] private readonly IntegritySystem _integritySystem = default!;

    private const float CheckRadius = 3.0f; // 3 tile radius
    private static readonly FixedPoint2 PatientBloodAllowance = FixedPoint2.New(20); // 20 units of patient's own blood allowed

    /// <summary>
    /// Calculates the unsanitary conditions penalty for a patient at a given location.
    /// Returns a value from 0-3 based on blood in the area and bed quality.
    /// Also updates the component with the current calculated penalty if not yet applied.
    /// </summary>
    public FixedPoint2 CalculateUnsanitaryPenalty(EntityUid patient, EntityCoordinates coordinates)
    {
        var patientXform = Transform(patient);

        // Get bed quality if patient is on a bed
        float bedQualityMultiplier = 1.0f;
        if (TryComp<SurgicalQualityComponent>(coordinates.EntityId, out var bedQuality))
        {
            bedQualityMultiplier = bedQuality.QualityMultiplier;
        }

        // Calculate blood contamination in the area
        var bloodContamination = CalculateBloodContamination(patient, coordinates, patientXform);

        // Penalty starts at 0, increases based on blood contamination
        // Bed quality reduces the penalty (better bed = less penalty)
        var basePenalty = bloodContamination;
        var adjustedPenalty = basePenalty * bedQualityMultiplier;

        // Clamp to 0-3 range
        var finalPenalty = FixedPoint2.Clamp(adjustedPenalty, FixedPoint2.Zero, FixedPoint2.New(3));
        
        // Update the component with current calculated penalty if not yet applied
        if (TryComp<UnsanitaryConditionsComponent>(patient, out var unsanitary) && !unsanitary.PenaltyApplied)
        {
            unsanitary.Penalty = finalPenalty;
            Dirty(patient, unsanitary);
        }
        
        return finalPenalty;
    }

    /// <summary>
    /// Calculates blood contamination in the area around a patient.
    /// Uses the same tile-based flood fill as the zombie system.
    /// </summary>
    private FixedPoint2 CalculateBloodContamination(EntityUid patient, EntityCoordinates coordinates, TransformComponent patientXform)
    {
        var totalBlood = FixedPoint2.Zero;
        var patientDna = GetPatientDna(patient);

        // Must be on a grid to use tile-based pathfinding
        if (!TryComp<MapGridComponent>(patientXform.GridUid, out var grid))
        {
            // Fallback to simple range check if not on a grid
            return CalculateBloodInRange(patient, coordinates, patientDna, CheckRadius);
        }

        var startTile = _mapSystem.TileIndicesFor(patientXform.GridUid.Value, grid, coordinates);
        var visited = new HashSet<Vector2i>();
        var queue = new Queue<Vector2i>();

        queue.Enqueue(startTile);
        visited.Add(startTile);

        var directions = new[] { AtmosDirection.North, AtmosDirection.South, AtmosDirection.East, AtmosDirection.West };
        var offsets = new[] { new Vector2i(0, 1), new Vector2i(0, -1), new Vector2i(1, 0), new Vector2i(-1, 0) };

        // Breadth-first search through tiles
        while (queue.Count > 0)
        {
            var currentTile = queue.Dequeue();

            // Calculate distance from start tile
            var tileDistance = (currentTile - startTile).Length;
            if (tileDistance > CheckRadius)
                continue;

            // Check for blood puddles on this tile
            var worldPos = _mapSystem.GridTileToWorld(patientXform.GridUid.Value, grid, currentTile);
            totalBlood += GetBloodOnTile(patient, worldPos, patientDna);

            // Check adjacent tiles
            for (int i = 0; i < directions.Length; i++)
            {
                var direction = directions[i];
                var offset = offsets[i];
                var neighborTile = currentTile + offset;

                if (visited.Contains(neighborTile))
                    continue;

                // Check if blocked by walls/closed doors using atmos system
                if (_atmosphereSystem.IsTileAirBlocked(patientXform.GridUid.Value, currentTile, direction, grid))
                    continue;

                // Check distance from start
                var neighborDistance = (neighborTile - startTile).Length;
                if (neighborDistance > CheckRadius)
                    continue;

                queue.Enqueue(neighborTile);
                visited.Add(neighborTile);
            }
        }

        // Convert blood amount to penalty (roughly 1 penalty per 50 units of blood, max 3)
        // After allowing patient's own blood
        var contamination = FixedPoint2.Max(FixedPoint2.Zero, totalBlood - PatientBloodAllowance);
        return FixedPoint2.Min(FixedPoint2.New(3), contamination / FixedPoint2.New(50));
    }

    /// <summary>
    /// Fallback method for calculating blood in range when not on a grid.
    /// </summary>
    private FixedPoint2 CalculateBloodInRange(EntityUid patient, EntityCoordinates coordinates, string? patientDna, float range)
    {
        var totalBlood = FixedPoint2.Zero;
        var puddlesInRange = _entityLookup.GetEntitiesInRange<PuddleComponent>(coordinates, range, LookupFlags.Uncontained);

        foreach (var puddle in puddlesInRange)
        {
            totalBlood += GetBloodFromPuddle(patient, puddle, patientDna);
        }

        var contamination = FixedPoint2.Max(FixedPoint2.Zero, totalBlood - PatientBloodAllowance);
        return FixedPoint2.Min(FixedPoint2.New(3), contamination / FixedPoint2.New(50));
    }

    /// <summary>
    /// Gets the amount of blood on a specific tile.
    /// </summary>
    private FixedPoint2 GetBloodOnTile(EntityUid patient, MapCoordinates worldPos, string? patientDna)
    {
        var totalBlood = FixedPoint2.Zero;
        var puddlesInRange = _entityLookup.GetEntitiesInRange<PuddleComponent>(worldPos, 0.5f, LookupFlags.Uncontained);

        foreach (var puddle in puddlesInRange)
        {
            totalBlood += GetBloodFromPuddle(patient, puddle, patientDna);
        }

        return totalBlood;
    }

    /// <summary>
    /// Gets the amount of blood from a puddle, excluding patient's own blood up to the allowance.
    /// </summary>
    private FixedPoint2 GetBloodFromPuddle(EntityUid patient, EntityUid puddle, string? patientDna)
    {
        if (!TryComp<PuddleComponent>(puddle, out var puddleComp))
            return FixedPoint2.Zero;

        if (!_solutionContainer.ResolveSolution((puddle, null), puddleComp.SolutionName, ref puddleComp.Solution, out var solution))
            return FixedPoint2.Zero;

        var totalBlood = FixedPoint2.Zero;

        // Check if this is the patient's own blood
        bool isPatientBlood = false;
        if (patientDna != null && TryComp<DnaComponent>(puddle, out var dna) && dna.DNA == patientDna)
        {
            isPatientBlood = true;
        }

        // Check for valid blood reagents in solution
        foreach (var (reagentId, quantity) in solution.Contents)
        {
            // Check if this reagent is a valid blood type
            if (BloodCultConstants.SacrificeBloodReagents.Contains(reagentId.Prototype))
            {
                // Patient's own blood doesn't count toward contamination
                if (isPatientBlood)
                    continue;

                totalBlood += quantity;
            }
        }

        return totalBlood;
    }

    /// <summary>
    /// Gets the patient's DNA string for comparison.
    /// </summary>
    private string? GetPatientDna(EntityUid patient)
    {
        if (TryComp<DnaComponent>(patient, out var dna))
            return dna.DNA;

        return null;
    }

    /// <summary>
    /// Updates the unsanitary conditions penalty for a patient based on current room cleanliness.
    /// </summary>
    public void UpdateUnsanitaryPenalty(EntityUid patient)
    {
        var xform = Transform(patient);
        var penalty = CalculateUnsanitaryPenalty(patient, xform.Coordinates);
        var unsanitary = EnsureComp<UnsanitaryConditionsComponent>(patient);
        
        // Only update if penalty hasn't been applied yet (surgery hasn't gone below skin)
        if (!unsanitary.PenaltyApplied)
        {
            unsanitary.Penalty = penalty;
            Dirty(patient, unsanitary);
        }
    }

    /// <summary>
    /// Applies the unsanitary conditions penalty when surgery goes below skin level.
    /// </summary>
    public void ApplyUnsanitaryPenalty(EntityUid patient)
    {
        if (!TryComp<UnsanitaryConditionsComponent>(patient, out var unsanitary))
            return;

        if (unsanitary.PenaltyApplied)
            return; // Already applied

        // Calculate current penalty based on room cleanliness
        unsanitary.Penalty = CalculateUnsanitaryPenalty(patient, Transform(patient).Coordinates);
        unsanitary.PenaltyApplied = true;
        Dirty(patient, unsanitary);

        // The unsanitary penalty is included in bio-rejection via IntegritySystem.UpdateCachedSurgeryPenalty(),
        // which queries UnsanitaryConditionsComponent and adds it to the cached surgery penalty total.
        // The cached penalty is then included in bio-rejection calculation via GetTotalSurgeryPenalty().
        // No additional action needed here - the integration is complete.
        if (TryComp<IntegrityComponent>(patient, out var integrity))
        {
            // Trigger update of cached surgery penalty to include the new unsanitary penalty
            _integritySystem.UpdateCachedSurgeryPenalty(patient, integrity);
            _integritySystem.RecalculateTargetBioRejection(patient, integrity);
        }
    }

    /// <summary>
    /// Treats unsanitary conditions, resetting the patient to current room cleanliness level.
    /// </summary>
    public void TreatUnsanitaryConditions(EntityUid patient)
    {
        if (!TryComp<UnsanitaryConditionsComponent>(patient, out var unsanitary))
            return;

        // Recalculate current room cleanliness
        var currentPenalty = CalculateUnsanitaryPenalty(patient, Transform(patient).Coordinates);
        
        // Reset to current cleanliness level
        unsanitary.Penalty = currentPenalty;
        unsanitary.PenaltyApplied = false; // Allow it to be updated again
        Dirty(patient, unsanitary);
    }
}

