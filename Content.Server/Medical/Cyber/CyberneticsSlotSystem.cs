// SPDX-FileCopyrightText: 2025 terkala <appleorange64@gmail.com>
//
// SPDX-License-Identifier: MIT

using Content.Shared.Body.Components;
using Content.Shared.Body.Events;
using Content.Shared.Body.Organ;
using Content.Shared.Body.Part;
using Content.Shared.Body.Systems;
using Content.Shared.Medical.Cyber;
using Content.Shared.Medical.Cyber.Components;
using Content.Shared.Medical.CyberLimb;
using Content.Shared.Medical.CyberLimb.Modules;
using Content.Shared.Storage;
using Content.Shared._Shitmed.Body.Events;
using Content.Shared._Shitmed.Cybernetics;
using Robust.Shared.Containers;
using Robust.Shared.GameObjects;
using Robust.Shared.Reflection;

namespace Content.Server.Medical.Cyber;

/// <summary>
/// Cyber system: Server-side slot component management with service time calculation.
/// </summary>
    public sealed class CyberneticsSlotSystem : SharedCyberneticSlotSystem
{
    [Dependency] private readonly IReflectionManager _reflection = default!;
    [Dependency] private readonly Content.Shared.Medical.Cyber.SharedCyberneticsFunctionalitySystem _cyberFunctionality = default!;

    public override void Initialize()
    {
        base.Initialize();
        
        // Register subscriptions here (after base.Initialize) to ensure proper ordering with server-only systems
        // Cyber system: Subscribe to CyberneticsComponent for organs (unique from DonorSpeciesSystem which uses OrganComponent)
        // Body parts (cybernetic limbs) are handled when they're added/removed via organ events or through other mechanisms
        // Subscribe AFTER DonorSpeciesSystem (which runs first), BEFORE SharedCyberneticsFunctionalitySystem
        SubscribeLocalEvent<CyberneticsComponent, OrganAddedToBodyEvent>(OnCyberneticAdded, after: new[] { typeof(Content.Server.Medical.Compatibility.DonorSpeciesSystem) }, before: new[] { typeof(Content.Shared.Medical.Cyber.SharedCyberneticsFunctionalitySystem) });
        SubscribeLocalEvent<CyberneticsComponent, OrganRemovedFromBodyEvent>(OnCyberneticRemoved, after: new[] { typeof(Content.Server.Medical.Compatibility.DonorSpeciesSystem) }, before: new[] { typeof(Content.Shared.Medical.Cyber.SharedCyberneticsFunctionalitySystem) });
    }

    protected override void InitializeSlotComponent(EntityUid body, Type slotComponentType, EntityUid cybernetic)
    {
        if (!TryComp<BodyComponent>(body, out _))
            return;

        // Get the slot component
        if (!EntityManager.TryGetComponent(body, slotComponentType, out var slotComp) || slotComp is not Component component)
            return;

        // Calculate initial service time
        CalculateServiceTime(body, component);

        // Initialize slot-specific properties
        InitializeSlotProperties(body, component, cybernetic);

        Dirty(body, component);
    }

    protected override void UpdateSlotComponent(EntityUid body, Type slotComponentType, EntityUid cybernetic)
    {
        if (!EntityManager.TryGetComponent(body, slotComponentType, out var slotComp) || slotComp is not Component component)
            return;

        // Recalculate service time (preserves percentage)
        RecalculateServiceTimeForAll(body);
        Dirty(body, component);
    }

    protected override void OnSlotComponentCreated(EntityUid body)
    {
        base.OnSlotComponentCreated(body);
        
        // Evaluate all cybernetics after slot component creation
        _cyberFunctionality.EvaluateAllCybernetics(body);
    }

    protected override void OnSlotComponentRemoved(EntityUid body)
    {
        base.OnSlotComponentRemoved(body);
        
        // Evaluate all cybernetics after slot component removal
        _cyberFunctionality.EvaluateAllCybernetics(body);
    }

    /// <summary>
    /// Calculates initial service time for a slot component based on matter bins and capacitors.
    /// </summary>
    private void CalculateServiceTime(EntityUid body, Component slotComp)
    {
        if (slotComp is not ICyberneticSlotComponent slot)
            return;

        // Get matter bin count for this cybernetic (read from storage component)
        var matterBinCount = GetMatterBinCountForSlot(body, slotComp);
        
        // Get global capacitor count (all capacitors across all cybernetics on body)
        var globalCapacitorCount = GetGlobalCapacitorCount(body);

        // Calculate service time
        var initialMaxServiceTime = matterBinCount * 600f; // 600 seconds per matter bin
        var multiplier = 1.0f + (globalCapacitorCount * 0.10f); // 110% per capacitor
        var maxServiceTime = initialMaxServiceTime * multiplier;

        // Set values on slot component using reflection (interface properties)
        SetServiceTimeProperties(slotComp, maxServiceTime, maxServiceTime);
    }

