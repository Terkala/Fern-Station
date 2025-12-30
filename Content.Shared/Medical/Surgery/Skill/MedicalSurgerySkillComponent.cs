// SPDX-FileCopyrightText: 2025 terkala <appleorange64@gmail.com>
//
// SPDX-License-Identifier: MIT

using Robust.Shared.GameStates;

namespace Content.Shared.Medical.Surgery.Skill;

/// <summary>
/// Component that marks an entity as having medical surgery training.
/// Medical jobs (Doctor, Paramedic, CMO, etc.) have this component.
/// Entities with this component perform surgeries better than those without.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class MedicalSurgerySkillComponent : Component
{
}


