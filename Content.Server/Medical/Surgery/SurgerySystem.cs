// SPDX-FileCopyrightText: 2025 terkala <appleorange64@gmail.com>
//
// SPDX-License-Identifier: MIT

using Content.Server.Body.Systems;
using Content.Shared.Body.Components;
using Content.Shared.Body.Organ;
using Content.Shared.Body.Part;
using Content.Shared.Body.Systems;
using Content.Shared.Medical.Surgery;
using SSSharedSurgerySystem = Content.Shared.Medical.Surgery.SharedSurgerySystem;
using Content.Shared.Medical.Integrity;
using Content.Server.Medical.Integrity;
using Content.Shared.Medical.Surgery.Skill;
using Content.Shared.Medical.Surgery.Equipment;
using Content.Shared.Medical.Compatibility;
using Content.Shared.Medical.CyberLimb;
using Content.Server.Medical.CyberLimb;
using Content.Shared.Tag;
using Content.Shared.Popups;
using Content.Server.Popups;
using Content.Shared.FixedPoint;
using Content.Shared.Verbs;
using Content.Shared.Hands.Components;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Damage;
using Content.Shared.Damage.Components;
using Content.Shared.Damage.Systems;
using Content.Shared.Mobs.Systems;
using Content.Shared.Weapons.Melee;
using Content.Shared.Stacks;
using Content.Shared._Shitmed.Medical.Surgery;
using Content.Shared._Shitmed.Medical.Surgery.Effects.Step;
using Content.Shared._Shitmed.Medical.Surgery.Tools;
using Content.Shared.DoAfter;
using ShitmedSurgeryDoAfterEvent = Content.Shared._Shitmed.Medical.Surgery.SurgeryDoAfterEvent;
using NewSurgeryDoAfterEvent = Content.Shared.Medical.Surgery.SurgeryDoAfterEvent;
using Content.Shared._Shitmed.Cybernetics;
using SharedCyberneticsFunctionalitySystem = Content.Shared._Shitmed.Cybernetics.SharedCyberneticsFunctionalitySystem;
using ShitmedSurgerySteps = Content.Shared._Shitmed.Medical.Surgery.Steps;
using ShitmedSurgeryUIKey = Content.Shared._Shitmed.Medical.Surgery.SurgeryUIKey;
using Content.Shared.Medical.Surgery.Components;
using Content.Shared.Medical.Surgery.Operations;
using Content.Shared.Medical.Surgery.Prototypes;
using Content.Server.Medical.Surgery.Operations;
using Content.Shared.Implants.Components;
using Content.Shared.UserInterface;
using Content.Shared.Prototypes;
using Content.Shared.Interaction;
using Content.Shared._Shitmed.Targeting;
using Robust.Server.GameObjects;
using Robust.Shared.Containers;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;
using Robust.Shared.Utility;
using Robust.Shared.Map;
using Robust.Shared.GameObjects;
using Robust.Shared.Audio.Systems;
using System.Linq;

namespace Content.Server.Medical.Surgery;

/// <summary>
/// Server-side surgery system that handles surgery execution.
/// </summary>
public sealed class SurgerySystem : SSSharedSurgerySystem
{
    [Dependency] private readonly BodySystem _body = default!;
    [Dependency] private readonly SharedIntegritySystem _integrity = default!;
    [Dependency] private readonly IntegritySystem _vitality = default!;
    [Dependency] private readonly PopupSystem _popup = default!;
    [Dependency] private readonly UserInterfaceSystem _ui = default!;
    [Dependency] private readonly IPrototypeManager _prototypes = default!;
    [Dependency] private readonly IComponentFactory _componentFactory = default!;
    [Dependency] private readonly SharedHandsSystem _hands = default!;
    [Dependency] private readonly CyberLimbStatsSystem _cyberLimbStats = default!;
    [Dependency] private readonly CyberneticsUpkeepSystem _cyberneticsUpkeep = default!;
    [Dependency] private readonly SharedCyberneticsFunctionalitySystem _cyberneticsFunctionality = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly EntityLookupSystem _lookup = default!;
    [Dependency] private readonly SharedStackSystem _stack = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;
    [Dependency] private readonly MobStateSystem _mobState = default!;
    [Dependency] private readonly DamageableSystem _damageable = default!;
    [Dependency] private readonly SharedContainerSystem _containers = default!;
    [Dependency] private readonly SharedInteractionSystem _interaction = default!;
    [Dependency] private readonly MetaDataSystem _metaData = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly RotateToFaceSystem _rotateToFace = default!;
    
    /// <summary>
    /// Tracks which method (primary/improvised) was selected for each step.
    /// Key: Step entity UID, Value: true if improvised, false if primary
    /// </summary>
    private readonly Dictionary<EntityUid, bool> _stepMethodSelection = new();

    /// <summary>
    /// Cached surgery step data to avoid spawning entities every UI update.
    /// </summary>
    private readonly Dictionary<string, SurgeryStepData> _cachedStepData = new();

    /// <summary>
    /// Cached data for surgery steps to avoid spawning entities every UI update.
    /// </summary>
    private sealed class SurgeryStepData
    {
        public SurgeryLayer Layer;
        public List<BodyPartType> ValidPartTypes = new();
        public string? TargetOrganSlot;
        public EntProtoId StepId;
    }

    /// <summary>
    /// Tracks which surgery UIs are open and need material scanning.
    /// Key: Body part entity, Value: Next scan time
    /// </summary>
    private readonly Dictionary<EntityUid, TimeSpan> _openSurgeryUIs = new();

    /// <summary>
    /// Tracks the current layer for each body part in open surgery UIs.
    /// Key: Body part entity, Value: Current layer
    /// </summary>
    private readonly Dictionary<EntityUid, SurgeryLayer> _bodyPartCurrentLayer = new();

    /// <summary>
    /// Tracks the selected body part for each surgery UI.
    /// Key: Body entity (where UI is opened), Value: Selected body part entity
    /// </summary>
    private readonly Dictionary<EntityUid, EntityUid> _selectedBodyParts = new();

    /// <summary>
    /// Tracks the selected target body part for each surgery UI (for missing limbs).
    /// Key: Body entity (where UI is opened), Value: Selected target body part enum
    /// </summary>
    private readonly Dictionary<EntityUid, TargetBodyPart?> _selectedTargetBodyParts = new();

    /// <summary>
    /// Tracks items in each surgeon's hands for dynamic step generation.
    /// Key: User entity, Value: List of (item net entity, is implant, is organ, name)
    /// </summary>
    private readonly Dictionary<EntityUid, List<(NetEntity Item, bool IsImplant, bool IsOrgan, string Name)>> _userHandItems = new();

    private const float MaterialScanRange = 1.5f;
    private const float MaterialScanInterval = 0.5f;

    public override void Initialize()
    {
        base.Initialize();
        
        // Cache surgery step data at initialization to avoid spawning entities every UI update
        CacheSurgeryStepData();

        Subs.BuiEvents<SurgeryLayerComponent>(Content.Shared.Medical.Surgery.SurgeryUIKey.Key, subs =>
        {
            subs.Event<SurgeryStepSelectedMessage>(OnStepSelected);
            subs.Event<SurgeryOperationMethodSelectedMessage>(OnOperationMethodSelected);
            subs.Event<SurgeryLayerChangedMessage>(OnLayerChanged);
            subs.Event<SurgeryBodyPartSelectedMessage>(OnBodyPartSelected);
            subs.Event<SurgeryHandItemsMessage>(OnHandItemsReceived);
            subs.Event<BoundUIOpenedEvent>(OnSurgeryUIOpened);
            subs.Event<BoundUIClosedEvent>(OnSurgeryUIClosed);
        });

        SubscribeLocalEvent<SurgeryPlasteelBonePlatingEffectComponent, SurgeryStepEvent>(OnPlasteelBonePlatingStep);
        SubscribeLocalEvent<SurgeryDermalPlasteelWeaveEffectComponent, SurgeryStepEvent>(OnDermalPlasteelWeaveStep);
        
        // Note: SurgeryTendWoundsEffectComponent subscriptions are handled by Shitmed SharedSurgerySystem
        // to avoid duplicate subscriptions

        SubscribeLocalEvent<BodyPartComponent, ComponentStartup>(OnBodyPartStartup);
        SubscribeLocalEvent<SurgeryLayerComponent, ComponentStartup>(OnSurgeryLayerStartup);
        SubscribeLocalEvent<SurgeryLayerComponent, GetVerbsEvent<Verb>>(OnGetSurgeryVerb);
        SubscribeLocalEvent<SurgeryTargetComponent, BoundUserInterfaceCheckRangeEvent>(OnSurgeryUIRangeCheck);
        SubscribeLocalEvent<BoundUserInterfaceMessageAttempt>(OnSurgeryUIMessageAttempt, before: new[] { typeof(SharedInteractionSystem) });
        SubscribeLocalEvent<InRangeOverrideEvent>(OnInRangeOverride);
        SubscribeLocalEvent<UnskilledSurgeryPenaltyComponent, GetVerbsEvent<Verb>>(OnGetUnskilledPenaltyVerb);
        SubscribeLocalEvent<SurgeryTargetComponent, GetVerbsEvent<Verb>>(OnGetBodySurgeryVerb);
        SubscribeLocalEvent<SurgeryLayerComponent, NewSurgeryDoAfterEvent>(OnSurgeryDoAfter);
    }

    /// <summary>
    /// Caches surgery step data at initialization to avoid expensive entity spawning in UI updates.
    /// </summary>
    private void CacheSurgeryStepData()
    {
        foreach (var stepProto in _prototypes.EnumeratePrototypes<EntityPrototype>())
        {
            if (!stepProto.HasComponent<SurgeryStepComponent>(_componentFactory))
                continue;

            // Spawn once, cache data, delete
            EntityUid stepEntity;
            try
            {
                stepEntity = Spawn(stepProto.ID);
            }
            catch
            {
                // Skip if entity can't be spawned (e.g., during initialization)
                continue;
            }

            if (!Exists(stepEntity) || !TryComp<SurgeryStepComponent>(stepEntity, out var step))
            {
                if (Exists(stepEntity))
                    Del(stepEntity);
                continue;
            }

            _cachedStepData[stepProto.ID] = new SurgeryStepData
            {
                Layer = step.Layer,
                ValidPartTypes = step.ValidPartTypes,
                TargetOrganSlot = step.TargetOrganSlot,
                StepId = stepProto.ID
            };

            Del(stepEntity);
        }
    }

    private void OnBodyPartStartup(EntityUid uid, BodyPartComponent component, ComponentStartup args)
    {
        // Automatically add SurgeryLayerComponent to body parts
        if (!HasComp<SurgeryLayerComponent>(uid))
        {
            var layer = AddComp<SurgeryLayerComponent>(uid);
            layer.PartType = component.PartType;
            Dirty(uid, layer);
        }
    }

    private void OnGetSurgeryVerb(Entity<SurgeryLayerComponent> ent, ref GetVerbsEvent<Verb> args)
    {
        if (!args.CanAccess || !args.CanInteract)
            return;

        // Only show surgery verb on body parts attached to a body with SurgeryTargetComponent (players)
        if (!TryComp<BodyPartComponent>(ent, out var partComp) || partComp.Body == null)
            return;

        if (!HasComp<SurgeryTargetComponent>(partComp.Body.Value))
            return;

        // Only show if user has a surgical tool or slashing weapon in hand
        var hasSurgicalTool = false;
        foreach (var heldItem in _hands.EnumerateHeld(args.User))
        {
            // Check for surgery tool component
            if (HasComp<SurgeryToolComponent>(heldItem))
            {
                hasSurgicalTool = true;
                break;
            }
            
            // Check for melee weapon with slashing damage
            if (TryComp<MeleeWeaponComponent>(heldItem, out var melee))
            {
                if (melee.Damage.DamageDict.TryGetValue("Slash", out var slashDamage) && slashDamage > 0)
                {
                    hasSurgicalTool = true;
                    break;
                }
            }
        }

        if (!hasSurgicalTool)
            return;

        var user = args.User;
        args.Verbs.Add(new Verb
        {
            Text = Loc.GetString("surgery-verb-open"),
            Icon = new SpriteSpecifier.Texture(new("/Textures/Interface/VerbIcons/settings.svg.192dpi.png")),
            Act = () => OpenSurgeryUI(ent, user)
        });
    }

    private void OnGetBodySurgeryVerb(Entity<SurgeryTargetComponent> ent, ref GetVerbsEvent<Verb> args)
    {
        if (!args.CanAccess || !args.CanInteract)
            return;

        // Only show if user has a surgical tool or slashing weapon in hand
        var hasSurgicalTool = false;
        foreach (var heldItem in _hands.EnumerateHeld(args.User))
        {
            // Check for surgery tool component
            if (HasComp<SurgeryToolComponent>(heldItem))
            {
                hasSurgicalTool = true;
                break;
            }
            
            // Check for melee weapon with slashing damage
            if (TryComp<MeleeWeaponComponent>(heldItem, out var melee))
            {
                if (melee.Damage.DamageDict.TryGetValue("Slash", out var slashDamage) && slashDamage > 0)
                {
                    hasSurgicalTool = true;
                    break;
                }
            }
        }

        if (!hasSurgicalTool)
            return;

        // Find the first body part with SurgeryLayerComponent (prefer torso, then any part)
        EntityUid? targetPart = null;
        foreach (var part in _body.GetBodyChildren(ent))
        {
            if (HasComp<SurgeryLayerComponent>(part.Id))
            {
                // Prefer torso if available
                if (TryComp<BodyPartComponent>(part.Id, out var partComp) && 
                    partComp.PartType == BodyPartType.Torso)
                {
                    targetPart = part.Id;
                    break;
                }
                
                // Otherwise use the first available part
                if (targetPart == null)
                    targetPart = part.Id;
            }
        }

        // If no body part found, don't show the verb
        if (targetPart == null)
            return;

        var user = args.User;
        var partToOpen = targetPart.Value;
        args.Verbs.Add(new Verb
        {
            Text = Loc.GetString("surgery-verb-open"),
            Icon = new SpriteSpecifier.Texture(new("/Textures/Interface/VerbIcons/settings.svg.192dpi.png")),
            Act = () =>
            {
                if (TryComp<SurgeryLayerComponent>(partToOpen, out var layer))
                    OpenSurgeryUI((partToOpen, layer), user);
            }
        });
    }

    private void OnGetUnskilledPenaltyVerb(Entity<UnskilledSurgeryPenaltyComponent> ent, ref GetVerbsEvent<Verb> args)
    {
        if (!args.CanAccess || !args.CanInteract)
            return;

        // Only medical personnel can remove unskilled surgery penalties
        if (!HasMedicalSkill(args.User))
            return;

        var user = args.User;
        args.Verbs.Add(new Verb
        {
            Text = Loc.GetString("surgery-verb-fix-unskilled-surgery"),
            Icon = new SpriteSpecifier.Texture(new("/Textures/Interface/VerbIcons/bandage.svg.192dpi.png")),
            Act = () => RemoveUnskilledPenalty(ent, user)
        });
    }

    /// <summary>
    /// Removes unskilled surgery penalty from a body part.
    /// Only medical personnel can perform this action.
    /// </summary>
    private void RemoveUnskilledPenalty(Entity<UnskilledSurgeryPenaltyComponent> ent, EntityUid user)
    {
        if (!HasMedicalSkill(user))
        {
            _popup.PopupEntity(Loc.GetString("surgery-fix-unskilled-requires-medical"), user, user);
            return;
        }

        // Remove the component
        RemComp<UnskilledSurgeryPenaltyComponent>(ent);
        
        // Update cached surgery penalty and recalculate bio-rejection
        if (TryComp<BodyPartComponent>(ent, out var part) && part.Body != null)
        {
            if (TryComp<IntegrityComponent>(part.Body.Value, out var integrity))
            {
                _vitality.UpdateCachedSurgeryPenalty(part.Body.Value, integrity);
                _integrity.RecalculateTargetBioRejection(part.Body.Value, integrity);
            }
            
            _popup.PopupEntity(Loc.GetString("surgery-fix-unskilled-success"), user, user);
        }
    }

