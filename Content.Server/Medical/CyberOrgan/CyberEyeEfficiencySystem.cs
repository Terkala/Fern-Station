// SPDX-FileCopyrightText: 2025 terkala <appleorange64@gmail.com>
//
// SPDX-License-Identifier: MIT

using Content.Shared.Body.Organ;
using Content.Shared.Eye;
using Content.Shared.Inventory;
using Content.Shared.Medical.CyberLimb;
using Content.Shared.Medical.CyberOrgan;
using Content.Shared.Storage;
using Content.Shared.Storage.EntitySystems;
using Content.Shared.Tag;
using Content.Shared.Overlays;
using Content.Server.Flash.Components;
using Content.Shared._EE.Overlays.Switchable;
using Robust.Shared.Containers;
using Robust.Shared.GameObjects;
using Robust.Shared.Prototypes;

namespace Content.Server.Medical.CyberOrgan;

/// <summary>
/// System that handles cyber-eye efficiency effects: vision range scaling, telescopic mode, and HUD functionality.
/// </summary>
public sealed class CyberEyeEfficiencySystem : EntitySystem
{
    [Dependency] private readonly CyberOrganEfficiencySystem _organEfficiency = default!;
    [Dependency] private readonly SharedEyeSystem _eye = default!;
    [Dependency] private readonly SharedStorageSystem _storage = default!;
    [Dependency] private readonly TagSystem _tags = default!;

    private static readonly ProtoId<TagPrototype> HudMedicalTag = "HudMedical";
    private static readonly ProtoId<TagPrototype> HudSecurityTag = "HudSecurity";
    private static readonly ProtoId<TagPrototype> MedSecHudTag = "MedSecHud";
    private static readonly ProtoId<TagPrototype> AdminGlassesTag = "AdminGlasses";

    private const float BlindnessVisionRange = 1.0f; // Adjacent tile only
    private const float NormalVisionRange = 8.0f; // Default vision range
    private const float EfficiencyThreshold = 0.9f; // Below this, vision is impaired

    public override void Initialize()
    {
        base.Initialize();

        // Note: ComponentStartup and container event subscriptions moved to CyberOrganEfficiencySystem to avoid duplicates
    }

    /// <summary>
    /// Called by CyberOrganEfficiencySystem when a cyber organ with eyes starts up.
    /// </summary>
    public void OnEyeEfficiencyStartup(EntityUid uid, CyberOrganEfficiencyComponent component)
    {
        UpdateEyeEfficiency(uid, component);
    }

    /// <summary>
    /// Called by CyberOrganEfficiencySystem when storage changes for a cyber organ with eyes.
    /// </summary>
    public void OnEyeStorageChanged(EntityUid uid, CyberOrganEfficiencyComponent component)
    {
        if (!HasComp<EyeComponent>(uid))
            return;

        UpdateEyeEfficiency(uid, component);
    }

    /// <summary>
    /// Updates eye efficiency effects: vision range, telescopic mode, and HUD functionality.
    /// </summary>
    private void UpdateEyeEfficiency(EntityUid eyeUid, CyberOrganEfficiencyComponent efficiency)
    {
        if (!TryComp<OrganComponent>(eyeUid, out var organ) || organ.Body == null)
            return;

        var body = organ.Body.Value;
        var finalEfficiency = _organEfficiency.GetFinalEfficiency(eyeUid, efficiency);

        // Apply vision range scaling
        ApplyVisionRangeScaling(body, finalEfficiency);

        // Update HUD functionality from storage
        UpdateHudFromStorage(eyeUid, body);

        // Update telescopic range if efficiency >= 100%
        if (finalEfficiency >= 1.0f)
        {
            UpdateTelescopicRange(eyeUid, finalEfficiency);
        }
    }

