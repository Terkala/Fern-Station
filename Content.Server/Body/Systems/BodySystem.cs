// SPDX-FileCopyrightText: 2021 Javier Guardia Fernández <DrSmugleaf@users.noreply.github.com>
// SPDX-FileCopyrightText: 2021 Matt <matt@isnor.io>
// SPDX-FileCopyrightText: 2021 Vera Aguilera Puerto <6766154+Zumorica@users.noreply.github.com>
// SPDX-FileCopyrightText: 2021 Vera Aguilera Puerto <gradientvera@outlook.com>
// SPDX-FileCopyrightText: 2022 Jezithyr <Jezithyr@gmail.com>
// SPDX-FileCopyrightText: 2022 Kara <lunarautomaton6@gmail.com>
// SPDX-FileCopyrightText: 2022 Rane <60792108+Elijahrane@users.noreply.github.com>
// SPDX-FileCopyrightText: 2022 keronshb <54602815+keronshb@users.noreply.github.com>
// SPDX-FileCopyrightText: 2022 mirrorcult <lunarautomaton6@gmail.com>
// SPDX-FileCopyrightText: 2022 wrexbe <81056464+wrexbe@users.noreply.github.com>
// SPDX-FileCopyrightText: 2023 Doru991 <75124791+Doru991@users.noreply.github.com>
// SPDX-FileCopyrightText: 2023 DrSmugleaf <DrSmugleaf@users.noreply.github.com>
// SPDX-FileCopyrightText: 2023 Leon Friedrich <60421075+ElectroJr@users.noreply.github.com>
// SPDX-FileCopyrightText: 2023 Nemanja <98561806+EmoGarbage404@users.noreply.github.com>
// SPDX-FileCopyrightText: 2023 Pieter-Jan Briers <pieterjan.briers@gmail.com>
// SPDX-FileCopyrightText: 2023 Psychpsyo <60073468+Psychpsyo@users.noreply.github.com>
// SPDX-FileCopyrightText: 2023 ShadowCommander <10494922+ShadowCommander@users.noreply.github.com>
// SPDX-FileCopyrightText: 2023 Visne <39844191+Visne@users.noreply.github.com>
// SPDX-FileCopyrightText: 2023 metalgearsloth <31366439+metalgearsloth@users.noreply.github.com>
// SPDX-FileCopyrightText: 2023 metalgearsloth <comedian_vs_clown@hotmail.com>
// SPDX-FileCopyrightText: 2024 0x6273 <0x40@keemail.me>
// SPDX-FileCopyrightText: 2024 Jake Huxell <JakeHuxell@pm.me>
// SPDX-FileCopyrightText: 2024 Jezithyr <jezithyr@gmail.com>
// SPDX-FileCopyrightText: 2024 Piras314 <p1r4s@proton.me>
// SPDX-FileCopyrightText: 2024 Plykiya <58439124+Plykiya@users.noreply.github.com>
// SPDX-FileCopyrightText: 2024 Tadeo <td12233a@gmail.com>
// SPDX-FileCopyrightText: 2024 Winkarst <74284083+Winkarst-cpu@users.noreply.github.com>
// SPDX-FileCopyrightText: 2024 deathride58 <deathride58@users.noreply.github.com>
// SPDX-FileCopyrightText: 2025 corresp0nd <46357632+corresp0nd@users.noreply.github.com>
// SPDX-FileCopyrightText: 2025 deltanedas <39013340+deltanedas@users.noreply.github.com>
// SPDX-FileCopyrightText: 2025 taydeo <td12233a@gmail.com>
//
// SPDX-License-Identifier: MIT

using Content.Server.Body.Components;
using Content.Server.Ghost;
using Content.Server.Humanoid;
using Content.Shared._Shitmed.Body.Part;
using Content.Shared.Body.Components;
using Content.Shared.Body.Part;
using Content.Shared.Body.Systems;
using Content.Shared.Humanoid;
using Content.Shared.Mind;
using Content.Shared.Mobs.Systems;
using Content.Shared.Movement.Events;
using Content.Shared.Movement.Systems;
using Content.Shared.Storage;
using Robust.Shared.Audio;
using Robust.Shared.Timing;
using System.Numerics;

