// SPDX-FileCopyrightText: 2025 terkala <appleorange64@gmail.com>
//
// SPDX-License-Identifier: MIT

using Content.Client.Gameplay;
using Content.Client.Medical.Limb.Targeting.Widgets;
using Content.Client.UserInterface.Systems.Gameplay;
using Content.Shared.Medical.Limb.Targeting;
using Robust.Client.GameObjects;
using Robust.Client.Player;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controllers;

namespace Content.Client.Medical.Limb.Targeting;

/// <summary>
/// UI controller for the targeting widget.
/// Manages the targeting control and syncs with LimbTargetingComponent.
/// </summary>
public sealed class TargetingUIController : UIController, IOnStateEntered<GameplayState>
{
    [Dependency] private readonly IEntityManager _entMan = default!;
    [Dependency] private readonly IPlayerManager _playerManager = default!;
    [Dependency] private readonly SharedTargetingSystem _targetingSystem = default!;

    private TargetingControl? _widget;

    private TargetingControl? Widget => UIManager.GetActiveUIWidgetOrNull<TargetingControl>();

    public override void Initialize()
    {
        base.Initialize();

        var gameplayStateLoad = UIManager.GetUIController<GameplayStateLoadController>();
        gameplayStateLoad.OnScreenLoad += OnScreenLoad;
        gameplayStateLoad.OnScreenUnload += OnScreenUnload;
    }

    private void OnScreenLoad()
    {
        _widget = Widget;
        if (_widget == null)
            return;

        _widget.OnBodyPartPressed += OnBodyPartPressed;
    }

    private void OnScreenUnload()
    {
        if (_widget != null)
        {
            _widget.OnBodyPartPressed -= OnBodyPartPressed;
            _widget = null;
        }
    }

    private void OnBodyPartPressed(TargetBodyPart target)
    {
        if (_playerManager.LocalEntity is not { } player)
            return;

        // Raise entity event to request target change
        var msg = new TargetingRequestMessage(target);
        _entMan.RaisePredictiveEvent(msg);
    }

    public void OnStateEntered(GameplayState state)
    {
        // Sync initial state
        SyncTargeting();
    }

    public void SyncTargeting()
    {
        if (_playerManager.LocalEntity is not { } player)
            return;

        if (!_entMan.TryGetComponent<LimbTargetingComponent>(player, out var targeting))
            return;

        _widget?.UpdateTargetSelection(targeting.SelectedTarget);
    }

    public void HandleComponentUpdate(LimbTargetingComponent component)
    {
        _widget?.UpdateTargetSelection(component.SelectedTarget);
    }
}