    private void OpenSurgeryUI(Entity<SurgeryLayerComponent> ent, EntityUid user)
    {
        // Ensure UserInterfaceComponent exists
        if (!HasComp<SurgeryLayerComponent>(ent.Owner))
            return;

        // Get the body from the body part - open UI on the body entity (like old Shitmed system)
        // This avoids range check issues since body parts are inside the body and may not have proper world transforms
        if (!TryComp<BodyPartComponent>(ent, out var part) || part.Body == null)
            return;

        var body = part.Body.Value;

        // Initialize selected body part to the one that was clicked (or default to torso)
        EntityUid selectedPart = ent.Owner;
        TargetBodyPart? selectedTargetPart = _body.GetTargetBodyPart(part);
        
        // If this isn't the torso, try to find torso as default
        if (part.PartType != BodyPartType.Torso)
        {
            var torsoParts = _body.GetBodyChildrenOfType(body, BodyPartType.Torso);
            var torso = torsoParts.FirstOrDefault();
            if (torso.Id != default)
            {
                selectedPart = torso.Id;
                selectedTargetPart = TargetBodyPart.Torso;
            }
        }

        // Store the selected body part
        _selectedBodyParts[body] = selectedPart;
        _selectedTargetBodyParts[body] = selectedTargetPart;

        // Initialize layer state for the selected body part (default to Skin)
        if (!_bodyPartCurrentLayer.ContainsKey(selectedPart))
        {
            _bodyPartCurrentLayer[selectedPart] = SurgeryLayer.Skin;
        }

        // Ensure the body has UserInterfaceComponent
        var uiComp = EnsureComp<UserInterfaceComponent>(body);
        var uiKey = Content.Shared.Medical.Surgery.SurgeryUIKey.Key;
        
        // Ensure the interface is registered (fallback in case it wasn't registered on the body)
        // SetUi is safe to call multiple times - it will just update the existing entry
        _ui.SetUi((body, uiComp), uiKey, new InterfaceData(
            "Content.Client.Medical.Surgery.SurgeryBui", // Full namespace to avoid ambiguity with _Shitmed version
            interactionRange: 2f, // Range check between surgeon and body (body has proper world transform)
            requireInputValidation: true
        ));
        
        // Initial state will be updated by UpdateUI
        _ui.SetUiState((body, uiComp), uiKey, new SurgeryBoundUserInterfaceState(
            GetNetEntity(ent), // Original body part (for reference)
            ent.Comp.PartType,
            ent.Comp.SkinRetracted,
            ent.Comp.TissueRetracted,
            ent.Comp.BonesSawed,
            new List<NetEntity>(),
            new List<NetEntity>(),
            new List<NetEntity>(),
            ent.Comp.BonesSmashed,
            null,
            GetNetEntity(selectedPart),
            selectedTargetPart,
            false,
            false
        ));

        // Update UI with the selected body part
        if (TryComp<SurgeryLayerComponent>(selectedPart, out var selectedLayer))
        {
            UpdateUI((selectedPart, selectedLayer));
        }
        else
        {
            UpdateUI(ent);
        }
        
        _ui.TryOpenUi((body, uiComp), uiKey, user);
        
        // Start material scanning for this UI (track by selected body part)
        _openSurgeryUIs[selectedPart] = _timing.CurTime + TimeSpan.FromSeconds(MaterialScanInterval);
    }

    private void OnSurgeryUIOpened(Entity<SurgeryLayerComponent> ent, ref BoundUIOpenedEvent args)
    {
        // Start material scanning when UI opens
        _openSurgeryUIs[ent] = _timing.CurTime + TimeSpan.FromSeconds(MaterialScanInterval);
    }

    private void OnSurgeryUIClosed(Entity<SurgeryLayerComponent> ent, ref BoundUIClosedEvent args)
    {
        // Stop material scanning when UI closes
        _openSurgeryUIs.Remove(ent);
        
        // Clean up layer tracking for this body part
        _bodyPartCurrentLayer.Remove(ent);
        
        // Clean up selected body part tracking
        if (TryComp<BodyPartComponent>(ent, out var part) && part.Body != null)
        {
            _selectedBodyParts.Remove(part.Body.Value);
            _selectedTargetBodyParts.Remove(part.Body.Value);
        }
        
        // Clean up hand items tracking for the user who closed the UI
        _userHandItems.Remove(args.Actor);
    }

    private void OnHandItemsReceived(Entity<SurgeryLayerComponent> ent, ref SurgeryHandItemsMessage msg)
    {
        // Store hand items for the user who sent the message
        // We need to get the user from the message context
        // For now, we'll track by the body entity that the UI is open on
        // This will be updated when we have access to the user entity
        
        // Get the body entity from the body part
        if (!TryComp<BodyPartComponent>(ent, out var part) || part.Body == null)
            return;

        // Find the user who has this UI open
        if (!TryComp<UserInterfaceComponent>(part.Body.Value, out var uiComp))
            return;

        var actors = _ui.GetActors((part.Body.Value, uiComp), Content.Shared.Medical.Surgery.SurgeryUIKey.Key);
        var actorList = actors.ToList();
        if (actorList.Count > 0)
        {
            var user = actorList[0];
            _userHandItems[user] = msg.HandItems;
            
            // Update UI to reflect new hand items
            UpdateUI(ent);
        }
    }

    private void OnSurgeryUIMessageAttempt(BoundUserInterfaceMessageAttempt args)
    {
        // Only handle surgery UI key
        if (!args.UiKey.Equals(Content.Shared.Medical.Surgery.SurgeryUIKey.Key))
            return;

        // Only handle if target has SurgeryTargetComponent (body entity)
        // The UI is now opened on the body entity, not the body part
        if (!HasComp<SurgeryTargetComponent>(args.Target))
            return;

        // The UI is on the body entity which has a proper world transform,
        // so the range check should work correctly without special handling
    }

    private void OnInRangeOverride(ref InRangeOverrideEvent ev)
    {
        // If the target is a body with surgery UI, we need to check if it's for surgery
        // The UI is now opened on the body entity, so this should work correctly
        // But we still need to handle the case where the range check is being done
        // This handler is mainly for body parts, but since we moved UI to body, it may not be needed
        // However, keep it for safety in case there are edge cases
    }

    private void OnSurgeryUIRangeCheck(Entity<SurgeryTargetComponent> ent, ref BoundUserInterfaceCheckRangeEvent args)
    {
        // If already failed, don't override
        if (args.Result == BoundUserInterfaceRangeResult.Fail)
            return;

        // Only handle surgery UI key
        if (!args.UiKey.Equals(Content.Shared.Medical.Surgery.SurgeryUIKey.Key))
            return;

        // The UI is now opened on the body entity (ent.Owner), which has a proper world transform
        // So the default range check should work fine. But we can still override it if needed.
        // Since the body has a proper transform, the range check should pass automatically.
        // We don't need to do anything special here - the default check will work.
    }

    private void OnSurgeryLayerStartup(EntityUid uid, SurgeryLayerComponent component, ComponentStartup args)
    {
        // Initialize part type if not set
        if (component.PartType == null && TryComp<BodyPartComponent>(uid, out var part))
        {
            component.PartType = part.PartType;
            Dirty(uid, component);
        }
        
        // Note: UI registration is now done on the body entity in OpenSurgeryUI,
        // not on the body part. This matches the old Shitmed system behavior.
    }

    private void OnStepSelected(Entity<SurgeryLayerComponent> ent, ref SurgeryStepSelectedMessage msg)
    {
        var stepEntity = GetEntity(msg.Step);
        if (!TryComp<SurgeryStepComponent>(stepEntity, out var step))
            return;

        // Get user if provided
        EntityUid? user = null;
        if (msg.User != null)
            user = GetEntity(msg.User);

        if (user == null)
            return;

        // Validate step can be performed
        if (!CanPerformStep(ent, stepEntity, step, user))
            return;

        // Start doafter for the surgery step
        StartSurgeryDoAfter(ent, stepEntity, step, user.Value);
    }

    /// <summary>
    /// Starts a doafter for a surgery step execution.
    /// </summary>
    private void StartSurgeryDoAfter(Entity<SurgeryLayerComponent> bodyPart, EntityUid stepEntity, SurgeryStepComponent step, EntityUid user)
    {
        // Get body entity for doafter target
        EntityUid? bodyEntity = null;
        if (TryComp<BodyPartComponent>(bodyPart, out var part) && part.Body != null)
        {
            bodyEntity = part.Body.Value;
        }

        if (bodyEntity == null)
            return;
        
        // Get body part from args.Target (it's passed as target in DoAfterArgs)
        var targetBodyPart = bodyPart;

        // Calculate duration
        float duration = step.Duration;
        
        // Duration can be modified by tool speed in the future if needed
        // For now, use the step's base duration

        // Play start sound
        if (TryComp<HandsComponent>(user, out var userHands))
        {
            foreach (var heldItem in _hands.EnumerateHeld(user, userHands))
            {
                if (TryComp<SurgeryToolComponent>(heldItem, out var tool) && tool.StartSound != null)
                {
                    _audio.PlayPvs(tool.StartSound, heldItem);
                }
            }
        }

        // Face the target
        if (TryComp<TransformComponent>(bodyEntity.Value, out var xform))
        {
            _rotateToFace.TryFaceCoordinates(user, _transform.GetMapCoordinates(bodyEntity.Value, xform).Position);
        }

        // Create doafter event
        var doAfterEvent = new NewSurgeryDoAfterEvent(GetNetEntity(stepEntity), GetNetEntity(bodyPart));
        var doAfterArgs = new DoAfterArgs(EntityManager, user, TimeSpan.FromSeconds(duration), doAfterEvent, bodyEntity.Value, target: bodyPart)
        {
            BreakOnMove = true,
            CancelDuplicate = true,
            DuplicateCondition = DuplicateConditions.SameEvent,
            NeedHand = true,
            BreakOnHandChange = true,
        };

        if (!DoAfter.TryStartDoAfter(doAfterArgs))
            return;

        // Popup will be shown when step completes
    }

    /// <summary>
    /// Handles doafter completion for surgery steps.
    /// </summary>
    private void OnSurgeryDoAfter(Entity<SurgeryLayerComponent> bodyPart, ref NewSurgeryDoAfterEvent args)
    {
        if (args.Cancelled || args.Handled)
            return;

        var stepEntity = GetEntity(args.Step);
        if (!TryComp<SurgeryStepComponent>(stepEntity, out var step))
            return;

        var user = args.User;
        if (user == null)
            return;

        // Get body part from args.Target (passed as target in DoAfterArgs)
        var targetBodyPart = args.Target ?? bodyPart;

        args.Handled = true;

        // Play end sound from tools
        if (TryComp<HandsComponent>(user, out var hands))
        {
            foreach (var heldItem in _hands.EnumerateHeld(user, hands))
            {
                if (TryComp<SurgeryToolComponent>(heldItem, out var tool) && tool.EndSound != null)
                {
                    _audio.PlayPvs(tool.EndSound, heldItem);
                }
            }
        }

        // Execute the step
        ExecuteStep(targetBodyPart, stepEntity, step, user);
    }

    private void OnLayerChanged(Entity<SurgeryLayerComponent> ent, ref SurgeryLayerChangedMessage msg)
    {
        // Store the current layer for this body part
        _bodyPartCurrentLayer[ent] = msg.Layer;
        
        // Update UI when layer changes
        UpdateUI(ent);
    }

    private void OnBodyPartSelected(Entity<SurgeryLayerComponent> ent, ref SurgeryBodyPartSelectedMessage msg)
    {
        // Get the body from the body part
        if (!TryComp<BodyPartComponent>(ent, out var part) || part.Body == null)
            return;

        var body = part.Body.Value;
        EntityUid? selectedPart = null;
        TargetBodyPart? selectedTargetPart = null;
        bool isMissingLimb = false;

        // If a target body part was selected, find the corresponding body part entity
        if (msg.TargetBodyPart != null)
        {
            var (targetType, targetSymmetry) = _body.ConvertTargetBodyPart(msg.TargetBodyPart.Value);
            var bodyParts = _body.GetBodyChildrenOfType(body, targetType, symmetry: targetSymmetry);
            var foundPart = bodyParts.FirstOrDefault();
            
            if (foundPart.Id != default)
            {
                selectedPart = foundPart.Id;
                selectedTargetPart = msg.TargetBodyPart;
            }
            else
            {
                // Limb is missing - we still want to allow selection for attach limb surgery
                // Use the parent part (usually torso) as the base, but mark as missing
                isMissingLimb = true;
                selectedTargetPart = msg.TargetBodyPart;
                
                // For missing limbs, we need to find the parent part where the limb would attach
                // For arms/legs, this is usually the torso
                // For hands/feet, this is the corresponding arm/leg
                if (targetType == BodyPartType.Arm || targetType == BodyPartType.Leg || targetType == BodyPartType.Head)
                {
                    var torsoParts = _body.GetBodyChildrenOfType(body, BodyPartType.Torso);
                    var torso = torsoParts.FirstOrDefault();
                    if (torso.Id != default)
                    {
                        selectedPart = torso.Id;
                    }
                }
                else if (targetType == BodyPartType.Hand)
                {
                    // Find the corresponding arm
                    var armType = targetSymmetry == BodyPartSymmetry.Left ? BodyPartSymmetry.Left : BodyPartSymmetry.Right;
                    var armParts = _body.GetBodyChildrenOfType(body, BodyPartType.Arm, symmetry: armType);
                    var arm = armParts.FirstOrDefault();
                    if (arm.Id != default)
                    {
                        selectedPart = arm.Id;
                    }
                    else
                    {
                        // Arm is also missing, use torso
                        var torsoParts = _body.GetBodyChildrenOfType(body, BodyPartType.Torso);
                        var torso = torsoParts.FirstOrDefault();
                        if (torso.Id != default)
                        {
                            selectedPart = torso.Id;
                        }
                    }
                }
                else if (targetType == BodyPartType.Foot)
                {
                    // Find the corresponding leg
                    var legType = targetSymmetry == BodyPartSymmetry.Left ? BodyPartSymmetry.Left : BodyPartSymmetry.Right;
                    var legParts = _body.GetBodyChildrenOfType(body, BodyPartType.Leg, symmetry: legType);
                    var leg = legParts.FirstOrDefault();
                    if (leg.Id != default)
                    {
                        selectedPart = leg.Id;
                    }
                    else
                    {
                        // Leg is also missing, use torso
                        var torsoParts = _body.GetBodyChildrenOfType(body, BodyPartType.Torso);
                        var torso = torsoParts.FirstOrDefault();
                        if (torso.Id != default)
                        {
                            selectedPart = torso.Id;
                        }
                    }
                }
                
                // Fallback to original body part if we couldn't find a parent
                if (selectedPart == null)
                {
                    selectedPart = ent;
                }
            }
        }
        else
        {
            // No selection - default to the original body part
            selectedPart = ent;
            if (TryComp<BodyPartComponent>(ent, out var entPart))
            {
                selectedTargetPart = _body.GetTargetBodyPart(entPart);
            }
        }

        // If no body part found, don't update
        if (selectedPart == null)
            return;

        // Store the selected body part for this UI (store the target body part info for missing limbs)
        _selectedBodyParts[body] = selectedPart.Value;
        _selectedTargetBodyParts[body] = selectedTargetPart;

        // Determine which layer to switch to
        SurgeryLayer targetLayer = SurgeryLayer.Skin;
        
        // For missing limbs, always start at skin layer (where attach limb surgeries would be)
        if (!isMissingLimb)
        {
            // Check if the selected body part has a stored layer state
            if (_bodyPartCurrentLayer.TryGetValue(selectedPart.Value, out var storedLayer))
            {
                targetLayer = storedLayer;
            }
            else if (TryComp<SurgeryLayerComponent>(selectedPart.Value, out var selectedLayer))
            {
                // If tissue is retracted, we can access tissue layer
                if (selectedLayer.TissueRetracted && (selectedLayer.BonesSawed || selectedLayer.BonesSmashed))
                {
                    targetLayer = SurgeryLayer.Organ;
                }
                else if (selectedLayer.SkinRetracted)
                {
                    targetLayer = SurgeryLayer.Tissue;
                }
                else
                {
                    targetLayer = SurgeryLayer.Skin;
                }
            }
        }

        // Store the layer for the selected body part
        _bodyPartCurrentLayer[selectedPart.Value] = targetLayer;

        // Update UI with the new selected body part
        // Always use the original body part (ent) which has SurgeryLayerComponent
        // UpdateUI will look up the selected part from _selectedBodyParts dictionary
        UpdateUI(ent);
    }

