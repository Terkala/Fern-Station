// SPDX-FileCopyrightText: 2025 terkala <appleorange64@gmail.com>
//
// SPDX-License-Identifier: MIT

using Content.Shared.DeviceLinking;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared.Medical.Autodoc;

/// <summary>
/// Simplified autodoc component with 3 operation modes:
/// 1. Organ Implant - Implants organ from slot into strapped patient
/// 2. Medical Care - Applies brute/burn healing to strapped patient
/// 3. Organ Removal - Removes selected organ from strapped patient
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class AutodocComponent : Component
{
    [DataField]
    public ProtoId<SinkPortPrototype> OperatingTablePort = "OperatingTableReceiver";

    /// <summary>
    /// The linked operating table.
    /// Autodocs require a linked operating table to be used.
    /// </summary>
    [DataField, AutoNetworkedField]
    public EntityUid? OperatingTable;

    /// <summary>
    /// Current operation mode.
    /// </summary>
    [DataField, AutoNetworkedField]
    public AutodocMode Mode = AutodocMode.OrganImplant;

    /// <summary>
    /// Item slot for organ insertion (Organ Implant mode).
    /// </summary>
    [DataField]
    public string OrganSlot = "organ_slot";

    /// <summary>
    /// Whether the autodoc is currently active/operating.
    /// </summary>
    [DataField, AutoNetworkedField]
    public bool IsActive = false;

    /// <summary>
    /// Selected organ for removal (Organ Removal mode).
    /// </summary>
    [DataField, AutoNetworkedField]
    public EntityUid? SelectedOrgan;
}

/// <summary>
/// Autodoc operation modes.
/// </summary>
[Serializable, NetSerializable]
public enum AutodocMode : byte
{
    /// <summary>
    /// Implant organ from slot into strapped patient.
    /// </summary>
    OrganImplant,

    /// <summary>
    /// Apply basic brute/burn healing to strapped patient.
    /// </summary>
    MedicalCare,

    /// <summary>
    /// Remove selected organ from strapped patient.
    /// </summary>
    OrganRemoval
}

