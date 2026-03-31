namespace Content.Shared._Funkystation.Fishing;

/// <summary>
/// Server-side tracking of the silhouette child for a puddle so it can be removed when the puddle is too shallow.
/// </summary>
[RegisterComponent]
public sealed partial class PuddleFishingSilhouetteAnchorComponent : Component
{
    public EntityUid? Silhouette;
}
