// SPDX-FileCopyrightText: 2025 terkala <appleorange64@gmail.com>
//
// SPDX-License-Identifier: MIT

using Robust.Shared.GameStates;

namespace Content.Shared.Medical.CyberLimb;

/// <summary>
/// Component that tracks the currently active item in a cyber arm.
/// When the use key is pressed on an empty hand that's a cyber arm,
/// it cycles through items in the cyber arm's storage.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class CyberArmActiveItemComponent : Component
{
    /// <summary>
    /// The entity UID of the currently active item in the cyber arm storage.
    /// Null if no item is active.
    /// This can be either a direct item or an item inside a special module.
    /// </summary>
    [DataField, ViewVariables, AutoNetworkedField]
    public EntityUid? ActiveItem;

    /// <summary>
    /// The entity UID of the special module that contains the active item.
    /// Null if the active item is not inside a special module.
    /// </summary>
    [DataField, ViewVariables, AutoNetworkedField]
    public EntityUid? ActiveItemModule;

    /// <summary>
    /// The entity UID of the virtual item currently in the hand.
    /// This represents the active item visually.
    /// </summary>
    [DataField, ViewVariables, AutoNetworkedField]
    public EntityUid? VirtualItem;

    /// <summary>
    /// The last time the firearm handler checked this cyber arm's active firearm.
    /// Used to throttle firearm operations to every 0.5 seconds.
    /// </summary>
    [ViewVariables]
    public TimeSpan? LastFirearmCheckTime;
}