    /// <summary>
    /// Recalculates service time for all slot components on a body, preserving percentage.
    /// Called when capacitors are added/removed.
    /// </summary>
    public void RecalculateServiceTimeForAll(EntityUid body)
    {
        if (!TryComp<BodyComponent>(body, out _))
            return;

        // Get global capacitor count
        var globalCapacitorCount = GetGlobalCapacitorCount(body);

        // Get all slot components on body
        var slotComps = GetAllSlotComponents(body);

        foreach (var (slotComp, slotComponentType) in slotComps)
        {
            if (slotComp is not ICyberneticSlotComponent slot)
                continue;

            // Get current percentage
            var currentMax = slot.MaxServiceTime;
            var currentRemaining = slot.ServiceTimeRemaining;
            var percentage = currentMax > 0 ? currentRemaining / currentMax : 1.0f;

            // Get matter bin count for this slot
            var matterBinCount = GetMatterBinCountForSlot(body, slotComp);

            // Recalculate with new multiplier
            var initialMaxServiceTime = matterBinCount * 600f;
            var multiplier = 1.0f + (globalCapacitorCount * 0.10f);
            var newMaxServiceTime = initialMaxServiceTime * multiplier;

            // Apply percentage
            var newServiceTimeRemaining = newMaxServiceTime * percentage;

            // Update slot component
            SetServiceTimeProperties(slotComp, newMaxServiceTime, newServiceTimeRemaining);
            Dirty(body, slotComp);
        }
    }

    /// <summary>
    /// Gets matter bin count for a specific slot's cybernetic entity.
    /// </summary>
    private int GetMatterBinCountForSlot(EntityUid body, Component slotComp)
    {
        // Get slot ID from component type
        var slotId = SlotIdMapper.GetSlotId(slotComp.GetType());
        if (slotId == null)
            return 0;

        // Find cybernetic entity in this slot
        var cybernetic = GetCyberneticInSlot(body, slotId);
        if (cybernetic == null)
            return 0;

        // Read matter bin count from storage component
        if (TryComp<CyberLimbStorageComponent>(cybernetic.Value, out var storage))
        {
            return storage.CachedMatterBinCount;
        }

        return 0;
    }

    /// <summary>
    /// Gets total capacitor count across all cybernetics on the body.
    /// </summary>
    private int GetGlobalCapacitorCount(EntityUid body)
    {
        if (!TryComp<BodyComponent>(body, out var bodyComp))
            return 0;

        int totalCapacitors = 0;

        // Check all body parts
        var allParts = Body.GetBodyChildren(body, bodyComp);
        foreach (var (partUid, _) in allParts)
        {
            if (HasComp<CyberneticsComponent>(partUid) && 
                TryComp<CyberLimbStorageComponent>(partUid, out var partStorage))
            {
                // Count capacitors - need to check storage items for CyberLimbCapacitorModuleComponent
                totalCapacitors += CountCapacitorsInStorage(partUid);
            }

            // Check organs in this part
            var organs = Body.GetPartOrgans(partUid);
            foreach (var (organUid, _) in organs)
            {
                if (HasComp<CyberneticsComponent>(organUid) &&
                    TryComp<CyberLimbStorageComponent>(organUid, out var organStorage))
                {
                    totalCapacitors += CountCapacitorsInStorage(organUid);
                }
            }
        }

        return totalCapacitors;
    }

    [Dependency] private readonly SharedContainerSystem _containers = default!;

    /// <summary>
    /// Counts capacitors in a cybernetic's storage.
    /// </summary>
    private int CountCapacitorsInStorage(EntityUid cybernetic)
    {
        if (!TryComp<StorageComponent>(cybernetic, out var storage) || storage.Container == null)
            return 0;

        int count = 0;
        foreach (var item in storage.Container.ContainedEntities)
        {
            if (HasComp<CyberLimbCapacitorModuleComponent>(item))
                count++;
        }

        return count;
    }

    /// <summary>
    /// Gets the cybernetic entity in a specific slot.
    /// </summary>
    private EntityUid? GetCyberneticInSlot(EntityUid body, string slotId)
    {
        if (!TryComp<BodyComponent>(body, out var bodyComp))
            return null;

        // Try to find as organ first
        var allParts = Body.GetBodyChildren(body, bodyComp);
        foreach (var (partUid, _) in allParts)
        {
            var organs = Body.GetPartOrgans(partUid);
            foreach (var (organUid, organComp) in organs)
            {
                if (HasComp<CyberneticsComponent>(organUid) && organComp.SlotId == slotId)
                    return organUid;
            }
        }

        // Try to find as body part
        foreach (var (partUid, partComp) in allParts)
        {
            if (HasComp<CyberneticsComponent>(partUid))
            {
                var partSlotId = Body.GetSlotFromBodyPart(partComp);
                if (partSlotId == slotId)
                    return partUid;
            }
        }

        return null;
    }

    /// <summary>
    /// Gets all slot components on a body.
    /// </summary>
    private List<(Component Component, Type ComponentType)> GetAllSlotComponents(EntityUid body)
    {
        var result = new List<(Component, Type)>();

        // Get all components on body
        foreach (var component in EntityManager.GetComponents(body))
        {
            if (component is ICyberneticSlotComponent && component is Component comp)
            {
                result.Add((comp, component.GetType()));
            }
        }

        return result;
    }

    /// <summary>
    /// Sets service time properties on a slot component using reflection.
    /// </summary>
    private void SetServiceTimeProperties(Component slotComp, float maxServiceTime, float serviceTimeRemaining)
    {
        if (slotComp is not ICyberneticSlotComponent slot)
            return;

        slot.MaxServiceTime = maxServiceTime;
        slot.ServiceTimeRemaining = serviceTimeRemaining;
        // IsServiceTimeExpired is computed from ServiceTimeRemaining
    }

    /// <summary>
    /// Initializes slot-specific properties (e.g., BreathGas for lungs).
    /// </summary>
    private void InitializeSlotProperties(EntityUid body, Component slotComp, EntityUid cybernetic)
    {
        // Handle lung-specific properties
        if (slotComp is CyberneticLungComponent lungComp)
        {
            // TODO: Read BreathGas from cybernetic entity if it has that property
            // For now, use default
        }
    }
}
