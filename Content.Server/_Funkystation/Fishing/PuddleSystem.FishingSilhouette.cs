using System.Linq;
using Content.Shared._Funkystation.Fishing;
using Content.Shared.Chemistry.Components;
using Content.Shared.EntityTable;
using Content.Shared.FixedPoint;
using Content.Shared.Fluids.Components;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;

namespace Content.Server.Fluids.EntitySystems;

public sealed partial class PuddleSystem
{
    [Dependency] private readonly EntityTableSystem _entityTable = default!;

    private static readonly FixedPoint2 MinFishingSilhouetteVol = FixedPoint2.New(10);

    private static readonly ProtoId<EntityTablePrototype> FishingSilhouetteTable = new("FishingLootTable");

    private void UpdateFishingSilhouette(Entity<PuddleComponent> puddle, Solution currentSolution)
    {
        var volume = currentSolution.Volume;

        if (volume < MinFishingSilhouetteVol)
        {
            if (TryComp<PuddleFishingSilhouetteAnchorComponent>(puddle.Owner, out var shallowAnchor))
            {
                if (shallowAnchor.Silhouette is { } silo && Exists(silo))
                    QueueDel(silo);
                RemComp<PuddleFishingSilhouetteAnchorComponent>(puddle.Owner);
            }
            return;
        }

        var anchor = EnsureComp<PuddleFishingSilhouetteAnchorComponent>(puddle.Owner);

        if (anchor.Silhouette is { } existing && Exists(existing))
            return;

        var lootTable = _prototypeManager.Index(FishingSilhouetteTable).Table;
        var picks = _entityTable.GetSpawns(lootTable).ToList();
        if (picks.Count == 0)
            return;

        var pick = _random.Pick(picks);
        // Spawn unparented at the puddle location to avoid transform init ordering with a child entity.
        var child = Spawn("FishingPuddleSilhouette", Transform(puddle.Owner).Coordinates);

        var silhouette = EnsureComp<PuddleFishingSilhouetteComponent>(child);
        silhouette.SourceItem = pick;
        Dirty(child, silhouette);

        anchor.Silhouette = child;
    }
}
