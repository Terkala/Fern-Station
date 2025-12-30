// SPDX-FileCopyrightText: 2025 terkala <appleorange64@gmail.com>
//
// SPDX-License-Identifier: MIT

using Content.Shared.Atmos;
using Robust.Shared.GameStates;

namespace Content.Shared.Medical.CyberOrgan;

/// <summary>
/// Lightweight data component for cyber-lungs that stores selected gas type.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class CyberLungDataComponent : Component
{
    /// <summary>
    /// The gas type that this cyber-lung processes.
    /// Set via multitool interaction on the gas processing module.
    /// </summary>
    [DataField, ViewVariables, AutoNetworkedField]
    public Gas? SelectedGas;
}

