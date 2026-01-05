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
        // Storage subscriptions removed - organs no longer have storage
    }

    /// <summary>
    /// Centralized handler that sets efficiency from BaseEfficiency field.
    /// </summary>
    private void OnEfficiencyStartup(EntityUid uid, CyberOrganEfficiencyComponent component, ComponentStartup args)
    {
        // Set efficiency directly from BaseEfficiency (no module-based calculation)
        component.CachedEfficiency = component.BaseEfficiency;
        Dirty(uid, component);
    }


    /// <summary>
    /// Gets the final efficiency for an organ.
    /// Organs no longer have battery or service time penalties - they use flat efficiency values.
    /// </summary>
    public float GetFinalEfficiency(EntityUid organ, CyberOrganEfficiencyComponent efficiency)
    {
        // Organs use flat efficiency values - no penalties
        return efficiency.CachedEfficiency;
    }
}

