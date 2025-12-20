// SPDX-FileCopyrightText: 2025 terkala <appleorange64@gmail.com>
//
// SPDX-License-Identifier: MIT

using System.Numerics;
using Robust.Shared.Prototypes;
using Robust.Shared.Utility;

namespace Content.Shared.Arcade.Prototypes;

/// <summary>
/// Prototype for tutorial definitions.
/// </summary>
[Prototype("tutorial")]
public sealed partial class TutorialPrototype : IPrototype
{
    /// <summary>
    /// The unique identifier for this tutorial.
    /// </summary>
    [IdDataField]
    public string ID { get; private set; } = default!;

    /// <summary>
    /// Display name of the tutorial.
    /// </summary>
    [DataField(required: true)]
    public string Name { get; private set; } = default!;

    /// <summary>
    /// Description of the tutorial.
    /// </summary>
    [DataField(required: true)]
    public string Description { get; private set; } = default!;

    /// <summary>
    /// Path to the tutorial map file.
    /// </summary>
    [DataField(required: true)]
    public ResPath MapPath { get; private set; } = default!;

    /// <summary>
    /// Grid coordinates for spawning the tutorial body.
    /// </summary>
    [DataField(required: true)]
    public Vector2i SpawnLocation { get; private set; }
}

