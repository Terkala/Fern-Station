// SPDX-FileCopyrightText: 2025 terkala <appleorange64@gmail.com>
//
// SPDX-License-Identifier: MIT

using Content.Shared.Medical.Surgery;
using Robust.Client.GameObjects;
using Robust.Client.UserInterface;
using Robust.Client.Player;

namespace Content.Client.Medical.Surgery;

public sealed class SurgeryBui : BoundUserInterface
{
    [Dependency] private readonly IEntityManager _entMan = default!;
    [Dependency] private readonly IPlayerManager _playerManager = default!;

    private SurgeryWindow? _window;

    public SurgeryBui(EntityUid owner, Enum uiKey) : base(owner, uiKey)
    {
    }

    protected override void Open()
    {
        base.Open();

        _window = new SurgeryWindow(_entMan);
        _window.OnClose += Close;
        _window.OnLayerChanged += OnLayerChanged;
        _window.OnStepSelected += OnStepSelected;
        _window.OnBodyPartSelected += OnBodyPartSelected;
        _window.OpenCentered();
    }

    protected override void UpdateState(BoundUserInterfaceState state)
    {
        base.UpdateState(state);

        if (state is SurgeryBoundUserInterfaceState surgeryState && _window != null)
        {
            _window.UpdateState(surgeryState);
        }
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (disposing)
        {
            _window?.Dispose();
        }
    }

    private void OnLayerChanged(SurgeryLayer layer)
    {
        SendMessage(new SurgeryLayerChangedMessage(layer));
    }

    private void OnStepSelected(NetEntity step)
    {
        // Get the local player entity to pass as user (for bone smashing which needs held item)
        var player = _playerManager.LocalEntity;
        SendMessage(new SurgeryStepSelectedMessage(step, _window?.CurrentLayer ?? SurgeryLayer.Skin, player != null ? _entMan.GetNetEntity(player.Value) : null));
    }

    private void OnBodyPartSelected(Content.Shared._Shitmed.Targeting.TargetBodyPart? targetBodyPart)
    {
        SendMessage(new SurgeryBodyPartSelectedMessage(targetBodyPart));
    }
}