    private void OnOperationMethodSelected(Entity<SurgeryLayerComponent> ent, ref SurgeryOperationMethodSelectedMessage msg)
    {
        var stepEntity = GetEntity(msg.Step);
        // Store the method selection for when the step is executed
        _stepMethodSelection[stepEntity] = msg.IsImprovised;
    }

    /// <summary>
    /// Checks if a surgery step can be performed.
    /// Requirements are now optional - steps can be skipped to allow surgeons
    /// to work around missing tools or incomplete procedures.
    /// Example: Close skin without mending bones if bone-gel is unavailable,
    /// leaving the patient with broken ribs (surgery penalty remains).
    /// </summary>
    private bool CanPerformStep(EntityUid bodyPart, EntityUid stepEntity, SurgeryStepComponent step, EntityUid? user = null)
    {
        if (!TryComp<SurgeryLayerComponent>(bodyPart, out var layer))
            return false;

        // Check part type compatibility (still required - can't do head surgery on torso)
        if (step.ValidPartTypes.Count > 0 && layer.PartType != null)
        {
            if (!step.ValidPartTypes.Contains(layer.PartType.Value))
                return false;
        }

        // If step has an operation, check tool availability
        if (step.OperationId != null && user != null)
        {
            if (!_prototypes.TryIndex(step.OperationId, out var operation))
                return false;

            // Check if this is a repair operation
            if (operation.RepairOperationFor != null)
            {
                // Repair operations can only be performed if corresponding improvised component exists
                if (!HasImprovisedComponentForOperation(bodyPart, operation.RepairOperationFor.Value))
                    return false;
            }

            // For repair operations, we need primary tools (no secondary method)
            if (operation.RepairOperationFor != null)
            {
                return HasPrimaryToolsForOperation(user.Value, operation);
            }

            // For regular operations, check if we have either primary tools OR secondary method available
            return HasPrimaryToolsForOperation(user.Value, operation) ||
                   (operation.SecondaryMethod != null && HasSecondaryMethodForOperation(user.Value, operation));
        }

        // Layer requirements are now optional - steps can be skipped
        // This allows surgeons to work around missing tools or skip steps
        // (e.g., closing skin without mending bones if bone-gel is unavailable)
        // Complications come from using bad tools or bad conditions, not randomness
        return true;
    }

    /// <summary>
    /// Checks if user has any primary tools for the operation.
    /// Medical Multitool counts as having all primary tools.
    /// Also checks for special entity prototype IDs that should work for certain operations.
    /// </summary>
    private bool HasPrimaryToolsForOperation(EntityUid user, SurgeryOperationPrototype operation)
    {
        if (operation.PrimaryTools.Count == 0)
            return true;

        if (!TryComp<HandsComponent>(user, out var hands))
            return false;

        foreach (var heldItem in _hands.EnumerateHeld(user, hands))
        {
            // Check if it's a medical multitool (has all tools)
            if (Tags.HasTag(heldItem, "AdvancedSurgeryTool"))
            {
                // Medical multitool has all primary tools
                return true;
            }

            // Check if item has any of the required primary tool components
            foreach (var toolReg in operation.PrimaryTools)
            {
                // ComponentRegistry is a Dictionary<string, ComponentRegistryEntry>
                // Get the first component name (key) and use it to get the component type
                var componentName = toolReg.Keys.FirstOrDefault();
                if (componentName != null && _componentFactory.TryGetRegistration(componentName, out var reg))
                {
                    if (HasComp(heldItem, reg.Type))
                        return true;
                }
            }

            // Special cases: Check for specific entity prototype IDs that work for certain operations
            var meta = MetaData(heldItem);
            var prototypeId = meta.EntityPrototype?.ID;

            // ScalpelLaser works for Cautery operations (even though it doesn't have Cautery component)
            if (prototypeId == "ScalpelLaser")
            {
                // Check if this operation requires Cautery
                foreach (var toolReg in operation.PrimaryTools)
                {
                    // ComponentRegistry is a Dictionary<string, ComponentRegistryEntry>
                    // Get the first component name (key) and check if it's "Cautery"
                    var componentName = toolReg.Keys.FirstOrDefault();
                    if (componentName == "Cautery")
                    {
                        return true;
                    }
                }
            }
        }

        return false;
    }

    /// <summary>
    /// Checks if user can use secondary method for the operation.
    /// </summary>
    private bool HasSecondaryMethodForOperation(EntityUid user, SurgeryOperationPrototype operation)
    {
        if (operation.SecondaryMethod == null)
            return false;

        var evaluator = EntitySystem.Get<SurgeryOperationEvaluatorSystem>();

        // Handle MultiEvaluator type
        if (operation.SecondaryMethod.Type == "MultiEvaluator" && operation.SecondaryMethod.Evaluators != null)
        {
            var result = evaluator.EvaluateMultiEvaluator(user, operation.SecondaryMethod.Evaluators);
            return result.IsValid;
        }

        // Handle single evaluator
        var singleResult = evaluator.EvaluateSecondaryMethod(
            user,
            operation.SecondaryMethod.Evaluator,
            operation.SecondaryMethod.Tools);

        return singleResult.IsValid;
    }

    /// <summary>
    /// Checks if body part has an improvised component for the given operation.
    /// </summary>
    private bool HasImprovisedComponentForOperation(EntityUid bodyPart, ProtoId<SurgeryOperationPrototype> operationId)
    {
        // Map operation IDs to component types
        // Use local variables for ProtoId (cannot use const or static readonly in method scope)
        var boneRemoval = new ProtoId<SurgeryOperationPrototype>("BoneRemoval");
        var cutTissue = new ProtoId<SurgeryOperationPrototype>("CutTissue");
        var clampBloodVessels = new ProtoId<SurgeryOperationPrototype>("ClampBloodVessels");
        var retractTissue = new ProtoId<SurgeryOperationPrototype>("RetractTissue");
        var cauterizeWounds = new ProtoId<SurgeryOperationPrototype>("CauterizeWounds");
        var severBloodVessels = new ProtoId<SurgeryOperationPrototype>("SeverBloodVessels");
        
        return operationId switch
        {
            _ when operationId == boneRemoval => HasComp<ImprovisedBoneRemovalComponent>(bodyPart),
            _ when operationId == cutTissue => HasComp<ImprovisedTissueCutComponent>(bodyPart),
            _ when operationId == clampBloodVessels => HasComp<ImprovisedBleederClampingComponent>(bodyPart),
            _ when operationId == retractTissue => HasComp<ImprovisedRetractTissueComponent>(bodyPart),
            _ when operationId == cauterizeWounds => HasComp<ImprovisedCauterizationComponent>(bodyPart),
            _ when operationId == severBloodVessels => HasComp<ImprovisedSeverBloodVesselsComponent>(bodyPart),
            _ => false
        };
    }

    /// <summary>
    /// Applies integrity penalty for an improvised surgery step and adds tracking component.
    /// The penalty remains visible until the repair operation removes it.
    /// </summary>
    private void ApplyImprovisedIntegrityCost(EntityUid bodyPart, SurgeryOperationPrototype operation, FixedPoint2 cost)
    {
        if (!TryComp<BodyPartComponent>(bodyPart, out var part) || part.Body == null)
            return;

        // Add tracking component based on operation type
        // Use local variables for ProtoId (cannot use const or static readonly in method scope)
        var boneRemoval = new ProtoId<SurgeryOperationPrototype>("BoneRemoval");
        var cutTissue = new ProtoId<SurgeryOperationPrototype>("CutTissue");
        var clampBloodVessels = new ProtoId<SurgeryOperationPrototype>("ClampBloodVessels");
        var retractTissue = new ProtoId<SurgeryOperationPrototype>("RetractTissue");
        var cauterizeWounds = new ProtoId<SurgeryOperationPrototype>("CauterizeWounds");
        var severBloodVessels = new ProtoId<SurgeryOperationPrototype>("SeverBloodVessels");
        
        ImprovisedSurgeryComponent? improvisedComp = operation.ID switch
        {
            _ when operation.ID == boneRemoval => EnsureComp<ImprovisedBoneRemovalComponent>(bodyPart),
            _ when operation.ID == cutTissue => EnsureComp<ImprovisedTissueCutComponent>(bodyPart),
            _ when operation.ID == clampBloodVessels => EnsureComp<ImprovisedBleederClampingComponent>(bodyPart),
            _ when operation.ID == retractTissue => EnsureComp<ImprovisedRetractTissueComponent>(bodyPart),
            _ when operation.ID == cauterizeWounds => EnsureComp<ImprovisedCauterizationComponent>(bodyPart),
            _ when operation.ID == severBloodVessels => EnsureComp<ImprovisedSeverBloodVesselsComponent>(bodyPart),
            _ => null
        };

        if (improvisedComp != null)
        {
            improvisedComp.IntegrityCost = cost;
            improvisedComp.OperationId = operation.ID;
            Dirty(bodyPart, improvisedComp);

            // Apply as a visible surgery penalty that can be scanned by health analyzer
            ApplySurgeryPenalty(bodyPart, cost);
        }
    }

    /// <summary>
    /// Handles repair operation execution - removes the penalty by removing the improvised component.
    /// The component removal triggers penalty removal automatically.
    /// </summary>
    private void HandleRepairOperation(EntityUid bodyPart, SurgeryOperationPrototype repairOperation)
    {
        if (repairOperation.RepairOperationFor == null)
            return;

        // Find and remove the improvised component
        // The component stores the penalty amount, so removing it will remove the penalty
        // Use local variables for ProtoId (cannot use const or static readonly in method scope)
        var boneRemoval = new ProtoId<SurgeryOperationPrototype>("BoneRemoval");
        var cutTissue = new ProtoId<SurgeryOperationPrototype>("CutTissue");
        var clampBloodVessels = new ProtoId<SurgeryOperationPrototype>("ClampBloodVessels");
        var retractTissue = new ProtoId<SurgeryOperationPrototype>("RetractTissue");
        var cauterizeWounds = new ProtoId<SurgeryOperationPrototype>("CauterizeWounds");
        var severBloodVessels = new ProtoId<SurgeryOperationPrototype>("SeverBloodVessels");
        
        ImprovisedSurgeryComponent? improvisedComp = repairOperation.RepairOperationFor.Value switch
        {
            _ when repairOperation.RepairOperationFor.Value == boneRemoval => CompOrNull<ImprovisedBoneRemovalComponent>(bodyPart),
            _ when repairOperation.RepairOperationFor.Value == cutTissue => CompOrNull<ImprovisedTissueCutComponent>(bodyPart),
            _ when repairOperation.RepairOperationFor.Value == clampBloodVessels => CompOrNull<ImprovisedBleederClampingComponent>(bodyPart),
            _ when repairOperation.RepairOperationFor.Value == retractTissue => CompOrNull<ImprovisedRetractTissueComponent>(bodyPart),
            _ when repairOperation.RepairOperationFor.Value == cauterizeWounds => CompOrNull<ImprovisedCauterizationComponent>(bodyPart),
            _ when repairOperation.RepairOperationFor.Value == severBloodVessels => CompOrNull<ImprovisedSeverBloodVesselsComponent>(bodyPart),
            _ => null
        };

        if (improvisedComp == null)
            return;

        // Get the penalty amount from the component
        var penaltyAmount = improvisedComp.IntegrityCost;

        // Remove the improvised component first
        RemComp(bodyPart, improvisedComp.GetType());

        // Remove the penalty that was added by this improvised surgery
        if (penaltyAmount > FixedPoint2.Zero)
        {
            RemoveSurgeryPenalty(bodyPart, penaltyAmount);
        }
    }

