// SPDX-FileCopyrightText: 2025 terkala <appleorange64@gmail.com>
//
// SPDX-License-Identifier: MIT

using Content.Shared.Medical.Surgery;
using Content.Shared.Implants.Components;
using Content.Shared.Body.Organ;
using Content.Shared.Hands.Components;
using Content.Shared.Hands.EntitySystems;
using Robust.Client.GameObjects;
using Robust.Client.UserInterface;
using Robust.Client.Player;
using Robust.Shared.Timing;

namespace Content.Client.Medical.Surgery;

public sealed class SurgeryBui : BoundUserInterface
{
    [Dependency] private readonly IEntityManager _entMan = default!;
    [Dependency] private readonly IPlayerManager _playerManager = default!;
    [Dependency] private readonly IGameTiming _timing = default!;

    private SurgeryWindow? _window;
    private TimeSpan _lastHandScan = TimeSpan.Zero;
    private const float HandScanInterval = 0.5f; // Scan hands every 0.5 seconds

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
        _window.OnToolMethodSelected += OnToolMethodSelected;
        _window.OnBodyPartSelected += OnBodyPartSelected;
        _window.OpenCentered();
        
        // Initial hand scan
        ScanAndSendHandItems();
    }

    protected override void UpdateState(BoundUserInterfaceState state)
    {
        base.UpdateState(state);

        if (state is SurgeryBoundUserInterfaceState surgeryState && _window != null)
        {
            _window.UpdateState(surgeryState);
        }
        
        // Periodically scan hands and send updates
        if (_timing.CurTime - _lastHandScan > TimeSpan.FromSeconds(HandScanInterval))
        {
            ScanAndSendHandItems();
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

    private void OnToolMethodSelected(NetEntity step, bool isImprovised)
    {
        // Send tool method selection to server
        SendMessage(new SurgeryOperationMethodSelectedMessage(step, isImprovised));
        
        // Then select the step to execute it
        OnStepSelected(step);
    }

    private void OnBodyPartSelected(Content.Shared._Shitmed.Targeting.TargetBodyPart? targetBodyPart)
    {
        SendMessage(new SurgeryBodyPartSelectedMessage(targetBodyPart));
    }

    /// <summary>
    /// Scans the local player's hands for implants and organs, then sends the list to the server.
    /// </summary>
    private void ScanAndSendHandItems()
    {
        var player = _playerManager.LocalEntity;
        if (player == null)
            return;

        if (!_entMan.TryGetComponent<HandsComponent>(player.Value, out var hands))
            return;

        var handItems = new List<(NetEntity, bool, bool, string)>();
        var handsSystem = _entMan.System<SharedHandsSystem>();

        foreach (var heldItem in handsSystem.EnumerateHeld(player.Value, hands))
        {
            var isImplant = _entMan.HasComponent<SubdermalImplantComponent>(heldItem);
            var isOrgan = _entMan.HasComponent<OrganComponent>(heldItem);
            var name = _entMan.GetComponent<MetaDataComponent>(heldItem).EntityName;
            var netEntity = _entMan.GetNetEntity(heldItem);

            if (isImplant || isOrgan)
            {
                handItems.Add((netEntity, isImplant, isOrgan, name));
            }
        }

        SendMessage(new SurgeryHandItemsMessage(handItems));
        _lastHandScan = _timing.CurTime;
    }
}

