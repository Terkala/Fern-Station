// SPDX-FileCopyrightText: 2025 terkala <appleorange64@gmail.com>
//
// SPDX-License-Identifier: MIT

using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom.Prototype;

namespace Content.Shared.Medical.CyberLimb.Modules;

/// <summary>
/// Base component for special modules (Jaws of Life, bio-battery, etc.).
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class CyberLimbSpecialModuleComponent : Component
{
    /// <summary>
    /// The type of special module (e.g., "JawsOfLife", "BioBattery").
    /// </summary>
    [DataField(required: true, customTypeSerializer: typeof(PrototypeIdSerializer<EntityPrototype>))]
    public string ModuleType = string.Empty;
}

