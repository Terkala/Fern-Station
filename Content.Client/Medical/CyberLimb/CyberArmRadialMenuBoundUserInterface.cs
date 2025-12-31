// SPDX-FileCopyrightText: 2025 terkala <appleorange64@gmail.com>
//
// SPDX-License-Identifier: MIT

using Content.Client.Medical.CyberLimb.UI;
using Content.Shared.Medical.CyberLimb;
using JetBrains.Annotations;
using Robust.Client.Graphics;
using Robust.Client.Input;
using Robust.Client.UserInterface;

namespace Content.Client.Medical.CyberLimb;

[UsedImplicitly]
public sealed class CyberArmRadialMenuBoundUserInterface : BoundUserInterface
{
    [Dependency] private readonly IClyde _displayManager = default!;
    [Dependency] private readonly IInputManager _inputManager = default!;

    private CyberArmRadialMenu? _menu;

    public CyberArmRadialMenuBoundUserInterface(EntityUid owner, Enum uiKey) : base(owner, uiKey)
    {
        IoCManager.InjectDependencies(this);
    }

    protected override void Open()
    {
        base.Open();

        _menu = this.CreateWindow<CyberArmRadialMenu>();
        _menu.SetEntity(Owner);
        _menu.ItemSelected += OnItemSelected;
        _menu.OpenHandSelected += OnOpenHandSelected;

        // Open the menu, centered on the mouse
        var vpSize = _displayManager.ScreenSize;
        _menu.OpenCenteredAt(_inputManager.MouseScreenPosition.Position / vpSize);
    }

    protected override void UpdateState(BoundUserInterfaceState state)
    {
        base.UpdateState(state);

        if (state is CyberArmRadialMenuState menuState && _menu != null)
        {
            _menu.UpdateState(menuState);
        }
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);

        if (_menu != null)
        {
            _menu.ItemSelected -= OnItemSelected;
            _menu.OpenHandSelected -= OnOpenHandSelected;
            _menu.Dispose();
            _menu = null;
        }
    }

    private void OnItemSelected(NetEntity item)
    {
        SendMessage(new CyberArmSelectItemMessage(item));
    }

    private void OnOpenHandSelected()
    {
        SendMessage(new CyberArmOpenHandMessage());
    }
}
