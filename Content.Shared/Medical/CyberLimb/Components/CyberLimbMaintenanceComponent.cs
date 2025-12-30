// SPDX-FileCopyrightText: 2025 terkala <appleorange64@gmail.com>
//
// SPDX-License-Identifier: MIT

using Robust.Shared.GameStates;

namespace Content.Shared.Medical.CyberLimb;

/// <summary>
/// Component that tracks maintenance panel state and maintenance progress for cyber limbs.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class CyberLimbMaintenanceComponent : Component
{
    /// <summary>
    /// Whether screws have been exposed (Skin layer).
    /// </summary>
    [DataField, ViewVariables, AutoNetworkedField]
    public bool ScrewsExposed = false;

    /// <summary>
    /// Whether the maintenance panel is open (Tissue layer).
    /// </summary>
    [DataField, ViewVariables, AutoNetworkedField]
    public bool PanelOpen = false;

    /// <summary>
    /// Whether bolts have been adjusted (Tissue layer).
    /// </summary>
    [DataField, ViewVariables, AutoNetworkedField]
    public bool BoltsAdjusted = false;

    /// <summary>
    /// Whether wiring has been replaced (Organ layer).
    /// </summary>
    [DataField, ViewVariables, AutoNetworkedField]
    public bool WiringReplaced = false;

    /// <summary>
    /// Whether the panel has been closed (Tissue layer).
    /// </summary>
    [DataField, ViewVariables, AutoNetworkedField]
    public bool PanelClosed = false;

    /// <summary>
    /// Whether the panel has been sealed (Skin layer).
    /// </summary>
    [DataField, ViewVariables, AutoNetworkedField]
    public bool PanelSealed = false;
}

