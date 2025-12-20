// SPDX-FileCopyrightText: 2025 terkala <appleorange64@gmail.com>
//
// SPDX-License-Identifier: MIT

using System.Collections.Generic;
using System.Numerics;
using Content.Shared.Arcade;
using Robust.Client.UserInterface.Controls;
using Robust.Client.UserInterface.CustomControls;
using Robust.Shared.Localization;
using static Robust.Client.UserInterface.Controls.BoxContainer;

namespace Content.Client.Arcade.UI;

public sealed class VRPodWindow : DefaultWindow
{
    private readonly ItemList _tutorialList;
    private readonly RichTextLabel _descriptionLabel;
    private readonly Label _statusLabel;
    private readonly Button _startButton;

    public event Action<string>? OnTutorialSelected;
    public event Action? OnStartTutorial;

    private string? _selectedTutorialId;

    public VRPodWindow()
    {
        Title = Loc.GetString("vr-pod-window-title");
        MinSize = SetSize = new Vector2(500, 400);

        var vbox = new BoxContainer
        {
            Orientation = LayoutOrientation.Vertical,
            Children =
            {
                new Label
                {
                    Text = Loc.GetString("vr-pod-window-select-tutorial"),
                    HorizontalAlignment = Control.HAlignment.Center
                },
                new BoxContainer
                {
                    Orientation = LayoutOrientation.Horizontal,
                    Children =
                    {
                        (_tutorialList = new ItemList
                        {
                            MinSize = new Vector2(200, 200),
                            VerticalExpand = true
                        }),
                        new BoxContainer
                        {
                            Orientation = LayoutOrientation.Vertical,
                            HorizontalExpand = true,
                            Children =
                            {
                                (_descriptionLabel = new RichTextLabel
                                {
                                    MinSize = new Vector2(0, 150),
                                    VerticalExpand = true
                                }),
                                (_statusLabel = new Label
                                {
                                    Text = "",
                                    HorizontalAlignment = Control.HAlignment.Center
                                })
                            }
                        }
                    }
                },
                (_startButton = new Button
                {
                    Text = Loc.GetString("vr-pod-window-start-button"),
                    HorizontalAlignment = Control.HAlignment.Center,
                    Disabled = true
                })
            }
        };

        Contents.AddChild(vbox);

        _tutorialList.OnItemSelected += OnTutorialItemSelected;
        _startButton.OnPressed += _ => OnStartTutorial?.Invoke();
    }

    private void OnTutorialItemSelected(ItemList.ItemListSelectedEventArgs args)
    {
        if (args.ItemList[args.ItemIndex].Metadata is not string tutorialId)
            return;

        _selectedTutorialId = tutorialId;
        
        // Update description immediately
        var tutorial = args.ItemList[args.ItemIndex].Text;
        // We'll get the full description from UpdateState, but for now show the tutorial name
        _descriptionLabel.SetMessage(tutorial);
        
        OnTutorialSelected?.Invoke(tutorialId);
    }

    public void UpdateState(VRPodBoundUserInterfaceState state)
    {
        var selectedIndex = -1;
        _tutorialList.Clear();

        for (var i = 0; i < state.AvailableTutorials.Count; i++)
        {
            var tutorial = state.AvailableTutorials[i];
            var item = _tutorialList.AddItem(tutorial.Name);
            item.Metadata = tutorial.Id;

            if (tutorial.Id == _selectedTutorialId)
            {
                selectedIndex = i;
                _descriptionLabel.SetMessage(tutorial.Description);
            }
        }

        // Select the previously selected tutorial if it still exists
        if (selectedIndex >= 0)
        {
            _tutorialList.Select(selectedIndex);
        }

        // Update status
        var statusParts = new List<string>();
        statusParts.Add(Loc.GetString("vr-pod-status-locked", ("locked", state.IsLocked ? Loc.GetString("vr-pod-status-yes") : Loc.GetString("vr-pod-status-no"))));
        statusParts.Add(Loc.GetString("vr-pod-status-powered", ("powered", state.IsPowered ? Loc.GetString("vr-pod-status-yes") : Loc.GetString("vr-pod-status-no"))));
        statusParts.Add(Loc.GetString("vr-pod-status-battery", ("battery", state.HasBattery ? Loc.GetString("vr-pod-status-yes") : Loc.GetString("vr-pod-status-no"))));
        statusParts.Add(Loc.GetString("vr-pod-status-player-inside", ("inside", state.PlayerInside ? Loc.GetString("vr-pod-status-yes") : Loc.GetString("vr-pod-status-no"))));

        _statusLabel.Text = string.Join(" | ", statusParts);

        // Update start button
        _startButton.Disabled = !state.CanStartTutorial;

        // Update description if tutorial is selected
        if (_selectedTutorialId != null)
        {
            var tutorial = state.AvailableTutorials.FirstOrDefault(t => t.Id == _selectedTutorialId);
            if (tutorial != null)
            {
                _descriptionLabel.SetMessage(tutorial.Description);
            }
        }
    }
}

