// SPDX-FileCopyrightText: 2025 terkala <appleorange64@gmail.com>
//
// SPDX-License-Identifier: MIT

using Robust.Shared.Serialization;

namespace Content.Shared.Medical.Autodoc;

[Serializable, NetSerializable]
public enum AutodocUIKey : byte
{
    Key
}

[Serializable, NetSerializable]
public sealed class AutodocBoundUserInterfaceState : BoundUserInterfaceState
{
    public AutodocMode Mode;
    public bool IsActive;
    public NetEntity? SelectedOrgan;
    public bool HasPatient;
    public bool HasOrganInSlot;
    public List<NetEntity> AvailableOrgans = new();

    public AutodocBoundUserInterfaceState(
        AutodocMode mode,
        bool isActive,
        NetEntity? selectedOrgan,
        bool hasPatient,
        bool hasOrganInSlot,
        List<NetEntity> availableOrgans)
    {
        Mode = mode;
        IsActive = isActive;
        SelectedOrgan = selectedOrgan;
        HasPatient = hasPatient;
        HasOrganInSlot = hasOrganInSlot;
        AvailableOrgans = availableOrgans;
    }
}

[Serializable, NetSerializable]
public sealed class AutodocSetModeMessage : BoundUserInterfaceMessage
{
    public AutodocMode Mode;

    public AutodocSetModeMessage(AutodocMode mode)
    {
        Mode = mode;
    }
}

[Serializable, NetSerializable]
public sealed class AutodocSelectOrganMessage : BoundUserInterfaceMessage
{
    public NetEntity Organ;

    public AutodocSelectOrganMessage(NetEntity organ)
    {
        Organ = organ;
    }
}

[Serializable, NetSerializable]
public sealed class AutodocActivateMessage : BoundUserInterfaceMessage
{
}

