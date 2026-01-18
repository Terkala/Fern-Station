// SPDX-FileCopyrightText: 2025 terkala <appleorange64@gmail.com>
//
// SPDX-License-Identifier: MIT

using Robust.Shared.GameStates;

namespace Content.Shared.Medical.Limb.Targeting;

/// <summary>
/// Component that tracks the selected target body part and the status of each targetable limb.
/// This component is completely original and not based on any _Shitmed implementations.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class LimbTargetingComponent : Component
{
    /// <summary>
    /// The currently selected target body part for this entity.
    /// Defaults to Torso.
    /// </summary>
    [ViewVariables, AutoNetworkedField]
    public TargetBodyPart SelectedTarget = TargetBodyPart.Torso;

    /// <summary>
    /// The current integrity state of each targetable body part.
    /// </summary>
    [ViewVariables, AutoNetworkedField]
    public Dictionary<TargetBodyPart, LimbIntegrityState> LimbStatus = new()
    {
        { TargetBodyPart.Head, LimbIntegrityState.Healthy },
        { TargetBodyPart.Torso, LimbIntegrityState.Healthy },
        { TargetBodyPart.Arms, LimbIntegrityState.Healthy },
        { TargetBodyPart.Legs, LimbIntegrityState.Healthy }
    };

    /// <summary>
    /// Sound played when the entity changes their selected target.
    /// </summary>
    [DataField]
    public string? TargetChangeSound = "/Audio/Effects/toggleoncombat.ogg";
}
