// SPDX-FileCopyrightText: 2025 terkala <appleorange64@gmail.com>
//
// SPDX-License-Identifier: MIT

using Robust.Shared.Serialization;

namespace Content.Shared.Arcade;

[Serializable, NetSerializable]
public sealed class VRPodBoundUserInterfaceState : BoundUserInterfaceState
{
    public readonly List<TutorialInfo> AvailableTutorials;
    public readonly bool IsLocked;
    public readonly bool IsPowered;
    public readonly bool HasBattery;
    public readonly bool PlayerInside;
    public readonly bool CanStartTutorial;

    public VRPodBoundUserInterfaceState(
        List<TutorialInfo> availableTutorials,
        bool isLocked,
        bool isPowered,
        bool hasBattery,
        bool playerInside,
        bool canStartTutorial)
    {
        AvailableTutorials = availableTutorials;
        IsLocked = isLocked;
        IsPowered = isPowered;
        HasBattery = hasBattery;
        PlayerInside = playerInside;
        CanStartTutorial = canStartTutorial;
    }
}

[Serializable, NetSerializable]
public sealed class TutorialInfo
{
    public readonly string Id;
    public readonly string Name;
    public readonly string Description;

    public TutorialInfo(string id, string name, string description)
    {
        Id = id;
        Name = name;
        Description = description;
    }
}

[Serializable, NetSerializable]
public sealed class VRPodSelectTutorialMessage : BoundUserInterfaceMessage
{
    public readonly string TutorialId;

    public VRPodSelectTutorialMessage(string tutorialId)
    {
        TutorialId = tutorialId;
    }
}

[Serializable, NetSerializable]
public sealed class VRPodStartTutorialMessage : BoundUserInterfaceMessage
{
}


