using Content.Shared._Funkystation.Fishing;
using Robust.Client.GameObjects;
using Robust.Shared.GameObjects;
using Robust.Shared.Maths;
using Robust.Shared.Prototypes;

namespace Content.Client._Funkystation.Fishing;

public sealed class FishingSilhouetteVisualizerSystem : EntitySystem
{
    [Dependency] private readonly IPrototypeManager _proto = default!;
    [Dependency] private readonly IComponentFactory _factory = default!;
    [Dependency] private readonly SpriteSystem _sprite = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<PuddleFishingSilhouetteComponent, ComponentInit>(OnInit);
        SubscribeLocalEvent<PuddleFishingSilhouetteComponent, AfterAutoHandleStateEvent>(OnStateHandled);
    }

    private void OnInit(Entity<PuddleFishingSilhouetteComponent> ent, ref ComponentInit args)
    {
        Apply(ent);
    }

    private void OnStateHandled(Entity<PuddleFishingSilhouetteComponent> ent, ref AfterAutoHandleStateEvent args)
    {
        Apply(ent);
    }

    private void Apply(Entity<PuddleFishingSilhouetteComponent> ent)
    {
        if (!TryComp<SpriteComponent>(ent, out var sprite))
            return;

        if (!_proto.TryIndex(ent.Comp.SourceItem, out var itemProto)
            || !itemProto.TryGetComponent<SpriteComponent>(out var template, _factory))
        {
            sprite.Visible = false;
            return;
        }

        foreach (var layer in template.AllLayers)
        {
            if (layer.Rsi is not { } rsi)
                continue;

            var state = layer.RsiState;
            _sprite.LayerSetRsi((ent, sprite), 0, rsi.Path, state);
            _sprite.LayerSetColor((ent, sprite), 0, Color.White);
            sprite.LayerSetShader(0, "FishingSilhouetteOutline");
            sprite.Visible = true;
            return;
        }

        sprite.Visible = false;
    }
}
