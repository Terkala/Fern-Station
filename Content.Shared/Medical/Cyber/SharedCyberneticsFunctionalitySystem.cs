// SPDX-FileCopyrightText: 2025 terkala <appleorange64@gmail.com>
//
// SPDX-License-Identifier: MIT

using Content.Shared.Body.Components;
using Content.Shared.Body.Events;
using Content.Shared.Body.Organ;
using Content.Shared.Body.Part;
using Content.Shared.Body.Systems;
using Content.Shared.Medical.Cyber.Components;
using Content.Shared._Shitmed.Body.Events;
using Content.Shared._Shitmed.Body.Organ;
using Content.Shared._Shitmed.BodyEffects.Subsystems;
using Content.Shared._Shitmed.Cybernetics;
using Robust.Shared.GameObjects;

namespace Content.Shared.Medical.Cyber;

/// <summary>
/// Cyber system: Handles cybernetic enable/disable based on panel state.
/// Cybernetics cease to function when their maintenance panel is open.
/// Uses slot components to track panel state instead of iterating cybernetic entities.
/// </summary>
public sealed class SharedCyberneticsFunctionalitySystem : EntitySystem
{
    [Dependency] private readonly SharedBodySystem _body = default!;

    public override void Initialize()
    {
        base.Initialize();

        // Subscribe to cybernetics being added/removed (slot system handles component creation)
        // Cyber system: Body part add/remove subscriptions removed to avoid duplicates with SharedBodySystem.PartAppearance
        // Body parts are handled via other mechanisms (slot system or evaluation is triggered elsewhere)

        // Subscribe to enable/disable events - run AFTER SharedBodySystem to re-check panel state
        // Cyber system: Subscribe via CyberneticsComponent (unique from SharedBodySystem which uses OrganComponent/BodyPartComponent)
        SubscribeLocalEvent<CyberneticsComponent, OrganEnableChangedEvent>(
            OnOrganEnableChanged, 
            after: new[] { typeof(SharedBodySystem) });

        SubscribeLocalEvent<CyberneticsComponent, BodyPartEnableChangedEvent>(
            OnBodyPartEnableChanged, 
            after: new[] { typeof(SharedBodySystem) });
    }


    private void OnOrganEnableChanged(Entity<CyberneticsComponent> cyberEnt, ref OrganEnableChangedEvent ev)
    {
        // Only process if this is a cybernetic organ (has OrganComponent)
        if (!HasComp<OrganComponent>(cyberEnt))
            return;

        // If being enabled, check if panel is open and disable if needed
        if (ev.Enabled)
        {
            EvaluateSingleCybernetic(cyberEnt);
        }
    }

    private void OnBodyPartEnableChanged(Entity<CyberneticsComponent> cyberEnt, ref BodyPartEnableChangedEvent ev)
    {
        // Only process if this is a cybernetic body part (has BodyPartComponent)
        if (!HasComp<BodyPartComponent>(cyberEnt))
            return;

        // If being enabled, check if panel is open and disable if needed
        if (ev.Enabled)
        {
            EvaluateSingleCybernetic(cyberEnt);
        }
    }

