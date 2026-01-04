// SPDX-FileCopyrightText: 2025 terkala <appleorange64@gmail.com>
//
// SPDX-License-Identifier: MIT

using Content.Client.Medical.CyberLimb.UI;
using Content.Shared.Medical.CyberLimb;
using JetBrains.Annotations;
using Robust.Client.UserInterface;

namespace Content.Client.Medical.CyberLimb;

[UsedImplicitly]
public sealed class CyberneticsSelectionBoundUserInterface : BoundUserInterface
{
    [ViewVariables]
    private CyberneticsSelectionWindow? _window;

    public CyberneticsSelectionBoundUserInterface(EntityUid owner, Enum uiKey) : base(owner, uiKey)
    {
    }

    protected override void Open()
    {
        base.Open();

        _window = this.CreateWindow<CyberneticsSelectionWindow>();
        _window.OnCyberneticSelected += OnCyberneticSelected;
    }

    protected override void UpdateState(BoundUserInterfaceState state)
    {
        base.UpdateState(state);

        if (state is CyberneticsSelectionState selectionState && _window != null)
        {
            _window.UpdateState(selectionState);
        }
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);

        if (!disposing)
            return;

        if (_window != null)
        {
            _window.OnCyberneticSelected -= OnCyberneticSelected;
            _window.Dispose();
            _window = null;
        }
    }

    private void OnCyberneticSelected(NetEntity cybernetic)
    {
        SendMessage(new CyberneticsSelectionMessage(cybernetic));
    }
}
