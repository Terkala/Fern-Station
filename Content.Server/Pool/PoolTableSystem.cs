using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Content.Server.Pool.Components;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Pool.Components;
using Content.Shared.Verbs;
using Robust.Shared.Containers;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;

namespace Content.Server.Pool;

/// <summary>
/// Handles pool table verbs: Rack balls and Retrieve pocketed balls.
/// </summary>
public sealed class PoolTableSystem : EntitySystem
{
    [Dependency] private readonly SharedHandsSystem _hands = default!;
    [Dependency] private readonly SharedContainerSystem _container = default!;
    [Dependency] private readonly SharedTransformSystem _xform = default!;
    [Dependency] private readonly IPrototypeManager _proto = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly PoolClusterSetupSystem _clusterSetup = default!;

    private const float BallSpacing = 0.11f;
    private const float BallRadius = 0.05f;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<GamingTableComponent, GetVerbsEvent<AlternativeVerb>>(OnGetVerbs);
    }

    private void OnGetVerbs(Entity<GamingTableComponent> ent, ref GetVerbsEvent<AlternativeVerb> args)
    {
        if (!args.CanInteract || !args.CanAccess)
            return;

        if (args.Using is not { } usedUid || !HasComp<PoolCueComponent>(usedUid))
            return;

        EntityUid clusterUid;
        PoolClusterComponent cluster;

        if (TryComp(ent, out PoolClusterMemberComponent? member) && member.Cluster is { } c && TryComp(c, out PoolClusterComponent? existingCluster))
        {
            clusterUid = c;
            cluster = existingCluster;
        }
        else
        {
            _clusterSetup.SetupPoolForTable(ent.Owner);
            if (!TryComp(ent, out PoolClusterMemberComponent? newMember) || newMember.Cluster is not { } newClusterUid)
                return;
            if (!TryComp(newClusterUid, out PoolClusterComponent? newCluster))
                return;
            clusterUid = newClusterUid;
            cluster = newCluster;
        }

        var centroid = _xform.GetWorldPosition(clusterUid);

        var rackVerb = new AlternativeVerb
        {
            Act = () => RackBalls(clusterUid, cluster, centroid),
            Text = "Rack balls",
            Priority = 1
        };
        args.Verbs.Add(rackVerb);

        var pocketContainer = _container.EnsureContainer<Container>(clusterUid, PoolClusterComponent.PocketedContainerId);
        if (pocketContainer.ContainedEntities.Count > 0)
        {
            var user = args.User;
            var retrieveVerb = new AlternativeVerb
            {
                Act = () => RetrieveBalls(clusterUid, cluster, centroid, user, pocketContainer),
                Text = "Retrieve pocketed balls",
                Priority = 2
            };
            args.Verbs.Add(retrieveVerb);
        }
    }

    private void RemoveExistingBalls(EntityUid clusterUid, PoolClusterComponent cluster)
    {
        var pocketContainer = _container.EnsureContainer<Container>(clusterUid, PoolClusterComponent.PocketedContainerId);
        foreach (var ball in pocketContainer.ContainedEntities.ToArray())
        {
            _container.Remove(ball, pocketContainer);
            QueueDel(ball);
        }

        var query = EntityQueryEnumerator<PoolBallComponent>();
        while (query.MoveNext(out var ballUid, out var ballComp))
        {
            if (ballComp.Cluster == clusterUid && !_container.IsEntityInContainer(ballUid))
                QueueDel(ballUid);
        }
    }

    private void RackBalls(EntityUid clusterUid, PoolClusterComponent cluster, Vector2 centroid)
    {
        var ballCount = cluster.Pockets.Count;
        if (ballCount <= 0)
            return;

        if (!_proto.TryIndex<EntityPrototype>("PoolBall", out _))
            return;

        RemoveExistingBalls(clusterUid, cluster);

        var positions = GetTriangleRackPositions(ballCount);
        var mapId = Transform(clusterUid).MapID;

        foreach (var offset in positions)
        {
            var pos = centroid + offset + _random.NextVector2(-0.02f, 0.02f);
            var coords = new EntityCoordinates(GetClusterMapEntity(clusterUid), pos);
            var ball = Spawn("PoolBall", coords);
            if (TryComp(ball, out PoolBallComponent? ballComp))
                ballComp.Cluster = clusterUid;
        }
    }

    private void RetrieveBalls(EntityUid clusterUid, PoolClusterComponent cluster, Vector2 centroid, EntityUid user, Container pocketContainer)
    {
        var first = true;
        var toRetrieve = pocketContainer.ContainedEntities.ToArray();
        foreach (var ball in toRetrieve)
        {
            if (!_container.Remove(ball, pocketContainer))
                continue;

            var pos = centroid + _random.NextVector2(-0.15f, 0.15f);
            var mapEntity = GetClusterMapEntity(clusterUid);
            var coords = new EntityCoordinates(mapEntity, pos);
            _xform.SetCoordinates(ball, coords);

            if (first && _hands.TryGetEmptyHand(user, out var hand))
            {
                if (_hands.TryPickup(user, ball, hand, checkActionBlocker: false))
                    first = false;
            }
        }
    }

    private static List<Vector2> GetTriangleRackPositions(int count)
    {
        var result = new List<Vector2>();
        var row = 0;
        var remaining = count;
        var idx = 0;

        while (remaining > 0)
        {
            row++;
            var inRow = Math.Min(row, remaining);
            remaining -= inRow;

            var rowOffset = (row - 1) * BallSpacing * 0.866f; // sqrt(3)/2
            var startX = -(inRow - 1) * BallSpacing * 0.5f;

            for (var i = 0; i < inRow; i++)
            {
                var x = startX + i * BallSpacing;
                result.Add(new Vector2(x, -rowOffset));
                idx++;
            }
        }

        return result;
    }

    private EntityUid GetClusterMapEntity(EntityUid clusterUid)
    {
        var xform = Transform(clusterUid);
        return xform.MapUid ?? clusterUid;
    }
}
