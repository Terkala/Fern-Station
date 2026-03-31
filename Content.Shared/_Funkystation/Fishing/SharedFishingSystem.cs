using System.Linq;
using Content.Shared.Administration.Logs;
using Content.Shared.Chemistry.Components.SolutionManager;
using Content.Shared.Chemistry.EntitySystems;
using Content.Shared.Database;
using Content.Shared.DoAfter;
using Content.Shared.EntityTable;
using Content.Shared.FixedPoint;
using Content.Shared.Fluids.Components;
using Content.Shared.Interaction;
using Content.Shared.Popups;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Network;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;

namespace Content.Shared._Funkystation.Fishing;

public sealed class SharedFishingSystem : EntitySystem
{
    [Dependency] private readonly SharedDoAfterSystem _doAfter = default!;
    [Dependency] private readonly INetManager _net = default!;
    [Dependency] private readonly IPrototypeManager _proto = default!;
    [Dependency] private readonly EntityTableSystem _entityTable = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly SharedSolutionContainerSystem _solution = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly ISharedAdminLogManager _adminLog = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<FishingRodComponent, AfterInteractEvent>(OnAfterInteract);
        SubscribeLocalEvent<FishingRodComponent, FishingDoAfterEvent>(OnDoAfter);
    }

    private void OnAfterInteract(EntityUid uid, FishingRodComponent comp, AfterInteractEvent args)
    {
        if (args.Handled || !args.CanReach || args.Target is not { } target)
            return;

        if (!TryComp<PuddleComponent>(target, out var puddle)
            || !TryComp<SolutionContainerManagerComponent>(target, out var scm))
            return;

        if (!_solution.TryGetSolution((target, scm), puddle.SolutionName, out _, out var solution))
            return;

        if (solution.Volume < comp.MinimumPuddleVolume)
        {
            _popup.PopupPredicted(Loc.GetString("fishing-puddle-too-shallow"), args.User, args.User);
            args.Handled = true;
            return;
        }

        if (!_doAfter.TryStartDoAfter(new DoAfterArgs(EntityManager, args.User, comp.CastDuration,
                new FishingDoAfterEvent(), uid, target, uid)
            {
                BreakOnMove = true,
                BreakOnDamage = true,
                MovementThreshold = 0.5f,
                NeedHand = true,
                RequireCanInteract = false,
            }))
            return;

        _audio.PlayPredicted(comp.CastStartSound, args.User, args.User);
        args.Handled = true;
    }

    private void OnDoAfter(EntityUid uid, FishingRodComponent comp, DoAfterEvent args)
    {
        if (args.Cancelled || args.Handled || args.Args.Target is not { } target)
            return;

        if (!_net.IsServer)
            return;

        if (!TryComp<PuddleComponent>(target, out var puddle)
            || !TryComp<SolutionContainerManagerComponent>(target, out var scm))
            return;

        if (!_solution.TryGetSolution((target, scm), puddle.SolutionName, out var solEnt, out var solution)
            || solEnt is not { } sEnt)
            return;

        if (solution.Volume < comp.MinimumPuddleVolume)
            return;

        var drain = FixedPoint2.Min(comp.DrainPerCast, solution.Volume);
        if (drain <= FixedPoint2.Zero)
            return;

        _solution.SplitSolution(sEnt, drain);

        var lootTable = _proto.Index(comp.LootTable).Table;
        var picks = _entityTable.GetSpawns(lootTable).ToList();
        if (picks.Count == 0)
            return;

        var loot = _random.Pick(picks);
        var lootEnt = Spawn(loot, Transform(target).Coordinates);

        _audio.PlayPvs(comp.CastCompleteSound, target);
        _adminLog.Add(LogType.Action, LogImpact.Low,
            $"{ToPrettyString(args.User):player} fished {ToPrettyString(lootEnt):entity} from {ToPrettyString(target):target} using {ToPrettyString(uid):tool}");
        args.Handled = true;
    }
}
