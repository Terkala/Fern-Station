using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared._Funkystation.Fishing;

/// <summary>
/// Visual-only child under a puddle that shows a dark outline of a random item.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState(true)]
public sealed partial class PuddleFishingSilhouetteComponent : Component
{
    [DataField, AutoNetworkedField]
    public EntProtoId SourceItem = default!;
}
