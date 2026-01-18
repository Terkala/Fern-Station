// SPDX-FileCopyrightText: 2025 terkala <appleorange64@gmail.com>
//
// SPDX-License-Identifier: MIT

namespace Content.Shared.Medical.Limb.Targeting;

/// <summary>
/// Represents the integrity/health state of a limb.
/// This enum is completely original and not based on any _Shitmed implementations.
/// </summary>
public enum LimbIntegrityState
{
    /// <summary>
    /// Limb is in perfect health with no damage.
    /// </summary>
    Healthy = 0,

    /// <summary>
    /// Limb has minor damage but is fully functional.
    /// </summary>
    LightlyDamaged = 1,

    /// <summary>
    /// Limb has moderate damage but remains functional.
    /// </summary>
    ModeratelyDamaged = 2,

    /// <summary>
    /// Limb has significant damage and may have reduced functionality.
    /// </summary>
    HeavilyDamaged = 3,

    /// <summary>
    /// Limb is critically damaged and non-functional (disabled).
    /// Occurs at 100 damage threshold.
    /// </summary>
    Disabled = 4,

    /// <summary>
    /// Limb has been destroyed/severed and removed from the body.
    /// Occurs at 200 damage threshold.
    /// </summary>
    Destroyed = 5,

    /// <summary>
    /// Limb is missing from the body (never existed or was removed).
    /// </summary>
    Missing = 6,
}
