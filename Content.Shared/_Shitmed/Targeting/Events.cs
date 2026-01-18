// SPDX-FileCopyrightText: 2025 terkala <appleorange64@gmail.com>
//
// SPDX-License-Identifier: MIT

using Content.Shared._Shitmed.Targeting;
using Robust.Shared.Serialization;

namespace Content.Shared._Shitmed.Targeting.Events;

/// <summary>
/// Stub events for targeting - kept for compatibility with _Shitmed files.
/// The actual targeting system has been removed and replaced with the new system.
/// </summary>
[ByRefEvent]
public record struct TargetChangedEvent(TargetBodyPart? OldTarget, TargetBodyPart? NewTarget);

/// <summary>
/// Stub event for target integrity changes.
/// </summary>
[Serializable, NetSerializable]
public sealed class TargetIntegrityChangeEvent : EntityEventArgs
{
    public NetEntity BodyEntity { get; }

    public TargetIntegrityChangeEvent(NetEntity bodyEntity)
    {
        BodyEntity = bodyEntity;
    }
}
