using System.Numerics;
using Content.Server.Pool.Components;
using Content.Shared.Pool.Components;
using Robust.Shared.Physics.Components;
using Robust.Shared.Physics.Systems;

namespace Content.Server.Pool;

/// <summary>
/// Clamps pool ball velocity to prevent tunneling.
/// </summary>
public sealed class PoolBallVelocitySystem : EntitySystem
{
    [Dependency] private readonly SharedPhysicsSystem _physics = default!;

    private const float DefaultMaxBallSpeed = 2f;

    public override void Initialize()
    {
        base.Initialize();
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var query = EntityQueryEnumerator<PoolBallComponent, PhysicsComponent>();
        while (query.MoveNext(out var uid, out var ball, out var physics))
        {
            var speed = physics.LinearVelocity.Length();
            if (speed <= 0f)
                continue;

            var maxSpeed = DefaultMaxBallSpeed;
            if (ball.Cluster is { } clusterUid && TryComp(clusterUid, out PoolClusterComponent? cluster))
                maxSpeed = cluster.MaxBallSpeed;

            if (speed <= maxSpeed)
                continue;

            var scaled = physics.LinearVelocity * (maxSpeed / speed);
            _physics.SetLinearVelocity(uid, scaled, body: physics);
        }
    }
}
