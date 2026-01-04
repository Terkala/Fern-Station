// SPDX-FileCopyrightText: 2025 terkala <appleorange64@gmail.com>
//
// SPDX-License-Identifier: MIT

using Content.Shared.Body.Components;
using Content.Shared.Body.Events;
using Content.Shared.Body.Organ;
using Content.Shared.Body.Part;
using Content.Shared.Body.Systems;
using Content.Shared.Medical.Cyber.Components;
using Content.Shared.Medical.CyberLimb;
using Content.Shared._Shitmed.Body.Events;
using Content.Shared._Shitmed.Cybernetics;
using Robust.Shared.GameObjects;

namespace Content.Shared.Medical.Cyber;

/// <summary>
/// Cyber system: Manages slot components on body entities for cybernetics.
/// Creates and removes slot components when cybernetics are added/removed.
/// </summary>
public abstract class SharedCyberneticSlotSystem : EntitySystem
{
    [Dependency] protected readonly SharedBodySystem Body = default!;
    [Dependency] protected readonly IComponentFactory ComponentFactory = default!;

    public override void Initialize()
    {
        base.Initialize();

        // Subscribe to cybernetics being added/removed - run BEFORE SharedCyberneticsFunctionalitySystem
        // Cyber system: Subscriptions are done in server-side implementation to ensure proper ordering with DonorSpeciesSystem
    }

    protected virtual void OnCyberneticAdded(Entity<CyberneticsComponent> cyberEnt, ref OrganAddedToBodyEvent ev)
    {
        // Only process if this is a cybernetic organ (has OrganComponent)
        if (!TryComp<OrganComponent>(cyberEnt, out var organComp))
            return;

        // Get slot ID from organ component
        var slotId = organComp.SlotId;
        if (string.IsNullOrEmpty(slotId))
            return;

        CreateSlotComponent(ev.Body, slotId, cyberEnt);
    }

    protected virtual void OnCyberneticRemoved(Entity<CyberneticsComponent> cyberEnt, ref OrganRemovedFromBodyEvent ev)
    {
        // Only process if this is a cybernetic organ (has OrganComponent)
        if (!TryComp<OrganComponent>(cyberEnt, out var organComp))
            return;

        // Get slot ID from organ component
        var slotId = organComp.SlotId;
        if (string.IsNullOrEmpty(slotId))
            return;

        RemoveSlotComponent(ev.OldBody, slotId);
    }

    /// <summary>
    /// Creates a slot component for the given slot ID on the body entity.
    /// </summary>
    protected void CreateSlotComponent(EntityUid body, string slotId, EntityUid cybernetic)
    {
        if (TerminatingOrDeleted(body) || !TryComp<BodyComponent>(body, out _))
            return;

        // Map slot ID to component type
        var componentType = SlotIdMapper.GetComponentType(slotId, body, EntityManager, ComponentFactory);
        if (componentType == null)
            return;

        // Check if slot component already exists (shouldn't happen, but handle gracefully)
        if (EntityManager.HasComponent(body, componentType))
        {
            // Already exists - update it instead
            UpdateSlotComponent(body, componentType, cybernetic);
            return;
        }

        // Create the slot component using ComponentFactory (sandbox-safe)
        var slotComp = ComponentFactory.GetComponent(componentType);
        EntityManager.AddComponent(body, slotComp);

        // Initialize the slot component
        InitializeSlotComponent(body, componentType, cybernetic);
        
        // Notify derived classes that slot component was created
        OnSlotComponentCreated(body);
    }

    /// <summary>
    /// Removes a slot component for the given slot ID from the body entity.
    /// </summary>
    protected void RemoveSlotComponent(EntityUid body, string slotId)
    {
        if (TerminatingOrDeleted(body))
            return;

        // Map slot ID to component type
        var componentType = SlotIdMapper.GetComponentType(slotId, body, EntityManager, ComponentFactory);
        if (componentType == null)
            return;

        // Remove the component
        if (EntityManager.HasComponent(body, componentType))
        {
            EntityManager.RemoveComponent(body, componentType);
            
            // Notify derived classes that slot component was removed
            OnSlotComponentRemoved(body);
        }
    }

    /// <summary>
    /// Initializes a slot component with service time and other properties.
    /// </summary>
    protected abstract void InitializeSlotComponent(EntityUid body, Type slotComponentType, EntityUid cybernetic);

    /// <summary>
    /// Updates an existing slot component (e.g., when service time needs recalculation).
    /// </summary>
    protected abstract void UpdateSlotComponent(EntityUid body, Type slotComponentType, EntityUid cybernetic);
    
    /// <summary>
    /// Called after a slot component is created. Override to trigger evaluation.
    /// </summary>
    protected virtual void OnSlotComponentCreated(EntityUid body)
    {
    }
    
    /// <summary>
    /// Called after a slot component is removed. Override to trigger evaluation.
    /// </summary>
    protected virtual void OnSlotComponentRemoved(EntityUid body)
    {
    }
}
