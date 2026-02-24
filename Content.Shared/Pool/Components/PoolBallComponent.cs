using Robust.Shared.GameStates;

namespace Content.Shared.Pool.Components;

/// <summary>
/// Marks an entity as a pool ball. Used for velocity clamping and pocket detection.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class PoolBallComponent : Component
{
    /// <summary>
    /// The pool cluster this ball belongs to. Set when racked. Used for MaxBallSpeed.
    /// </summary>
    [ViewVariables]
    public EntityUid? Cluster;
}
