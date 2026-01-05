// SPDX-FileCopyrightText: 2025 terkala <appleorange64@gmail.com>
//
// SPDX-License-Identifier: MIT

using Content.Server.Body.Components;
using Content.Server.Body.Systems;
using Content.Shared.Body.Organ;
using Content.Shared.Medical.CyberOrgan;
using Content.Shared.Medical.CyberOrgan.Modules;
using Content.Shared.Medical.CyberLimb;
using Content.Shared.Storage;
using Content.Shared.Atmos;
using Content.Shared.Body.Systems;
using Content.Shared.Interaction;
using Robust.Shared.Containers;
using System.Linq;

namespace Content.Server.Medical.CyberOrgan;

/// <summary>
/// System that handles cyber-lung efficiency effects: gas processing, gas requirement scaling, and internal tank breathing.
/// </summary>
public sealed class CyberLungEfficiencySystem : EntitySystem
{
    [Dependency] private readonly CyberOrganEfficiencySystem _organEfficiency = default!;
    [Dependency] private readonly SharedBodySystem _body = default!;

    public override void Initialize()
    {
        base.Initialize();

        // Note: ComponentStartup subscription moved to CyberOrganEfficiencySystem to avoid duplicates
        SubscribeLocalEvent<CyberLungGasProcessingModuleComponent, ActivateInWorldEvent>(OnGasModuleActivated);
    }

    /// <summary>
    /// Called by CyberOrganEfficiencySystem when a cyber organ with lungs starts up.
    /// </summary>
    public void OnLungEfficiencyStartup(EntityUid uid, CyberOrganEfficiencyComponent component)
    {
        // Initialize gas processing module if present
        InitializeGasProcessing(uid);
    }

    /// <summary>
    /// Initializes gas processing - organs no longer have storage, so this just sets default gas.
    /// </summary>
    private void InitializeGasProcessing(EntityUid lungUid)
    {
        // Initialize data component if not present
        if (!TryComp<CyberLungDataComponent>(lungUid, out var lungData))
        {
            lungData = EnsureComp<CyberLungDataComponent>(lungUid);
            // Default to oxygen if not set
            lungData.SelectedGas = Gas.Oxygen;
            Dirty(lungUid, lungData);
        }
    }

    /// <summary>
    /// Handles multitool activation on gas processing module to select gas type.
    /// Note: Organs no longer have storage, so this functionality may need to be reworked.
    /// </summary>
    private void OnGasModuleActivated(EntityUid uid, CyberLungGasProcessingModuleComponent component, ActivateInWorldEvent args)
    {
        // Organs no longer have storage, so gas module activation needs to be handled differently
        // For now, this is a placeholder - gas selection would need to be done via a different method
        args.Handled = true;
    }

    /// <summary>
    /// Cycles through common gas types. TODO: Replace with proper UI.
    /// </summary>
    private void CycleGasType(EntityUid lungUid, CyberLungDataComponent lungData)
    {
        var currentGas = lungData.SelectedGas ?? Gas.Oxygen;
        var gases = new[] { Gas.Oxygen, Gas.Nitrogen, Gas.CarbonDioxide, Gas.Plasma, Gas.Tritium };
        var currentIndex = Array.IndexOf(gases, currentGas);
        var nextIndex = (currentIndex + 1) % gases.Length;
        lungData.SelectedGas = gases[nextIndex];
        Dirty(lungUid, lungData);
    }

    /// <summary>
    /// Gets the gas requirement multiplier based on lung efficiency.
    /// Efficiency < 100%: Requires more gas (multiplier > 1.0)
    /// Efficiency > 100%: Requires less gas (multiplier < 1.0)
    /// </summary>
    public float GetGasRequirementMultiplier(EntityUid lungUid)
    {
        if (!TryComp<CyberOrganEfficiencyComponent>(lungUid, out var efficiency))
            return 1.0f;

        var finalEfficiency = _organEfficiency.GetFinalEfficiency(lungUid, efficiency);
        
        // Inverse relationship: lower efficiency = higher requirement
        return 1.0f / finalEfficiency;
    }

    /// <summary>
    /// Gets the selected gas type for a cyber-lung.
    /// </summary>
    public Gas? GetSelectedGas(EntityUid lungUid)
    {
        if (!TryComp<CyberLungDataComponent>(lungUid, out var lungData))
            return null;

        return lungData.SelectedGas;
    }

    /// <summary>
    /// Handles internal gas tank breathing when airloss damage occurs.
    /// </summary>
    public void HandleInternalTankBreathing(EntityUid body)
    {
        // Find cyber-lungs
        var lungOrgans = _body.GetBodyOrganEntityComps<LungComponent>(body);
        if (lungOrgans.Count == 0)
            return;

        foreach (var (lungUid, lungComp, organ) in lungOrgans)
        {
            if (!TryComp<CyberOrganEfficiencyComponent>(lungUid, out var efficiency))
                continue;

            if (!TryComp<CyberLungDataComponent>(lungUid, out var lungData))
                continue;

            var finalEfficiency = _organEfficiency.GetFinalEfficiency(lungUid, efficiency);
            var selectedGas = lungData.SelectedGas ?? Gas.Oxygen;

            // Organs no longer have storage, so internal gas tank breathing needs to be reworked
            // TODO: Implement gas tank detection via a different method if needed
        }
    }
}

