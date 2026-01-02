// SPDX-FileCopyrightText: 2025 terkala <appleorange64@gmail.com>
//
// SPDX-License-Identifier: MIT

using Content.Shared.Body.Components;
using Content.Shared.Body.Events;
using Content.Shared.Body.Organ;
using Content.Shared.Body.Part;
using Content.Shared.Medical.Compatibility;
using Content.Shared.Medical.Surgery;
using Content.Server.Mindshield;
using Content.Server.Body.Systems;

namespace Content.Server.Medical.Compatibility;

/// <summary>
/// System that tracks donor species when organs/limbs are removed from bodies.
/// This allows compatible donors (same species) to have 0 integrity cost when implanted.
/// </summary>
public sealed class DonorSpeciesSystem : EntitySystem
{
    [Dependency] private readonly SharedSurgerySystem _surgery = default!;
    [Dependency] private readonly MindShieldSystem _mindShield = default!;
    [Dependency] private readonly BodySystem _bodySystem = default!;

    public override void Initialize()
    {
        base.Initialize();

        // Centralized subscriptions to avoid duplicates with MindShieldSystem and SharedBodySystem
        // Subscribe to organ/limb addition events to set donor species when first added
        SubscribeLocalEvent<OrganComponent, OrganAddedToBodyEvent>(OnOrganAdded);
        // Note: BodyPartAddedEvent subscription moved to BodySystem to avoid duplicates
        
        // Subscribe to organ/limb removal events to track donor species
        SubscribeLocalEvent<OrganComponent, OrganRemovedFromBodyEvent>(OnOrganRemoved);
        // Note: BodyPartRemovedEvent subscription moved to BodySystem to avoid duplicates
    }

    private void OnOrganAdded(EntityUid uid, OrganComponent component, ref OrganAddedToBodyEvent args)
    {
        // Set donor species when organ is first added to a body
        // This handles organs spawned as part of body initialization
        if (args.Body != EntityUid.Invalid && !HasComp<DonorSpeciesComponent>(uid))
        {
            var donorSpecies = EnsureComp<DonorSpeciesComponent>(uid);
            var bodySpecies = _surgery.GetBodySpecies(args.Body);
            if (bodySpecies != null)
            {
                donorSpecies.DonorSpecies = bodySpecies.Value;
                Dirty(uid, donorSpecies);
            }
        }

        // Dispatch to MindShieldSystem for mindshield organ handling
        _mindShield.OnMindShieldOrganAdded(uid, component, ref args);
    }

    /// <summary>
    /// Called by BodySystem when a limb is added to a body.
    /// </summary>
    public void OnLimbAdded(EntityUid uid, BodyComponent component, ref BodyPartAddedEvent args)
    {
        // Set donor species when limb is first added to a body
        // This handles limbs spawned as part of body initialization
        // The event is raised on the body, args.Part contains the part entity
        if (args.Part.Comp.Body != null && !HasComp<DonorSpeciesComponent>(args.Part))
        {
            var donorSpecies = EnsureComp<DonorSpeciesComponent>(args.Part);
            var bodySpecies = _surgery.GetBodySpecies(args.Part.Comp.Body.Value);
            if (bodySpecies != null)
            {
                donorSpecies.DonorSpecies = bodySpecies.Value;
                Dirty(args.Part, donorSpecies);
            }
        }
    }

    private void OnOrganRemoved(EntityUid uid, OrganComponent component, ref OrganRemovedFromBodyEvent args)
    {
        // Set donor species on the removed organ
        if (args.OldBody != EntityUid.Invalid)
        {
            var donorSpecies = EnsureComp<DonorSpeciesComponent>(uid);
            var bodySpecies = _surgery.GetBodySpecies(args.OldBody);
            if (bodySpecies != null)
            {
                donorSpecies.DonorSpecies = bodySpecies.Value;
                Dirty(uid, donorSpecies);
            }
        }

        // Dispatch to MindShieldSystem for mindshield organ handling
        _mindShield.OnMindShieldOrganRemoved(uid, component, ref args);
        
        // Dispatch to BodySystem for cybernetics ability recalculation
        _bodySystem.OnOrganRemovedFromBody(uid, component, ref args);
    }

    /// <summary>
    /// Called by BodySystem when a limb is removed from a body.
    /// </summary>
    public void OnLimbRemoved(EntityUid uid, BodyComponent component, ref BodyPartRemovedEvent args)
    {
        // Set donor species on the removed limb
        var donorSpecies = EnsureComp<DonorSpeciesComponent>(args.Part);
        var bodySpecies = _surgery.GetBodySpecies(uid);
        if (bodySpecies != null)
        {
            donorSpecies.DonorSpecies = bodySpecies.Value;
            Dirty(args.Part, donorSpecies);
        }
    }
}

