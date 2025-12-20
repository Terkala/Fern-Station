// SPDX-FileCopyrightText: 2025 terkala <appleorange64@gmail.com>
//
// SPDX-License-Identifier: MIT

using Content.Shared.Arcade;
using Robust.Client.UserInterface;

namespace Content.Client.Arcade.UI;

public sealed class VRPodBoundUserInterface : BoundUserInterface
{
    [ViewVariables]
    private VRPodWindow? _window;

    public VRPodBoundUserInterface(EntityUid owner, Enum uiKey) : base(owner, uiKey)
    {
    }

    protected override void Open()
    {
        base.Open();
        _window = this.CreateWindow<VRPodWindow>();
        _window.OnTutorialSelected += OnTutorialSelected;
        _window.OnStartTutorial += OnStartTutorial;
    }

    protected override void UpdateState(BoundUserInterfaceState state)
    {
        base.UpdateState(state);

        if (state is VRPodBoundUserInterfaceState uiState)
        {
            _window?.UpdateState(uiState);
        }
    }

    private void OnTutorialSelected(string tutorialId)
    {
        SendMessage(new VRPodSelectTutorialMessage(tutorialId));
    }

    private void OnStartTutorial()
    {
        SendMessage(new VRPodStartTutorialMessage());
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (!disposing)
            return;

        _window?.Dispose();
    }
}


