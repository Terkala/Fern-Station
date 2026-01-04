// SPDX-FileCopyrightText: 2025 terkala <appleorange64@gmail.com>
//
// SPDX-License-Identifier: MIT

using Content.Shared.Body.Components;
using Content.Shared.Body.Organ;
using Content.Shared.Body.Part;
using Content.Shared.Body.Systems;
using Content.Shared.Containers.ItemSlots;
using Content.Shared.Medical.CyberLimb;
using Content.Shared.Medical.CyberLimb.Modules;
using Content.Shared.Storage;
using Content.Shared.Storage.Components;
using Content.Shared.Storage.EntitySystems;
using Content.Shared.Verbs;
using Content.Shared._Shitmed.Cybernetics;
using SharedCyberneticsFunctionalitySystem = Content.Shared._Shitmed.Cybernetics.SharedCyberneticsFunctionalitySystem;
using Content.Server.Power.Components;
using Robust.Shared.Containers;
using Robust.Shared.Timing;
using Robust.Shared.Utility;

namespace Content.Server.Medical.CyberLimb;

/// <summary>
/// System that handles cybernetics upkeep: maintenance panel state, battery wattage tracking, and service time.
/// Uses efficient timestamp-based calculations when panel is closed, and detailed wattage calculations when open.
/// </summary>
public sealed class CyberneticsUpkeepSystem : EntitySystem
{
    [Dependency] private readonly SharedBodySystem _body = default!;
    [Dependency] private readonly SharedStorageSystem _storage = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly SharedCyberneticsFunctionalitySystem _cyberneticsFunctionality = default!;
    [Dependency] private readonly SharedUserInterfaceSystem _uiSystem = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<CyberneticsUpkeepComponent, ComponentStartup>(OnUpkeepStartup);
        SubscribeLocalEvent<CyberneticsUpkeepComponent, GetVerbsEvent<ExamineVerb>>(OnGetExamineVerbs);
        // Note: BodyComponent GetVerbsEvent subscription moved to StorageImplantOrganSystem to avoid duplicates
        // Note: Container event subscriptions moved to CyberLimbStorageSystem to avoid duplicates