    private void ExecuteStep(EntityUid bodyPart, EntityUid stepEntity, SurgeryStepComponent step, EntityUid? user = null)
    {
        // Handle operation-based steps
        bool isImprovised = false;
        float speedModifier = 1.0f;
        
        if (step.OperationId != null && user != null && 
            _prototypes.TryIndex(step.OperationId, out var operation))
        {
            // Check if this is a repair operation
            if (operation.RepairOperationFor != null)
            {
                // Repair operation - remove integrity cost and improvised component
                HandleRepairOperation(bodyPart, operation);
            }
            else
            {
                // Regular operation - check which method is being used
                bool usingPrimary = HasPrimaryToolsForOperation(user.Value, operation);
                bool usingSecondary = operation.SecondaryMethod != null && HasSecondaryMethodForOperation(user.Value, operation);
                
                // Check if method was explicitly selected, otherwise default to primary if available
                if (_stepMethodSelection.TryGetValue(stepEntity, out var wasImprovised))
                {
                    isImprovised = wasImprovised;
                }
                else
                {
                    // Default: use primary if available, otherwise use secondary
                    isImprovised = !usingPrimary && usingSecondary;
                }
                
                // Apply operation-specific logic
                if (isImprovised && operation.SecondaryMethod != null)
                {
                    // Apply integrity cost for improvised method
                    if (operation.SecondaryMethod.IntegrityCost > FixedPoint2.Zero)
                    {
                        ApplyImprovisedIntegrityCost(bodyPart, operation, operation.SecondaryMethod.IntegrityCost);
                    }
                    
                    // Get speed modifier from evaluator
                    var evaluator = EntitySystem.Get<SurgeryOperationEvaluatorSystem>();
                    SurgeryOperationEvaluationResult evalResult;
                    
                    if (operation.SecondaryMethod.Type == "MultiEvaluator" && operation.SecondaryMethod.Evaluators != null)
                    {
                        evalResult = evaluator.EvaluateMultiEvaluator(user.Value, operation.SecondaryMethod.Evaluators);
                    }
                    else
                    {
                        evalResult = evaluator.EvaluateSecondaryMethod(
                            user.Value,
                            operation.SecondaryMethod.Evaluator,
                            operation.SecondaryMethod.Tools);
                    }
                    
                    if (evalResult.IsValid)
                    {
                        speedModifier = evalResult.SpeedModifier;
                    }
                }
                
                // Clear method selection after use
                _stepMethodSelection.Remove(stepEntity);
            }
        }
        
        // Note: Speed modifier from evaluators could be applied to step duration if needed
        // For now, the speed is determined by the tool itself in the existing system
        
        // Check if user has medical skill
        bool hasMedicalSkill = user != null && HasMedicalSkill(user.Value);
        
        // Apply unskilled surgery penalty if non-medical personnel performs surgery
        if (!hasMedicalSkill && user != null)
        {
            // Apply +2 bio-rejection penalty for unskilled surgery
            // This penalty persists until a medical professional fixes it
            if (!HasComp<UnskilledSurgeryPenaltyComponent>(bodyPart))
            {
                var unskilledPenalty = EnsureComp<UnskilledSurgeryPenaltyComponent>(bodyPart);
                Dirty(bodyPart, unskilledPenalty);
                
                    // Update cached surgery penalty and recalculate bio-rejection
                if (TryComp<BodyPartComponent>(bodyPart, out var part) && part.Body != null)
                {
                    if (TryComp<IntegrityComponent>(part.Body.Value, out var integrity))
                    {
                        _vitality.UpdateCachedSurgeryPenalty(part.Body.Value, integrity);
                        _integrity.RecalculateTargetBioRejection(part.Body.Value, integrity);
                    }
                    
                    // Notify about slower speed and penalty
                    _popup.PopupEntity(Loc.GetString("surgery-unskilled-penalty-applied"), user.Value, user.Value);
                }
            }
            else
            {
                // Already has penalty, just notify about slower speed
                _popup.PopupEntity(Loc.GetString("surgery-unskilled-slower-speed"), user.Value, user.Value);
            }
        }
        
        // Check for cyberlimb maintenance steps that require skilled technician
        // Specifically: "Adjust Bolts" (Tissue layer) and "Replace Wiring" (Organ layer)
        var stepMeta = MetaData(stepEntity);
        var stepId = stepMeta.EntityPrototype?.ID ?? "";
        var stepName = stepMeta.EntityName ?? "";
        
        // Check if this is a cyberlimb (has CyberLimbMaintenanceComponent)
        bool isCyberlimb = HasComp<CyberLimbMaintenanceComponent>(bodyPart);
        
        // Check if this is one of the two maintenance steps that require skilled technician
        bool isAdjustBoltsStep = (stepId.Contains("Adjust") && stepId.Contains("Bolt")) ||
                                  (stepName.Contains("Adjust") && stepName.Contains("Bolt"));
        bool isReplaceWiringStep = (stepId.Contains("Replace") && stepId.Contains("Wiring")) ||
                                    (stepName.Contains("Replace") && stepName.Contains("Wiring"));
        bool isCyberlimbMaintenanceStep = isAdjustBoltsStep || isReplaceWiringStep;
        
        if (isCyberlimbMaintenanceStep && isCyberlimb && user != null)
        {
            bool hasSkilledTechnician = HasComp<SkilledTechnicianComponent>(user.Value);
            
            if (!hasSkilledTechnician)
            {
                // Apply unskilled technician penalty
                if (!HasComp<UnskilledTechnicianPenaltyComponent>(bodyPart))
                {
                    var unskilledTechPenalty = EnsureComp<UnskilledTechnicianPenaltyComponent>(bodyPart);
                    Dirty(bodyPart, unskilledTechPenalty);
                    
                    // Update cached surgery penalty and recalculate bio-rejection
                    if (TryComp<BodyPartComponent>(bodyPart, out var part) && part.Body != null)
                    {
                        if (TryComp<IntegrityComponent>(part.Body.Value, out var integrity))
                        {
                            _vitality.UpdateCachedSurgeryPenalty(part.Body.Value, integrity);
                            _integrity.RecalculateTargetBioRejection(part.Body.Value, integrity);
                        }
                        
                        _popup.PopupEntity(Loc.GetString("surgery-unskilled-technician-penalty"), user.Value, user.Value);
                    }
                }
            }
            else
            {
                // Skilled technician performing the step - remove unskilled penalty if present
                if (HasComp<UnskilledTechnicianPenaltyComponent>(bodyPart))
                {
                    RemComp<UnskilledTechnicianPenaltyComponent>(bodyPart);
                    
                    // Update cached surgery penalty and recalculate bio-rejection
                    if (TryComp<BodyPartComponent>(bodyPart, out var part) && part.Body != null)
                    {
                        if (TryComp<IntegrityComponent>(part.Body.Value, out var integrity))
                        {
                            _vitality.UpdateCachedSurgeryPenalty(part.Body.Value, integrity);
                            _integrity.RecalculateTargetBioRejection(part.Body.Value, integrity);
                        }
                        
                        _popup.PopupEntity(Loc.GetString("surgery-skilled-technician-fixed"), user.Value, user.Value);
                    }
                }
            }
        }
        
        // Apply step effects
        if (step.Add != null)
        {
            foreach (var (compType, comp) in step.Add)
            {
                var compReg = new ComponentRegistry();
                compReg.Add(compType, comp);
                EntityManager.AddComponents(bodyPart, compReg);
            }
        }

        if (step.Remove != null)
        {
            foreach (var (compName, _) in step.Remove)
            {
                if (_componentFactory.TryGetRegistration(compName, out var registration))
                {
                    EntityManager.RemoveComponent(bodyPart, registration.Type);
                }
            }
        }

        // Update layer state based on step layer
        if (TryComp<SurgeryLayerComponent>(bodyPart, out var layer))
        {
            switch (step.Layer)
            {
                case SurgeryLayer.Skin:
                    var meta = MetaData(stepEntity);
                    var wasSkinRetracted = layer.SkinRetracted;
                    var skinStepId = meta.EntityPrototype?.ID ?? "";
                    
                    // Handle cybernetics maintenance panel state changes (before skin retraction logic)
                    HandleCyberneticsMaintenanceSteps(bodyPart, stepEntity, step, meta);
                    
                    // Check if this is a step that closes skin (reverses retraction)
                    if (skinStepId.Contains("CloseSkin"))
                    {
                        // Skin is being closed - check if this is the final step
                        var progress = CompOrNull<SurgeryStepProgressComponent>(bodyPart);
                        if (progress != null)
                        {
                            var closeProgress = progress.SequenceProgress.GetValueOrDefault("CloseSkinSequence", -1);
                            // If this is the last step (index 1), fully close skin
                            if (closeProgress >= 0 && step.SequenceIndex == 1)
                            {
                                layer.SkinRetracted = false;
                                RemoveSurgeryPenalty(bodyPart, FixedPoint2.New(1)); // Remove skin penalty
                            }
                        }
                        else if (wasSkinRetracted)
                        {
                            // Fallback: if no progress tracking, just close
                            layer.SkinRetracted = false;
                            RemoveSurgeryPenalty(bodyPart, FixedPoint2.New(1));
                        }
                        // Note: If bones are still sawed or tissue still retracted, those penalties remain
                        // This allows surgeons to close skin with broken ribs (penalty persists)
                    }
                    else if (skinStepId.Contains("RetractSkin"))
                    {
                        // Retracting skin - check if this is the final step
                        var progress = CompOrNull<SurgeryStepProgressComponent>(bodyPart);
                        if (progress != null)
                        {
                            var retractProgress = progress.SequenceProgress.GetValueOrDefault("RetractSkinSequence", -1);
                            // If this is the last step (index 1), fully retract skin
                            if (retractProgress >= 0 && step.SequenceIndex == 1 && !wasSkinRetracted)
                            {
                                layer.SkinRetracted = true;
                                ApplySurgeryPenalty(bodyPart, FixedPoint2.New(1)); // +1 for skin
                            }
                        }
                        else if (!wasSkinRetracted)
                        {
                            // Fallback: if no progress tracking, just retract
                            layer.SkinRetracted = true;
                            ApplySurgeryPenalty(bodyPart, FixedPoint2.New(1)); // +1 for skin
                        }
                    }
                    break;
                case SurgeryLayer.Tissue:
                    var tissueMeta = MetaData(stepEntity);
                    var tissueStepId = tissueMeta.EntityPrototype?.ID ?? "";
                    
                    // Handle implant removal steps (tissue layer)
                    if (tissueStepId.Contains("RemoveImplant") && !tissueStepId.Contains("Organ"))
                    {
                        if (!TryComp<BodyPartComponent>(bodyPart, out var implantPartComp) || implantPartComp.Body == null)
                            break;

                        // Get implants in body part
                        var implants = GetImplantsInBodyPart(bodyPart, SurgeryLayer.Tissue);
                        if (implants.Count > 0)
                        {
                            // Remove first implant (or could be more specific based on step metadata)
                            var (implant, _, _) = implants[0];
                            
                            // Remove implant from body
                            if (TryComp<ImplantedComponent>(implantPartComp.Body.Value, out var implanted))
                            {
                                if (implanted.ImplantContainer.Contains(implant))
                                {
                                    _containers.Remove((implant, null, null), implanted.ImplantContainer);
                                    
                                    // Try to pick up the implant if user is available
                                    if (user != null)
                                    {
                                        _hands.TryPickupAnyHand(user.Value, implant);
                                    }
                                }
                            }
                        }
                    }
                    // Handle implant insertion steps (tissue layer)
                    else if (tissueStepId.Contains("AddImplant") && !tissueStepId.Contains("Organ"))
                    {
                        if (!TryComp<BodyPartComponent>(bodyPart, out var addImplantPartComp) || addImplantPartComp.Body == null)
                            break;

                        EntityUid? implantToAdd = null;
                        
                        // Look for implant in user's hands first
                        if (user != null)
                        {
                            var hands = _hands.EnumerateHeld(user.Value);
                            foreach (var hand in hands)
                            {
                                if (HasComp<SubdermalImplantComponent>(hand) && !Tags.HasTag(hand, "Organ"))
                                {
                                    implantToAdd = hand;
                                    break;
                                }
                            }
                        }
                        
                        // If not found in hands, scan nearby items
                        if (implantToAdd == null)
                        {
                            var xform = Transform(bodyPart);
                            var nearbyEntities = _lookup.GetEntitiesInRange(xform.Coordinates, MaterialScanRange);
                            foreach (var nearby in nearbyEntities)
                            {
                                if (HasComp<SubdermalImplantComponent>(nearby) && !Tags.HasTag(nearby, "Organ"))
                                {
                                    implantToAdd = nearby;
                                    break;
                                }
                            }
                        }
                        
                        // Add the implant if found
                        if (implantToAdd != null && addImplantPartComp.Body != null)
                        {
                            var implanted = EnsureComp<ImplantedComponent>(addImplantPartComp.Body.Value);
                            if (!implanted.ImplantContainer.Contains(implantToAdd.Value))
                            {
                                _containers.Insert((implantToAdd.Value, null, null), implanted.ImplantContainer);
                            }
                        }
                    }
                    // Existing tissue layer logic (bone sawing, etc.)
                    else
                    {
                        var wasTissueRetracted = layer.TissueRetracted;
                        var wasBonesSawed = layer.BonesSawed;
                        
                        // Handle cybernetics maintenance panel state changes (for replace wiring step)
                        HandleCyberneticsMaintenanceSteps(bodyPart, stepEntity, step, tissueMeta);
                        
                        // Check if this is a step that closes tissue (reverses retraction)
                        if (tissueMeta.EntityPrototype?.ID.Contains("Close") == true)
                        {
                            // Tissue is being closed - remove tissue penalty
                            if (wasTissueRetracted)
                            {
                                layer.TissueRetracted = false;
                                RemoveSurgeryPenalty(bodyPart, FixedPoint2.New(1)); // Remove tissue penalty
                            }
                        }
                        else if (!wasTissueRetracted)
                        {
                            // Retracting tissue for the first time - apply +1 bio-rejection penalty
                            layer.TissueRetracted = true;
                            ApplySurgeryPenalty(bodyPart, FixedPoint2.New(1)); // +1 for tissue
                            
                            // Apply unsanitary conditions penalty when going below skin level
                            if (TryComp<BodyPartComponent>(bodyPart, out var part) && part.Body != null)
                            {
                                var cleanlinessSystem = EntitySystem.Get<RoomCleanlinessSystem>();
                                cleanlinessSystem.ApplyUnsanitaryPenalty(part.Body.Value);
                            }
                        }
                        
                        // Check if this is a bone-sawing step by checking the step ID
                        // Make sure it's not a smashing step
                        if ((tissueMeta.EntityPrototype?.ID.Contains("Saw") == true ||
                             tissueMeta.EntityPrototype?.ID.Contains("Skull") == true) &&
                            tissueMeta.EntityPrototype?.ID.Contains("Smash") != true)
                        {
                            if (!wasBonesSawed && !layer.BonesSmashed)
                            {
                                layer.BonesSawed = true;
                                // Apply +8 bio-rejection penalty when bones are sawed through
                                ApplySurgeryPenalty(bodyPart, FixedPoint2.New(8)); // +8 for bones
                            }
                        }
                        // Check if this is a bone-smashing step (crude surgery)
                        else if (tissueMeta.EntityPrototype?.ID.Contains("Smash") == true ||
                                 tissueMeta.EntityPrototype?.ID.Contains("Crude") == true)
                        {
                            if (!wasBonesSawed && !layer.BonesSmashed)
                            {
                                // Calculate speed based on held item's blunt damage
                                float speed = 1.0f; // Default speed (10 blunt = average)
                                if (user != null && TryComp<HandsComponent>(user, out var hands))
                                {
                                    if (_hands.TryGetActiveItem((user.Value, hands), out var heldItem))
                                    {
                                        // Check if item is a melee weapon with damage
                                        if (TryComp<MeleeWeaponComponent>(heldItem, out var melee))
                                        {
                                            // Get blunt damage from the melee weapon
                                            if (melee.Damage.DamageDict.TryGetValue("Blunt", out var bluntDamage))
                                            {
                                                // 10 blunt = average speed (1.0), scale accordingly
                                                speed = (float)bluntDamage / 10.0f;
                                                if (speed < 0.1f) speed = 0.1f; // Minimum speed
                                                if (speed > 3.0f) speed = 3.0f; // Maximum speed
                                            }
                                        }
                                    }
                                }

                                layer.BonesSmashed = true;
                                // Apply 2x penalty for smashed bones (+16 instead of +8)
                                ApplySurgeryPenalty(bodyPart, FixedPoint2.New(16)); // +16 for smashed bones
                                
                                // Duration is affected by speed (faster with higher blunt damage)
                                // This is handled by the step's duration field, but we could modify it here if needed
                            }
                        }
                        // Check if this is a step that closes/mends bones (for sawed bones)
                        else if (tissueMeta.EntityPrototype?.ID.Contains("Close") == true ||
                                 tissueMeta.EntityPrototype?.ID.Contains("Mend") == true)
                        {
                            // Bones are being closed (for sawed bones)
                            if (wasBonesSawed)
                            {
                                layer.BonesSawed = false;
                                RemoveSurgeryPenalty(bodyPart, FixedPoint2.New(8)); // Remove sawed bone penalty
                            }
                        }
                        // Check if this is a bone repair step (for smashed bones - 5 stages)
                        else if (tissueMeta.EntityPrototype?.ID.Contains("Repair") == true)
                        {
                            if (layer.BonesSmashed)
                            {
                                // Check if this is the final repair step (Stage 5)
                                if (tissueMeta.EntityPrototype?.ID.Contains("Stage5") == true ||
                                    tissueMeta.EntityPrototype?.ID.Contains("Final") == true ||
                                    tissueMeta.EntityPrototype?.ID.Contains("Complete") == true)
                                {
                                    // Final stage - remove smashed bone penalty
                                    layer.BonesSmashed = false;
                                    RemoveSurgeryPenalty(bodyPart, FixedPoint2.New(16)); // Remove smashed bone penalty
                                }
                                // Otherwise, it's an intermediate repair step (stages 1-4)
                                // Don't remove penalty yet, just progress the repair
                            }
                        }
                    }
                    break;
                case SurgeryLayer.Organ:
                    var organMeta = MetaData(stepEntity);
                    var organStepId = organMeta.EntityPrototype?.ID ?? "";
                    
                    // Handle organ implant removal steps
                    if (organStepId.Contains("RemoveOrganImplant"))
                    {
                        if (!TryComp<BodyPartComponent>(bodyPart, out var organImplantPartComp) || organImplantPartComp.Body == null)
                            break;

                        // Get organ implants in body part
                        var organImplants = GetImplantsInBodyPart(bodyPart, SurgeryLayer.Organ);
                        if (organImplants.Count > 0)
                        {
                            // Remove first organ implant
                            var (implant, _, _) = organImplants[0];
                            
                            // Remove implant from body
                            if (TryComp<ImplantedComponent>(organImplantPartComp.Body.Value, out var implanted))
                            {
                                if (implanted.ImplantContainer.Contains(implant))
                                {
                                    _containers.Remove((implant, null, null), implanted.ImplantContainer);
                                    
                                    // Try to pick up the implant if user is available
                                    if (user != null)
                                    {
                                        _hands.TryPickupAnyHand(user.Value, implant);
                                    }
                                }
                            }
                        }
                    }
                    // Handle organ implant insertion steps
                    else if (organStepId.Contains("AddOrganImplant"))
                    {
                        if (!TryComp<BodyPartComponent>(bodyPart, out var addOrganImplantPartComp) || addOrganImplantPartComp.Body == null)
                            break;

                        EntityUid? organImplantToAdd = null;
                        
                        // Look for organ implant in user's hands first
                        if (user != null)
                        {
                            var hands = _hands.EnumerateHeld(user.Value);
                            foreach (var hand in hands)
                            {
                                if (HasComp<SubdermalImplantComponent>(hand) && Tags.HasTag(hand, "Organ"))
                                {
                                    organImplantToAdd = hand;
                                    break;
                                }
                            }
                        }
                        
                        // If not found in hands, scan nearby items
                        if (organImplantToAdd == null)
                        {
                            var xform = Transform(bodyPart);
                            var nearbyEntities = _lookup.GetEntitiesInRange(xform.Coordinates, MaterialScanRange);
                            foreach (var nearby in nearbyEntities)
                            {
                                if (HasComp<SubdermalImplantComponent>(nearby) && Tags.HasTag(nearby, "Organ"))
                                {
                                    organImplantToAdd = nearby;
                                    break;
                                }
                            }
                        }
                        
                        // Add the organ implant if found
                        if (organImplantToAdd != null && addOrganImplantPartComp.Body != null)
                        {
                            var implanted = EnsureComp<ImplantedComponent>(addOrganImplantPartComp.Body.Value);
                            if (!implanted.ImplantContainer.Contains(organImplantToAdd.Value))
                            {
                                _containers.Insert((organImplantToAdd.Value, null, null), implanted.ImplantContainer);
                            }
                        }
                    }
                    // Handle organ removal steps
                    else if (organStepId.Contains("RemoveOrgan") && !organStepId.Contains("Implant"))
                    {
                        if (!TryComp<BodyPartComponent>(bodyPart, out var organPartComp) || organPartComp.Body == null)
                            break;
                        
                        // Find the organ to remove based on TargetOrganSlot
                        if (step.TargetOrganSlot != null)
                        {
                            var organs = _body.GetPartOrgans(bodyPart, organPartComp);
                            foreach (var (organUid, organ) in organs)
                            {
                                if (organ.SlotId == step.TargetOrganSlot)
                                {
                                    // Remove the organ - this will trigger OrganRemovedFromBodyEvent
                                    // which BrainSystem listens to for mind swapping
                                    if (_body.RemoveOrgan(organUid, organ))
                                    {
                                        // Try to pick up the organ if user is available
                                        if (user != null)
                                        {
                                            _hands.TryPickupAnyHand(user.Value, organUid);
                                        }
                                    }
                                    break;
                                }
                            }
                        }
                        else
                        {
                            // Generic organ removal - remove first organ found
                            var organs = _body.GetPartOrgans(bodyPart, organPartComp);
                            var firstOrgan = organs.FirstOrDefault();
                            if (firstOrgan != default)
                            {
                                var (organUid, organ) = firstOrgan;
                                if (_body.RemoveOrgan(organUid, organ))
                                {
                                    if (user != null)
                                    {
                                        _hands.TryPickupAnyHand(user.Value, organUid);
                                    }
                                }
                            }
                        }
                    }
                    // Handle organ insertion steps
                    else if (organStepId.Contains("InsertOrgan") && !organStepId.Contains("Implant"))
                    {
                        if (!TryComp<BodyPartComponent>(bodyPart, out var insertPartComp) || insertPartComp.Body == null)
                            break;
                        
                        EntityUid? organToInsert = null;
                        var targetSlot = step.TargetOrganSlot;
                        
                        // Look for organ in user's hands first
                        if (user != null)
                        {
                            var hands = _hands.EnumerateHeld(user.Value);
                            foreach (var hand in hands)
                            {
                                if (TryComp<OrganComponent>(hand, out var organ))
                                {
                                    // Check if this organ matches the target slot
                                    if (targetSlot == null || organ.SlotId == targetSlot)
                                    {
                                        organToInsert = hand;
                                        break;
                                    }
                                }
                            }
                        }
                        
                        // If not found in hands, scan nearby items
                        if (organToInsert == null)
                        {
                            var xform = Transform(bodyPart);
                            var nearbyEntities = _lookup.GetEntitiesInRange(xform.Coordinates, MaterialScanRange);
                            foreach (var nearby in nearbyEntities)
                            {
                                if (TryComp<OrganComponent>(nearby, out var organ))
                                {
                                    // Check if this organ matches the target slot
                                    if (targetSlot == null || organ.SlotId == targetSlot)
                                    {
                                        organToInsert = nearby;
                                        break;
                                    }
                                }
                            }
                        }
                        
                        // Insert the organ if found - this will trigger OrganAddedToBodyEvent
                        // which BrainSystem listens to for mind swapping
                        if (organToInsert != null)
                        {
                            TryInstallImplant(organToInsert.Value, insertPartComp.Body.Value, bodyPart, user);
                        }
                        else if (targetSlot != null && user.HasValue)
                        {
                            // Organ not found but slot specified - show message
                            _popup.PopupEntity(Loc.GetString("surgery-organ-not-found", ("slot", targetSlot)), user.Value, user.Value);
                        }
                    }
                    
                    // Check if this is the "Replace Wiring" step for cyber-limb maintenance
                    bool isReplaceWiring = (organMeta.EntityPrototype?.ID.Contains("Replace") == true &&
                                           organMeta.EntityPrototype?.ID.Contains("Wiring") == true) ||
                                          (organMeta.EntityName?.Contains("Replace") == true &&
                                           organMeta.EntityName?.Contains("Wiring") == true);
                    
                    if (isReplaceWiring && HasComp<CyberLimbStorageComponent>(bodyPart))
                    {
                        // Reset service time to maximum for this specific limb
                        if (TryComp<CyberLimbStorageComponent>(bodyPart, out var limbStorage))
                        {
                            limbStorage.ServiceTimeRemaining = limbStorage.MaxServiceTime;
                            limbStorage.IsServiceTimeExpired = false;
                            limbStorage.NeedsServiceTimeUpdate = true;
                            limbStorage.LastServiceTimeUpdate = _timing.CurTime;
                            Dirty(bodyPart, limbStorage);
                            
                            // Update next expiration time in integrity component
                            if (TryComp<BodyPartComponent>(bodyPart, out var part) && part.Body != null)
                            {
                                _cyberLimbStats.UpdateNextServiceTimeExpiration(part.Body.Value);
                            }
                            
                            if (user != null)
                            {
                                _popup.PopupEntity(Loc.GetString("cyberlimb-maintenance-wiring-replaced"), bodyPart, user.Value);
                            }
                        }
                    }
                    
                    // Handle cybernetics maintenance panel state changes
                    HandleCyberneticsMaintenanceSteps(bodyPart, stepEntity, step, organMeta);
                    
                    // Check if this is the "Treat Unsanitary Conditions" step
                    bool isTreatUnsanitary = (organMeta.EntityPrototype?.ID.Contains("Treat") == true &&
                                             organMeta.EntityPrototype?.ID.Contains("Unsanitary") == true) ||
                                            (organMeta.EntityName?.Contains("Treat") == true &&
                                             organMeta.EntityName?.Contains("Unsanitary") == true);
                    
                    if (isTreatUnsanitary && TryComp<BodyPartComponent>(bodyPart, out var treatPart) && treatPart.Body != null)
                    {
                        var cleanlinessSystem = EntitySystem.Get<RoomCleanlinessSystem>();
                        cleanlinessSystem.TreatUnsanitaryConditions(treatPart.Body.Value);
                        
                        if (user != null)
                        {
                            _popup.PopupEntity(Loc.GetString("surgery-unsanitary-conditions-treated"), treatPart.Body.Value, user.Value);
                        }
                    }
                    break;
            }
            Dirty(bodyPart, layer);
        }

        // Raise SurgeryStepEvent for compatibility with shitmed effect components (e.g., SurgeryTendWoundsEffectComponent)
        if (user != null && TryComp<BodyPartComponent>(bodyPart, out var partComp) && partComp.Body != null)
        {
            // Get tools from user's hands
            var tools = new List<EntityUid>();
            var hands = _hands.EnumerateHeld(user.Value);
            foreach (var hand in hands)
            {
                tools.Add(hand);
            }
            
            // Raise event on step entity and user for effect components to handle
            var stepEvent = new SurgeryStepEvent(
                user.Value,
                partComp.Body.Value,
                bodyPart,
                tools,
                stepEntity, // Use stepEntity as surgery entity (new system doesn't have separate surgery entities)
                stepEntity,
                false // Complete flag - could be enhanced to check if step is actually complete
            );
            RaiseLocalEvent(stepEntity, ref stepEvent);
            RaiseLocalEvent(user.Value, ref stepEvent);
        }

        // Track step progress for bidirectional operations
        TrackStepProgress(bodyPart, stepEntity, step);

        // Update UI - ensure layer component exists
        if (TryComp<SurgeryLayerComponent>(bodyPart, out var layerForUI))
        {
            UpdateUI((bodyPart, layerForUI));
        }
    }

