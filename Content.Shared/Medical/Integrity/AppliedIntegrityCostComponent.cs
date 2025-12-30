// SPDX-FileCopyrightText: 2025 terkala <appleorange64@gmail.com>
//
// SPDX-License-Identifier: MIT

using Content.Shared.FixedPoint;
using Robust.Shared.GameStates;

namespace Content.Shared.Medical.Integrity;

/// <summary>
/// Component that tracks the actual integrity cost that was applied when an organ/limb/cybernetic was installed.
/// This is used to correctly remove integrity when the item is removed.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class AppliedIntegrityCostComponent : Component
{
    /// <summary>
    /// The actual integrity cost that was applied when this item was installed.
    /// This may be 0 for compatible donors, or modified by tool/equipment quality.
    /// </summary>
    [DataField, AutoNetworkedField]
    public FixedPoint2 AppliedCost = FixedPoint2.Zero;
}