    /// <summary>
    /// Applies vision range scaling based on efficiency.
    /// Efficiency < 90%: Linearly scale from normal to blindness range.
    /// </summary>
    private void ApplyVisionRangeScaling(EntityUid body, float efficiency)
    {
        if (!TryComp<EyeComponent>(body, out var eye))
            return;

        if (efficiency < EfficiencyThreshold)
        {
            // Linearly scale from normal to blindness range
            var normalizedEfficiency = efficiency / EfficiencyThreshold;
            var visionRange = NormalVisionRange * normalizedEfficiency;
            visionRange = Math.Max(visionRange, BlindnessVisionRange);
            
            // Apply via PvsScale (this affects the view range)
            _eye.SetPvsScale((body, eye), visionRange / NormalVisionRange);
        }
        else
        {
            _eye.SetPvsScale((body, eye), 1.0f); // Normal vision
        }

        Dirty(body, eye);
    }

    /// <summary>
    /// Updates telescopic range based on efficiency.
    /// Pre-computed during installation and stored in CyberEyeDataComponent.
    /// </summary>
    private void UpdateTelescopicRange(EntityUid eyeUid, float efficiency)
    {
        if (!TryComp<CyberEyeDataComponent>(eyeUid, out var eyeData))
        {
            eyeData = EnsureComp<CyberEyeDataComponent>(eyeUid);
        }

        // Calculate telescopic range: base range + (efficiency - 1.0) * multiplier
        // For example: 110% efficiency = 1.1x range, 150% = 1.5x range
        var telescopicRange = NormalVisionRange * efficiency;
        eyeData.TelescopicRange = telescopicRange;
        Dirty(eyeUid, eyeData);
    }

    /// <summary>
    /// Checks cyber-eyes storage for HUD items and applies corresponding components to the body.
    /// </summary>
    private void UpdateHudFromStorage(EntityUid eyeUid, EntityUid body)
    {
        if (!TryComp<StorageComponent>(eyeUid, out var storage))
            return;

        bool hasMedHud = false;
        bool hasSecHud = false;
        bool hasMedSecHud = false;
        bool hasAdminGlasses = false;
        bool hasNightVision = false;
        bool hasFlashResistance = false;

        // Check all items in storage
        foreach (var item in storage.Container.ContainedEntities)
        {
            // Check for HUD items by tag
            if (_tags.HasTag(item, HudMedicalTag))
            {
                hasMedHud = true;
            }
            else if (_tags.HasTag(item, HudSecurityTag))
            {
                hasSecHud = true;
            }
            else if (_tags.HasTag(item, MedSecHudTag))
            {
                hasMedSecHud = true;
            }
            else if (_tags.HasTag(item, AdminGlassesTag))
            {
                hasAdminGlasses = true;
            }
            else if (HasComp<NightVisionComponent>(item))
            {
                hasNightVision = true;
            }
            else if (HasComp<FlashImmunityComponent>(item))
            {
                hasFlashResistance = true;
            }
        }

        // Apply HUD components to body
        if (hasMedSecHud || (hasMedHud && hasSecHud))
        {
            // MedSec HUD: Add all HUD components
            EnsureComp<ShowHealthBarsComponent>(body);
            EnsureComp<ShowJobIconsComponent>(body);
            EnsureComp<ShowMindShieldIconsComponent>(body);
            EnsureComp<ShowCriminalRecordIconsComponent>(body);
        }
        else
        {
            if (hasMedHud)
            {
                EnsureComp<ShowHealthBarsComponent>(body);
            }
            else
            {
                RemComp<ShowHealthBarsComponent>(body);
            }

            if (hasSecHud)
            {
                EnsureComp<ShowJobIconsComponent>(body);
                EnsureComp<ShowMindShieldIconsComponent>(body);
                EnsureComp<ShowCriminalRecordIconsComponent>(body);
            }
            else
            {
                RemComp<ShowJobIconsComponent>(body);
                RemComp<ShowMindShieldIconsComponent>(body);
                RemComp<ShowCriminalRecordIconsComponent>(body);
            }
        }

        // Admin glasses
        if (hasAdminGlasses)
        {
            // Add admin vision component (this would need to be defined elsewhere)
            // For now, we'll just ensure the component exists if it's a known type
        }

        // Night vision
        if (hasNightVision)
        {
            EnsureComp<NightVisionComponent>(body);
        }
        else
        {
            RemComp<NightVisionComponent>(body);
        }

        // Flash resistance
        if (hasFlashResistance)
        {
            EnsureComp<FlashImmunityComponent>(body);
        }
        else
        {
            RemComp<FlashImmunityComponent>(body);
        }
    }
}

