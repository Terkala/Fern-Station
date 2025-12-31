// SPDX-FileCopyrightText: 2025 terkala <appleorange64@gmail.com>
//
// SPDX-License-Identifier: MIT

using Content.Shared.Body.Part;
using Content.Shared.Medical.CyberOrgan;
using Content.Shared.Medical.CyberLimb;
using Content.Shared.Body.Organ;
using Content.Server.Body.Components;
using Content.Shared.Eye;
using Content.Shared._Shitmed.Body.Organ;
using Robust.Shared.Containers;

namespace Content.Server.Medical.CyberOrgan;

/// <summary>
/// System that handles efficiency calculation for cyber-organs.
/// Efficiency is calculated based on module count and cached for performance.
/// </summary>
public sealed class CyberOrganEfficiencySystem : EntitySystem
{
    [Dependency] private readonly CyberLungEfficiencySystem _lungEfficiency = default!;
    [Dependency] private readonly CyberHeartEfficiencySystem _heartEfficiency = default!;
    [Dependency] private readonly CyberStomachEfficiencySystem _stomachEfficiency = default!;
    [Dependency] private readonly CyberEyeEfficiencySystem _eyeEfficiency = default!;

    public override void Initialize()
    {
        base.Initialize();

        // Single subscription point to avoid duplicate subscriptions across multiple systems
        SubscribeLocalEvent<CyberOrganEfficiencyComponent, ComponentStartup>(OnEfficiencyStartup);
        SubscribeLocalEvent<CyberOrganEfficiencyComponent, EntInsertedIntoContainerMessage>(OnStorageInserted);
        SubscribeLocalEvent<CyberOrganEfficiencyComponent, EntRemovedFromContainerMessage>(OnStorageRemoved);
    }

    /// <summary>
    /// Centralized handler that dispatches to the appropriate organ-specific system.
    /// </summary>
    private void OnEfficiencyStartup(EntityUid uid, CyberOrganEfficiencyComponent component, ComponentStartup args)
    {
        // Dispatch to appropriate system based on organ type
        if (HasComp<LungComponent>(uid))
        {
            _lungEfficiency.OnLungEfficiencyStartup(uid, component);
        }
        else if (HasComp<HeartComponent>(uid))
        {
            _heartEfficiency.OnHeartEfficiencyStartup(uid, component);
        }
        else if (HasComp<StomachComponent>(uid))
        {
            _stomachEfficiency.OnStomachEfficiencyStartup(uid, component);
        }
        else if (HasComp<EyeComponent>(uid))
        {
            _eyeEfficiency.OnEyeEfficiencyStartup(uid, component);
        }
        // Kidneys don't need startup, they work continuously via Update()
    }

    /// <summary>
    /// Centralized handler for container insertion events that dispatches to the appropriate organ-specific system.
    /// </summary>
    private void OnStorageInserted(EntityUid uid, CyberOrganEfficiencyComponent component, ref EntInsertedIntoContainerMessage args)
    {
        // Dispatch to appropriate system based on organ type
        if (HasComp<LungComponent>(uid))
        {
            // Lungs don't need storage change handling
        }
        else if (HasComp<HeartComponent>(uid))
        {
            _heartEfficiency.OnHeartStorageChanged(uid, component);
        }
        else if (HasComp<StomachComponent>(uid))
        {
            _stomachEfficiency.OnStomachStorageChanged(uid, component);
        }
        else if (HasComp<EyeComponent>(uid))
        {
            _eyeEfficiency.OnEyeStorageChanged(uid, component);
        }
        // Kidneys don't need storage change handling
    }

    /// <summary>
    /// Centralized handler for container removal events that dispatches to the appropriate organ-specific system.
    /// </summary>
    private void OnStorageRemoved(EntityUid uid, CyberOrganEfficiencyComponent component, ref EntRemovedFromContainerMessage args)
    {
        // Dispatch to appropriate system based on organ type
        if (HasComp<LungComponent>(uid))
        {
            // Lungs don't need storage change handling
        }
        else if (HasComp<HeartComponent>(uid))
        {
            _heartEfficiency.OnHeartStorageChanged(uid, component);
        }
        else if (HasComp<StomachComponent>(uid))
        {
            _stomachEfficiency.OnStomachStorageChanged(uid, component);
        }
        else if (HasComp<EyeComponent>(uid))
        {
            _eyeEfficiency.OnEyeStorageChanged(uid, component);
        }
        // Kidneys don't need storage change handling
    }

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

