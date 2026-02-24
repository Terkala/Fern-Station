using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Content.Server.Pool.Components;
using Content.Shared.Pool.Components;
using Content.Shared.Physics;
using Content.Shared.Tag;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Physics;
using Robust.Shared.Physics.Collision.Shapes;
using Robust.Shared.Physics.Components;
using Robust.Shared.Physics.Systems;

namespace Content.Server.Pool;

public sealed class PoolClusterSetupSystem : EntitySystem
{
    [Dependency] private readonly SharedMapSystem _map = default!;
    [Dependency] private readonly FixtureSystem _fixtures = default!;
    [Dependency] private readonly SharedPhysicsSystem _physics = default!;
    [Dependency] private readonly SharedTransformSystem _xform = default!;
    [Dependency] private readonly TagSystem _tags = default!;

    private const float PocketRadius = 0.07f;
    private const float PocketInset = 0.05f;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<PoolClusterMemberComponent, ComponentShutdown>(OnTableDeleted);
    }

    private void OnTableDeleted(Entity<PoolClusterMemberComponent> ent, ref ComponentShutdown args)
    {
        if (ent.Comp.Cluster is not { } clusterUid || !Exists(clusterUid))
            return;

        if (!TryComp(clusterUid, out PoolClusterComponent? cluster))
            return;

        cluster.MemberTables.Remove(ent.Owner);

        if (ent.Comp.Tile is { } tile)
            cluster.MemberTiles.Remove(tile);

        if (cluster.MemberTables.Count == 0)
        {
            QueueDel(clusterUid);
            return;
        }

        RebuildCluster(clusterUid, cluster);
    }

    /// <summary>
    /// Creates or updates the pool cluster for a gaming table. Called when user racks balls with pool cue.
    /// </summary>
    public void SetupPoolForTable(EntityUid tableUid)
    {
        if (!TryComp(tableUid, out TransformComponent? xform) || xform.GridUid is not { } gridUid)
            return;

        if (!TryComp(gridUid, out MapGridComponent? grid))
            return;

        var (memberTables, memberTiles) = FloodFillGamingTables(tableUid, gridUid, grid);
        if (memberTiles.Count == 0)
            return;

        EntityUid? existingCluster = null;
        foreach (var member in memberTables)
        {
            if (TryComp(member, out PoolClusterMemberComponent? memberComp) && memberComp.Cluster is { } c && Exists(c))
            {
                existingCluster = c;
                break;
            }
        }

        if (existingCluster is { } clusterUid && TryComp(clusterUid, out PoolClusterComponent? cluster))
        {
            cluster.MemberTables = memberTables;
            cluster.MemberTiles = memberTiles;
            RebuildCluster(clusterUid, cluster);

            foreach (var member in memberTables)
            {
                var comp = EnsureComp<PoolClusterMemberComponent>(member);
                comp.Cluster = clusterUid;
                if (TryComp(member, out TransformComponent? tx) && tx.GridUid is { } gid && TryComp(gid, out MapGridComponent? g))
                    comp.Tile = _map.TileIndicesFor(gid, g, tx.Coordinates);
                Dirty(member, comp);
            }
            return;
        }

        var centroid = ComputeCentroid(memberTiles, gridUid, grid);
        var clusterEntity = Spawn("PoolClusterEntity", new EntityCoordinates(gridUid, centroid));

        var poolCluster = EnsureComp<PoolClusterComponent>(clusterEntity);
        poolCluster.MemberTables = memberTables;
        poolCluster.MemberTiles = memberTiles;

        var (boundaryVertices, pockets) = ComputeBoundaryAndPockets(memberTiles, centroid, gridUid, grid);
        poolCluster.BoundaryVertices = boundaryVertices;
        poolCluster.Pockets = pockets;

        AddClusterFixtures(clusterEntity, boundaryVertices, pockets, centroid);

        foreach (var member in memberTables)
        {
            var comp = EnsureComp<PoolClusterMemberComponent>(member);
            comp.Cluster = clusterEntity;
            if (TryComp(member, out TransformComponent? tx) && tx.GridUid is { } gid && TryComp(gid, out MapGridComponent? g))
                comp.Tile = _map.TileIndicesFor(gid, g, tx.Coordinates);
            Dirty(member, comp);
        }
    }

    private (HashSet<EntityUid> Tables, HashSet<Vector2i> Tiles) FloodFillGamingTables(EntityUid start, EntityUid gridUid, MapGridComponent grid)
    {
        var tables = new HashSet<EntityUid>();
        var tiles = new HashSet<Vector2i>();
        var queue = new Queue<Vector2i>();

        var startTile = _map.TileIndicesFor(gridUid, grid, Transform(start).Coordinates);
        queue.Enqueue(startTile);

        while (queue.Count > 0)
        {
            var tile = queue.Dequeue();
            if (tiles.Contains(tile))
                continue;

            var enumerator = _map.GetAnchoredEntitiesEnumerator(gridUid, grid, tile);
            var hasGamingTable = false;
            while (enumerator.MoveNext(out var ent))
            {
                if (!_tags.HasTag(ent.Value, "GamingTable"))
                    continue;

                tables.Add(ent.Value);
                tiles.Add(tile);
                hasGamingTable = true;

                foreach (var offset in new[] { (0, 1), (0, -1), (1, 0), (-1, 0) })
                {
                    var neighbor = tile + new Vector2i(offset.Item1, offset.Item2);
                    if (!tiles.Contains(neighbor))
                        queue.Enqueue(neighbor);
                }
                break;
            }
        }

        return (tables, tiles);
    }

    private Vector2 ComputeCentroid(HashSet<Vector2i> tiles, EntityUid gridUid, MapGridComponent grid)
    {
        var sum = Vector2.Zero;
        foreach (var tile in tiles)
        {
            sum += _map.TileCenterToVector(gridUid, grid, tile);
        }
        return sum / tiles.Count;
    }

    private (List<Vector2> Boundary, List<(Vector2 Pos, float Radius)> Pockets) ComputeBoundaryAndPockets(
        HashSet<Vector2i> tiles, Vector2 centroid, EntityUid gridUid, MapGridComponent grid)
    {
        var edges = new Dictionary<(Vector2 v1, Vector2 v2), bool>();

        foreach (var tile in tiles)
        {
            var (i, j) = (tile.X, tile.Y);
            var v00 = _map.TileToVector((gridUid, grid), tile);
            var v10 = _map.TileToVector((gridUid, grid), new Vector2i(i + 1, j));
            var v11 = _map.TileToVector((gridUid, grid), new Vector2i(i + 1, j + 1));
            var v01 = _map.TileToVector((gridUid, grid), new Vector2i(i, j + 1));

            if (!tiles.Contains(new Vector2i(i, j - 1)))
                AddEdge(edges, v00, v10);
            if (!tiles.Contains(new Vector2i(i + 1, j)))
                AddEdge(edges, v10, v11);
            if (!tiles.Contains(new Vector2i(i, j + 1)))
                AddEdge(edges, v11, v01);
            if (!tiles.Contains(new Vector2i(i - 1, j)))
                AddEdge(edges, v01, v00);
        }

        var adjacency = new Dictionary<Vector2, List<Vector2>>();
        foreach (var ((v1, v2), _) in edges)
        {
            if (!adjacency.ContainsKey(v1))
                adjacency[v1] = new List<Vector2>();
            adjacency[v1].Add(v2);
        }

        var boundary = TraceBoundary(adjacency);
        if (boundary.Count < 3)
            return (new List<Vector2>(), new List<(Vector2, float)>());

        var pockets = new List<(Vector2, float)>();
        for (var i = 0; i < boundary.Count; i++)
        {
            var prev = boundary[(i - 1 + boundary.Count) % boundary.Count];
            var curr = boundary[i];
            var next = boundary[(i + 1) % boundary.Count];

            var toPrev = Vector2.Normalize(prev - curr);
            var toNext = Vector2.Normalize(next - curr);
            var inward = Vector2.Normalize(toPrev + toNext);
            if (inward.LengthSquared() < 0.001f)
                inward = new Vector2(-toPrev.Y, toPrev.X);

            var pocketPos = curr + inward * PocketInset;
            pockets.Add((pocketPos, PocketRadius));
        }

        var localBoundary = boundary.Select(v => v - centroid).ToList();
        return (localBoundary, pockets);
    }

    private static void AddEdge(Dictionary<(Vector2, Vector2), bool> edges, Vector2 a, Vector2 b)
    {
        var key = a.LengthSquared() < b.LengthSquared() ? (a, b) : (b, a);
        edges[key] = true;
    }

    private static List<Vector2> TraceBoundary(Dictionary<Vector2, List<Vector2>> adjacency)
    {
        var result = new List<Vector2>();
        if (adjacency.Count == 0)
            return result;

        var start = adjacency.Keys.First();
        var current = start;
        var prev = start;
        var first = true;

        do
        {
            if (!adjacency.TryGetValue(current, out var neighbors) || neighbors.Count == 0)
                break;

            result.Add(current);
            var next = neighbors[0] == prev && neighbors.Count > 1 ? neighbors[1] : neighbors[0];
            prev = current;
            current = next;
            first = false;
        } while (current != start && result.Count < adjacency.Count * 2);

        return result;
    }

    private void AddClusterFixtures(EntityUid clusterUid, List<Vector2> boundaryVertices, List<(Vector2 Pos, float Radius)> pockets, Vector2 centroid)
    {
        EnsureComp<FixturesComponent>(clusterUid);
        var body = EnsureComp<PhysicsComponent>(clusterUid);
        _physics.SetBodyType(clusterUid, BodyType.Static, body: body);
        _physics.SetCanCollide(clusterUid, true, body: body);

        if (boundaryVertices.Count >= 3)
        {
            var chain = new ChainShape();
            var verts = boundaryVertices.Select(v => v).ToArray();
            if (verts.Length >= 3)
            {
                chain.CreateLoop(verts);
                _fixtures.TryCreateFixture(clusterUid, chain, "pool_boundary",
                    density: 0, hard: true,
                    (int) CollisionGroup.PoolTableBoundaryLayer, (int) CollisionGroup.PoolBallMask,
                    manager: Comp<FixturesComponent>(clusterUid), body: body);
            }
        }

        for (var i = 0; i < pockets.Count; i++)
        {
            var (pos, radius) = pockets[i];
            var circle = new PhysShapeCircle(radius, pos - centroid);
            _fixtures.TryCreateFixture(clusterUid, circle, $"pool_pocket_{i}",
                density: 0, hard: false,
                (int) CollisionGroup.PoolPocketLayer, (int) CollisionGroup.PoolBallMask,
                manager: Comp<FixturesComponent>(clusterUid), body: body);
        }
    }

    private void RebuildCluster(EntityUid clusterUid, PoolClusterComponent cluster)
    {
        if (cluster.MemberTiles.Count == 0)
        {
            QueueDel(clusterUid);
            return;
        }

        if (cluster.MemberTables.Count == 0 || !TryComp(cluster.MemberTables.First(), out TransformComponent? xform) || xform.GridUid is not { } gridUid)
            return;

        if (!TryComp(gridUid, out MapGridComponent? grid))
            return;

        var centroid = ComputeCentroid(cluster.MemberTiles, gridUid, grid);
        var (boundaryVertices, pockets) = ComputeBoundaryAndPockets(cluster.MemberTiles, centroid, gridUid, grid);
        cluster.BoundaryVertices = boundaryVertices;
        cluster.Pockets = pockets;

        if (TryComp<FixturesComponent>(clusterUid, out var fixtures))
        {
            foreach (var id in fixtures.Fixtures.Keys.ToList())
            {
                if (id.StartsWith("pool_"))
                    _fixtures.DestroyFixture(clusterUid, id, manager: fixtures);
            }
        }

        _xform.SetLocalPosition(clusterUid, centroid);
        AddClusterFixtures(clusterUid, boundaryVertices, pockets, centroid);
    }
}
