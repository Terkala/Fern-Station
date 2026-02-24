using Robust.Shared.GameStates;

namespace Content.Shared.Pool.Components;

/// <summary>
/// Marker component for tables that can form pool clusters (TableCarpet, TableFancy*, etc.).
/// Used to trigger pool cluster setup on MapInit.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class GamingTableComponent : Component
{
}
