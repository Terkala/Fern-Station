// SPDX-FileCopyrightText: 2025 terkala <appleorange64@gmail.com>
//
// SPDX-License-Identifier: MIT

using System.Linq;
using Content.Server.Arcade.Components;
using Content.Server.Mind;
using Content.Server.Players;
using Content.Server.Players.PlayTimeTracking;
using Content.Server.Popups;
using Content.Server.Power.EntitySystems;
using Content.Server.Storage.EntitySystems;
using Content.Shared.Arcade;
using Content.Shared.Arcade.Components;
using Content.Shared.Arcade.Prototypes;
using Content.Shared.Destructible;
using Content.Shared.Lock;
using Content.Shared.Mind;
using Content.Shared.Mind.Components;
using Content.Shared.Players;
using Content.Shared.Popups;
using Content.Shared.Power;
using Content.Shared.Storage.Components;
using Robust.Server.Containers;
using Robust.Server.GameObjects;
using Robust.Server.Player;
using Robust.Shared.Containers;
using Robust.Shared.Containers.Events;
using Robust.Shared.EntitySerialization.Systems;
using Robust.Shared.GameStates;
using Robust.Shared.Map;
using Robust.Shared.Localization;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;
using Robust.Shared.Utility;

namespace Content.Server.Arcade.EntitySystems;

/// <summary>
/// System for managing VR Pods that allow players to access tutorials.
/// </summary>
public sealed class VRPodSystem : EntitySystem
{
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly IMapManager _mapManager = default!;
    [Dependency] private readonly IPlayerManager _playerManager = default!;
    [Dependency] private readonly IPrototypeManager _prototypeManager = default!;
    [Dependency] private readonly BatterySystem _battery = default!;
    [Dependency] private readonly ContainerSystem _container = default!;
    [Dependency] private readonly EntityStorageSystem _entityStorage = default!;
    [Dependency] private readonly LockSystem _lockSystem = default!;
    [Dependency] private readonly MapLoaderSystem _mapLoader = default!;
    [Dependency] private readonly MapSystem _mapSystem = default!;
    [Dependency] private readonly MindSystem _mindSystem = default!;
    [Dependency] private readonly PlayTimeTrackingManager _playTime = default!;
    [Dependency] private readonly PopupSystem _popup = default!;
    [Dependency] private readonly PowerReceiverSystem _powerReceiver = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;
    [Dependency] private readonly UserInterfaceSystem _ui = default!;

    private const float TutorialTimeLimit = 300f; // 5 minutes in seconds

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<VRPodComponent, ComponentInit>(OnComponentInit);
        SubscribeLocalEvent<VRPodComponent, ActivateInWorldEvent>(OnActivate);
        SubscribeLocalEvent<VRPodComponent, LockToggledEvent>(OnLockToggled);
        SubscribeLocalEvent<VRPodComponent, PowerChangedEvent>(OnPowerChanged);
        SubscribeLocalEvent<VRPodComponent, EntityTerminatingEvent>(OnEntityTerminating);
        SubscribeLocalEvent<VRPodComponent, EntInsertedIntoContainerMessage>(OnEntityInserted);
        SubscribeLocalEvent<VRPodComponent, EntRemovedFromContainerMessage>(OnEntityRemoved);