    /// <summary>
    /// Tracks step completion progress for bidirectional sequences.
    /// </summary>
    private void TrackStepProgress(EntityUid bodyPart, EntityUid stepEntity, SurgeryStepComponent step)
    {
        var progress = EnsureComp<SurgeryStepProgressComponent>(bodyPart);
        var stepMeta = MetaData(stepEntity);
        var stepId = stepMeta.EntityPrototype?.ID ?? "";

        // Track completed step
        if (!progress.CompletedSteps.Contains(stepId))
        {
            progress.CompletedSteps.Add(stepId);
        }

        // If this step belongs to a sequence, update sequence progress
        if (!string.IsNullOrEmpty(step.SequenceId) && step.SequenceIndex >= 0)
        {
            var currentProgress = progress.SequenceProgress.GetValueOrDefault(step.SequenceId, -1);
            
            // Update progress to the completed step index
            if (step.SequenceIndex > currentProgress)
            {
                progress.SequenceProgress[step.SequenceId] = step.SequenceIndex;
            }

            // Initialize sequence steps mapping if needed
            if (!progress.SequenceSteps.ContainsKey(step.SequenceId))
            {
                var sequenceProtoId = new ProtoId<SurgerySequencePrototype>(step.SequenceId);
                if (_prototypes.TryIndex(sequenceProtoId, out var sequence))
                {
                    var stepIds = sequence.ForwardSteps.Select(s => s.ToString()).ToList();
                    progress.SequenceSteps[step.SequenceId] = stepIds;
                }
            }
        }

        Dirty(bodyPart, progress);
    }

    /// <summary>
    /// Generates dynamic surgery steps based on current state, implants, organs, and hand items.
    /// </summary>
    private (List<NetEntity> SkinSteps, List<NetEntity> TissueSteps, List<NetEntity> OrganSteps) GenerateDynamicSteps(
        EntityUid selectedPart,
        SurgeryLayerComponent selectedLayer,
        EntityUid? evalUser)
    {
        var skinSteps = new List<NetEntity>();
        var tissueSteps = new List<NetEntity>();
        var organSteps = new List<NetEntity>();

        // Get step progress component
        var progress = CompOrNull<SurgeryStepProgressComponent>(selectedPart);
        if (progress == null)
        {
            progress = EnsureComp<SurgeryStepProgressComponent>(selectedPart);
        }

        // Get hand items for the user
        List<(NetEntity Item, bool IsImplant, bool IsOrgan, string Name)> handItems = new();
        if (evalUser != null && _userHandItems.TryGetValue(evalUser.Value, out var items))
        {
            handItems = items;
        }

        // SKIN LAYER STEPS
        GenerateSkinLayerSteps(selectedPart, selectedLayer, progress, skinSteps);

        // TISSUE LAYER STEPS
        if (selectedLayer.SkinRetracted)
        {
            GenerateTissueLayerSteps(selectedPart, selectedLayer, progress, handItems, tissueSteps);
        }

        // ORGAN LAYER STEPS
        if (selectedLayer.TissueRetracted && (selectedLayer.BonesSawed || selectedLayer.BonesSmashed))
        {
            GenerateOrganLayerSteps(selectedPart, selectedLayer, progress, handItems, organSteps);
        }

        return (skinSteps, tissueSteps, organSteps);
    }

    /// <summary>
    /// Generates steps for the skin layer: Retract Skin, Close Skin (if retract started), Mend Brute/Burn.
    /// </summary>
    private void GenerateSkinLayerSteps(
        EntityUid selectedPart,
        SurgeryLayerComponent selectedLayer,
        SurgeryStepProgressComponent progress,
        List<NetEntity> skinSteps)
    {
        // Retract Skin sequence - always available if not fully retracted
        var retractProgress = progress.SequenceProgress.GetValueOrDefault("RetractSkinSequence", -1);
        var closeProgress = progress.SequenceProgress.GetValueOrDefault("CloseSkinSequence", -1);
        
        // Get available steps for bidirectional sequence
        var retractSteps = GetAvailableStepsForSequence("RetractSkinSequence", "CloseSkinSequence", retractProgress, closeProgress, 2);
        foreach (var step in retractSteps)
        {
            skinSteps.Add(GetNetEntity(step));
        }

        // Mend Brute Damage - repeatable healing step
        if (TrySpawnStep("SurgeryStepTreatBruteWounds", out var bruteStep))
        {
            skinSteps.Add(GetNetEntity(bruteStep));
        }

        // Mend Burn Damage - repeatable healing step
        if (TrySpawnStep("SurgeryStepTreatBurnWounds", out var burnStep))
        {
            skinSteps.Add(GetNetEntity(burnStep));
        }
    }

