// SPDX-FileCopyrightText: 2025 terkala <appleorange64@gmail.com>
//
// SPDX-License-Identifier: MIT

using Robust.Shared.GameStates;

namespace Content.Shared.Medical.CyberOrgan;

/// <summary>
/// Lightweight data component for cyber-eyes that stores pre-computed values.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class CyberEyeDataComponent : Component
{
    /// <summary>
    /// Pre-computed telescopic range based on efficiency during installation.
    /// Only set when efficiency >= 100%.
    /// </summary>
    [DataField, ViewVariables, AutoNetworkedField]
    public float? TelescopicRange;
}

