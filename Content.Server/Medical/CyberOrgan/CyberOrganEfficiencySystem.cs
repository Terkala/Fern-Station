// SPDX-FileCopyrightText: 2025 terkala <appleorange64@gmail.com>
//
// SPDX-License-Identifier: MIT

using Content.Shared.Body.Part;
using Content.Shared.Medical.CyberOrgan;
using Content.Shared.Medical.CyberLimb;

namespace Content.Server.Medical.CyberOrgan;

/// <summary>
/// System that handles efficiency calculation for cyber-organs.
/// Efficiency is calculated based on module count and cached for performance.
/// </summary>
public sealed class CyberOrganEfficiencySystem : EntitySystem
{
    /// <summary>
    /// Calculates efficiency based on module count.
    /// Base: 100% (1.0) for first module, +10% for each additional.
    /// Returns 0% if no modules (for organs that require modules).
    /// </summary>
    public float CalculateEfficiency(int moduleCount)
    {
        if (moduleCount == 0)
            return 0f; // No modules = 0% efficiency

        // Base 100% for first module, +10% for each additional
        return 1.0f + (moduleCount - 1) * 0.1f;
    }

    /// <summary>
    /// Gets the final efficiency for an organ, applying battery and service time penalties.
    /// Final efficiency = (base + module_bonus) * battery_penalty * service_time_penalty
    /// </summary>
    public float GetFinalEfficiency(EntityUid organ, CyberOrganEfficiencyComponent efficiency)
    {
        var baseEfficiency = efficiency.CachedEfficiency;

        // Get battery penalty from body (shared across all cyber parts)
        float batteryPenalty = 1.0f;
        if (TryComp<BodyPartComponent>(organ, out var part) && part.Body != null)
        {
            if (TryComp<CyberLimbStatsComponent>(part.Body.Value, out var bodyStats))
            {
                batteryPenalty = bodyStats.CachedEfficiencyPenalty;
            }
        }

        // Get service time penalty from this specific organ's storage
        float serviceTimePenalty = 1.0f;
        if (TryComp<CyberLimbStorageComponent>(organ, out var storage))
        {
            serviceTimePenalty = storage.IsServiceTimeExpired ? 0.5f : 1.0f;
        }

        return baseEfficiency * batteryPenalty * serviceTimePenalty;
    }
}