    /// <summary>
    /// Generates steps for the tissue layer: Retract/Mend Tissue sequences, Remove/Add Implant.
    /// </summary>
    private void GenerateTissueLayerSteps(
        EntityUid selectedPart,
        SurgeryLayerComponent selectedLayer,
        SurgeryStepProgressComponent progress,
        List<(NetEntity Item, bool IsImplant, bool IsOrgan, string Name)> handItems,
        List<NetEntity> tissueSteps)
    {
        // Retract/Mend Tissue bidirectional sequence
        var retractTissueProgress = progress.SequenceProgress.GetValueOrDefault("RetractTissueSequence", -1);
        var mendProgress = progress.SequenceProgress.GetValueOrDefault("MendTissueSequence", -1);
        
        var tissueSequenceSteps = GetAvailableStepsForSequence("RetractTissueSequence", "MendTissueSequence", retractTissueProgress, mendProgress, 3);
        foreach (var step in tissueSequenceSteps)
        {
            tissueSteps.Add(GetNetEntity(step));
        }

        // Remove Implant steps - one for each tissue layer implant
        var implants = GetImplantsInBodyPart(selectedPart, SurgeryLayer.Tissue);
        foreach (var (implant, name, _) in implants)
        {
            // Spawn a "Remove Implant" step for each implant
            if (TrySpawnStep("SurgeryStepRemoveImplant", out var removeStep))
            {
                // Update step name to include implant name
                var meta = MetaData(removeStep);
                _metaData.SetEntityName(removeStep, $"Remove Implant {name}", meta);
                tissueSteps.Add(GetNetEntity(removeStep));
            }
        }

        // Add Implant steps - only if surgeon has implant in hand
        foreach (var (itemNetEntity, isImplant, isOrgan, name) in handItems)
        {
            if (isImplant && !isOrgan) // Tissue layer implant (no Organ tag)
            {
                if (TrySpawnStep("SurgeryStepAddImplant", out var addStep))
                {
                    // Update step name to include implant name
                    var meta = MetaData(addStep);
                    _metaData.SetEntityName(addStep, $"Add Implant {name}", meta);
                    tissueSteps.Add(GetNetEntity(addStep));
                }
            }
        }
    }

    /// <summary>
    /// Generates steps for the organ layer: Remove/Add Organ, Remove/Add Organ Implant.
    /// </summary>
    private void GenerateOrganLayerSteps(
        EntityUid selectedPart,
        SurgeryLayerComponent selectedLayer,
        SurgeryStepProgressComponent progress,
        List<(NetEntity Item, bool IsImplant, bool IsOrgan, string Name)> handItems,
        List<NetEntity> organSteps)
    {
        // Remove Organ steps - one for each organ
        var organs = GetOrgansInBodyPart(selectedPart);
        foreach (var (organ, slotId, name) in organs)
        {
            // Spawn step and set target organ slot
            if (TrySpawnStep("SurgeryStepRemoveOrgan", out var removeStep))
            {
                // Set the target organ slot on the step component
                if (TryComp<SurgeryStepComponent>(removeStep, out var stepComp))
                {
                    stepComp.TargetOrganSlot = slotId;
                    Dirty(removeStep, stepComp);
                }
                // Update step name to include organ name
                var meta = MetaData(removeStep);
                _metaData.SetEntityName(removeStep, $"Remove Organ {name}", meta);
                organSteps.Add(GetNetEntity(removeStep));
            }
        }

        // Add Organ steps - show for all empty organ slots, even without organ in hand
        var emptySlots = GetEmptyOrganSlots(selectedPart);
        foreach (var slotId in emptySlots)
        {
            // Check if surgeon has matching organ in hand
            EntityUid? organInHand = null;
            string? organName = null;
            foreach (var (itemNetEntity, _, isOrgan, name) in handItems)
            {
                if (isOrgan)
                {
                    var itemEntity = GetEntity(itemNetEntity);
                    if (TryComp<OrganComponent>(itemEntity, out var organComp) && organComp.SlotId == slotId)
                    {
                        organInHand = itemEntity;
                        organName = name;
                        break;
                    }
                }
            }

            // Show step even without organ in hand (as per requirements)
            // Spawn step and set target organ slot
            if (TrySpawnStep("NewSurgeryStepInsertOrgan", out var addStep))
            {
                // Set the target organ slot on the step component
                if (TryComp<SurgeryStepComponent>(addStep, out var stepComp))
                {
                    stepComp.TargetOrganSlot = slotId;
                    Dirty(addStep, stepComp);
                }
                // Update step name - use organ name if in hand, otherwise use slot ID
                var displayName = organName ?? slotId;
                var meta = MetaData(addStep);
                _metaData.SetEntityName(addStep, $"Add Organ {displayName}", meta);
                organSteps.Add(GetNetEntity(addStep));
            }
        }

        // Remove Organ Implant steps - one for each organ layer implant
        var organImplants = GetImplantsInBodyPart(selectedPart, SurgeryLayer.Organ);
        foreach (var (implant, name, _) in organImplants)
        {
            if (TrySpawnStep("SurgeryStepRemoveOrganImplant", out var removeStep))
            {
                // Update step name to include implant name
                var meta = MetaData(removeStep);
                _metaData.SetEntityName(removeStep, $"Remove Organ Implant {name}", meta);
                organSteps.Add(GetNetEntity(removeStep));
            }
        }

        // Add Organ Implant steps - only if surgeon has organ implant in hand
        foreach (var (itemNetEntity, isImplant, isOrgan, name) in handItems)
        {
            if (isImplant && isOrgan) // Organ layer implant (has Organ tag)
            {
                if (TrySpawnStep("SurgeryStepAddOrganImplant", out var addStep))
                {
                    // Update step name to include implant name
                    var meta = MetaData(addStep);
                    _metaData.SetEntityName(addStep, $"Add Organ Implant {name}", meta);
                    organSteps.Add(GetNetEntity(addStep));
                }
            }
        }
    }

    /// <summary>
    /// Gets available steps for a bidirectional sequence.
    /// Returns next step in forward direction OR reverse steps if forward is partially complete.
    /// </summary>
    private List<EntityUid> GetAvailableStepsForSequence(
        string forwardSequenceId,
        string reverseSequenceId,
        int forwardProgress,
        int reverseProgress,
        int maxSteps)
    {
        var availableSteps = new List<EntityUid>();

        var forwardProtoId = new ProtoId<SurgerySequencePrototype>(forwardSequenceId);
        var reverseProtoId = new ProtoId<SurgerySequencePrototype>(reverseSequenceId);

        if (!_prototypes.TryIndex(forwardProtoId, out var forwardSequence) ||
            !_prototypes.TryIndex(reverseProtoId, out var reverseSequence))
        {
            return availableSteps;
        }

        // If forward sequence is partially complete, show next forward step AND reverse steps
        if (forwardProgress >= 0 && forwardProgress < maxSteps - 1)
        {
            // Show next forward step
            var nextForwardIndex = forwardProgress + 1;
            if (nextForwardIndex < forwardSequence.ForwardSteps.Count)
            {
                var stepProtoId = forwardSequence.ForwardSteps[nextForwardIndex];
                var stepEntity = Spawn(stepProtoId);
                if (Exists(stepEntity))
                {
                    availableSteps.Add(stepEntity);
                }
            }

            // Also show reverse steps (can reverse mid-sequence)
            // Show reverse steps starting from the step that corresponds to current forward progress
            // If forward progress is 0 (step 1 done), show reverse step 1 (which is step 2 of close)
            // Reverse steps are in reverse order, so index 0 is the last step
            var reverseStepIndex = maxSteps - 1 - forwardProgress - 1; // Convert forward progress to reverse index
            if (reverseStepIndex >= 0 && reverseStepIndex < reverseSequence.ReverseSteps.Count)
            {
                var reverseStepProtoId = reverseSequence.ReverseSteps[reverseStepIndex];
                var reverseStepEntity = Spawn(reverseStepProtoId);
                if (Exists(reverseStepEntity))
                {
                    availableSteps.Add(reverseStepEntity);
                }
            }
        }
        // If reverse sequence is partially complete, show next reverse step AND forward steps
        else if (reverseProgress >= 0 && reverseProgress < maxSteps - 1)
        {
            // Show next reverse step
            var nextReverseIndex = reverseProgress + 1;
            if (nextReverseIndex < reverseSequence.ReverseSteps.Count)
            {
                var stepProtoId = reverseSequence.ReverseSteps[nextReverseIndex];
                var stepEntity = Spawn(stepProtoId);
                if (Exists(stepEntity))
                {
                    availableSteps.Add(stepEntity);
                }
            }

            // Also show forward steps (can reverse the reversal)
            // Convert reverse progress to forward index
            var forwardStepIndex = maxSteps - 1 - reverseProgress - 1;
            if (forwardStepIndex >= 0 && forwardStepIndex < forwardSequence.ForwardSteps.Count)
            {
                var forwardStepProtoId = forwardSequence.ForwardSteps[forwardStepIndex];
                var forwardStepEntity = Spawn(forwardStepProtoId);
                if (Exists(forwardStepEntity))
                {
                    availableSteps.Add(forwardStepEntity);
                }
            }
        }
        // If neither started, show first forward step
        else if (forwardProgress < 0 && reverseProgress < 0)
        {
            if (forwardSequence.ForwardSteps.Count > 0)
            {
                var stepProtoId = forwardSequence.ForwardSteps[0];
                var stepEntity = Spawn(stepProtoId);
                if (Exists(stepEntity))
                {
                    availableSteps.Add(stepEntity);
                }
            }
        }
        // If forward complete but reverse not, show reverse steps
        else if (forwardProgress >= maxSteps - 1 && reverseProgress < maxSteps - 1)
        {
            // Show next reverse step
            var nextReverseIndex = reverseProgress + 1;
            if (nextReverseIndex < reverseSequence.ReverseSteps.Count)
            {
                var stepProtoId = reverseSequence.ReverseSteps[nextReverseIndex];
                var stepEntity = Spawn(stepProtoId);
                if (Exists(stepEntity))
                {
                    availableSteps.Add(stepEntity);
                }
            }
        }
        // If reverse complete but forward not, show forward steps
        else if (reverseProgress >= maxSteps - 1 && forwardProgress < maxSteps - 1)
        {
            // Show next forward step
            var nextForwardIndex = forwardProgress + 1;
            if (nextForwardIndex < forwardSequence.ForwardSteps.Count)
            {
                var stepProtoId = forwardSequence.ForwardSteps[nextForwardIndex];
                var stepEntity = Spawn(stepProtoId);
                if (Exists(stepEntity))
                {
                    availableSteps.Add(stepEntity);
                }
            }
        }

        return availableSteps;
    }

    /// <summary>
    /// Tries to get a step from a sequence by index.
    /// </summary>
    private bool TryGetSequenceStep(string sequenceId, int stepIndex, bool isReverse, out EntityUid stepEntity)
    {
        stepEntity = EntityUid.Invalid;

        var sequenceProtoId = new ProtoId<SurgerySequencePrototype>(sequenceId);
        if (!_prototypes.TryIndex(sequenceProtoId, out var sequence))
            return false;

        var steps = isReverse ? sequence.ReverseSteps : sequence.ForwardSteps;
        if (stepIndex < 0 || stepIndex >= steps.Count)
            return false;

        var stepProtoId = steps[stepIndex];
        stepEntity = Spawn(stepProtoId);
        return Exists(stepEntity);
    }

    /// <summary>
    /// Tries to spawn a step by prototype ID.
    /// </summary>
    private bool TrySpawnStep(string stepId, out EntityUid stepEntity)
    {
        stepEntity = EntityUid.Invalid;
        try
        {
            stepEntity = Spawn(stepId);
            return Exists(stepEntity) && HasComp<SurgeryStepComponent>(stepEntity);
        }
        catch
        {
            return false;
        }
    }


    public void UpdateUI(Entity<SurgeryLayerComponent> ent)
    {
        var (uid, layer) = ent;

        // Get the body from the body part to find the selected body part
        EntityUid selectedPart = uid;
        TargetBodyPart? selectedTargetPart = null;
        
        if (TryComp<BodyPartComponent>(uid, out var part) && part.Body != null)
        {
            // Initialize target from the original part
            selectedTargetPart = _body.GetTargetBodyPart(part);
            
            // Check if there's a selected body part for this body
            if (_selectedBodyParts.TryGetValue(part.Body.Value, out var storedSelectedPart))
            {
                selectedPart = storedSelectedPart;
                
                // Get the stored target body part (for missing limbs)
                if (_selectedTargetBodyParts.TryGetValue(part.Body.Value, out var storedTargetPart))
                {
                    selectedTargetPart = storedTargetPart;
                }
                else if (TryComp<BodyPartComponent>(selectedPart, out var selectedPartCompForTarget))
                {
                    selectedTargetPart = _body.GetTargetBodyPart(selectedPartCompForTarget);
                }
            }
        }

        // Get the layer component for the selected body part
        // Use selected part's layer if available, otherwise use original part's layer for layer state
        // But always use selected part for filtering steps (part type, organ slots, etc.)
        SurgeryLayerComponent selectedLayer;
        if (!TryComp<SurgeryLayerComponent>(selectedPart, out var selectedLayerComp))
        {
            // Selected part doesn't have layer component - use original part's layer for layer state
            // But keep selectedPart for filtering steps
            selectedLayer = layer;
        }
        else
        {
            selectedLayer = selectedLayerComp;
        }

        // Get user for dynamic step generation
        EntityUid? evalUser = null;
        EntityUid? bodyEntity = null;
        if (TryComp<BodyPartComponent>(selectedPart, out var selectedPartComp) && selectedPartComp.Body != null)
        {
            bodyEntity = selectedPartComp.Body.Value;
        }
        else if (TryComp<BodyPartComponent>(uid, out var originalPart) && originalPart.Body != null)
        {
            bodyEntity = originalPart.Body.Value;
        }
        
        if (bodyEntity != null && TryComp<UserInterfaceComponent>(bodyEntity, out var uiComp))
        {
            var actors = _ui.GetActors((bodyEntity.Value, uiComp), Content.Shared.Medical.Surgery.SurgeryUIKey.Key);
            var actorList = actors.ToList();
            if (actorList.Count > 0)
            {
                evalUser = actorList[0];
            }
        }

        // Generate dynamic steps based on current state, implants, organs, and hand items
        var (skinSteps, tissueSteps, organSteps) = GenerateDynamicSteps(selectedPart, selectedLayer, evalUser);

        // Only scan for surgical items if the UI is actually open (performance optimization)
        // This prevents expensive spatial queries when UpdateUI is called from other places
        Dictionary<string, int> availableItems;
        if (_openSurgeryUIs.ContainsKey(selectedPart) || _openSurgeryUIs.ContainsKey(uid))
        {
            availableItems = ScanForSurgicalItems(selectedPart);
        }
        else
        {
            // UI not open, use empty dictionary (no items available)
            availableItems = new Dictionary<string, int>();
        }

        // Check if the selected part has cybernetics - if so, show maintenance steps
        bool hasCybernetics = HasComp<CyberneticsComponent>(selectedPart);
        
        // Check if the selected part is a cybernetic arm or leg - if so, only allow maintenance steps
        bool isCyberLimb = false;
        if (hasCybernetics && TryComp<BodyPartComponent>(selectedPart, out var partComp))
        {
            isCyberLimb = partComp.PartType == BodyPartType.Arm || partComp.PartType == BodyPartType.Leg;
        }

        // Add cybernetics maintenance steps if applicable
        // Use cached step data for maintenance steps only
        if (hasCybernetics)
        {
            BodyPartType? selectedPartType = null;
            if (TryComp<BodyPartComponent>(selectedPart, out var partTypeComp))
            {
                selectedPartType = partTypeComp.PartType;
            }
            else if (selectedLayer.PartType != null)
            {
                selectedPartType = selectedLayer.PartType;
            }

            foreach (var (cachedStepId, stepData) in _cachedStepData)
            {
                // Only process cybernetics maintenance steps
                bool isMaintenanceStep = cachedStepId.Contains("Cybernetics") || cachedStepId.Contains("Maintenance");
                if (!isMaintenanceStep)
                    continue;

                // Check if step is valid for the selected part type
                if (selectedPartType != null && stepData.ValidPartTypes.Count > 0)
                {
                    if (!stepData.ValidPartTypes.Contains(selectedPartType.Value))
                        continue;
                }

                // Spawn maintenance step entity
                var stepEntity = Spawn(stepData.StepId);
                var stepNetEntity = GetNetEntity(stepEntity);
                
                if (!TryComp<SurgeryStepComponent>(stepEntity, out var stepComp))
                {
                    Del(stepEntity);
                    continue;
                }
                
                switch (stepData.Layer)
                {
                    case SurgeryLayer.Skin:
                        skinSteps.Add(stepNetEntity);
                        break;
                    case SurgeryLayer.Tissue:
                        tissueSteps.Add(stepNetEntity);
                        break;
                    case SurgeryLayer.Organ:
                        organSteps.Add(stepNetEntity);
                        break;
                }
            }
        }

        // Evaluate operation availability - get first user with UI open
        // (evalUser and bodyEntity are already defined above, reuse them)
        var stepOperationInfo = new Dictionary<NetEntity, SurgeryStepOperationInfo>();

        // Evaluate operation info for all steps
        foreach (var stepNetEntity in skinSteps.Concat(tissueSteps).Concat(organSteps))
        {
            var stepEntity = GetEntity(stepNetEntity);
            if (!TryComp<SurgeryStepComponent>(stepEntity, out var stepComp))
                continue;

            if (stepComp.OperationId != null && evalUser != null &&
                _prototypes.TryIndex(stepComp.OperationId, out var operation))
            {
                bool hasPrimary = HasPrimaryToolsForOperation(evalUser.Value, operation);
                bool hasSecondary = operation.SecondaryMethod != null && HasSecondaryMethodForOperation(evalUser.Value, operation);
                bool isRepair = operation.RepairOperationFor != null;
                
                // For repair operations, only show if corresponding improvised component exists
                if (isRepair && !HasImprovisedComponentForOperation(selectedPart, operation.RepairOperationFor!.Value))
                {
                    hasPrimary = false; // Don't show repair if not needed
                }

                stepOperationInfo[stepNetEntity] = new SurgeryStepOperationInfo(
                    hasPrimary,
                    hasSecondary,
                    isRepair,
                    operation.Name
                );
            }
        }

        // Calculate layer accessibility based on selected body part
        bool canAccessTissueLayer = selectedLayer.SkinRetracted;
        bool canAccessOrganLayer = selectedLayer.TissueRetracted && (selectedLayer.BonesSawed || selectedLayer.BonesSmashed);

        // Get the current layer for the selected body part (default to Skin)
        SurgeryLayer currentLayer = SurgeryLayer.Skin;
        if (_bodyPartCurrentLayer.TryGetValue(selectedPart, out var storedLayer))
        {
            currentLayer = storedLayer;
        }

        // Get the body entity to send state to (reuse the one found earlier)
        if (bodyEntity == null)
        {
            if (TryComp<BodyPartComponent>(selectedPart, out var selectedPartCompForBody) && selectedPartCompForBody.Body != null)
            {
                bodyEntity = selectedPartCompForBody.Body.Value;
            }
            else if (TryComp<BodyPartComponent>(uid, out var originalPartForBody) && originalPartForBody.Body != null)
            {
                bodyEntity = originalPartForBody.Body.Value;
            }
        }

        if (bodyEntity == null)
            return;

        var state = new SurgeryBoundUserInterfaceState(
            GetNetEntity(uid), // Original body part (for reference)
            layer.PartType,
            selectedLayer.SkinRetracted, // Use selected body part's state
            selectedLayer.TissueRetracted,
            selectedLayer.BonesSawed,
            skinSteps,
            tissueSteps,
            organSteps,
            selectedLayer.BonesSmashed,
            stepOperationInfo,
            GetNetEntity(selectedPart), // Selected body part
            selectedTargetPart, // Target body part enum
            canAccessTissueLayer,
            canAccessOrganLayer
        );

        _ui.SetUiState(bodyEntity.Value, Content.Shared.Medical.Surgery.SurgeryUIKey.Key, state);
    }