        // Subscribe to selection UI messages
        Subs.BuiEvents<BodyComponent>(CyberneticsSelectionUiKey.Key, subs =>
        {
            subs.Event<CyberneticsSelectionMessage>(OnCyberneticSelected);
        });
    }

    private void OnUpkeepStartup(EntityUid uid, CyberneticsUpkeepComponent component, ComponentStartup args)
    {
        // Initialize upkeep state
        UpdateUpkeepState(uid, component);
    }

    private void OnGetExamineVerbs(EntityUid uid, CyberneticsUpkeepComponent component, GetVerbsEvent<ExamineVerb> args)
    {
        // Add verb to access storage when panel is unscrewed
        if (!component.IsPanelUnscrewed)
            return;

        if (!TryComp<StorageComponent>(uid, out var storage))
            return;

        // Only show if this is a cyber part
        if (!HasComp<CyberneticsComponent>(uid))
            return;

        // Only show this verb if the cybernetic is installed in a body
        // Uninstalled cybernetics should be accessed directly via their storage UI
        if (!IsInBody(uid))
            return;

        args.Verbs.Add(new ExamineVerb
        {
            Act = () =>
            {
                // Open storage UI
                _storage.OpenStorageUI(uid, args.User, storage);
            },
            Text = "Access Maintenance Panel",
            Message = "You access the maintenance panel.",
            Icon = new SpriteSpecifier.Texture(new("/Textures/Interface/VerbIcons/inventory.svg.192dpi.png")),
            Priority = 1
        });
    }

    /// <summary>
    /// Adds verbs to the body entity to access cybernetics.
    /// Shows "Access cybernetic maintenance panel" to open selection UI.
    /// Also shows individual verbs for cybernetics with open panels to access their storage.
    /// Called by StorageImplantOrganSystem.
    /// </summary>
    public void OnGetBodyVerbs(EntityUid uid, BodyComponent component, GetVerbsEvent<Verb> args)
    {
        if (!args.CanAccess || !args.CanInteract)
            return;

        // Collect all cybernetics with storage (both body parts and organs)
        var cyberneticsWithStorage = new List<(EntityUid Uid, string Name, bool IsPanelOpen)>();
        var allParts = _body.GetBodyChildren(uid, component);
        var allOrgans = _body.GetBodyOrgans(uid, component);
        
        // Check body parts
        foreach (var (partUid, _) in allParts)
        {
            // Check if this is a cyber part with storage
            if (!HasComp<CyberneticsComponent>(partUid))
                continue;

            if (!TryComp<StorageComponent>(partUid, out _))
                continue;

            var partName = MetaData(partUid).EntityName ?? "cybernetics";
            var isPanelOpen = TryComp<CyberneticsUpkeepComponent>(partUid, out var upkeep) && upkeep.IsPanelUnscrewed;
            
            cyberneticsWithStorage.Add((partUid, partName, isPanelOpen));
        }

        // Check organs
        foreach (var (organUid, _) in allOrgans)
        {
            // Check if this is a cyber organ with storage
            if (!HasComp<CyberneticsComponent>(organUid))
                continue;

            if (!TryComp<StorageComponent>(organUid, out _))
                continue;

            var organName = MetaData(organUid).EntityName ?? "cybernetics";
            var isPanelOpen = TryComp<CyberneticsUpkeepComponent>(organUid, out var upkeep) && upkeep.IsPanelUnscrewed;
            
            cyberneticsWithStorage.Add((organUid, organName, isPanelOpen));
        }

        if (cyberneticsWithStorage.Count == 0)
            return;

        // Add verb to open selection UI
        args.Verbs.Add(new Verb
        {
            Act = () =>
            {
                OpenSelectionUI(uid, args.User);
            },
            Text = "Access cybernetic maintenance panel",
            Icon = new SpriteSpecifier.Texture(new("/Textures/Interface/VerbIcons/inventory.svg.192dpi.png")),
            Priority = 2
        });

        // Add individual verbs for cybernetics with open panels
        foreach (var (partUid, partName, isPanelOpen) in cyberneticsWithStorage)
        {
            if (!isPanelOpen)
                continue;

            if (!TryComp<StorageComponent>(partUid, out var storage))
                continue;

            args.Verbs.Add(new Verb
            {
                Act = () =>
                {
                    // Open storage UI directly (panel is already open)
                    _storage.OpenStorageUI(partUid, args.User, storage);
                },
                Text = $"Open {partName}",
                Icon = new SpriteSpecifier.Texture(new("/Textures/Interface/VerbIcons/inventory.svg.192dpi.png")),
                Priority = 1
            });
        }
    }

    /// <summary>
    /// Opens the cybernetic selection UI for a body.
    /// </summary>
    private void OpenSelectionUI(EntityUid body, EntityUid user)
    {
        if (!TryComp<BodyComponent>(body, out var bodyComp))
            return;

        // Ensure UI component exists
        EnsureComp<UserInterfaceComponent>(body);

        // Collect all cybernetics with storage (both body parts and organs)
        var cybernetics = new List<CyberneticData>();
        var allParts = _body.GetBodyChildren(body, bodyComp);
        var allOrgans = _body.GetBodyOrgans(body, bodyComp);

        // Check body parts
        foreach (var (partUid, _) in allParts)
        {
            if (!HasComp<CyberneticsComponent>(partUid))
                continue;

            if (!TryComp<StorageComponent>(partUid, out _))
                continue;

            var partName = MetaData(partUid).EntityName ?? "cybernetics";
            var isPanelOpen = TryComp<CyberneticsUpkeepComponent>(partUid, out var upkeep) && upkeep.IsPanelUnscrewed;

            cybernetics.Add(new CyberneticData(GetNetEntity(partUid), partName, isPanelOpen));
        }

        // Check organs
        foreach (var (organUid, _) in allOrgans)
        {
            if (!HasComp<CyberneticsComponent>(organUid))
                continue;

            if (!TryComp<StorageComponent>(organUid, out _))
                continue;

            var organName = MetaData(organUid).EntityName ?? "cybernetics";
            var isPanelOpen = TryComp<CyberneticsUpkeepComponent>(organUid, out var upkeep) && upkeep.IsPanelUnscrewed;

            cybernetics.Add(new CyberneticData(GetNetEntity(organUid), organName, isPanelOpen));
        }

        if (cybernetics.Count == 0)
            return;

        // Open UI and send state
        if (_uiSystem.TryOpenUi(body, CyberneticsSelectionUiKey.Key, user))
        {
            _uiSystem.SetUiState(body, CyberneticsSelectionUiKey.Key, new CyberneticsSelectionState(cybernetics));
        }
    }

    /// <summary>
    /// Handles when a cybernetic is selected from the selection UI.
    /// Opens the maintenance panel for that cybernetic.
    /// </summary>
    private void OnCyberneticSelected(Entity<BodyComponent> ent, ref CyberneticsSelectionMessage args)
    {
        var cybernetic = GetEntity(args.Cybernetic);
        
        if (!Exists(cybernetic))
            return;

        // Verify this cybernetic is actually part of this body
        var isInBody = false;
        if (TryComp<BodyPartComponent>(cybernetic, out var part))
        {
            isInBody = part.Body == ent.Owner;
        }
        else if (TryComp<OrganComponent>(cybernetic, out var organ))
        {
            isInBody = organ.Body == ent.Owner;
        }
        
        if (!isInBody)
        if (!isInBody)
            return;

        // Open the maintenance panel
        var upkeep = EnsureComp<CyberneticsUpkeepComponent>(cybernetic);
        if (!upkeep.IsPanelUnscrewed)
        {
            upkeep.IsPanelUnscrewed = true;
            Dirty(cybernetic, upkeep);
        }

        // Close the selection UI
        _uiSystem.CloseUi(ent.Owner, CyberneticsSelectionUiKey.Key);
    }

    /// <summary>
    /// Called when batteries are added or removed from cybernetics storage.
    /// Recalculates wattage if panel is open, or updates prediction if closed.
    /// Called by CyberLimbStorageSystem.
    /// </summary>
    public void OnBatteryChanged(EntityUid uid, CyberLimbStorageComponent component, ref EntInsertedIntoContainerMessage args)
    {
        if (!HasComp<CyberneticsComponent>(uid))
            return;

        // Check if the inserted item is a battery module
        if (!TryComp<CyberLimbBatteryModuleComponent>(args.Entity, out var battery))
            return;

        // Check if panel is open
        if (TryComp<CyberneticsUpkeepComponent>(uid, out var upkeep) && upkeep.IsPanelUnscrewed)
        {
            // Panel is open - adjust battery charge to match body percentage
            if (TryComp<BodyPartComponent>(uid, out var part) && part.Body != null)
            {
                OnBatteryAddedWithPanelOpen(part.Body.Value, args.Entity, battery);
            }
        }
        else
        {
            // Panel is closed - just update prediction
            if (TryComp<BodyPartComponent>(uid, out var part) && part.Body != null)
            {
                RecalculateBodyWattage(part.Body.Value);
            }
        }
    }

    /// <summary>
    /// Called by CyberLimbStorageSystem when batteries are removed.
    /// </summary>
    public void OnBatteryChanged(EntityUid uid, CyberLimbStorageComponent component, ref EntRemovedFromContainerMessage args)
    {
        if (!HasComp<CyberneticsComponent>(uid))
            return;

        // Check if the removed item is a battery module
        if (!TryComp<CyberLimbBatteryModuleComponent>(args.Entity, out var battery))
            return;

        // Check if panel is open
        if (TryComp<CyberneticsUpkeepComponent>(uid, out var upkeep) && upkeep.IsPanelUnscrewed)
        {
            // Panel is open - adjust total wattage proportionally
            if (TryComp<BodyPartComponent>(uid, out var part) && part.Body != null)
            {
                OnBatteryRemovedWithPanelOpen(part.Body.Value, args.Entity, battery);
            }
        }
        else
        {
            // Panel is closed - just update prediction
            if (TryComp<BodyPartComponent>(uid, out var part) && part.Body != null)
            {
                RecalculateBodyWattage(part.Body.Value);
            }
        }
    }

    /// <summary>
    /// Updates upkeep state based on panel state.
    /// If panel is open, calculates detailed wattage. If closed, uses efficient timestamp system.
    /// </summary>
    public void UpdateUpkeepState(EntityUid cyberPart, CyberneticsUpkeepComponent upkeep)
    {
        // When panel is unscrewed, storage should be accessible
        // When panel is screwed, storage should not be accessible
        // This is handled by the verb system above

        // Recalculate wattage based on panel state
        if (TryComp<BodyPartComponent>(cyberPart, out var part) && part.Body != null)
        {
            if (upkeep.IsPanelUnscrewed)
            {
                // Panel is open - calculate detailed wattage
                RecalculateDetailedWattage(part.Body.Value);
            }
            else
            {
                // Panel is closed - update efficient timestamp prediction
                UpdateTimestampPrediction(part.Body.Value);
            }
        }
    }

    /// <summary>
    /// Recalculates body-level wattage when batteries change or panel state changes.
    /// </summary>
    public void RecalculateBodyWattage(EntityUid body)
    {
        if (!TryComp<BodyComponent>(body, out var bodyComp))
            return;

        // Check if any cyber part has panel open
        bool anyPanelOpen = false;
        var allParts = _body.GetBodyChildren(body, bodyComp);
        foreach (var (partUid, _) in allParts)
        {
            if (!HasComp<CyberneticsComponent>(partUid))
                continue;

            if (TryComp<CyberneticsUpkeepComponent>(partUid, out var upkeep) && upkeep.IsPanelUnscrewed)
            {
                anyPanelOpen = true;
                break;
            }
        }

        if (anyPanelOpen)
        {
            // At least one panel is open - calculate detailed wattage
            RecalculateDetailedWattage(body);
        }
        else
        {
            // All panels closed - update timestamp prediction
            UpdateTimestampPrediction(body);
        }
    }

    /// <summary>
    /// Calculates detailed wattage across all cybernetics when maintenance panel is open.
    /// This is more expensive but provides accurate wattage percentages for each battery.
    /// </summary>
    private void RecalculateDetailedWattage(EntityUid body)
    {
        if (!TryComp<BodyComponent>(body, out var bodyComp))
            return;

        if (!TryComp<CyberLimbStatsComponent>(body, out var stats))
            return;

        // First, calculate total wattage across ALL cybernetics (not just ones with open panels)
        float totalMaxWattage = 0f;
        int cyberneticsCount = 0;

        var allParts = _body.GetBodyChildren(body, bodyComp);
        foreach (var (partUid, _) in allParts)
        {
            if (!HasComp<CyberneticsComponent>(partUid))
                continue;

            cyberneticsCount++;

            if (!TryComp<StorageComponent>(partUid, out var storage))
                continue;

            // Sum up all battery capacities
            foreach (var item in storage.Container.ContainedEntities)
            {
                if (TryComp<CyberLimbBatteryModuleComponent>(item, out var moduleBattery))
                {
                    totalMaxWattage += moduleBattery.MaxCharge;
                }
            }
        }

        // Calculate current total wattage based on body's BatteryComponent
        // Get percentage from BatteryComponent
        float bodyPercentage = 0f;
        if (TryComp<BatteryComponent>(body, out var bodyBattery) && bodyBattery.MaxCharge > 0f)
        {
            bodyPercentage = bodyBattery.CurrentCharge / bodyBattery.MaxCharge;
        }
        var totalCurrentWattage = totalMaxWattage * bodyPercentage;

        // Update upkeep components for parts with open panels
        foreach (var (partUid, _) in allParts)
        {
            if (!HasComp<CyberneticsComponent>(partUid))
                continue;

            if (!TryComp<CyberneticsUpkeepComponent>(partUid, out var upkeep) || !upkeep.IsPanelUnscrewed)
                continue;

            // Calculate this part's battery wattage
            float partMaxWattage = 0f;

            if (TryComp<StorageComponent>(partUid, out var storage))
            {
                foreach (var item in storage.Container.ContainedEntities)
                {
                    if (TryComp<CyberLimbBatteryModuleComponent>(item, out var moduleBattery))
                    {
                        partMaxWattage += moduleBattery.MaxCharge;
                    }
                }
            }

            // Update upkeep component with total body wattage (same for all parts)
            upkeep.CurrentTotalWattage = totalCurrentWattage;
            upkeep.MaxTotalWattage = totalMaxWattage;
            Dirty(partUid, upkeep);
        }
    }

    /// <summary>
    /// Called when a battery is added to cybernetics storage with panel open.
    /// Adjusts the battery's charge to match the body's current percentage.
    /// </summary>
    public void OnBatteryAddedWithPanelOpen(EntityUid body, EntityUid batteryEntity, CyberLimbBatteryModuleComponent battery)
    {
        if (!TryComp<CyberLimbStatsComponent>(body, out var stats))
            return;

        // Calculate body's current percentage from BatteryComponent
        float bodyPercentage = 1f; // Default to full if no battery
        if (TryComp<BatteryComponent>(body, out var bodyBattery) && bodyBattery.MaxCharge > 0f)
        {
            bodyPercentage = bodyBattery.CurrentCharge / bodyBattery.MaxCharge;
        }

        // Calculate total wattage before adding this battery
        float oldTotalMax = 0f;
        if (!TryComp<BodyComponent>(body, out var bodyComp))
            return;
        var allParts = _body.GetBodyChildren(body, bodyComp);
        foreach (var (partUid, _) in allParts)
        {
            if (!HasComp<CyberneticsComponent>(partUid))
                continue;

            if (!TryComp<StorageComponent>(partUid, out var storage))
                continue;

            foreach (var item in storage.Container.ContainedEntities)
            {
                if (item == batteryEntity)
                    continue; // Skip the battery we're adding

                if (TryComp<CyberLimbBatteryModuleComponent>(item, out var otherBattery))
                {
                    oldTotalMax += otherBattery.MaxCharge;
                }
            }
        }

        // Calculate old current wattage
        var oldCurrentWattage = oldTotalMax * bodyPercentage;

        // Add new battery's capacity
        var newTotalMax = oldTotalMax + battery.MaxCharge;
        var newCurrentWattage = oldCurrentWattage + (battery.MaxCharge * bodyPercentage);

        // The body system handles battery updates via RecalculateAveragedStats
        // We just need to update the percentage to match the new total
        // The body's percentage should stay the same, but the total wattage changes

        // Recalculate detailed wattage to update all upkeep components
        RecalculateDetailedWattage(body);
    }

    /// <summary>
    /// Called when a battery is removed from cybernetics storage with panel open.
    /// Adjusts the body's total wattage proportionally.
    /// </summary>
    public void OnBatteryRemovedWithPanelOpen(EntityUid body, EntityUid batteryEntity, CyberLimbBatteryModuleComponent battery)
    {
        if (!TryComp<CyberLimbStatsComponent>(body, out var stats))
            return;

        // Calculate body's current percentage from BatteryComponent before removal
        float bodyPercentage = 0f;
        if (TryComp<BatteryComponent>(body, out var bodyBattery) && bodyBattery.MaxCharge > 0f)
        {
            bodyPercentage = bodyBattery.CurrentCharge / bodyBattery.MaxCharge;
        }

        // Calculate total wattage before removing this battery
        float oldTotalMax = 0f;
        if (!TryComp<BodyComponent>(body, out var bodyComp))
            return;
        var allParts = _body.GetBodyChildren(body, bodyComp);
        foreach (var (partUid, _) in allParts)
        {
            if (!HasComp<CyberneticsComponent>(partUid))
                continue;

            if (!TryComp<StorageComponent>(partUid, out var storage))
                continue;

            foreach (var item in storage.Container.ContainedEntities)
            {
                if (item == batteryEntity)
                    continue; // Skip the battery we're removing

                if (TryComp<CyberLimbBatteryModuleComponent>(item, out var otherBattery))
                {
                    oldTotalMax += otherBattery.MaxCharge;
                }
            }
        }

        // Calculate old current wattage
        var oldCurrentWattage = oldTotalMax * bodyPercentage;

        // Remove battery's capacity
        var newTotalMax = oldTotalMax; // Already calculated without the removed battery
        var newCurrentWattage = oldCurrentWattage - (battery.MaxCharge * bodyPercentage);

        // Recalculate detailed wattage to update all upkeep components
        RecalculateDetailedWattage(body);
    }

    /// <summary>
    /// Updates efficient timestamp prediction when maintenance panel is closed.
    /// Calculates when batteries will run out based on current draw rate.
    /// </summary>
    private void UpdateTimestampPrediction(EntityUid body)
    {
        if (!TryComp<BodyComponent>(body, out var bodyComp))
            return;

        if (!TryComp<CyberLimbStatsComponent>(body, out var stats))
            return;

        // Count cybernetics
        int cyberneticsCount = 0;
        float totalMaxWattage = 0f;

        var allParts = _body.GetBodyChildren(body, bodyComp);
        foreach (var (partUid, _) in allParts)
        {
            if (!HasComp<CyberneticsComponent>(partUid))
                continue;

            cyberneticsCount++;

            if (!TryComp<StorageComponent>(partUid, out var storage))
                continue;

            // Sum up battery capacities
            foreach (var item in storage.Container.ContainedEntities)
            {
                if (TryComp<CyberLimbBatteryModuleComponent>(item, out var moduleBattery))
                {
                    totalMaxWattage += moduleBattery.MaxCharge;
                }
            }
        }

        if (cyberneticsCount == 0 || totalMaxWattage <= 0f)
        {
            // No cybernetics or no batteries - set empty time to zero
            foreach (var (partUid, _) in allParts)
            {
                if (!HasComp<CyberneticsComponent>(partUid))
                    continue;

                if (TryComp<CyberneticsUpkeepComponent>(partUid, out var upkeep))
                {
                    upkeep.PredictedBatteryEmptyTime = TimeSpan.Zero;
                    upkeep.PredictedMaxWattage = 0f;
                    upkeep.PredictedCyberneticsCount = 0;
                    Dirty(partUid, upkeep);
                }
            }
            return;
        }

        // Calculate current draw rate: joules per second = (totalMaxWattage / baseline) * (cyberneticsCount / 1) * joulesPerSecondPerCybernetics
        // Baseline: 2000 joules for 20 minutes (1200 seconds) for 1 cybernetic
        // So: drawRate = (totalMaxWattage / 2000) * cyberneticsCount * (2000 / 1200)
        // Simplified: drawRate = totalMaxWattage * cyberneticsCount / 1200
        var drawRate = totalMaxWattage * cyberneticsCount / CyberneticsUpkeepComponent.BaselineDurationSeconds;

        // Calculate remaining wattage from body's BatteryComponent
        float remainingWattage = 0f;
        if (TryComp<BatteryComponent>(body, out var bodyBattery))
        {
            remainingWattage = bodyBattery.CurrentCharge;
        }

        // Calculate time until empty
        var secondsUntilEmpty = drawRate > 0f ? remainingWattage / drawRate : 0f;
        var emptyTime = _timing.CurTime + TimeSpan.FromSeconds(secondsUntilEmpty);

        // Update all cyber parts with prediction
        foreach (var (partUid, _) in allParts)
        {
            if (!HasComp<CyberneticsComponent>(partUid))
                continue;

            if (TryComp<CyberneticsUpkeepComponent>(partUid, out var upkeep))
            {
                upkeep.PredictedBatteryEmptyTime = emptyTime;
                upkeep.PredictedMaxWattage = totalMaxWattage;
                upkeep.PredictedCyberneticsCount = cyberneticsCount;
                Dirty(partUid, upkeep);
            }
        }
    }

    /// <summary>
    /// Gets the current battery percentage for a cyber part.
    /// Uses detailed wattage if panel is open, timestamp prediction if closed.
    /// </summary>
    public float GetBatteryPercentage(EntityUid cyberPart, CyberneticsUpkeepComponent upkeep)
    {
        if (upkeep.IsPanelUnscrewed)
        {
            // Panel open - use detailed wattage
            if (upkeep.MaxTotalWattage > 0f)
            {
                return upkeep.CurrentTotalWattage / upkeep.MaxTotalWattage;
            }
            return 0f;
        }
        else
        {
            // Panel closed - use timestamp prediction
            if (upkeep.PredictedMaxWattage <= 0f || upkeep.PredictedCyberneticsCount == 0)
                return 0f;

            var timeRemaining = upkeep.PredictedBatteryEmptyTime - _timing.CurTime;
            if (timeRemaining <= TimeSpan.Zero)
                return 0f;

            // Calculate draw rate
            var drawRate = upkeep.PredictedMaxWattage * upkeep.PredictedCyberneticsCount / CyberneticsUpkeepComponent.BaselineDurationSeconds;
            
            // Calculate remaining wattage
            var remainingWattage = (float)timeRemaining.TotalSeconds * drawRate;
            
            // Calculate percentage
            return remainingWattage / upkeep.PredictedMaxWattage;
        }
    }

    /// <summary>
    /// Checks if a cyber part has an unscrewed maintenance panel.
    /// </summary>
    public bool IsPanelUnscrewed(EntityUid cyberPart)
    {
        return TryComp<CyberneticsUpkeepComponent>(cyberPart, out var upkeep) && upkeep.IsPanelUnscrewed;
    }

    /// <summary>
    /// Sets the panel unscrewed state.
    /// </summary>
    public void SetPanelUnscrewed(EntityUid cyberPart, bool unscrewed)
    {
        if (!TryComp<CyberneticsUpkeepComponent>(cyberPart, out var upkeep))
            return;

        upkeep.IsPanelUnscrewed = unscrewed;
        Dirty(cyberPart, upkeep);
        UpdateUpkeepState(cyberPart, upkeep);
        
        // Re-evaluate all cybernetics on the body when panel state changes
        if (TryComp<BodyPartComponent>(cyberPart, out var part) && part.Body != null)
        {
            _cyberneticsFunctionality.EvaluateAllCybernetics(part.Body.Value);
        }
        else if (TryComp<OrganComponent>(cyberPart, out var organ) && organ.Body != null)
        {
            _cyberneticsFunctionality.EvaluateAllCybernetics(organ.Body.Value);
        }
    }

    /// <summary>
    /// Checks if a cybernetic is installed in a body.
    /// </summary>
    private bool IsInBody(EntityUid uid)
    {
        // Check if it's a body part in a body
        if (TryComp<BodyPartComponent>(uid, out var part))
        {
            return part.Body != null;
        }

        // Check if it's an organ in a body
        if (TryComp<OrganComponent>(uid, out var organ))
        {
            return organ.Body != null;
        }

        return false;
    }
}

