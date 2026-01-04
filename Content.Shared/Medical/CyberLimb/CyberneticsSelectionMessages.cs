// SPDX-FileCopyrightText: 2025 terkala <appleorange64@gmail.com>
//
// SPDX-License-Identifier: MIT

using Robust.Shared.Serialization;

namespace Content.Shared.Medical.CyberLimb;

[Serializable, NetSerializable]
public enum CyberneticsSelectionUiKey : byte
{
    Key
}

/// <summary>
/// Message sent from client to server when user selects a cybernetic from the selection menu.
/// </summary>
[Serializable, NetSerializable]
public sealed class CyberneticsSelectionMessage : BoundUserInterfaceMessage
{
    public NetEntity Cybernetic;

    public CyberneticsSelectionMessage(NetEntity cybernetic)
    {
        Cybernetic = cybernetic;
    }
}

/// <summary>
/// Message sent from server to client to update the available cybernetics list.
/// </summary>
[Serializable, NetSerializable]
public sealed class CyberneticsSelectionState : BoundUserInterfaceState
{
    public List<CyberneticData> AvailableCybernetics;

    public CyberneticsSelectionState(List<CyberneticData> availableCybernetics)
    {
        AvailableCybernetics = availableCybernetics;
    }
}

/// <summary>
/// Data about a cybernetic available for selection.
/// </summary>
[Serializable, NetSerializable]
public sealed class CyberneticData
{
    public NetEntity Cybernetic;
    public string CyberneticName;
    public bool IsPanelOpen;

    public CyberneticData(NetEntity cybernetic, string cyberneticName, bool isPanelOpen)
    {
        Cybernetic = cybernetic;
        CyberneticName = cyberneticName;
        IsPanelOpen = isPanelOpen;
    }
}