    /// <summary>
    /// Installs an organ/limb/cybernetic into a body, calculating and applying integrity cost.
    /// </summary>
    public bool TryInstallImplant(
        EntityUid item,
        EntityUid body,
        EntityUid targetPart,
        EntityUid? user = null,
        EntityUid? tool = null,
        EntityUid? operatingTable = null)
    {
        // Cybernetic arms and legs cannot have implants or organs installed - they're fully cybernetic
        if (HasComp<CyberneticsComponent>(targetPart) && TryComp<BodyPartComponent>(targetPart, out var targetPartComp))
        {
            if (targetPartComp.PartType == BodyPartType.Arm || targetPartComp.PartType == BodyPartType.Leg)
            {
                // Only allow maintenance, not implants or organs
                if (HasComp<OrganComponent>(item) || HasComp<SubdermalImplantComponent>(item))
                {
                    if (user != null)
                        _popup.PopupEntity("Cybernetic limbs are fully mechanical and cannot accept biological implants or organs.", targetPart, user.Value, PopupType.Medium);
                    return false;
                }
            }
        }
        
        // Slimes cannot have limbs or organs implanted (except core removal/replacement)
        if (IsSlimeBody(body))
        {
            // Only allow core organ replacement, not limb implantation
            if (HasComp<BodyPartComponent>(item))
            {
                // Slimes regenerate limbs automatically, cannot implant new ones
                return false;
            }
            
            // For organs, only allow core
            if (HasComp<OrganComponent>(item))
            {
                if (!TryComp<OrganComponent>(item, out var organ) || organ.SlotId != "core")
                {
                    // Only core organ can be replaced
                    return false;
                }
            }
        }

        // DonorSpeciesComponent should already be set by DonorSpeciesSystem when organs/limbs are removed
        // or when they're first added to a body. If it's not set, this is likely a new item (e.g., from bioprinter)
        // and it will have normal integrity cost.

        // Calculate integrity cost (will be 0 if donor species matches recipient)
        var cost = CalculateIntegrityCost(item, body, tool, operatingTable);

        // Track the actual cost that was applied
        var appliedCost = EnsureComp<AppliedIntegrityCostComponent>(item);
        appliedCost.AppliedCost = cost;
        Dirty(item, appliedCost);

        // Check if body has enough integrity capacity (or if over, that's okay, just reduces max health)
        if (!TryComp<IntegrityComponent>(body, out var integrity))
        {
            EnsureComp<IntegrityComponent>(body);
            integrity = Comp<IntegrityComponent>(body);
        }

        // Add integrity usage
        _integrity.AddIntegrityUsage(body, cost, integrity);

        // Install the item
        if (HasComp<OrganComponent>(item))
        {
            // Install organ
            var organ = Comp<OrganComponent>(item);
            var slotId = organ.SlotId;
            
            // Create organ slot if it doesn't exist (for dynamically added organs like mindshield/storage implant)
            if (!string.IsNullOrEmpty(slotId) && !_body.CanInsertOrgan(targetPart, slotId))
            {
                if (!_body.TryCreateOrganSlot(targetPart, slotId, out _, null))
                    return false;
            }
            
            // Use InsertOrgan with the organ's SlotId, or AddOrganToFirstValidSlot if no SlotId
            if (!string.IsNullOrEmpty(slotId))
            {
                if (!_body.InsertOrgan(targetPart, item, slotId))
                    return false;
            }
            else
            {
                if (!_body.AddOrganToFirstValidSlot(targetPart, item))
                    return false;
            }
        }
        else if (HasComp<BodyPartComponent>(item))
        {
            // Install limb - need to find appropriate slot on target part
            if (!TryComp<BodyPartComponent>(targetPart, out var installTargetPartComp))
                return false;

            // Find an available slot for this part type
            var partType = Comp<BodyPartComponent>(item).PartType;
            string? targetSlot = null;
            
            foreach (var (slotId, slot) in installTargetPartComp.Children)
            {
                if (slot.Type == partType)
                {
                    // Check if slot is empty by trying to get part in slot
                    var existingParts = Body.GetBodyPartChildren(targetPart);
                    var slotHasPart = existingParts.Any(p => 
                        Body.GetParentPartAndSlotOrNull(p.Id)?.Slot == slotId);
                    
                    if (!slotHasPart)
                    {
                        targetSlot = slotId;
                        break;
                    }
                }
            }

            if (targetSlot == null)
                return false;

            if (!Body.AttachPart(targetPart, targetSlot, item))
                return false;
        }

        return true;
    }

    /// <summary>
    /// Removes an organ/limb/cybernetic from a body, removing integrity cost.
    /// Sets donor species on the removed item so it can be used as a compatible donor later.
    /// </summary>
    public bool TryRemoveImplant(
        EntityUid item,
        EntityUid body)
    {
        if (!TryComp<IntegrityComponent>(body, out var integrity))
            return false;

        // Set donor species on the removed item before removing it
        var donorSpecies = EnsureComp<DonorSpeciesComponent>(item);
        var bodySpecies = GetBodySpecies(body);
        if (bodySpecies != null)
        {
            donorSpecies.DonorSpecies = bodySpecies.Value;
            Dirty(item, donorSpecies);
        }

        // Get the actual integrity cost that was applied when this item was installed
        FixedPoint2 cost = FixedPoint2.Zero;
        if (TryComp<AppliedIntegrityCostComponent>(item, out var appliedCost))
        {
            cost = appliedCost.AppliedCost;
        }
        else
        {
            // Fallback: if AppliedIntegrityCostComponent doesn't exist (old items), recalculate
            // This shouldn't happen for newly installed items, but handles edge cases
            if (TryComp<OrganIntegrityComponent>(item, out var organIntegrity))
                cost = organIntegrity.BaseIntegrityCost;
            else if (TryComp<LimbIntegrityComponent>(item, out var limbIntegrity))
                cost = limbIntegrity.BaseIntegrityCost;
            else if (TryComp<CyberneticIntegrityComponent>(item, out var cyberIntegrity))
                cost = cyberIntegrity.BaseIntegrityCost;
        }

        // Remove integrity usage (will be 0 for compatible donors)
        _integrity.RemoveIntegrityUsage(body, cost, integrity);

        // Remove the item
        if (HasComp<OrganComponent>(item))
        {
            _body.RemoveOrgan(item);
        }
        else if (HasComp<BodyPartComponent>(item))
        {
            // Remove limb - detach from body using container system
            if (!TryComp<BodyPartComponent>(item, out var part) || part.Body == null)
                return false;

            var slot = Body.GetParentPartAndSlotOrNull(item);
            if (slot != null)
            {
                // Remove from parent part's slot using container system
                var containerSlotId = Content.Shared.Body.Systems.SharedBodySystem.GetPartSlotContainerId(slot.Value.Slot);
                if (_containers.TryGetContainer(slot.Value.Parent, containerSlotId, out var container))
                {
                    _containers.Remove((item, null, null), container);
                }
            }
            else
            {
                // Root part - remove from body root
                if (_containers.TryGetContainer(part.Body.Value, Content.Shared.Body.Systems.SharedBodySystem.BodyRootContainerId, out var container))
                {
                    _containers.Remove((item, null, null), container);
                }
            }
        }

        return true;
    }

    public new ProtoId<EntityPrototype>? GetBodySpecies(EntityUid body)
    {
        // Get species from body prototype
        if (TryComp<BodyComponent>(body, out var bodyComp) && bodyComp.Prototype != null)
        {
            // Convert ProtoId<BodyPrototype> to ProtoId<EntityPrototype>
            var prototypeId = bodyComp.Prototype.Value;
            return new ProtoId<EntityPrototype>((string)prototypeId);
        }
        return null;
    }

    /// <summary>
    /// Checks if a body part or body belongs to a slime body.
    /// </summary>
    private bool IsSlimeBody(EntityUid entity)
    {
        // Check if it's a body directly
        if (TryComp<BodyComponent>(entity, out var body))
        {
            return body.Prototype?.Id == "Slime";
        }

        // Check if it's a body part
        if (TryComp<BodyPartComponent>(entity, out var part) && part.Body != null)
        {
            if (TryComp<BodyComponent>(part.Body.Value, out var bodyComp))
            {
                return bodyComp.Prototype?.Id == "Slime";
            }
        }

        return false;
    }

    /// <summary>
    /// Applies a surgery penalty incrementally as surgery progresses.
    /// Penalties accumulate: Skin (+1), Tissue (+1), Bones (+8) = Total 10.
    /// </summary>
    private void ApplySurgeryPenalty(EntityUid bodyPart, FixedPoint2 amount)
    {
        // Get or create penalty component
        var penalty = EnsureComp<SurgeryPenaltyComponent>(bodyPart);
        
        // Add to target penalty (accumulates as surgery progresses)
        penalty.TargetPenalty += amount;
        Dirty(bodyPart, penalty);

        // Update cached surgery penalty and recalculate bio-rejection for the body
        if (TryComp<BodyPartComponent>(bodyPart, out var part) && part.Body != null)
        {
            if (TryComp<IntegrityComponent>(part.Body.Value, out var integrity))
            {
                // Update cached surgery penalty using the server system
                var serverIntegrity = EntitySystem.Get<IntegritySystem>();
                serverIntegrity.UpdateCachedSurgeryPenalty(part.Body.Value, integrity);
                _integrity.RecalculateTargetBioRejection(part.Body.Value, integrity);
            }
        }
    }

    /// <summary>
    /// Removes surgery penalty incrementally as surgery is closed.
    /// Can remove specific amounts (e.g., just skin penalty) or all if amount is null.
    /// The penalty will gradually decrease to target over time.
    /// </summary>
    private void RemoveSurgeryPenalty(EntityUid bodyPart, FixedPoint2? amount = null)
    {
        if (!TryComp<SurgeryPenaltyComponent>(bodyPart, out var penalty))
            return;

        if (amount.HasValue)
        {
            // Remove specific amount (e.g., just skin or tissue penalty)
            penalty.TargetPenalty = FixedPoint2.Max(FixedPoint2.Zero, penalty.TargetPenalty - amount.Value);
        }
        else
        {
            // Remove all penalty
            penalty.TargetPenalty = FixedPoint2.Zero;
        }
        
        Dirty(bodyPart, penalty);

        // Update cached surgery penalty and recalculate bio-rejection for the body
        if (TryComp<BodyPartComponent>(bodyPart, out var part) && part.Body != null)
        {
            if (TryComp<IntegrityComponent>(part.Body.Value, out var integrity))
            {
                // Update cached surgery penalty using the server system
                var serverIntegrity = EntitySystem.Get<IntegritySystem>();
                serverIntegrity.UpdateCachedSurgeryPenalty(part.Body.Value, integrity);
                _integrity.RecalculateTargetBioRejection(part.Body.Value, integrity);
            }
        }
    }

