// SPDX-FileCopyrightText: 2025 terkala <appleorange64@gmail.com>
//
// SPDX-License-Identifier: MIT

using Content.Shared.Body.Components;
using Content.Shared.Body.Organ;
using Content.Shared.Body.Part;
using Content.Shared.Body.Systems;
using Content.Shared.Implants;
using Content.Shared.Implants.Components;
using Content.Shared.Tag;
using Robust.Shared.Containers;
using Robust.Shared.Network;
using System.Linq;

namespace Content.Shared._Shitmed.Cybernetics;

/// <summary>
/// Self-contained system for granting subdermal implants based on cybernetic tag configuration.
/// Processes cybernetics directly without event subscriptions for maximum modularity.
/// </summary>
public sealed class SharedCyberneticsAbilitySystem : EntitySystem
{
    [Dependency] private readonly SharedSubdermalImplantSystem _subdermalImplantSystem = default!;
    [Dependency] private readonly SharedBodySystem _body = default!;
    [Dependency] private readonly TagSystem _tag = default!;
    [Dependency] private readonly INetManager _net = default!;
    
    // No Initialize() event subscriptions - completely self-contained
    // All processing happens via EvaluateAllCyberneticAbilities() call
    
    /// <summary>
    /// Main entry point - evaluates all cybernetics and grants/revokes abilities.
    /// Called from SharedCyberneticsFunctionalitySystem.EvaluateAllCybernetics.
    /// </summary>
    public void EvaluateAllCyberneticAbilities(EntityUid body)
    {
        // Only run on server - spawns entities and modifies game state
        if (_net.IsClient)
            return;
            
        if (!TryComp<BodyComponent>(body, out var bodyComp))
            return;

        // Get all cybernetics on the body
        var allParts = _body.GetBodyChildren(body, bodyComp);
        var cybernetics = new List<(EntityUid cybernetic, bool enabled)>();
        
        foreach (var (partUid, _) in allParts)
        {
            if (!HasComp<CyberneticsComponent>(partUid))
                continue;
            
            // Check if enabled (panel closed, not disabled)
            var enabled = TryComp<CyberneticsComponent>(partUid, out var cyberComp) && !cyberComp.Disabled;
            
            // Also check organs
            var organs = _body.GetPartOrgans(partUid);
            foreach (var (organUid, _) in organs)
            {
                if (HasComp<CyberneticsComponent>(organUid))
                {
                    var organEnabled = TryComp<CyberneticsComponent>(organUid, out var organCyber) && !organCyber.Disabled;
                    cybernetics.Add((organUid, organEnabled));
                }
            }
            
            cybernetics.Add((partUid, enabled));
        }

        // Process each cybernetic
        foreach (var (cybernetic, enabled) in cybernetics)
        {
            if (enabled)
                GrantAbilities(cybernetic, body);
            else
                RevokeAbilities(cybernetic, body);
        }
    }

    private void GrantAbilities(EntityUid cybernetic, EntityUid body)
    {
        if (!TryComp<TagComponent>(cybernetic, out var tagComp))
            return;
        
        // Find grant tags (Grants[ImplantId] pattern)
        var grantTags = tagComp.Tags.Where(t => t.ToString().StartsWith("Grants")).ToList();
        
        if (grantTags.Count == 0)
            return;
        
        // Get implant container
        if (!TryComp<ImplantedComponent>(body, out var implanted))
            implanted = EnsureComp<ImplantedComponent>(body);
        
        var implantContainer = implanted.ImplantContainer;
        
        // Process each grant tag
        foreach (var grantTag in grantTags)
        {
            // Extract subdermal implant prototype ID from tag
            // Pattern: "Grants" + prototype ID (e.g., "GrantsMicroBombImplant" -> "MicroBombImplant")
            var tagString = grantTag.ToString();
            var implantPrototypeId = tagString.Substring("Grants".Length);
            
            // Check if already spawned
            bool alreadySpawned = false;
            foreach (var containedEntity in implantContainer.ContainedEntities)
            {
                if (TryComp<LinkedToCyberneticComponent>(containedEntity, out var linked) && 
                    linked.LinkedCybernetic == cybernetic)
                {
                    // Check if prototype ID matches
                    var proto = Prototype(containedEntity);
                    if (proto?.ID == implantPrototypeId)
                    {
                        alreadySpawned = true;
                        break;
                    }
                }
            }
            
            if (alreadySpawned)
                continue;
            
            // Spawn subdermal implant
            var coords = Transform(body).Coordinates;
            var subdermalImplant = Spawn(implantPrototypeId, coords);
            
            // Link to cybernetic
            var linkedComp = EnsureComp<LinkedToCyberneticComponent>(subdermalImplant);
            linkedComp.LinkedCybernetic = cybernetic;
            Dirty(subdermalImplant, linkedComp);
            
            // Insert into body
            if (TryComp<SubdermalImplantComponent>(subdermalImplant, out var subdermalComp))
            {
                _subdermalImplantSystem.ForceImplant(body, subdermalImplant, subdermalComp);
            }
        }
    }

    private void RevokeAbilities(EntityUid cybernetic, EntityUid body)
    {
        if (!TryComp<ImplantedComponent>(body, out var implanted))
            return;
        
        var implantContainer = implanted.ImplantContainer;
        
        // Find all linked implants
        var implantsToRemove = new List<EntityUid>();
        foreach (var containedEntity in implantContainer.ContainedEntities)
        {
            if (TryComp<LinkedToCyberneticComponent>(containedEntity, out var linked) && 
                linked.LinkedCybernetic == cybernetic)
            {
                implantsToRemove.Add(containedEntity);
            }
        }
        
        // Remove and delete
        foreach (var implant in implantsToRemove)
        {
            _subdermalImplantSystem.ForceRemove(body, implant);
            QueueDel(implant);
        }
    }
}