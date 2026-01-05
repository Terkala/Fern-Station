// SPDX-FileCopyrightText: 2025 terkala <appleorange64@gmail.com>
//
// SPDX-License-Identifier: MIT

using Content.Shared.DoAfter;
using Robust.Shared.Serialization;

namespace Content.Shared.Medical.Surgery;

/// <summary>
/// DoAfter event for surgery step execution.
/// </summary>
[Serializable, NetSerializable]
public sealed partial class SurgeryDoAfterEvent : SimpleDoAfterEvent
{
    [DataField("step")]
    public NetEntity Step { get; private set; }

    [DataField("bodyPart")]
    public NetEntity BodyPart { get; private set; }

    public SurgeryDoAfterEvent(NetEntity step, NetEntity bodyPart)
    {
        Step = step;
        BodyPart = bodyPart;
    }
}
