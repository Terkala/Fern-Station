// SPDX-FileCopyrightText: 2025 terkala <appleorange64@gmail.com>
//
// SPDX-License-Identifier: MIT

using Content.Shared.Medical.Autodoc;
using Robust.Client.GameObjects;

namespace Content.Client.Medical.Autodoc;

public sealed class AutodocBoundUserInterface : BoundUserInterface
{
    [Dependency] private readonly IEntityManager _entMan = default!;

    [ViewVariables]
    private AutodocWindow? _window;

    public AutodocBoundUserInterface(EntityUid owner, Enum uiKey) : base(owner, uiKey)
    {
        _window = new AutodocWindow(owner, _entMan);

        _window.OnModeSelected += mode => SendMessage(new AutodocSetModeMessage(mode));
        _window.OnOrganSelected += organ => SendMessage(new AutodocSelectOrganMessage(organ));
        _window.OnActivate += () => SendMessage(new AutodocActivateMessage());

        _window.OnClose += () => Close();
    }

    protected override void Open()
    {
        base.Open();
        _window?.OpenCentered();
    }

    protected override void UpdateState(BoundUserInterfaceState state)
    {
        base.UpdateState(state);
        if (state is AutodocBoundUserInterfaceState uiState)
        {
            _window?.UpdateState(uiState);
        }
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (disposing)
            _window?.Dispose();
    }
}


