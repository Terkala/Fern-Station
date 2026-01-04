// SPDX-FileCopyrightText: 2025 terkala <appleorange64@gmail.com>
//
// SPDX-License-Identifier: MIT

using Content.Shared.Body.Components;
using Content.Shared.Body.Events;
using Content.Shared.Body.Organ;
using Content.Shared.Body.Part;
using Content.Shared.Body.Systems;
using Content.Shared.Medical.CyberLimb;
using Content.Shared._Shitmed.Body.Events;
using Content.Shared._Shitmed.Body.Organ;

namespace Content.Shared._Shitmed.Cybernetics;

/// <summary>
/// System that handles cybernetics functionality based on maintenance panel state.
/// Cybernetics cease to function when their maintenance panel is open.
/// Re-evaluates all cybernetics when one is added/removed or when maintenance panels are opened/closed.
/// </summary>
public sealed class SharedCyberneticsFunctionalitySystem : EntitySystem
{
    [Dependency] private readonly SharedBodySystem _body = default!;

    public override void Initialize()
    {
        base.Initialize();

        // NOTE: Subscriptions removed - functionality moved to Content.Shared.Medical.Cyber.SharedCyberneticsFunctionalitySystem
        // This system is kept for backward compatibility but no longer subscribes to events.
        // Cyber system: All cybernetics functionality is now handled by the new slot-based system.
    }

    private void OnCyberneticsAdded(Entity<OrganComponent> organEnt, ref OrganAddedToBodyEvent ev)
    {
        // Only process if this is a cybernetic organ
        if (!HasComp<CyberneticsComponent>(organEnt))
            return;

        // Re-evaluate all cybernetics on the body
        EvaluateAllCybernetics(ev.Body);
    }

    private void OnCyberneticsRemoved(Entity<OrganComponent> organEnt, ref OrganRemovedFromBodyEvent ev)
    {
        // Only process if this is a cybernetic organ
        if (!HasComp<CyberneticsComponent>(organEnt))
            return;

        // Re-evaluate all remaining cybernetics on the body
        EvaluateAllCybernetics(ev.OldBody);
    }

    private void OnCyberneticsPartAdded(Entity<BodyComponent> bodyEnt, ref BodyPartAddedEvent ev)
    {
        // Only process if this is a cybernetic body part
        if (!HasComp<CyberneticsComponent>(ev.Part))
            return;

        // Re-evaluate all cybernetics on the body
        EvaluateAllCybernetics(bodyEnt);
    }

    private void OnCyberneticsPartRemoved(Entity<BodyComponent> bodyEnt, ref BodyPartRemovedEvent ev)
    {
        // Only process if this is a cybernetic body part
        if (!HasComp<CyberneticsComponent>(ev.Part))
            return;

        // Re-evaluate all remaining cybernetics on the body
        EvaluateAllCybernetics(bodyEnt);
    }

    private void OnOrganEnableChanged(Entity<OrganComponent> organEnt, ref OrganEnableChangedEvent ev)
    {
        // Only process if this is a cybernetic organ
        if (!HasComp<CyberneticsComponent>(organEnt))
            return;

        // If being enabled, check if panel is open and disable if needed
        if (ev.Enabled)
        {
            EvaluateSingleCybernetic(organEnt);
        }
    }

    private void OnBodyPartEnableChanged(Entity<BodyPartComponent> partEnt, ref BodyPartEnableChangedEvent ev)
    {
        // Only process if this is a cybernetic body part
        if (!HasComp<CyberneticsComponent>(partEnt))
            return;

        // If being enabled, check if panel is open and disable if needed
        if (ev.Enabled)
        {
            EvaluateSingleCybernetic(partEnt);
        }
    }


    /// <summary>
    /// Evaluates all cybernetics on a body and enables/disables them based on maintenance panel state.
    /// </summary>
    public void EvaluateAllCybernetics(EntityUid body)
    {
        if (!TryComp<BodyComponent>(body, out var bodyComp))
            return;

        var allParts = _body.GetBodyChildren(body, bodyComp);
        foreach (var (partUid, _) in allParts)
        {
            if (HasComp<CyberneticsComponent>(partUid))
            {
                EvaluateSingleCybernetic(partUid);
            }

            // Also check organs in this part
            var organs = _body.GetPartOrgans(partUid);
            foreach (var (organUid, _) in organs)
            {
                if (HasComp<CyberneticsComponent>(organUid))
                {
                    EvaluateSingleCybernetic(organUid);
                }
            }
        }
    }

    /// <summary>
    /// Evaluates a single cybernetic and enables/disables it based on maintenance panel state.
    /// </summary>
    private void EvaluateSingleCybernetic(EntityUid cybernetic)
    {
        if (!TryComp<CyberneticsComponent>(cybernetic, out var cyberComp))
            return;

        // Check if maintenance panel is open
        var panelOpen = false;
        if (TryComp<CyberneticsUpkeepComponent>(cybernetic, out var upkeep))
        {
            panelOpen = upkeep.IsPanelUnscrewed;
        }

        // Determine if cybernetic should be disabled
        var shouldBeDisabled = panelOpen;

        // Only update if state has changed (avoid unnecessary events)
        if (cyberComp.Disabled == shouldBeDisabled)
            return;

        // Update disabled state
        cyberComp.Disabled = shouldBeDisabled;
        Dirty(cybernetic, cyberComp);

        // Trigger enable/disable events to add/remove abilities
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
}