        Subs.BuiEvents<VRPodComponent>(VRPodUiKey.Key, subs =>
        {
            subs.Event<BoundUIOpenedEvent>(OnUIOpened);
            subs.Event<VRPodSelectTutorialMessage>(OnSelectTutorial);
            subs.Event<VRPodStartTutorialMessage>(OnStartTutorial);
        });
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var query = EntityQueryEnumerator<VRPodComponent>();
        while (query.MoveNext(out var uid, out var component))
        {
            if (component.ActiveTutorial == null || component.TutorialStartTime == null)
                continue;

            CheckTimeLimit(uid, component);
        }
    }

    private void OnComponentInit(Entity<VRPodComponent> ent, ref ComponentInit args)
    {
        // Ensure the pod has EntityStorageComponent
        EnsureComp<EntityStorageComponent>(ent);
    }

    private void OnActivate(Entity<VRPodComponent> ent, ref ActivateInWorldEvent args)
    {
        if (args.Handled)
            return;

        if (!TryComp<EntityStorageComponent>(ent, out var storage))
            return;

        // Check if player is inside the pod
        var playerInside = storage.Contents.Contains(args.User);

        if (playerInside)
        {
            // Open UI if player is inside
            _ui.TryOpenUi(ent.Owner, VRPodUiKey.Key, args.User);
            args.Handled = true;
        }
    }

    private void OnLockToggled(Entity<VRPodComponent> ent, ref LockToggledEvent args)
    {
        UpdateUI(ent.Owner, ent.Comp);
    }

    private void OnPowerChanged(Entity<VRPodComponent> ent, ref PowerChangedEvent args)
    {
        if (!args.Powered && ent.Comp.ActiveTutorial != null)
        {
            // Check if we have battery backup
            if (!HasBatteryPower(ent.Owner))
            {
                EmergencyReturn(ent.Owner, ent.Comp);
            }
        }

        UpdateUI(ent.Owner, ent.Comp);
    }

    private void OnEntityTerminating(Entity<VRPodComponent> ent, ref EntityTerminatingEvent args)
    {
        if (ent.Comp.ActiveTutorial != null)
        {
            EmergencyReturn(ent.Owner, ent.Comp);
        }
    }

    private void OnEntityInserted(Entity<VRPodComponent> ent, ref EntInsertedIntoContainerMessage args)
    {
        if (args.Container.ID != SharedEntityStorageSystem.ContainerName)
            return;

        UpdateUI(ent.Owner, ent.Comp);
    }

    private void OnEntityRemoved(Entity<VRPodComponent> ent, ref EntRemovedFromContainerMessage args)
    {
        if (args.Container.ID != SharedEntityStorageSystem.ContainerName)
            return;

        UpdateUI(ent.Owner, ent.Comp);
    }

    private void OnUIOpened(Entity<VRPodComponent> ent, ref BoundUIOpenedEvent args)
    {
        UpdateUI(ent.Owner, ent.Comp);
    }

    private void OnSelectTutorial(Entity<VRPodComponent> ent, ref VRPodSelectTutorialMessage args)
    {
        if (!_prototypeManager.TryIndex<TutorialPrototype>(args.TutorialId, out var tutorial))
            return;

        ent.Comp.SelectedTutorial = args.TutorialId;
        UpdateUI(ent.Owner, ent.Comp);
    }

    private void OnStartTutorial(Entity<VRPodComponent> ent, ref VRPodStartTutorialMessage args)
    {
        if (!TryComp<EntityStorageComponent>(ent, out var storage))
            return;

        if (!CheckTutorialConditions(ent.Owner, ent.Comp, storage, args.Actor))
            return;

        StartTutorial(ent.Owner, ent.Comp, storage, args.Actor);
    }

    private bool CheckTutorialConditions(EntityUid pod, VRPodComponent component, EntityStorageComponent storage, EntityUid user)
    {
        // Check if player is inside
        if (!storage.Contents.Contains(user))
        {
            _popup.PopupEntity(Loc.GetString("vr-pod-must-be-inside"), pod, user, PopupType.Medium);
            return false;
        }

        // Check if pod is locked
        if (!TryComp<LockComponent>(pod, out var lockComp) || !lockComp.Locked)
        {
            _popup.PopupEntity(Loc.GetString("vr-pod-must-be-locked"), pod, user, PopupType.Medium);
            return false;
        }

        // Check if tutorial is selected
        if (string.IsNullOrEmpty(component.SelectedTutorial))
        {
            _popup.PopupEntity(Loc.GetString("vr-pod-no-tutorial-selected"), pod, user, PopupType.Medium);
            return false;
        }

        // Check if already in a tutorial
        if (component.ActiveTutorial != null)
        {
            _popup.PopupEntity(Loc.GetString("vr-pod-tutorial-active"), pod, user, PopupType.Medium);
            return false;
        }

        // Check power
        if (!_powerReceiver.IsPowered(pod) && !HasBatteryPower(pod))
        {
            _popup.PopupEntity(Loc.GetString("vr-pod-no-power"), pod, user, PopupType.Medium);
            return false;
        }

        // Check session limit
        if (!_playerManager.TryGetSessionByEntity(user, out var session))
            return false;

        var contentData = session.ContentData();
        if (contentData?.HasCompletedTutorialThisSession == true)
        {
            _popup.PopupEntity(Loc.GetString("vr-pod-already-completed-this-session"), pod, user, PopupType.Medium);
            return false;
        }

        return true;
    }

    private void StartTutorial(EntityUid pod, VRPodComponent component, EntityStorageComponent storage, EntityUid user)
    {
        if (!_playerManager.TryGetSessionByEntity(user, out var session))
            return;

        if (!_mindSystem.TryGetMind(user, out var mindId, out var mind))
            return;

        if (!_prototypeManager.TryIndex<TutorialPrototype>(component.SelectedTutorial!, out var tutorial))
            return;

        // Load tutorial map
        var opts = DeserializationOptions.Default with { InitializeMaps = true };
        if (!_mapLoader.TryLoadMap(tutorial.MapPath, out var map, out var grids, opts))
        {
            _popup.PopupEntity(Loc.GetString("vr-pod-map-load-failed"), pod, user, PopupType.Medium);
            Log.Error($"Failed to load tutorial map: {tutorial.MapPath}");
            return;
        }

        var mapId = map.Value.Comp.MapId;
        var grid = grids.FirstOrDefault();

        if (grid == null)
        {
            _popup.PopupEntity(Loc.GetString("vr-pod-no-grid"), pod, user, PopupType.Medium);
            _mapSystem.DeleteMap(mapId);
            return;
        }

        // Spawn tutorial body
        var spawnCoords = _mapSystem.GridTileToLocal(grid.Value, tutorial.SpawnLocation);
        var tutorialBody = Spawn("BaseMobHuman", spawnCoords);

        // Add tutorial body component
        var tutorialBodyComp = EnsureComp<TutorialBodyComponent>(tutorialBody);
        tutorialBodyComp.VRPod = pod;

        // Transfer mind to tutorial body (using Visit to keep original body ownership)
        _mindSystem.Visit(mindId, tutorialBody, mind);

        // Store tutorial state
        component.ActiveTutorial = tutorialBody;
        component.TutorialMapId = mapId;
        component.OriginalBody = user;
        component.TutorialStartTime = _timing.CurTime;

        Dirty(pod, component);

        // Popup to tutorial body since that's where the mind is now
        _popup.PopupEntity(Loc.GetString("vr-pod-tutorial-started"), tutorialBody, tutorialBody, PopupType.Medium);
    }

    private void CheckTimeLimit(EntityUid pod, VRPodComponent component)
    {
        if (component.TutorialStartTime == null)
            return;

        var elapsed = (_timing.CurTime - component.TutorialStartTime.Value).TotalSeconds;
        if (elapsed >= TutorialTimeLimit)
        {
            EmergencyReturn(pod, component);
        }
    }

    public void EmergencyReturn(EntityUid pod, VRPodComponent component)
    {
        EndTutorial(pod, component, emergency: true);
    }

    private void EndTutorial(EntityUid pod, VRPodComponent component, bool emergency = false)
    {
        if (component.ActiveTutorial == null)
            return;

        var tutorialBody = component.ActiveTutorial.Value;
        var originalBody = component.OriginalBody;
        var mapId = component.TutorialMapId;
        var tutorialId = component.SelectedTutorial;

        // Return mind to original body
        if (originalBody.HasValue && Exists(originalBody.Value))
        {
            // Get mind from tutorial body (it's visiting)
            if (TryComp<VisitingMindComponent>(tutorialBody, out var visitingMind) && visitingMind.MindId.HasValue)
            {
                if (TryComp<MindComponent>(visitingMind.MindId.Value, out var mind))
                {
                    _mindSystem.UnVisit(visitingMind.MindId.Value, mind);
                }
            }
        }

        // Mark tutorial as completed (even on emergency - they still attempted it)
        if (tutorialId != null && originalBody.HasValue && _playerManager.TryGetSessionByEntity(originalBody.Value, out var session))
        {
            var contentData = session.ContentData();
            if (contentData != null)
            {
                contentData.HasCompletedTutorialThisSession = true;

                // Add playtime tracker for this specific tutorial
                // Even on emergency, they still completed the tutorial attempt
                var trackerId = GetTutorialTrackerId(tutorialId);
                _playTime.AddTimeToTracker(session, trackerId, TimeSpan.FromSeconds(1));
            }
        }

        // Clean up tutorial body
        if (Exists(tutorialBody))
        {
            QueueDel(tutorialBody);
        }

        // Unload tutorial map
        if (mapId.HasValue)
        {
            _mapSystem.DeleteMap(mapId.Value);
        }

        // Unlock pod
        if (TryComp<LockComponent>(pod, out var lockComp))
        {
            _lockSystem.Unlock(pod, pod, lockComp);
        }

        // Clear tutorial state
        component.ActiveTutorial = null;
        component.TutorialMapId = null;
        component.OriginalBody = null;
        component.SelectedTutorial = null;
        component.TutorialStartTime = null;

        Dirty(pod, component);
        UpdateUI(pod, component);

        // Popup to the original body (mind should be back by now)
        if (originalBody.HasValue && Exists(originalBody.Value))
        {
            _popup.PopupEntity(
                emergency ? Loc.GetString("vr-pod-tutorial-ended-emergency") : Loc.GetString("vr-pod-tutorial-ended"),
                originalBody.Value,
                originalBody.Value,
                PopupType.Medium);
        }
    }

    private bool HasBatteryPower(EntityUid pod)
    {
        if (!TryComp<BatteryComponent>(pod, out var battery))
            return false;

        // Check if battery has at least 5% charge
        return battery.CurrentCharge > battery.MaxCharge * 0.05f;
    }

    private void UpdateUI(EntityUid pod, VRPodComponent component)
    {
        if (!TryComp<EntityStorageComponent>(pod, out var storage))
            return;

        var tutorials = _prototypeManager.EnumeratePrototypes<TutorialPrototype>()
            .Select(t => new TutorialInfo(t.ID, t.Name, t.Description))
            .ToList();

        var isLocked = TryComp<LockComponent>(pod, out var lockComp) && lockComp.Locked;
        var isPowered = _powerReceiver.IsPowered(pod);
        var hasBattery = HasBatteryPower(pod);

        // Check if any player is inside
        var playerInside = storage.Contents.ContainedEntities.Any();

        // Can start tutorial if: locked, powered (or has battery), player inside, tutorial selected, not already active
        var canStartTutorial = isLocked &&
                               (isPowered || hasBattery) &&
                               playerInside &&
                               !string.IsNullOrEmpty(component.SelectedTutorial) &&
                               component.ActiveTutorial == null;

        var state = new VRPodBoundUserInterfaceState(
            tutorials,
            isLocked,
            isPowered,
            hasBattery,
            playerInside,
            canStartTutorial);

        _ui.SetUiState(pod, VRPodUiKey.Key, state);
    }

    /// <summary>
    /// Helper method to generate consistent tutorial tracker IDs.
    /// </summary>
    public static string GetTutorialTrackerId(string tutorialId)
    {
        return $"TutorialCompleted:{tutorialId}";
    }
}