    /// <summary>
    /// Handles cybernetics maintenance step state changes.
    /// </summary>
    private void HandleCyberneticsMaintenanceSteps(EntityUid bodyPart, EntityUid stepEntity, SurgeryStepComponent step, MetaDataComponent stepMeta)
    {
        var stepId = stepMeta.EntityPrototype?.ID ?? "";
        var stepName = stepMeta.EntityName ?? "";

        // Check if this is a cyber part
        if (!HasComp<CyberneticsComponent>(bodyPart))
            return;

        // Ensure upkeep component exists
        var upkeep = EnsureComp<CyberneticsUpkeepComponent>(bodyPart);

        // Handle different maintenance steps
        if (stepId.Contains("OpenCyberneticsPanel") || stepName.Contains("Open Maintenance Panel"))
        {
            // Open panel - unscrew
            upkeep.IsPanelUnscrewed = true;
            Dirty(bodyPart, upkeep);
            _cyberneticsUpkeep.UpdateUpkeepState(bodyPart, upkeep);
            
            // Re-evaluate all cybernetics on the body when panel is opened
            if (TryComp<BodyPartComponent>(bodyPart, out var part) && part.Body != null)
            {
                _cyberneticsFunctionality.EvaluateAllCybernetics(part.Body.Value);
            }
        }
        else if (stepId.Contains("CloseCyberneticsPanel") || stepName.Contains("Close Maintenance Panel"))
        {
            // Close panel - screw closed
            // Only allow closing if bolts are adjusted and wiring is replaced
            if (upkeep.BoltsAdjusted && upkeep.WiringReplaced)
            {
                upkeep.IsPanelUnscrewed = false;
                // Reset maintenance flags for next time
                upkeep.BoltsAdjusted = false;
                upkeep.WiringReplaced = false;
                Dirty(bodyPart, upkeep);
                _cyberneticsUpkeep.UpdateUpkeepState(bodyPart, upkeep);
                
                // Re-evaluate all cybernetics on the body when panel is closed
                if (TryComp<BodyPartComponent>(bodyPart, out var part) && part.Body != null)
                {
                    _cyberneticsFunctionality.EvaluateAllCybernetics(part.Body.Value);
                }
            }
            else
            {
                // Can't close panel yet - show message and prevent step completion
                _popup.PopupEntity("You must adjust bolts and replace wiring before closing the panel.", bodyPart, PopupType.Medium);
                // Note: The step will still complete, but the panel won't close
                // This allows the surgeon to see the message and do the required steps
            }
        }
        else if (stepId.Contains("AdjustCyberneticsBolts") || stepName.Contains("Adjust Bolts"))
        {
            // Adjust bolts
            upkeep.BoltsAdjusted = true;
            Dirty(bodyPart, upkeep);
        }
        else if (stepId.Contains("ReplaceCyberneticsWiring") || stepName.Contains("Replace Wiring"))
        {
            // Replace wiring - also resets service time
            upkeep.WiringReplaced = true;
            Dirty(bodyPart, upkeep);

            // Reset service time for this cyber part
            if (TryComp<CyberLimbStorageComponent>(bodyPart, out var storage))
            {
                storage.ServiceTimeRemaining = storage.MaxServiceTime;
                storage.IsServiceTimeExpired = false;
                storage.NeedsServiceTimeUpdate = true;
                storage.LastServiceTimeUpdate = _timing.CurTime;
                Dirty(bodyPart, storage);

                // Update next expiration time
                if (TryComp<BodyPartComponent>(bodyPart, out var part) && part.Body != null)
                {
                    _cyberLimbStats.UpdateNextServiceTimeExpiration(part.Body.Value);
                }
            }
        }
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        // Performance: Early exit if no surgery UIs are open
        // This ensures we don't iterate through empty dictionaries every frame
        if (_openSurgeryUIs.Count == 0)
            return;

        // Update material scanning for open surgery UIs only
        // This runs every frame but only processes UIs that need updating (every 0.5s)
        // Performance: Only iterates through open UIs, which should be very few at any time
        var curTime = _timing.CurTime;
        var toUpdate = new List<EntityUid>();
        
        foreach (var (bodyPart, nextScan) in _openSurgeryUIs)
        {
            if (curTime >= nextScan)
            {
                toUpdate.Add(bodyPart);
            }
        }

        // Only update UIs that need refreshing (performance: batch updates)
        foreach (var bodyPart in toUpdate)
        {
            if (TryComp<SurgeryLayerComponent>(bodyPart, out var layer))
            {
                // This will trigger a scan since the UI is in _openSurgeryUIs
                UpdateUI((bodyPart, layer));
                _openSurgeryUIs[bodyPart] = curTime + TimeSpan.FromSeconds(MaterialScanInterval);
            }
            else
            {
                // Component removed, clean up
                _openSurgeryUIs.Remove(bodyPart);
            }
        }
    }

    /// <summary>
    /// Scans for surgical items within 1.5 tiles of the body part.
    /// Returns a dictionary of item prototype ID -> count available.
    /// Performance: This uses spatial queries which are relatively expensive.
    /// Only call this when the surgery UI is actually open.
    /// </summary>
    private Dictionary<string, int> ScanForSurgicalItems(EntityUid bodyPart)
    {
        var items = new Dictionary<string, int>();
        
        if (!TryComp<TransformComponent>(bodyPart, out var xform))
            return items;

        var mapPos = _transform.GetMapCoordinates(bodyPart, xform);
        if (mapPos.MapId == MapId.Nullspace)
            return items;

        // Performance: GetEntitiesInRange does spatial queries - only call when UI is open
        // Using a small range (1.5 tiles) to minimize entities checked
        var entitiesInRange = _lookup.GetEntitiesInRange(mapPos, MaterialScanRange);
        
        // Performance: Early exit if no entities found
        if (entitiesInRange.Count == 0)
            return items;
        
        // Performance: Use string constants to avoid repeated allocations
        const string PlasteelBonesId = "PlasteelBones";
        const string DurathreadWovenSkinId = "DurathreadWovenSkin";
        const string PlasteelReinforcedSkinId = "PlasteelReinforcedSkin";
        
        foreach (var entity in entitiesInRange)
        {
            var protoId = MetaData(entity).EntityPrototype?.ID;
            if (protoId == null)
                continue;

            // Check for surgical items - using string comparison (fast for small set)
            if (protoId == PlasteelBonesId)
            {
                items.TryGetValue(PlasteelBonesId, out var currentCount);
                items[PlasteelBonesId] = currentCount + 1;
            }
            else if (protoId == DurathreadWovenSkinId)
            {
                items.TryGetValue(DurathreadWovenSkinId, out var currentCount);
                items[DurathreadWovenSkinId] = currentCount + 1;
            }
            else if (protoId == PlasteelReinforcedSkinId)
            {
                items.TryGetValue(PlasteelReinforcedSkinId, out var currentCount);
                items[PlasteelReinforcedSkinId] = currentCount + 1;
            }
        }

        return items;
    }

    /// <summary>
    /// Handles plasteel bone plating surgery step completion.
    /// Consumes PlasteelBones item, adds component, and applies integrity cost.
    /// </summary>
    private void OnPlasteelBonePlatingStep(Entity<SurgeryPlasteelBonePlatingEffectComponent> ent, ref SurgeryStepEvent args)
    {
        if (!args.Complete)
            return;

        // Find and consume PlasteelBones item
        if (!TryConsumeSurgicalItem(args.Part, "PlasteelBones", args.User))
        {
            _popup.PopupEntity("No Plasteel Bones item nearby.", args.Part, args.User, PopupType.Medium);
            return;
        }

        // Add plasteel bone plating component
        EnsureComp<PlasteelBonePlatingComponent>(args.Part);

        // Apply integrity cost (1 integrity)
        if (TryComp<BodyPartComponent>(args.Part, out var part) && part.Body != null)
        {
            var integrity = EnsureComp<IntegrityComponent>(part.Body.Value);
            _integrity.AddIntegrityUsage(part.Body.Value, FixedPoint2.New(1), integrity);
        }

        _popup.PopupEntity("Plasteel bone plating successfully applied.", args.Part, args.User, PopupType.Medium);
    }

    /// <summary>
    /// Handles dermal plasteel weave surgery step completion.
    /// Consumes DurathreadWovenSkin or PlasteelReinforcedSkin item, adds component, and applies integrity cost.
    /// </summary>
    private void OnDermalPlasteelWeaveStep(Entity<SurgeryDermalPlasteelWeaveEffectComponent> ent, ref SurgeryStepEvent args)
    {
        if (!args.Complete)
            return;

        // Find and consume DurathreadWovenSkin or PlasteelReinforcedSkin item
        string? consumedItem = null;
        if (TryConsumeSurgicalItem(args.Part, "DurathreadWovenSkin", args.User))
        {
            consumedItem = "DurathreadWovenSkin";
        }
        else if (TryConsumeSurgicalItem(args.Part, "PlasteelReinforcedSkin", args.User))
        {
            consumedItem = "PlasteelReinforcedSkin";
        }
        else
        {
            _popup.PopupEntity("No Durathread Woven Skin or Plasteel Reinforced Skin item nearby.", args.Part, args.User, PopupType.Medium);
            return;
        }

        // Add dermal plasteel weave component
        EnsureComp<DermalPlasteelWeaveComponent>(args.Part);

        // Apply integrity cost (1 integrity)
        if (TryComp<BodyPartComponent>(args.Part, out var part) && part.Body != null)
        {
            var integrity = EnsureComp<IntegrityComponent>(part.Body.Value);
            _integrity.AddIntegrityUsage(part.Body.Value, FixedPoint2.New(1), integrity);
        }

        _popup.PopupEntity("Dermal reinforcement successfully applied.", args.Part, args.User, PopupType.Medium);
    }

    /// <summary>
    /// Attempts to consume a surgical item from nearby entities.
    /// Returns true if the item was found and consumed.
    /// Performance: This is only called during surgery step completion (user-initiated action),
    /// so the spatial query cost is acceptable. Not called on every frame.
    /// </summary>
    private bool TryConsumeSurgicalItem(EntityUid bodyPart, string itemPrototypeId, EntityUid? user)
    {
        var xform = Transform(bodyPart);
        var mapPos = _transform.GetMapCoordinates(bodyPart, xform);
        if (mapPos.MapId == MapId.Nullspace)
            return false;

        // Performance: Only called during surgery execution, not on every frame
        // Using small range (1.5 tiles) to minimize entities checked
        var entitiesInRange = _lookup.GetEntitiesInRange(mapPos, MaterialScanRange);
        
        // Performance: Early exit if no entities found
        if (entitiesInRange.Count == 0)
            return false;
        
        foreach (var entity in entitiesInRange)
        {
            var protoId = MetaData(entity).EntityPrototype?.ID;
            if (protoId == itemPrototypeId)
            {
                // Consume the item
                QueueDel(entity);
                return true;
            }
        }

        return false;
    }

    // Damage type constants for wound treatment
    private static readonly string[] BruteDamageTypes = { "Slash", "Blunt", "Piercing" };
    private static readonly string[] BurnDamageTypes = { "Heat", "Shock", "Cold", "Caustic" };

    /// <summary>
    /// Checks if an entity has damage of a specific group.
    /// </summary>
    private bool HasDamageGroup(EntityUid entity, string[] group, out DamageableComponent? damageable)
    {
        if (!TryComp<DamageableComponent>(entity, out var damageableComp))
        {
            damageable = null;
            return false;
        }

        damageable = damageableComp;
        return group.Any(damageType => damageableComp.Damage.DamageDict.TryGetValue(damageType, out var value) && value > 0);
    }

    /// <summary>
    /// Handles the tend wounds surgery step effect.
    /// Heals brute or burn wounds based on the component's MainGroup.
    /// </summary>
    private void OnTendWoundsStep(Entity<SurgeryTendWoundsEffectComponent> ent, ref SurgeryStepEvent args)
    {
        var group = ent.Comp.MainGroup == "Brute" ? BruteDamageTypes : BurnDamageTypes;

        if (!HasDamageGroup(args.Body, group, out var damageable)
            && !HasDamageGroup(args.Part, group, out var _)
            || damageable == null)
            return;

        // Calculate healing bonus based on total damage
        var bonus = ent.Comp.HealMultiplier * damageable.DamagePerGroup[ent.Comp.MainGroup];

        if (_mobState.IsDead(args.Body))
            bonus *= 0.5f;

        var adjustedDamage = new DamageSpecifier(ent.Comp.Damage);

        foreach (var type in group)
            adjustedDamage.DamageDict[type] -= bonus;

        // Apply the healing damage
        if (TryComp<BodyPartComponent>(args.Part, out var partComp))
        {
            _damageable.TryChangeDamage(args.Body,
                adjustedDamage,
                true,
                origin: args.User,
                canSever: false,
                partMultiplier: 0.5f,
                targetPart: _body.GetTargetBodyPart(partComp));
        }
    }

    /// <summary>
    /// Checks if the tend wounds step should be cancelled (i.e., if wounds are already healed).
    /// </summary>
    private void OnTendWoundsCheck(Entity<SurgeryTendWoundsEffectComponent> ent, ref ShitmedSurgerySteps.SurgeryStepCompleteCheckEvent args)
    {
        var group = ent.Comp.MainGroup == "Brute" ? BruteDamageTypes : BurnDamageTypes;

        // If there's no damage of this type, cancel the step (wounds are already healed)
        if (!HasDamageGroup(args.Body, group, out var _)
            && !HasDamageGroup(args.Part, group, out var _))
            args.Cancelled = true;
    }

    /// <summary>
    /// Gets all implants in a body part or the body entity.
    /// Filters by layer: tissue layer implants don't have Organ tag, organ layer implants have Organ tag.
    /// </summary>
    public List<(EntityUid Implant, string Name, bool IsOrganLayer)> GetImplantsInBodyPart(EntityUid bodyPart, SurgeryLayer layer)
    {
        var implants = new List<(EntityUid, string, bool)>();
        
        // Get the body entity
        EntityUid? bodyEntity = null;
        if (TryComp<BodyPartComponent>(bodyPart, out var partComp))
        {
            bodyEntity = partComp.Body;
        }
        else if (HasComp<BodyComponent>(bodyPart))
        {
            bodyEntity = bodyPart;
        }

        if (bodyEntity == null)
            return implants;

        // Get implants from the body entity's ImplantedComponent
        if (!TryComp<ImplantedComponent>(bodyEntity.Value, out var implanted))
            return implants;

        foreach (var implant in implanted.ImplantContainer.ContainedEntities)
        {
            if (!HasComp<SubdermalImplantComponent>(implant))
                continue;

            // Check if this is an organ layer implant (has Organ tag)
            bool isOrganLayer = Tags.HasTag(implant, "Organ");
            
            // Filter by layer
            if (layer == SurgeryLayer.Tissue && isOrganLayer)
                continue; // Skip organ implants in tissue layer
            if (layer == SurgeryLayer.Organ && !isOrganLayer)
                continue; // Skip tissue implants in organ layer

            var name = MetaData(implant).EntityName;
            implants.Add((implant, name, isOrganLayer));
        }

        return implants;
    }

    /// <summary>
    /// Gets all organs in a body part with their slot IDs and names.
    /// </summary>
    public List<(EntityUid Organ, string SlotId, string Name)> GetOrgansInBodyPart(EntityUid bodyPart)
    {
        var organs = new List<(EntityUid, string, string)>();

        if (!TryComp<BodyPartComponent>(bodyPart, out var partComp))
            return organs;

        var organEntities = _body.GetPartOrgans(bodyPart, partComp);
        foreach (var (organUid, organ) in organEntities)
        {
            var slotId = organ.SlotId ?? "";
            var name = MetaData(organUid).EntityName;
            organs.Add((organUid, slotId, name));
        }

        return organs;
    }

    /// <summary>
    /// Gets all empty organ slots in a body part.
    /// </summary>
    public List<string> GetEmptyOrganSlots(EntityUid bodyPart)
    {
        var emptySlots = new List<string>();

        if (!TryComp<BodyPartComponent>(bodyPart, out var partComp))
            return emptySlots;

        // Get all organs to find which slots are occupied
        var existingOrgans = GetOrgansInBodyPart(bodyPart);
        var occupiedSlots = existingOrgans.Select(o => o.SlotId).ToHashSet();

        // Check all organ slots defined in the body part
        foreach (var (slotId, _) in partComp.Organs)
        {
            if (!occupiedSlots.Contains(slotId))
            {
                emptySlots.Add(slotId);
            }
        }

        return emptySlots;
    }
}

