// SPDX-FileCopyrightText: 2025 terkala <appleorange64@gmail.com>
//
// SPDX-License-Identifier: MIT

using Content.Shared.Body.Components;
using Content.Shared.Body.Events;
using Content.Shared.Body.Organ;
using Content.Shared.Body.Part;
using Content.Shared.Medical.Compatibility;
using Content.Shared.Medical.Surgery;

namespace Content.Server.Medical.Compatibility;

/// <summary>
/// System that tracks donor species when organs/limbs are removed from bodies.
/// This allows compatible donors (same species) to have 0 integrity cost when implanted.
/// </summary>
public sealed class DonorSpeciesSystem : EntitySystem
{
    [Dependency] private readonly SharedSurgerySystem _surgery = default!;

    public override void Initialize()
    {
        base.Initialize();

        // Subscribe to organ/limb addition events to set donor species when first added
        SubscribeLocalEvent<OrganComponent, OrganAddedToBodyEvent>(OnOrganAdded);
        SubscribeLocalEvent<BodyComponent, BodyPartAddedEvent>(OnLimbAdded);
        
        // Subscribe to organ/limb removal events to track donor species
        SubscribeLocalEvent<OrganComponent, OrganRemovedFromBodyEvent>(OnOrganRemoved);
        SubscribeLocalEvent<BodyComponent, BodyPartRemovedEvent>(OnLimbRemoved);
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
    }

    private void OnLimbAdded(EntityUid uid, BodyComponent component, ref BodyPartAddedEvent args)
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
    }

    private void OnLimbRemoved(EntityUid uid, BodyComponent component, ref BodyPartRemovedEvent args)
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