// Shitmed Change
using System.Linq;
using Content.Shared.Damage;
using Content.Shared.Gibbing.Events;
using Content.Shared._Shitmed.Body.Events;
using Content.Server.Medical.Compatibility;
using Robust.Shared.Containers;
using Content.Shared.Actions;
using Content.Shared.Tag;
using Content.Shared.Medical.CyberLimb;
using Content.Shared._Shitmed.Cybernetics;
using Content.Shared.Body.Events;
using Content.Shared.Body.Organ;
using Robust.Shared.Prototypes;

namespace Content.Server.Body.Systems;

public sealed class BodySystem : SharedBodySystem
{
    [Dependency] private readonly BloodstreamSystem _bloodstream = default!; // Shitmed Change
    [Dependency] private readonly GhostSystem _ghostSystem = default!;
    [Dependency] private readonly IGameTiming _gameTiming = default!;
    [Dependency] private readonly HumanoidAppearanceSystem _humanoidSystem = default!;
    [Dependency] private readonly SharedAppearanceSystem _appearance = default!; // Shitmed Change
    [Dependency] private readonly MobStateSystem _mobState = default!;
    [Dependency] private readonly SharedMindSystem _mindSystem = default!;
    [Dependency] private readonly DonorSpeciesSystem _donorSpecies = default!;
    [Dependency] private readonly LimbCapabilitiesSystem _limbCapabilities = default!;
    [Dependency] private readonly SharedActionsSystem _actionsSystem = default!;
    [Dependency] private readonly TagSystem _tagSystem = default!;
    [Dependency] private readonly SharedContainerSystem _containerSystem = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<BodyComponent, MoveInputEvent>(OnRelayMoveInput);
        SubscribeLocalEvent<BodyComponent, ApplyMetabolicMultiplierEvent>(OnApplyMetabolicMultiplier);
        
