using Content.Server.Hands.Systems;
using Content.Server.Fluids.EntitySystems;
using Content.Shared._Funkystation.Fishing;
using Content.Shared.Chemistry.Components;
using Content.Shared.Chemistry.Components.SolutionManager;
using Content.Shared.Chemistry.EntitySystems;
using Content.Shared.FixedPoint;
using Content.Shared.Fluids.Components;
using Content.Shared.Interaction;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests.Tests._Funkystation.Fishing;

[TestFixture]
[TestOf(typeof(SharedFishingSystem))]
public sealed class FishingInteractionTest
{
    private static readonly EntProtoId FishingRodProto = new("FishingRod");
    private static readonly EntProtoId FishingTestUserProto = new("AdminObserver");
    [Test]
    public async Task DeepPuddleSpawnsSilhouetteAnchor()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var testMap = await pair.CreateTestMap();
        var spill = server.System<PuddleSystem>();
        var entMan = server.ResolveDependency<IEntityManager>();

        EntityUid puddle = default!;

        await server.WaitPost(() =>
        {
            var solution = new Solution("Water", FixedPoint2.New(20));
            var tile = testMap.Tile;
            var gridUid = tile.GridUid;
            var (x, y) = tile.GridIndices;
            var coordinates = new EntityCoordinates(gridUid, x, y);
            Assert.That(spill.TrySpillAt(coordinates, solution, out puddle, sound: false), Is.True);
        });

        await pair.RunTicksSync(10);

        await server.WaitAssertion(() =>
        {
            Assert.That(entMan.TryGetComponent<PuddleFishingSilhouetteAnchorComponent>(puddle, out var anchor),
                Is.True);
            Assert.That(anchor.Silhouette, Is.Not.Null);
            Assert.That(anchor.Silhouette!.Value.IsValid(), Is.True);
            Assert.That(entMan.HasComponent<PuddleFishingSilhouetteComponent>(anchor.Silhouette.Value), Is.True);
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task ShallowPuddleHasNoSilhouetteAnchor()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var testMap = await pair.CreateTestMap();
        var spill = server.System<PuddleSystem>();
        var entMan = server.ResolveDependency<IEntityManager>();

        EntityUid puddle = default!;

        await server.WaitPost(() =>
        {
            var solution = new Solution("Water", FixedPoint2.New(9));
            var tile = testMap.Tile;
            var gridUid = tile.GridUid;
            var (x, y) = tile.GridIndices;
            var coordinates = new EntityCoordinates(gridUid, x, y);
            Assert.That(spill.TrySpillAt(coordinates, solution, out puddle, sound: false), Is.True);
        });

        await pair.RunTicksSync(10);

        await server.WaitAssertion(() =>
        {
            Assert.That(entMan.HasComponent<PuddleFishingSilhouetteAnchorComponent>(puddle), Is.False);
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task FishingDrainsFiveUnitsAndSpawnsLootFromTable()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var testMap = await pair.CreateTestMap();

        var spill = server.System<PuddleSystem>();
        var hands = server.System<HandsSystem>();
        var interact = server.System<SharedInteractionSystem>();
        var entMan = server.ResolveDependency<IEntityManager>();
        var solSys = server.System<SharedSolutionContainerSystem>();
        var protoMan = server.ResolveDependency<IPrototypeManager>();
        var xformSys = server.System<SharedTransformSystem>();
        EntityUid puddle = default!;
        EntityUid player = default!;
        EntityUid rod = default!;
        FixedPoint2 volumeBefore = default;

        await server.WaitPost(() =>
        {
            var solution = new Solution("Water", FixedPoint2.New(20));
            var tile = testMap.Tile;
            var gridUid = tile.GridUid;
            var (x, y) = tile.GridIndices;
            var coordinates = new EntityCoordinates(gridUid, x, y);
            Assert.That(spill.TrySpillAt(coordinates, solution, out puddle, sound: false), Is.True);

            player = entMan.SpawnEntity(FishingTestUserProto, testMap.GridCoords);
            var playerCoords = entMan.GetComponent<TransformComponent>(player).Coordinates;
            rod = entMan.SpawnEntity(FishingRodProto, playerCoords);
            Assert.That(hands.TryPickupAnyHand(player, rod, checkActionBlocker: false), Is.True);

            var puddleCoords = entMan.GetComponent<TransformComponent>(puddle).Coordinates;
            xformSys.SetCoordinates(player, puddleCoords);

            var puddleComp = entMan.GetComponent<PuddleComponent>(puddle);
            var scm = entMan.GetComponent<SolutionContainerManagerComponent>(puddle);
            Assert.That(solSys.TryGetSolution((puddle, scm), puddleComp.SolutionName, out _, out var sol), Is.True);
            volumeBefore = sol!.Volume;

            Assert.That(
                interact.InteractUsing(player, rod, puddle, puddleCoords, checkCanInteract: false,
                    checkCanUse: false), Is.True);
        });

        // AdminObserver has InstantDoAfters; ticks let split/spawn settle.
        await pair.RunTicksSync(15);

        await server.WaitAssertion(() =>
        {
            var puddleComp = entMan.GetComponent<PuddleComponent>(puddle);
            var scm = entMan.GetComponent<SolutionContainerManagerComponent>(puddle);
            Assert.That(solSys.TryGetSolution((puddle, scm), puddleComp.SolutionName, out _, out var sol), Is.True);
            Assert.That(sol!.Volume, Is.EqualTo(volumeBefore - FixedPoint2.New(5)));

            var puddleCoords = entMan.GetComponent<TransformComponent>(puddle).Coordinates;
            EntityUid? silhouetteUid = null;
            if (entMan.TryGetComponent<PuddleFishingSilhouetteAnchorComponent>(puddle, out var anchor)
                && anchor.Silhouette is { } silo)
                silhouetteUid = silo;

            var foundLoot = false;
            var query = entMan.AllEntityQueryEnumerator<MetaDataComponent>();
            while (query.MoveNext(out var uid, out var meta))
            {
                if (uid == player || uid == rod || uid == puddle || uid == silhouetteUid)
                    continue;

                if (meta.EntityPrototype is not { } proto)
                    continue;

                if (proto.ID == "FishingPuddleSilhouette")
                    continue;

                if (entMan.TryGetComponent<TransformComponent>(uid, out var xform)
                    && xform.Coordinates.TryDistance(entMan, puddleCoords, out var dist)
                    && dist < 1.5f)
                {
                    foundLoot = true;
                    break;
                }
            }

            Assert.That(foundLoot, Is.True, "Expected fished loot near the puddle.");

            entMan.DeleteEntity(player);
        });

        await pair.CleanReturnAsync();
    }
}
