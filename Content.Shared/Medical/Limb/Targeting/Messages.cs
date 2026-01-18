// SPDX-FileCopyrightText: 2025 terkala <appleorange64@gmail.com>
//
// SPDX-License-Identifier: MIT

using Robust.Shared.Serialization;

namespace Content.Shared.Medical.Limb.Targeting;

/// <summary>
/// Network message sent from client to server when the player wants to change their targeting selection.
/// </summary>
[Serializable, NetSerializable]
public sealed class TargetingRequestMessage : EntityEventArgs
{
    public TargetBodyPart Target;

    public TargetingRequestMessage(TargetBodyPart target)
    {
        Target = target;
    }
}
