// SPDX-FileCopyrightText: 2025 terkala <appleorange64@gmail.com>
//
// SPDX-License-Identifier: MIT

using Robust.Shared.Serialization;

namespace Content.Shared.Medical.CyberLimb;

[Serializable, NetSerializable]
public enum CyberArmRadialMenuUiKey : byte
{
    Key
}

/// <summary>
/// Message sent from client to server when user selects an item from the radial menu.
/// </summary>
[Serializable, NetSerializable]
public sealed class CyberArmSelectItemMessage : BoundUserInterfaceMessage
{
    public NetEntity Item;

    public CyberArmSelectItemMessage(NetEntity item)
    {
        Item = item;
    }
}

/// <summary>
/// Message sent from client to server when user selects "open hand" option.
/// </summary>
[Serializable, NetSerializable]
public sealed class CyberArmOpenHandMessage : BoundUserInterfaceMessage
{
}

/// <summary>
/// Message sent from server to client to update the available items list.
/// </summary>
[Serializable, NetSerializable]
public sealed class CyberArmRadialMenuState : BoundUserInterfaceState
{
    public List<CyberArmItemData> AvailableItems;
    public NetEntity? ActiveItem;

    public CyberArmRadialMenuState(List<CyberArmItemData> availableItems, NetEntity? activeItem)
    {
        AvailableItems = availableItems;
        ActiveItem = activeItem;
    }
}

/// <summary>
/// Data about an item available in the cyber arm storage.
/// </summary>
[Serializable, NetSerializable]
public sealed class CyberArmItemData
{
    public NetEntity Item;
    public string ItemName;
    public string? SpritePath;

    public CyberArmItemData(NetEntity item, string itemName, string? spritePath)
    {
        Item = item;
        ItemName = itemName;
        SpritePath = spritePath;
    }
}
