// SPDX-FileCopyrightText: 2024 Tadeo <td12233a@gmail.com>
// SPDX-FileCopyrightText: 2025 taydeo <td12233a@gmail.com>
//
// SPDX-License-Identifier: AGPL-3.0-or-later AND MIT

using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared._Shitmed.Medical.Surgery;

[RegisterComponent, NetworkedComponent]
[ComponentProtoName("ShitmedSurgery")]
[Prototype("Surgeries")]
public sealed partial class SurgeryComponent : Component
{
    [DataField]
    public int Priority;

    [DataField]
    public EntProtoId? Requirement;

    [DataField(required: true)]
    public List<EntProtoId> Steps = new();
}