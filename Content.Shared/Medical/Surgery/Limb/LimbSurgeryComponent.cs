// SPDX-FileCopyrightText: 2025 terkala <appleorange64@gmail.com>
//
// SPDX-License-Identifier: MIT

using Content.Shared.Medical.Limb.Targeting;
using Robust.Shared.GameStates;

namespace Content.Shared.Medical.Surgery.Limb;

/// <summary>
/// Component marking entities that can have limb surgery performed on them.
/// This component is completely original and not based on any _Shitmed implementations.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class LimbSurgeryComponent : Component
{
    /// <summary>
    /// The target body part for the current surgery operation.
    /// </summary>
    [ViewVariables]
    public TargetBodyPart? CurrentTarget = null;
}
