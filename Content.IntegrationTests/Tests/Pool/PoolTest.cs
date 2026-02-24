#nullable enable
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Content.Server.Pool.Components;
using Content.Shared.Hands.Components;
using Content.Shared.Pool.Components;
using Content.Shared.Verbs;
using NUnit.Framework;
using Robust.Shared.Containers;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Maths;
using Robust.Shared.Physics.Systems;

namespace Content.IntegrationTests.Tests.Pool;

[TestFixture]
[TestOf(typeof(PoolClusterComponent))]
[TestOf(typeof(PoolClusterMemberComponent))]
[TestOf(typeof(PoolBallComponent))]
public sealed class PoolTest
{
    [TestPrototypes]
    private const string Prototypes = @"
- type: entity
  id: PoolTestDummy
  name: pool test dummy
  components:
  - type: Body
    prototype: Human
  - type: MobState
  - type: MobThresholds
    thresholds:
      0: Alive
  - type: Damageable
    damageContainer: Biological
  - type: Physics
    bodyType: KinematicController
  - type: Fixtures
    fixtures:
      fix1:
        shape:
          !type:PhysShapeCircle
          radius: 0.35
  - type: Hands
  - type: ComplexInteraction
";

    [Test]
    public async Task GamingTableRackAndPocketTest()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;

        var testMap = await pair.CreateTestMap();

        EntityUid table = default!;
        EntityUid cue = default!;
        EntityUid player = default!;

        var entMan = server.ResolveDependency<IEntityManager>();
        var handsSystem = entMan.System<Content.Server.Hands.Systems.HandsSystem>();
        var containerSystem = entMan.System<SharedContainerSystem>();
        var xformSystem = entMan.System<SharedTransformSystem>();
        var physicsSystem = entMan.System<SharedPhysicsSystem>();

        await server.WaitAssertion(() =>
        {
            var mapSys = server.System<SharedMapSystem>();
            var tileDef = server.ResolveDependency<Robust.Shared.Map.ITileDefinitionManager>()["Plating"];
            for (var x = -1; x <= 1; x++)
            for (var y = -1; y <= 1; y++)
                mapSys.SetTile(testMap.Grid.Owner, testMap.Grid.Comp, new Vector2i(x, y), new Tile(tileDef.TileId));

            var grid = testMap.Grid;
            table = entMan.SpawnEntity("TableCarpet", new EntityCoordinates(grid.Owner, 0, 0));
            entMan.SpawnEntity("TableCarpet", new EntityCoordinates(grid.Owner, 1, 0));
            entMan.SpawnEntity("TableCarpet", new EntityCoordinates(grid.Owner, 0, 1));
            entMan.SpawnEntity("TableCarpet", new EntityCoordinates(grid.Owner, 1, 1));
            cue = entMan.SpawnEntity("PoolCue", testMap.GridCoords);
            player = entMan.SpawnEntity("PoolTestDummy", testMap.GridCoords);

            Assert.That(entMan.HasComponent<GamingTableComponent>(table));
            Assert.That(entMan.HasComponent<PoolCueComponent>(cue));

            Assert.That(handsSystem.TryPickup(player, cue));
        });

        await pair.RunTicksSync(5);

        await server.WaitAssertion(() =>
        {
            var hands = entMan.GetComponent<HandsComponent>(player);
            Assert.That(hands.ActiveHandEntity, Is.EqualTo(cue));

            var verbEvent = new GetVerbsEvent<AlternativeVerb>(
                player, table, cue, hands,
                canInteract: true, canComplexInteract: true, canAccess: true,
                new List<VerbCategory>());
            entMan.EventBus.RaiseLocalEvent(table, verbEvent);

            var rackVerb = verbEvent.Verbs.FirstOrDefault(v => v.Text == "Rack balls");
            Assert.That(rackVerb, Is.Not.Null, "Rack balls verb should appear when holding pool cue");
            rackVerb!.Act?.Invoke();
        });

        await pair.RunTicksSync(5);

        EntityUid clusterUid = default;
        await server.WaitAssertion(() =>
        {
            Assert.That(entMan.TryGetComponent(table, out PoolClusterMemberComponent? member));
            Assert.That(member!.Cluster, Is.Not.Null);
            clusterUid = member.Cluster!.Value;

            Assert.That(entMan.TryGetComponent(clusterUid, out PoolClusterComponent? cluster));
            Assert.That(cluster!.Pockets.Count, Is.GreaterThan(0));

            var ballQuery = entMan.EntityQuery<PoolBallComponent>().ToList();
            Assert.That(ballQuery.Count, Is.GreaterThan(0), "Balls should be spawned after racking");
        });

        EntityUid ballToStrike = default;
        await server.WaitAssertion(() =>
        {
            var found = false;
            var ballQuery = entMan.AllEntityQueryEnumerator<PoolBallComponent>();
            while (ballQuery.MoveNext(out var ballUid, out _))
            {
                if (!containerSystem.IsEntityInContainer(ballUid))
                {
                    ballToStrike = ballUid;
                    found = true;
                    break;
                }
            }
            Assert.That(found, "At least one ball should be on table");

            if (!entMan.TryGetComponent(clusterUid, out PoolClusterComponent? clusterComp) || clusterComp.Pockets.Count == 0)
                Assert.Fail("No pockets");

            var clusterXform = entMan.GetComponent<TransformComponent>(clusterUid);
            var gridUid = clusterXform.GridUid ?? clusterXform.ParentUid;
            var gridWorld = xformSystem.GetWorldPosition(gridUid);
            var pocketGridLocal = clusterComp.Pockets[0].WorldPos;
            var pocketWorldPos = gridWorld + pocketGridLocal;
            var ballPos = xformSystem.GetWorldPosition(ballToStrike);
            var toPocket = Vector2.Normalize(pocketWorldPos - ballPos);
            xformSystem.SetWorldPosition(ballToStrike, pocketWorldPos - toPocket * 0.05f);
            var impulse = toPocket * 2f;

            physicsSystem.ApplyLinearImpulse(ballToStrike, impulse);
        });

        for (var i = 0; i < 120; i++)
            await pair.RunTicksSync(1);

        await server.WaitAssertion(() =>
        {
            var pocketContainer = containerSystem.EnsureContainer<Container>(clusterUid, PoolClusterComponent.PocketedContainerId);
            Assert.That(pocketContainer.ContainedEntities.Count, Is.GreaterThan(0),
                "At least one ball should be pocketed after striking");
        });

        await pair.CleanReturnAsync();
    }
}
