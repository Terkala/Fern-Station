// SPDX-FileCopyrightText: 2025 terkala <appleorange64@gmail.com>
//
// SPDX-License-Identifier: MIT

using Content.Shared.Medical.Surgery;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Client.GameObjects;
using Robust.Shared.Utility;
using BoxContainer = Robust.Client.UserInterface.Controls.BoxContainer;

namespace Content.Client.Medical.Surgery.Controls;

/// <summary>
/// Control that displays a surgery step with expandable tool selection options.
/// Shows the step name, and when clicked, expands to show "Use Proper Tool" and "Use Improvised Tool" options.
/// </summary>
public sealed class SurgeryStepControl : BoxContainer
{
    private readonly IEntityManager _entMan;
    private readonly NetEntity _stepNetEntity;
    private readonly SurgeryStepOperationInfo? _operationInfo;
    private readonly SurgeryLayer _layer;
    
    private Button? _stepButton;
    private BoxContainer? _toolOptionsContainer;
    private bool _isExpanded = false;

    public event Action<NetEntity>? OnStepSelected;
    public event Action<NetEntity, bool>? OnToolMethodSelected; // step, isImprovised

    public SurgeryStepControl(
        IEntityManager entMan,
        NetEntity stepNetEntity,
        SurgeryStepOperationInfo? operationInfo,
        SurgeryLayer layer)
    {
        _entMan = entMan;
        _stepNetEntity = stepNetEntity;
        _operationInfo = operationInfo;
        _layer = layer;

        SetupUI();
    }

    private void SetupUI()
    {
        // Set orientation for vertical layout
        Orientation = BoxContainer.LayoutOrientation.Vertical;
        
        // Main step button
        _stepButton = new Button
        {
            Text = GetStepName(),
            HorizontalExpand = true,
            StyleClasses = { "OpenLeft" }
        };

        _stepButton.OnPressed += _ =>
        {
            if (!_isExpanded && HasToolOptions())
            {
                ExpandToolOptions();
            }
            else
            {
                // If already expanded or no tool options, select the step
                // (tool method selection will be handled by the expanded buttons)
                if (!HasToolOptions())
                {
                    OnStepSelected?.Invoke(_stepNetEntity);
                }
            }
        };

        AddChild(_stepButton);

        // Tool options container (initially hidden)
        _toolOptionsContainer = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Horizontal,
            Visible = false,
            Margin = new Thickness(20, 0, 0, 0) // Indent for hierarchy
        };

        AddChild(_toolOptionsContainer);
    }

    private string GetStepName()
    {
        var stepEntity = _entMan.GetEntity(_stepNetEntity);
        if (_entMan.TryGetComponent<MetaDataComponent>(stepEntity, out var meta))
        {
            return meta.EntityName;
        }
        return "Unknown Step";
    }

    private bool HasToolOptions()
    {
        return _operationInfo != null && 
               (_operationInfo.HasPrimaryTools || _operationInfo.HasSecondaryMethod);
    }

    private void ExpandToolOptions()
    {
        if (_toolOptionsContainer == null || _isExpanded)
            return;

        _isExpanded = true;
        _toolOptionsContainer.Visible = true;
        _toolOptionsContainer.RemoveAllChildren();

        // Left button: Use Proper Tool
        if (_operationInfo?.HasPrimaryTools == true)
        {
            var properToolButton = new Button
            {
                Text = "Use Proper Tool",
                HorizontalExpand = true,
                StyleClasses = { "OpenLeft" }
            };

            properToolButton.OnPressed += _ =>
            {
                OnToolMethodSelected?.Invoke(_stepNetEntity, false);
            };

            _toolOptionsContainer.AddChild(properToolButton);
        }

        // Right button: Use Improvised Tool
        if (_operationInfo?.HasSecondaryMethod == true)
        {
            var improvisedToolButton = new Button
            {
                Text = "Use Improvised Tool",
                HorizontalExpand = true,
                StyleClasses = { "OpenRight" }
            };

            improvisedToolButton.OnPressed += _ =>
            {
                OnToolMethodSelected?.Invoke(_stepNetEntity, true);
            };

            _toolOptionsContainer.AddChild(improvisedToolButton);
        }

        // If no tool options available after expanding, just select the step
        if (_operationInfo == null || (!_operationInfo.HasPrimaryTools && !_operationInfo.HasSecondaryMethod))
        {
            OnStepSelected?.Invoke(_stepNetEntity);
        }
    }

    public void CollapseToolOptions()
    {
        if (_toolOptionsContainer != null)
        {
            _toolOptionsContainer.Visible = false;
            _isExpanded = false;
        }
    }
}
