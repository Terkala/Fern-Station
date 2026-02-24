using System.Numerics;
using Robust.Shared.GameStates;

namespace Content.Server.Pool.Components;

/// <summary>
/// Attached to the pool cluster entity. Holds the shared pool state.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class PoolClusterComponent : Component
{
    /// <summary>
    /// Gaming tables in this cluster.
    /// </summary>
    [ViewVariables]
    public HashSet<EntityUid> MemberTables = new();

    /// <summary>
    /// Grid indices for boundary computation.
    /// </summary>
    [ViewVariables]
    public HashSet<Vector2i> MemberTiles = new();

    /// <summary>
    /// World-space polygon vertices (inner edge of play area).
    /// </summary>
    [ViewVariables]
    public List<Vector2> BoundaryVertices = new();

    /// <summary>
    /// Pocket positions (world position, radius).
    /// </summary>
    [ViewVariables]
    public List<(Vector2 WorldPos, float Radius)> Pockets = new();

    /// <summary>
    /// Ball speed cap in m/s to prevent tunneling.
    /// </summary>
    [DataField, ViewVariables(VVAccess.ReadWrite)]
    public float MaxBallSpeed = 2f;

    /// <summary>
    /// Balls that have been pocketed (stored in container).
    /// </summary>
    [ViewVariables]
    public List<EntityUid> PocketedBalls = new();

    /// <summary>
    /// Container ID for pocketed balls.
    /// </summary>
    public const string PocketedContainerId = "pool_pocketed";
}
