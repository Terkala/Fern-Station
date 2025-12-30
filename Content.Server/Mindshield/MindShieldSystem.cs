// SPDX-FileCopyrightText: 2023 DrSmugleaf <DrSmugleaf@users.noreply.github.com>
// SPDX-FileCopyrightText: 2023 coolmankid12345 <55817627+coolmankid12345@users.noreply.github.com>
// SPDX-FileCopyrightText: 2023 coolmankid12345 <coolmankid12345@users.noreply.github.com>
// SPDX-FileCopyrightText: 2024 BombasterDS <115770678+BombasterDS@users.noreply.github.com>
// SPDX-FileCopyrightText: 2024 Tadeo <td12233a@gmail.com>
// SPDX-FileCopyrightText: 2024 fishbait <gnesse@gmail.com>
// SPDX-FileCopyrightText: 2025 Skye <57879983+Rainbeon@users.noreply.github.com>
// SPDX-FileCopyrightText: 2025 taydeo <td12233a@gmail.com>
//
// SPDX-License-Identifier: MIT

using Content.Server.Administration.Logs;
using Content.Server.Body.Components;
using Content.Server.Body.Systems;
using Content.Server.GameTicking.Rules; // GoobStation
using Content.Server.Mind;
using Content.Server.Popups;
using Content.Server.Revolutionary.Components; // GoobStation
using Content.Server.Roles;
using Content.Shared.Body.Events;
using Content.Shared.Body.Organ;
using Content.Shared.Body.Part;
using Content.Shared.Database;
using Content.Shared.Implants;
using Content.Shared.Implants.Components;
using Content.Shared.Mindshield.Components;
using Content.Shared.Revolutionary; // GoobStation
using Content.Shared.Revolutionary.Components;
using Content.Shared.Tag;
using Content.Shared.Mindcontrol;  //Goobstation - Mindcontrol Implant
using Robust.Shared.Containers;

namespace Content.Server.Mindshield;

/// <summary>
/// System used for checking if the implanted is a Rev or Head Rev.
/// </summary>
public sealed class MindShieldSystem : EntitySystem
{
    [Dependency] private readonly IAdminLogManager _adminLogManager = default!;
    [Dependency] private readonly RoleSystem _roleSystem = default!;
    [Dependency] private readonly MindSystem _mindSystem = default!;
    [Dependency] private readonly TagSystem _tag = default!;
    [Dependency] private readonly PopupSystem _popupSystem = default!;
    [Dependency] private readonly SharedRevolutionarySystem _revolutionarySystem = default!; // Goobstation
    [Dependency] private readonly SharedBodySystem _body = default!;
    [Dependency] private readonly SharedContainerSystem _container = default!;

    [ValidatePrototypeId<TagPrototype>]
    public const string MindShieldTag = "MindShield";

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<SubdermalImplantComponent, ImplantImplantedEvent>(ImplantCheck);
        SubscribeLocalEvent<MindShieldComponent, ImplantRemovedFromEvent>(OnMindShieldRemoved); // GoobStation
        
        // Handle mindshield organ added/removed
        SubscribeLocalEvent<OrganComponent, OrganAddedToBodyEvent>(OnMindShieldOrganAdded);
        SubscribeLocalEvent<OrganComponent, OrganRemovedFromBodyEvent>(OnMindShieldOrganRemoved);
        
