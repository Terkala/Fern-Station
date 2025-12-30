// SPDX-FileCopyrightText: 2025 terkala <appleorange64@gmail.com>
//
// SPDX-License-Identifier: MIT

using Robust.Shared.GameStates;

namespace Content.Shared.Medical.CyberOrgan.Modules;

/// <summary>
/// Component for gas processing modules that are required for cyber-lungs.
/// This module allows the lung to process a specific gas type (selected via multitool).
/// Without this module, the lung has 0% efficiency.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class CyberLungGasProcessingModuleComponent : Component
{
}

