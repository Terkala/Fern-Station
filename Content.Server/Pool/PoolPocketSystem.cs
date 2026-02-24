using Content.Server.Pool.Components;
using Content.Shared.Pool.Components;
using Robust.Shared.Containers;
using Robust.Shared.Physics.Events;

namespace Content.Server.Pool;

/// <summary>
/// Handles pool balls falling into pockets.
/// </summary>
public sealed class PoolPocketSystem : EntitySystem
{
    [Dependency] private readonly SharedContainerSystem _container = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<PoolClusterComponent, StartCollideEvent>(OnClusterCollide);
    }

    private void OnClusterCollide(Entity<PoolClusterComponent> ent, ref StartCollideEvent args)
    {
        if (!args.OurFixtureId.StartsWith("pool_pocket_"))
            return;

        if (!HasComp<PoolBallComponent>(args.OtherEntity))
            return;

        var ball = args.OtherEntity;
        var cluster = ent.Comp;

        var pocketContainer = _container.EnsureContainer<Container>(ent.Owner, PoolClusterComponent.PocketedContainerId);
        if (_container.Insert(ball, pocketContainer))
        {
            if (TryComp(ball, out PoolBallComponent? ballComp))
                ballComp.Cluster = ent.Owner;
        }
    }
}
