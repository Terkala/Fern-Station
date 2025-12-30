// SPDX-FileCopyrightText: 2025 terkala <appleorange64@gmail.com>
//
// SPDX-License-Identifier: MIT

using Content.Shared.FixedPoint;
using Robust.Shared.GameStates;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom;

namespace Content.Shared.Medical.Integrity;

/// <summary>
/// Component that tracks integrity usage for organs, limbs, and cybernetics.
/// Integrity represents the body's capacity to support implants and replacements.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class IntegrityComponent : Component
{
    /// <summary>
    /// Maximum integrity this entity can have. Base is 6, but species can override (e.g., dwarves = 8).
    /// </summary>
    [DataField, AutoNetworkedField]
    public int MaxIntegrity = 6;

    /// <summary>
    /// Current integrity usage from all installed organs, limbs, and cybernetics.
    /// </summary>
    [ViewVariables, AutoNetworkedField]
    public FixedPoint2 UsedIntegrity = FixedPoint2.Zero;

    /// <summary>
    /// How much bio-rejection damage per point of integrity over the limit.
    /// </summary>
    [DataField]
    public FixedPoint2 BioRejectionPerPoint = FixedPoint2.New(10);

    /// <summary>
    /// Current bio-rejection damage. Gradually adjusts toward TargetBioRejection.
    /// </summary>
    [ViewVariables, AutoNetworkedField]
    public FixedPoint2 CurrentBioRejection = FixedPoint2.Zero;

    /// <summary>
    /// Target bio-rejection damage based on integrity over limit.
    /// CurrentBioRejection gradually moves toward this value at 0.2 per tick.
    /// </summary>
    [ViewVariables, AutoNetworkedField]
    public FixedPoint2 TargetBioRejection = FixedPoint2.Zero;

    /// <summary>
    /// Temporary integrity bonus from immunosuppressants.
    /// This is added to MaxIntegrity when calculating bio-rejection.
    /// </summary>
    [ViewVariables, AutoNetworkedField]
    public FixedPoint2 TemporaryIntegrityBonus = FixedPoint2.Zero;

    /// <summary>
    /// Whether this entity needs bio-rejection updates.
    /// Set to false when current == target to skip processing.
    /// </summary>
    [ViewVariables]
    public bool NeedsUpdate = true;

    /// <summary>
    /// Cached total surgery penalty. Updated when surgery penalties change.
    /// Avoids iterating all body parts every tick.
    /// </summary>
    [ViewVariables]
    public FixedPoint2 CachedSurgeryPenalty = FixedPoint2.Zero;

    /// <summary>
    /// The next service time expiration from all cyber-limbs, in seconds from now.
    /// When this time is reached, we check all limbs for expired service times.
    /// Set to null if no cyber-limbs have service time tracking.
    /// </summary>
    [ViewVariables]
    public float? NextServiceTimeExpiration = null;

    /// <summary>
    /// The time when the next service time expiration will occur.
    /// Used to check if we've reached the expiration time.
    /// </summary>
    [ViewVariables, DataField(customTypeSerializer: typeof(TimeOffsetSerializer))]
    public TimeSpan NextServiceTimeExpirationTime = TimeSpan.Zero;
}

