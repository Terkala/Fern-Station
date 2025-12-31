// SPDX-FileCopyrightText: 2025 terkala <appleorange64@gmail.com>
//
// SPDX-License-Identifier: MIT

using System;
using Robust.Shared.GameObjects;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared.Medical.Slime;

/// <summary>
/// Data for a single pending regeneration.
/// </summary>
[Serializable]
[NetSerializable]
[DataRecord]
public sealed record SlimeRegenerationData
{
    /// <summary>
    /// When the part was severed (or when regeneration should start).
    /// </summary>
    [DataField]
    public TimeSpan SeveredTime;

    /// <summary>
    /// The prototype ID of the part that was severed (e.g., "HeadSlime", "LeftArmSlime").
    /// </summary>
    [DataField]
    public EntProtoId PartPrototypeId;

    /// <summary>
    /// The slot ID where the new part should be attached.
    /// </summary>
    [DataField]
    public string? SlotId;

    /// <summary>
    /// The parent part this part was attached to (null if attached directly to body).
    /// </summary>
    [DataField]
    public NetEntity? ParentPart;

    /// <summary>
    /// Whether the part has started regenerating (1 minute passed and new part spawned).
    /// </summary>
    [DataField]
    public bool HasRegenerated = false;

    /// <summary>
    /// The entity ID of the newly spawned regenerated part (set after regeneration).
    /// </summary>
    [DataField]
    public NetEntity? RegeneratedPart;
}

/// <summary>
/// Component that tracks pending slime limb/head regenerations on a body.
/// Slime limbs and heads automatically regenerate 1 minute after being severed,
/// then slowly heal to full health over 4 minutes.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class SlimeLimbRegenerationComponent : Component
{
    /// <summary>
    /// List of pending regenerations. Multiple parts can regenerate simultaneously.
    /// </summary>
    [DataField, AutoNetworkedField]
    public List<SlimeRegenerationData> Regenerations = new();
}

