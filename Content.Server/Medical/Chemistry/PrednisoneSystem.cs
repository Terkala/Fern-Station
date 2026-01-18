// SPDX-FileCopyrightText: 2025 terkala <appleorange64@gmail.com>
//
// SPDX-License-Identifier: MIT

using Content.Server.Body.Components;
using Content.Server.Body.Systems;
using Content.Shared.Chemistry.Components;
using Content.Shared.Chemistry.EntitySystems;
using Content.Shared.Chemistry.Reagent;
using Content.Shared.FixedPoint;
using Content.Shared.Medical.Chemistry;
using Content.Shared.Medical.Surgery.Integrity;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Server.Medical.Chemistry;

/// <summary>
/// Server-side immunosuppressant system that handles reagent metabolism, duration tracking, and overdose.
/// </summary>
public sealed class PrednisoneSystem : SharedPrednisoneSystem
{
    [Dependency] private readonly IPrototypeManager _prototypes = default!;
    [Dependency] private readonly SharedIntegritySystem _integrity = default!;
    [Dependency] private readonly SharedSolutionContainerSystem _solutionContainerSystem = default!;
    [Dependency] private readonly IGameTiming _gameTiming = default!;

    private const string PrednisoneReagentId = "Immunosuppressant";
    private static readonly FixedPoint2 OverdoseThreshold = FixedPoint2.New(20);

    public override void Initialize()
    {
        base.Initialize();
        
        // Note: ComponentStartup and ComponentShutdown subscriptions are handled by base class (SharedPrednisoneSystem)
        // We override the handlers instead
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var currentTime = _gameTiming.CurTime;

        // Update all immunosuppressant components and recalculate total temporary integrity
        var entityQuery = EntityQueryEnumerator<PrednisoneComponent>();
        var integrityQuery = EntityQueryEnumerator<IntegrityComponent>();
        
        // First, update components and remove expired ones
        while (entityQuery.MoveNext(out var uid, out var prednisone))
        {
            // Check if component has expired (Duration is expiration time in seconds)
            if (prednisone.Duration > 0 && currentTime.TotalSeconds >= prednisone.Duration)
            {
                RemComp<PrednisoneComponent>(uid);
                continue;
            }

            // Check if still has immunosuppressant in bloodstream
            if (!TryComp<BloodstreamComponent>(uid, out var bloodstream) ||
                !_solutionContainerSystem.ResolveSolution(uid, bloodstream.ChemicalSolutionName, ref bloodstream.ChemicalSolution, out var chemicals))
            {
                // No chemical solution, remove component
                RemComp<PrednisoneComponent>(uid);
                continue;
            }

            if (!chemicals.TryGetReagentQuantity(new ReagentId(PrednisoneReagentId, null), out var quantity) || quantity < FixedPoint2.New(1))
            {
                // Not enough immunosuppressant (less than 1 unit), remove component
                RemComp<PrednisoneComponent>(uid);
                continue;
            }

            // Check for overdose
            if (quantity >= OverdoseThreshold)
            {
                var ev = new PrednisoneOverdoseEvent(uid);
                RaiseLocalEvent(uid, ref ev);
            }
        }

        // Recalculate total temporary integrity for all entities with integrity components
        while (integrityQuery.MoveNext(out var uid, out var integrity))
        {
            // Get integrity bonus from active immunosuppressant component (if any)
            // The effect already handles taking the maximum bonus when multiple doses are taken
            var bonus = FixedPoint2.Zero;
            if (TryComp<PrednisoneComponent>(uid, out var prednisone))
            {
                bonus = prednisone.IntegrityBonus;
            }

            // Update temporary integrity bonus if it changed
            if (integrity.TemporaryIntegrityBonus != bonus)
            {
                integrity.TemporaryIntegrityBonus = bonus;
                Dirty(uid, integrity);
                _integrity.RecalculateTargetBioRejection(uid, integrity);
            }
        }
    }

    protected override void OnPrednisoneStartup(EntityUid uid, PrednisoneComponent component, ComponentStartup args)
    {
        // Convert duration from seconds remaining to expiration time
        if (component.Duration > 0)
        {
            component.Duration = (float)_gameTiming.CurTime.TotalSeconds + component.Duration;
        }
        
        // Check for overdose when component is added
        CheckOverdose(uid);
    }

    protected override void OnPrednisoneShutdown(EntityUid uid, PrednisoneComponent component, ComponentShutdown args)
    {
        // Component removed, integrity bonus already handled by base system
    }

    /// <summary>
    /// Checks if entity has immunosuppressant overdose and applies effects.
    /// </summary>
    public void CheckOverdose(EntityUid uid)
    {
        if (!TryComp<BloodstreamComponent>(uid, out var bloodstream))
            return;

        if (!_solutionContainerSystem.ResolveSolution(uid, bloodstream.ChemicalSolutionName, ref bloodstream.ChemicalSolution, out var chemicals))
            return;

        if (!chemicals.TryGetReagentQuantity(new ReagentId(PrednisoneReagentId, null), out var quantity))
            return;

        if (quantity >= OverdoseThreshold)
        {
            var ev = new PrednisoneOverdoseEvent(uid);
            RaiseLocalEvent(uid, ref ev);
        }
    }
}

/// <summary>
/// Event raised when immunosuppressant overdose threshold is reached.
/// </summary>
[ByRefEvent]
public record struct PrednisoneOverdoseEvent(EntityUid Uid);