    /// <summary>
    /// Evaluates all cybernetics on a body using slot components.
    /// </summary>
    public void EvaluateAllCybernetics(EntityUid body)
    {
        if (TerminatingOrDeleted(body) || !TryComp<BodyComponent>(body, out var bodyComp))
            return;

        // Get all slot components on the body
        var slotComps = GetAllSlotComponents(body);
        
        foreach (var (slotComp, componentType) in slotComps)
        {
            if (slotComp is not ICyberneticSlotComponent slot)
                continue;

            // Get slot ID from component type
            var slotId = SlotIdMapper.GetSlotId(componentType);
            if (slotId == null)
                continue;

            // Check if cybernetic is functional (exists and both parts exist for arms/legs)
            if (!IsCyberneticFunctional(body, slotId, componentType))
            {
                // Cybernetic missing or part destroyed - disable it
                DisableCyberneticInSlot(body, slotId);
                continue;
            }

            // Check panel state from slot component
            var panelOpen = slot.IsPanelUnscrewed;
            var shouldBeDisabled = panelOpen;

            // Find the cybernetic entity
            var cybernetic = GetCyberneticInSlot(body, slotId);
            if (cybernetic == null)
                continue;

            // Update disabled state
            if (TryComp<CyberneticsComponent>(cybernetic.Value, out var cyberComp))
            {
                if (cyberComp.Disabled != shouldBeDisabled)
                {
                    cyberComp.Disabled = shouldBeDisabled;
                    Dirty(cybernetic.Value, cyberComp);

                    // Trigger enable/disable events
                    if (HasComp<OrganComponent>(cybernetic.Value))
                    {
                        var enableEvent = new OrganEnableChangedEvent(!shouldBeDisabled);
                        RaiseLocalEvent(cybernetic.Value, ref enableEvent);
                    }
                    else if (HasComp<BodyPartComponent>(cybernetic.Value))
                    {
                        var enableEvent = new BodyPartEnableChangedEvent(!shouldBeDisabled);
                        RaiseLocalEvent(cybernetic.Value, ref enableEvent);
                    }
                }
            }
        }
    }

    /// <summary>
    /// Evaluates a single cybernetic entity.
    /// </summary>
    private void EvaluateSingleCybernetic(EntityUid cybernetic)
    {
        if (!TryComp<CyberneticsComponent>(cybernetic, out var cyberComp))
            return;

        // Find which slot this cybernetic belongs to
        string? slotId = null;
        EntityUid? body = null;

        if (TryComp<OrganComponent>(cybernetic, out var organ))
        {
            slotId = organ.SlotId;
            body = organ.Body;
        }
        else if (TryComp<BodyPartComponent>(cybernetic, out var part))
        {
            slotId = _body.GetSlotFromBodyPart(part);
            body = part.Body;
        }

        if (slotId == null || body == null)
            return;

        // Get slot component
        var componentType = SlotIdMapper.GetComponentType(slotId, body.Value, EntityManager, ComponentFactory);
        if (componentType == null || !EntityManager.TryGetComponent(body.Value, componentType, out var slotComp))
            return;

        if (slotComp is not ICyberneticSlotComponent slot)
            return;

        // Check panel state
        var panelOpen = slot.IsPanelUnscrewed;
        var shouldBeDisabled = panelOpen;

        if (cyberComp.Disabled == shouldBeDisabled)
            return;

        // Update disabled state
        cyberComp.Disabled = shouldBeDisabled;
        Dirty(cybernetic, cyberComp);

        // Trigger enable/disable events
        if (HasComp<OrganComponent>(cybernetic))
        {
            var enableEvent = new OrganEnableChangedEvent(!shouldBeDisabled);
            RaiseLocalEvent(cybernetic, ref enableEvent);
        }
        else if (HasComp<BodyPartComponent>(cybernetic))
        {
            var enableEvent = new BodyPartEnableChangedEvent(!shouldBeDisabled);
            RaiseLocalEvent(cybernetic, ref enableEvent);
        }
    }

    /// <summary>
    /// Checks if a cybernetic in a slot is functional (exists and all required parts exist).
    /// </summary>
    private bool IsCyberneticFunctional(EntityUid body, string slotId, Type slotComponentType)
    {
        // Get the main cybernetic entity (arm, leg, or organ)
        var cybernetic = GetCyberneticInSlot(body, slotId);
        if (cybernetic == null || !Exists(cybernetic.Value) || TerminatingOrDeleted(cybernetic.Value))
            return false;

        // For arms: check if hand child also exists
        if (slotComponentType == typeof(CyberneticLeftArmComponent) || 
            slotComponentType == typeof(CyberneticRightArmComponent))
        {
            var hand = GetHandForArm(cybernetic.Value);
            if (hand == null || !Exists(hand.Value) || TerminatingOrDeleted(hand.Value))
                return false; // Hand destroyed, arm+hand non-functional
        }

        // For legs: check if foot child exists
        if (slotComponentType == typeof(CyberneticLeftLegComponent) || 
            slotComponentType == typeof(CyberneticRightLegComponent))
        {
            var foot = GetFootForLeg(cybernetic.Value);
            if (foot == null || !Exists(foot.Value) || TerminatingOrDeleted(foot.Value))
                return false; // Foot destroyed, leg+foot non-functional
        }

        return true;
    }

