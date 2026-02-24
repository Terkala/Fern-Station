using System.Numerics;
using Content.Server.Pool.Components;
using Content.Shared.Interaction;
using Content.Shared.Pool.Components;
using Robust.Shared.Containers;
using Robust.Shared.Physics.Components;
using Robust.Shared.Physics.Systems;

namespace Content.Server.Pool;

/// <summary>
/// Handles striking pool balls with the pool cue.
/// </summary>
public sealed class PoolCueSystem : EntitySystem
{
    [Dependency] private readonly EntityLookupSystem _lookup = default!;
    [Dependency] private readonly SharedContainerSystem _container = default!;
    [Dependency] private readonly SharedPhysicsSystem _physics = default!;
    [Dependency] private readonly SharedTransformSystem _xform = default!;

    private const float StrikeRadius = 2f;
    private const float ImpulseMagnitude = 0.5f;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<PoolClusterMemberComponent, InteractUsingEvent>(OnInteractUsing);
    }

    private void OnInteractUsing(Entity<PoolClusterMemberComponent> ent, ref InteractUsingEvent args)
    {
        if (args.Handled)
            return;

        if (!HasComp<PoolCueComponent>(args.Used))
            return;

        if (ent.Comp.Cluster is not { } clusterUid || !TryComp(clusterUid, out PoolClusterComponent? cluster))
            return;

        var clickPos = _xform.ToMapCoordinates(args.ClickLocation);
        var balls = _lookup.GetEntitiesInRange<PoolBallComponent>(args.ClickLocation, StrikeRadius);

        EntityUid? closestBall = null;
        var closestDist = float.MaxValue;

        foreach (var ballEnt in balls)
        {
            if (ballEnt.Comp.Cluster != clusterUid)
                continue;

            if (_container.IsEntityInContainer(ballEnt.Owner))
                continue;

            var ballPos = _xform.GetMapCoordinates(ballEnt.Owner);
            var dist = (ballPos.Position - clickPos.Position).LengthSquared();
            if (dist < closestDist)
            {
                closestDist = dist;
                closestBall = ballEnt.Owner;
            }
        }

        if (closestBall is not { } targetBall || !TryComp(targetBall, out PhysicsComponent? body))
            return;

        var ballPos2 = _xform.GetWorldPosition(targetBall);
        var toClick = clickPos.Position - ballPos2;
        if (toClick.LengthSquared() < 0.0001f)
            return;

        var dir = Vector2.Normalize(toClick);
        var impulse = dir * ImpulseMagnitude;

        _physics.ApplyLinearImpulse(targetBall, impulse, body: body);
        args.Handled = true;
    }
}