        // Handle head removal/reattachment
        SubscribeLocalEvent<BodyPartComponent, BodyPartRemovedEvent>(OnBodyPartRemoved);
        SubscribeLocalEvent<BodyPartComponent, BodyPartAddedEvent>(OnBodyPartAdded);
    }

    /// <summary>
    /// Checks if the implant was a mindshield or not
    /// </summary>
    public void ImplantCheck(EntityUid uid, SubdermalImplantComponent comp, ref ImplantImplantedEvent ev)
    {
        if (!_tag.HasTag(ev.Implant, MindShieldTag) || ev.Implanted == null) // Edited Goobstation
            return;

        EnsureComp<MindShieldComponent>(ev.Implanted.Value);
        MindShieldRemovalCheck(ev.Implanted.Value, ev.Implant);

        // GoobStation
        if (!TryComp<CommandStaffComponent>(ev.Implanted, out var commandComp))
            return;

        commandComp.Enabled = true;
    }

    /// <summary>
    /// Checks if the implanted person was a Rev or Head Rev and remove role or destroy mindshield respectively.
    /// </summary>
    public void MindShieldRemovalCheck(EntityUid implanted, EntityUid implant)
    {
        if (TryComp<HeadRevolutionaryComponent>(implanted, out var headRevComp)) // GoobStation - headRevComp
        {
            _popupSystem.PopupEntity(Loc.GetString("head-rev-break-mindshield"), implanted);
            _revolutionarySystem.ToggleConvertAbility((implanted, headRevComp), false); // GoobStation - turn off headrev ability to convert
            //QueueDel(implant); - Goobstation - Headrevs should remove implant before turning on ability
            return;
        }

        if (_mindSystem.TryGetMind(implanted, out var mindId, out _) &&
            _roleSystem.MindTryRemoveRole<RevolutionaryRoleComponent>(mindId))
        {
            if (HasComp<ShowRevolutionaryIconsComponent>(implanted))
                RemComp<ShowRevolutionaryIconsComponent>(implanted);

            _adminLogManager.Add(LogType.Mind, LogImpact.Medium, $"{ToPrettyString(implanted)} was deconverted due to being implanted with a Mindshield.");
        }
        if (HasComp<MindcontrolledComponent>(implanted))   //Goobstation - Mindcontrol Implant
            RemComp<MindcontrolledComponent>(implanted);
    }

    // GoobStation
    /// <summary>
    /// Removes mindshield comp if mindshield implant was ejected
    /// </summary>
    public void OnMindShieldRemoved(Entity<MindShieldComponent> mindshielded, ref ImplantRemovedFromEvent args)
    {
        if (!_tag.HasTag(args.Implant, MindShieldTag))
            return;

        _popupSystem.PopupEntity(Loc.GetString("mindshield-implant-effect-removed"), mindshielded, mindshielded);

        if (TryComp<HeadRevolutionaryComponent>(mindshielded, out var headRevComp))
            _revolutionarySystem.ToggleConvertAbility((mindshielded, headRevComp), true);

        RemComp<MindShieldComponent>(mindshielded);
    }

    /// <summary>
    /// Handles mindshield organ being added to a body.
    /// </summary>
    private void OnMindShieldOrganAdded(EntityUid uid, OrganComponent component, ref OrganAddedToBodyEvent args)
    {
        // Check if this is a mindshield organ
        if (!_tag.HasTag(uid, MindShieldTag))
            return;

        // Add MindShieldComponent to the body
        EnsureComp<MindShieldComponent>(args.Body);
        MindShieldRemovalCheck(args.Body, uid);

        // GoobStation
        if (!TryComp<CommandStaffComponent>(args.Body, out var commandComp))
            return;

        commandComp.Enabled = true;
    }

    /// <summary>
    /// Handles mindshield organ being removed from a body.
    /// </summary>
    private void OnMindShieldOrganRemoved(EntityUid uid, OrganComponent component, ref OrganRemovedFromBodyEvent args)
    {
        // Check if this is a mindshield organ
        if (!_tag.HasTag(uid, MindShieldTag))
            return;

        // Check if body still has a mindshield organ in the head
        if (!TryComp<BodyComponent>(args.OldBody, out var body))
            return;

        // Check all heads for mindshield organ
        var heads = _body.GetBodyChildrenOfType(args.OldBody, BodyPartType.Head, body);
        bool hasMindShield = false;
        
        foreach (var (headId, _) in heads)
        {
            if (!TryComp<BodyPartComponent>(headId, out var headPart))
                continue;

            if (!headPart.Organs.ContainsKey("mindshield"))
                continue;

            var organContainerId = SharedBodySystem.GetOrganContainerId("mindshield");
            if (!_container.TryGetContainer(headId, organContainerId, out var organContainer))
                continue;

            foreach (var organ in organContainer.ContainedEntities)
            {
                if (_tag.HasTag(organ, MindShieldTag))
                {
                    hasMindShield = true;
                    break;
                }
            }

            if (hasMindShield)
                break;
        }

        // If no mindshield organ found, remove MindShieldComponent from body
        if (!hasMindShield && HasComp<MindShieldComponent>(args.OldBody))
        {
            _popupSystem.PopupEntity(Loc.GetString("mindshield-implant-effect-removed"), args.OldBody, args.OldBody);

            if (TryComp<HeadRevolutionaryComponent>(args.OldBody, out var headRevComp))
                _revolutionarySystem.ToggleConvertAbility((args.OldBody, headRevComp), true);

            RemComp<MindShieldComponent>(args.OldBody);
        }
    }

    /// <summary>
    /// Handles body part removal - checks if head was removed and removes mindshield from body.
    /// </summary>
    private void OnBodyPartRemoved(EntityUid uid, BodyPartComponent component, ref BodyPartRemovedEvent args)
    {
        // Only care about head removal
        if (component.PartType != BodyPartType.Head)
            return;

        // Check if the removed head has a mindshield organ
        if (!TryComp<BodyPartComponent>(uid, out var headPart))
            return;

        if (!headPart.Organs.ContainsKey("mindshield"))
            return;

        var organContainerId = SharedBodySystem.GetOrganContainerId("mindshield");
        if (!_container.TryGetContainer(uid, organContainerId, out var organContainer))
            return;

        bool hasMindShield = false;
        foreach (var organ in organContainer.ContainedEntities)
        {
            if (_tag.HasTag(organ, MindShieldTag))
            {
                hasMindShield = true;
                break;
            }
        }

        // If head had mindshield, remove MindShieldComponent from body
        if (hasMindShield && component.Body != null && HasComp<MindShieldComponent>(component.Body.Value))
        {
            _popupSystem.PopupEntity(Loc.GetString("mindshield-implant-effect-removed"), component.Body.Value, component.Body.Value);

            if (TryComp<HeadRevolutionaryComponent>(component.Body.Value, out var headRevComp))
                _revolutionarySystem.ToggleConvertAbility((component.Body.Value, headRevComp), true);

            RemComp<MindShieldComponent>(component.Body.Value);
        }
    }

    /// <summary>
    /// Handles body part addition - checks if head was attached and adds mindshield if head has it.
    /// </summary>
    private void OnBodyPartAdded(EntityUid uid, BodyPartComponent component, ref BodyPartAddedEvent args)
    {
        // Only care about head addition
        if (component.PartType != BodyPartType.Head)
            return;

        // Check if the attached head has a mindshield organ
        if (!TryComp<BodyPartComponent>(uid, out var headPart))
            return;

        if (!headPart.Organs.ContainsKey("mindshield"))
            return;

        var organContainerId = SharedBodySystem.GetOrganContainerId("mindshield");
        if (!_container.TryGetContainer(uid, organContainerId, out var organContainer))
            return;

        foreach (var organ in organContainer.ContainedEntities)
        {
            if (_tag.HasTag(organ, MindShieldTag))
            {
                // Head has mindshield organ - add MindShieldComponent to body
                if (component.Body != null)
                {
                    EnsureComp<MindShieldComponent>(component.Body.Value);
                    MindShieldRemovalCheck(component.Body.Value, organ);

                    // GoobStation
                    if (TryComp<CommandStaffComponent>(component.Body.Value, out var commandComp))
                        commandComp.Enabled = true;
                }
                break;
            }
        }
    }
}
