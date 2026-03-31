using Content.Shared._Funkystation.Fishing;
using Robust.Shared.GameObjects;

namespace Content.Server._Funkystation.Fishing;

/// <summary>
/// Fishing silhouettes are not parented to puddles; clean them up when the puddle entity goes away.
/// </summary>
public sealed class FishingSilhouetteCleanupSystem : EntitySystem
{
    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<PuddleFishingSilhouetteAnchorComponent, EntityTerminatingEvent>(OnTerminating);
    }

    private void OnTerminating(EntityUid uid, PuddleFishingSilhouetteAnchorComponent comp, ref EntityTerminatingEvent args)
    {
        if (comp.Silhouette is { } silo && Exists(silo))
            QueueDel(silo);
    }
}
