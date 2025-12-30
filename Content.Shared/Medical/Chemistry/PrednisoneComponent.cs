// SPDX-FileCopyrightText: 2025 terkala <appleorange64@gmail.com>
//
// SPDX-License-Identifier: MIT

using Content.Shared.FixedPoint;
using Robust.Shared.GameStates;

namespace Content.Shared.Medical.Chemistry;

/// <summary>
/// Component that tracks immunosuppressant reagent effects.
/// This is added to entities when they have immunosuppressant in their bloodstream.
/// Tracks the integrity bonus and duration from the immunosuppressant.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class PrednisoneComponent : Component
{
    /// <summary>
    /// Temporary integrity bonus provided by this immunosuppressant.
    /// This is added to MaxIntegrity when calculating bio-rejection.
    /// </summary>
    [ViewVariables, AutoNetworkedField]
    public FixedPoint2 IntegrityBonus = FixedPoint2.Zero;

    /// <summary>
    /// When this immunosuppressant effect expires (game time in seconds since server start).
    /// Set to 0 for no expiration (lasts as long as reagent is in bloodstream).
    /// </summary>
    [ViewVariables, AutoNetworkedField]
    public float Duration = 0f;
}