        // Subscribe to events for cybernetics ability recalculation
        // Note: OrganRemovedFromBodyEvent subscription handled by DonorSpeciesSystem to avoid duplicates
        // Note: BodyPartRemovedEvent subscription handled by MindShieldSystem to avoid duplicates
        SubscribeLocalEvent<BodyPartComponent, BeingGibbedEvent>(OnBodyPartGibbed);
        SubscribeLocalEvent<OrganComponent, BeingGibbedEvent>(OnOrganGibbed);
    }

    private void OnRelayMoveInput(Entity<BodyComponent> ent, ref MoveInputEvent args)
    {
        // If they haven't actually moved then ignore it.
        if ((args.Entity.Comp.HeldMoveButtons &
             (MoveButtons.Down | MoveButtons.Left | MoveButtons.Up | MoveButtons.Right)) == 0x0)
        {
            return;
        }

        if (_mobState.IsDead(ent) && _mindSystem.TryGetMind(ent, out var mindId, out var mind))
        {
            mind.TimeOfDeath ??= _gameTiming.RealTime;
            _ghostSystem.OnGhostAttempt(mindId, canReturnGlobal: true, mind: mind);
        }
    }

    private void OnApplyMetabolicMultiplier(
        Entity<BodyComponent> ent,
        ref ApplyMetabolicMultiplierEvent args)
    {
        foreach (var organ in GetBodyOrgans(ent, ent))
        {
            RaiseLocalEvent(organ.Id, ref args);
        }
    }

    protected override void AddPart(
        Entity<BodyComponent?> bodyEnt,
        Entity<BodyPartComponent> partEnt,
        string slotId)
    {
        // TODO: Predict this probably.
        base.AddPart(bodyEnt, partEnt, slotId);

        if (TryComp<HumanoidAppearanceComponent>(bodyEnt, out var humanoid))
        {
            var layer = partEnt.Comp.ToHumanoidLayers();
            if (layer != null)
            {
                var layers = HumanoidVisualLayersExtension.Sublayers(layer.Value);
                _humanoidSystem.SetLayersVisibility(
                    bodyEnt, new[] { layer.Value }, visible: true, permanent: true, humanoid); // Shitmed Change
            }
        }
    }

    protected override void RemovePart(
        Entity<BodyComponent?> bodyEnt,
        Entity<BodyPartComponent> partEnt,
        string slotId)
    {
        base.RemovePart(bodyEnt, partEnt, slotId);

        if (!TryComp<HumanoidAppearanceComponent>(bodyEnt, out var humanoid))
            return;

        var layer = partEnt.Comp.ToHumanoidLayers();

        if (layer is null)
            return;

        var layers = HumanoidVisualLayersExtension.Sublayers(layer.Value);
        _humanoidSystem.SetLayersVisibility(
            bodyEnt, layers, visible: false, permanent: true, humanoid);
        _appearance.SetData(bodyEnt, layer, true); // Shitmed Change
    }

    public override HashSet<EntityUid> GibBody(
        EntityUid bodyId,
        bool gibOrgans = false,
        BodyComponent? body = null,
        bool launchGibs = true,
        Vector2? splatDirection = null,
        float splatModifier = 1,
        Angle splatCone = default,
        SoundSpecifier? gibSoundOverride = null,
        // Shitmed Change
        GibType gib = GibType.Gib,
        GibContentsOption contents = GibContentsOption.Drop)
    {
        if (!Resolve(bodyId, ref body, logMissing: false)
            || TerminatingOrDeleted(bodyId)
            || EntityManager.IsQueuedForDeletion(bodyId))
        {
            return new HashSet<EntityUid>();
        }

        var xform = Transform(bodyId);
        if (xform.MapUid is null)
            return new HashSet<EntityUid>();

        var gibs = base.GibBody(bodyId, gibOrgans, body, launchGibs: launchGibs,
            splatDirection: splatDirection, splatModifier: splatModifier, splatCone: splatCone,
            gib: gib, contents: contents); // Shitmed Change

        var ev = new BeingGibbedEvent(gibs);
        RaiseLocalEvent(bodyId, ref ev);

        QueueDel(bodyId);

        return gibs;
    }

    // Shitmed Change Start
    public override HashSet<EntityUid> GibPart(
        EntityUid partId,
        BodyPartComponent? part = null,
        bool launchGibs = true,
        Vector2? splatDirection = null,
        float splatModifier = 1,
        Angle splatCone = default,
        SoundSpecifier? gibSoundOverride = null)
    {
        if (!Resolve(partId, ref part, logMissing: false)
            || TerminatingOrDeleted(partId)
            || EntityManager.IsQueuedForDeletion(partId))
            return new HashSet<EntityUid>();

        if (Transform(partId).MapUid is null)
            return new HashSet<EntityUid>();

        var gibs = base.GibPart(partId, part, launchGibs: launchGibs,
            splatDirection: splatDirection, splatModifier: splatModifier, splatCone: splatCone);

        var ev = new BeingGibbedEvent(gibs);
        RaiseLocalEvent(partId, ref ev);

        if (gibs.Any())
            QueueDel(partId);

        return gibs;
    }

    public override bool BurnPart(EntityUid partId, BodyPartComponent? part = null)
    {
        if (!Resolve(partId, ref part, logMissing: false)
            || TerminatingOrDeleted(partId)
            || EntityManager.IsQueuedForDeletion(partId))
            return false;

        return base.BurnPart(partId, part);
    }

    protected override void OnPartAttachedToBody(EntityUid uid, BodyComponent component, ref BodyPartAddedEvent args)
    {
        // Call base implementation for appearance handling
        base.OnPartAttachedToBody(uid, component, ref args);
        
        // Dispatch to DonorSpeciesSystem for donor species tracking
        _donorSpecies.OnLimbAdded(uid, component, ref args);
    }

    protected override void OnPartDroppedFromBody(EntityUid uid, BodyComponent component, ref BodyPartRemovedEvent args)
    {
        // Call base implementation for appearance handling
        base.OnPartDroppedFromBody(uid, component, ref args);
        
        // Dispatch to DonorSpeciesSystem for donor species tracking
        _donorSpecies.OnLimbRemoved(uid, component, ref args);
    }

    protected override void ApplyPartMarkings(EntityUid target, BodyPartAppearanceComponent component)
    {
        return;
    }

    protected override void RemoveBodyMarkings(EntityUid target, BodyPartAppearanceComponent partAppearance, HumanoidAppearanceComponent bodyAppearance)
    {
        foreach (var (visualLayer, markingList) in partAppearance.Markings)
            foreach (var marking in markingList)
                _humanoidSystem.RemoveMarking(target, marking.MarkingId, sync: false, humanoid: bodyAppearance);

        Dirty(target, bodyAppearance);
    }

    protected override void PartRemoveDamage(Entity<BodyComponent?> bodyEnt, Entity<BodyPartComponent> partEnt)
    {
        var bleeding = partEnt.Comp.SeverBleeding;
        if (partEnt.Comp.IsVital)
            bleeding *= 2f;
        _bloodstream.TryModifyBleedAmount(bodyEnt, bleeding);
    }

    protected override void OnBodyInserted(Entity<BodyComponent> ent, ref EntInsertedIntoContainerMessage args)
    {
        // Call base implementation for core body part handling
        base.OnBodyInserted(ent, ref args);
        
        // Dispatch to LimbCapabilitiesSystem for capability recalculation
        _limbCapabilities.OnBodyPartInserted(ent, ent.Comp, ref args);
    }

    protected override void OnBodyRemoved(Entity<BodyComponent> ent, ref EntRemovedFromContainerMessage args)
    {
        // Call base implementation for core body part handling
        base.OnBodyRemoved(ent, ref args);
        
        // Dispatch to LimbCapabilitiesSystem for capability recalculation
        _limbCapabilities.OnBodyPartRemoved(ent, ent.Comp, ref args);
    }

    // Shitmed Change End

    #region Cybernetics Ability Recalculation

    /// <summary>
    /// Called by DonorSpeciesSystem when an organ is removed from a body.
    /// Recalculates cybernetics abilities if the removed organ was a cybernetic.
    /// </summary>
    public void OnOrganRemovedFromBody(EntityUid uid, OrganComponent component, ref OrganRemovedFromBodyEvent args)
    {
        // Check if this is a cybernetic organ
        if (!HasComp<CyberneticsComponent>(uid) && !HasComp<CyberLimbStorageComponent>(uid))
            return;

        // Recalculate abilities for the body
        if (args.OldBody.IsValid() && !TerminatingOrDeleted(args.OldBody))
        {
            RecalculateCyberneticsAbilities(args.OldBody);
        }
    }

    /// <summary>
    /// Called by MindShieldSystem when a body part is removed from a body.
    /// Recalculates cybernetics abilities if the removed part was a cybernetic.
    /// </summary>
    public void OnBodyPartRemoved(EntityUid uid, BodyPartComponent component, ref BodyPartRemovedEvent args)
    {
        // Check if this is a cybernetic limb
        if (!HasComp<CyberneticsComponent>(uid) && !HasComp<CyberLimbStorageComponent>(uid))
            return;

        // Find the body entity
        var bodyUid = component.Body;
        if (bodyUid.HasValue && bodyUid.Value.IsValid() && !TerminatingOrDeleted(bodyUid.Value))
        {
            RecalculateCyberneticsAbilities(bodyUid.Value);
        }
    }

    /// <summary>
    /// Event handler for when a body part is gibbed.
    /// Recalculates cybernetics abilities if the gibbed part was a cybernetic.
    /// </summary>
    private void OnBodyPartGibbed(Entity<BodyPartComponent> ent, ref BeingGibbedEvent args)
    {
        // Check if this is a cybernetic limb
        if (!HasComp<CyberneticsComponent>(ent) && !HasComp<CyberLimbStorageComponent>(ent))
            return;

        // Find the body entity
        var bodyUid = ent.Comp.Body;
        if (bodyUid.HasValue && bodyUid.Value.IsValid() && !TerminatingOrDeleted(bodyUid.Value))
        {
            RecalculateCyberneticsAbilities(bodyUid.Value);
        }
    }

    /// <summary>
    /// Event handler for when an organ is gibbed.
    /// Recalculates cybernetics abilities if the gibbed organ was a cybernetic.
    /// </summary>
    private void OnOrganGibbed(Entity<OrganComponent> ent, ref BeingGibbedEvent args)
    {
        // Check if this is a cybernetic organ
        if (!HasComp<CyberneticsComponent>(ent) && !HasComp<CyberLimbStorageComponent>(ent))
            return;

        // Find the body entity
        var bodyUid = ent.Comp.Body;
        if (bodyUid.HasValue && bodyUid.Value.IsValid() && !TerminatingOrDeleted(bodyUid.Value))
        {
            RecalculateCyberneticsAbilities(bodyUid.Value);
        }
    }

    /// <summary>
    /// Recalculates all cybernetics abilities for a body.
    /// This ensures all abilities (storage features, hotbar actions) are properly synced
    /// to the torso based on currently attached cyber-limbs and cyber-organs.
    /// </summary>
    public void RecalculateCyberneticsAbilities(EntityUid bodyUid)
    {
        // Safety checks
        if (!bodyUid.IsValid() || TerminatingOrDeleted(bodyUid))
            return;

        if (!TryComp<BodyComponent>(bodyUid, out var body))
            return;

        // First, remove all existing cybernetics-provided actions
        // We'll re-add them based on current cybernetics
        RemoveAllCyberneticsAbilities(bodyUid);

        // Get all body parts and organs
        var allParts = GetBodyChildren(bodyUid, body).ToList();
        var allOrgans = GetBodyOrgans(bodyUid, body).ToList();

        // Process all cybernetics
        foreach (var (partId, partComp) in allParts)
        {
            if (HasComp<CyberneticsComponent>(partId) || HasComp<CyberLimbStorageComponent>(partId))
            {
                ProcessCyberneticsAbilities(bodyUid, partId, partComp);
            }
        }

        foreach (var (organId, organComp) in allOrgans)
        {
            if (HasComp<CyberneticsComponent>(organId) || HasComp<CyberLimbStorageComponent>(organId))
            {
                ProcessCyberneticsAbilities(bodyUid, organId, null);
            }
        }
    }

    /// <summary>
    /// Processes abilities for a single cybernetic entity (limb or organ).
    /// </summary>
    private void ProcessCyberneticsAbilities(EntityUid bodyUid, EntityUid cyberneticUid, BodyPartComponent? partComp)
    {
        if (TerminatingOrDeleted(cyberneticUid))
            return;

        // Process direct abilities from the cybernetic itself
        ProcessDirectAbilities(bodyUid, cyberneticUid);

        // Process dynamic abilities from items inside cybernetic storage
        if (HasComp<CyberLimbStorageComponent>(cyberneticUid))
        {
            ProcessDynamicAbilities(bodyUid, cyberneticUid);
        }
    }

    /// <summary>
    /// Processes direct abilities granted by the cybernetic entity itself (via tags).
    /// </summary>
    private void ProcessDirectAbilities(EntityUid bodyUid, EntityUid cyberneticUid)
    {
        // Check for storage ability
        if (_tagSystem.HasTag(cyberneticUid, "GrantsStorage"))
        {
            // Grant storage access ability if the cybernetic has storage
            if (TryComp<StorageComponent>(cyberneticUid, out var storage))
            {
                // Storage is handled via verbs/UI, not actions
                // But we could add an action here if needed in the future
            }
        }

        // Check for action grants (format: "GrantsAction:<ActionPrototypeId>")
        if (TryComp<TagComponent>(cyberneticUid, out var tagComp))
        {
            foreach (var tag in tagComp.Tags)
            {
                var tagString = tag.ToString();
                if (tagString.StartsWith("GrantsAction:", StringComparison.Ordinal))
                {
                    var actionId = tagString.Substring("GrantsAction:".Length);
                    if (!string.IsNullOrWhiteSpace(actionId))
                    {
                        EntityUid? actionEntity = null;
                        _actionsSystem.AddAction(bodyUid, ref actionEntity, out _, actionId, cyberneticUid);
                    }
                }
            }
        }
    }

    /// <summary>
    /// Processes dynamic abilities from items stored inside the cybernetic's storage.
    /// </summary>
    private void ProcessDynamicAbilities(EntityUid bodyUid, EntityUid cyberneticUid)
    {
        if (!TryComp<StorageComponent>(cyberneticUid, out var storage))
            return;

        // Check all items in the storage
        foreach (var item in storage.Container.ContainedEntities)
        {
            if (TerminatingOrDeleted(item))
                continue;

            // Check if the item grants storage
            if (_tagSystem.HasTag(item, "GrantsStorage"))
            {
                if (TryComp<StorageComponent>(item, out var itemStorage))
                {
                    // Storage from items inside cybernetics
                    // This could be handled via verbs/UI or actions
                }
            }

            // Check if the item grants actions
            if (TryComp<TagComponent>(item, out var itemTagComp))
            {
                foreach (var tag in itemTagComp.Tags)
                {
                    var tagString = tag.ToString();
                    if (tagString.StartsWith("GrantsAction:", StringComparison.Ordinal))
                    {
                        var actionId = tagString.Substring("GrantsAction:".Length);
                        if (!string.IsNullOrWhiteSpace(actionId))
                        {
                            // Grant action to body, with the item as the container
                            EntityUid? actionEntity = null;
                            _actionsSystem.AddAction(bodyUid, ref actionEntity, out _, actionId, item);
                        }
                    }
                }
            }
        }
    }

    /// <summary>
    /// Removes all abilities that were granted by cybernetics.
    /// This is called before recalculating to ensure clean state.
    /// </summary>
    private void RemoveAllCyberneticsAbilities(EntityUid bodyUid)
    {
        if (!TryComp<ActionsComponent>(bodyUid, out var actions))
            return;

        // Get all body parts and organs to find cybernetics
        if (!TryComp<BodyComponent>(bodyUid, out var body))
            return;

        var allParts = GetBodyChildren(bodyUid, body).ToList();
        var allOrgans = GetBodyOrgans(bodyUid, body).ToList();

        // Remove actions granted by cybernetics
        foreach (var (partId, _) in allParts)
        {
            if (HasComp<CyberneticsComponent>(partId) || HasComp<CyberLimbStorageComponent>(partId))
            {
                _actionsSystem.RemoveProvidedActions(bodyUid, partId, actions);
                
                // Also remove actions from items inside storage
                if (TryComp<StorageComponent>(partId, out var storage))
                {
                    foreach (var item in storage.Container.ContainedEntities)
                    {
                        _actionsSystem.RemoveProvidedActions(bodyUid, item, actions);
                    }
                }
            }
        }

        foreach (var (organId, _) in allOrgans)
        {
            if (HasComp<CyberneticsComponent>(organId) || HasComp<CyberLimbStorageComponent>(organId))
            {
                _actionsSystem.RemoveProvidedActions(bodyUid, organId, actions);
                
                // Also remove actions from items inside storage
                if (TryComp<StorageComponent>(organId, out var storage))
                {
                    foreach (var item in storage.Container.ContainedEntities)
                    {
                        _actionsSystem.RemoveProvidedActions(bodyUid, item, actions);
                    }
                }
            }
        }
    }

    #endregion
}