    /// <summary>
    /// Gets the hand child entity for an arm.
    /// </summary>
    private EntityUid? GetHandForArm(EntityUid armEntity)
    {
        // Check if arm has GenerateChildPartComponent with hand spawned
        if (!TryComp<GenerateChildPartComponent>(armEntity, out var generateComp))
            return null;

        // Check if hand entity exists (child part)
        if (generateComp.ChildPart == null || !Exists(generateComp.ChildPart.Value))
            return null;

        // Verify it's actually a hand
        if (generateComp.ChildPart != null && 
            TryComp<BodyPartComponent>(generateComp.ChildPart.Value, out var handPart) &&
            handPart.PartType == BodyPartType.Hand)
        {
            return generateComp.ChildPart.Value;
        }

        return null;
    }

    /// <summary>
    /// Gets the foot child entity for a leg.
    /// </summary>
    private EntityUid? GetFootForLeg(EntityUid legEntity)
    {
        // Check if leg has GenerateChildPartComponent with foot spawned
        if (!TryComp<GenerateChildPartComponent>(legEntity, out var generateComp))
            return null;

        // Check if foot entity exists (child part)
        if (generateComp.ChildPart == null || !Exists(generateComp.ChildPart.Value))
            return null;

        // Verify it's actually a foot
        if (generateComp.ChildPart != null &&
            TryComp<BodyPartComponent>(generateComp.ChildPart.Value, out var footPart) &&
            footPart.PartType == BodyPartType.Foot)
        {
            return generateComp.ChildPart.Value;
        }

        return null;
    }

    /// <summary>
    /// Gets all slot components on a body.
    /// </summary>
    private List<(Component Component, Type ComponentType)> GetAllSlotComponents(EntityUid body)
    {
        var result = new List<(Component, Type)>();

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
    /// Gets the cybernetic entity in a specific slot.
    /// </summary>
    private EntityUid? GetCyberneticInSlot(EntityUid body, string slotId)
    {
        if (!TryComp<BodyComponent>(body, out var bodyComp))
            return null;

        // Try to find as organ first
        var allParts = _body.GetBodyChildren(body, bodyComp);
        foreach (var (partUid, _) in allParts)
        {
            var organs = _body.GetPartOrgans(partUid);
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
                var partSlotId = _body.GetSlotFromBodyPart(partComp);
                if (partSlotId == slotId)
                    return partUid;
            }
        }

        return null;
    }

    /// <summary>
    /// Disables a cybernetic in a slot.
    /// </summary>
    private void DisableCyberneticInSlot(EntityUid body, string slotId)
    {
        var cybernetic = GetCyberneticInSlot(body, slotId);
        if (cybernetic == null || !TryComp<CyberneticsComponent>(cybernetic.Value, out var cyberComp))
            return;

        if (cyberComp.Disabled)
            return;

        cyberComp.Disabled = true;
        Dirty(cybernetic.Value, cyberComp);

        // Trigger disable events
        if (HasComp<OrganComponent>(cybernetic.Value))
        {
            var enableEvent = new OrganEnableChangedEvent(false);
            RaiseLocalEvent(cybernetic.Value, ref enableEvent);
        }
        else if (HasComp<BodyPartComponent>(cybernetic.Value))
        {
            var enableEvent = new BodyPartEnableChangedEvent(false);
            RaiseLocalEvent(cybernetic.Value, ref enableEvent);
        }
    }

    [Dependency] private readonly IComponentFactory ComponentFactory = default!;
}
