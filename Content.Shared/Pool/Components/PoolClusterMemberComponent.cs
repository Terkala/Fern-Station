using System.Numerics;
using Robust.Shared.GameStates;

namespace Content.Shared.Pool.Components;

/// <summary>
/// Attached to each gaming table in a pool cluster. Links to the cluster entity.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class PoolClusterMemberComponent : Component
{
    /// <summary>
    /// The pool cluster entity this table belongs to.
    /// </summary>
    [ViewVariables, AutoNetworkedField]
    public EntityUid? Cluster;

    /// <summary>
    /// The grid tile this table occupies. Stored for cleanup when table is deleted.
    /// </summary>
    [ViewVariables]
    public Vector2i? Tile;
}
