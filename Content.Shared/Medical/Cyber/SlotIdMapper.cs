// SPDX-FileCopyrightText: 2025 terkala <appleorange64@gmail.com>
//
// SPDX-License-Identifier: MIT

using System;
using System.Collections.Generic;
using Content.Shared.Humanoid;
using Content.Shared.Medical.Cyber.Components;
using Robust.Shared.GameObjects;

namespace Content.Shared.Medical.Cyber;

/// <summary>
/// Maps body slot IDs to their corresponding cybernetic slot component types.
/// Handles species-specific mappings (e.g., Diona has different valid slots than standard species).
/// </summary>
public static class SlotIdMapper
{
    // Standard mappings for most species (humans, lizards, etc.)
    private static readonly Dictionary<string, Type> StandardMappings = new()
    {
        { "left arm", typeof(CyberneticLeftArmComponent) },
        { "right arm", typeof(CyberneticRightArmComponent) },
        { "left leg", typeof(CyberneticLeftLegComponent) },
        { "right leg", typeof(CyberneticRightLegComponent) },
        { "heart", typeof(CyberneticHeartComponent) },
        { "lungs", typeof(CyberneticLungComponent) },
        { "liver", typeof(CyberneticLiverComponent) },
        { "stomach", typeof(CyberneticStomachComponent) },
        { "kidneys", typeof(CyberneticKidneyComponent) },
        { "eyes", typeof(CyberneticEyeComponent) },
    };

    // Diona-specific mappings (only stomach and lungs are valid for cybernetics)
    private static readonly Dictionary<string, Type> DionaMappings = new()
    {
        { "stomach", typeof(CyberneticDionaStomachComponent) },
        { "lungs", typeof(CyberneticDionaLungComponent) },
    };

    // Slimes: No valid cybernetic organs (empty mapping)
    private static readonly Dictionary<string, Type> SlimeMappings = new();

    /// <summary>
    /// Gets the component type for a given slot ID on a body entity.
    /// Returns null if the slot is not valid for cybernetics on this species.
    /// </summary>
    public static Type? GetComponentType(string slotId, EntityUid body, IEntityManager entManager, IComponentFactory componentFactory)
    {
        // Check species
        if (IsSlime(body, entManager, componentFactory))
            return SlimeMappings.TryGetValue(slotId, out var slimeType) ? slimeType : null;

        if (IsDiona(body, entManager, componentFactory))
            return DionaMappings.TryGetValue(slotId, out var dionaType) ? dionaType : null;

        // Default to standard mappings
        return StandardMappings.TryGetValue(slotId, out var standardType) ? standardType : null;
    }

    /// <summary>
    /// Gets the slot ID for a given component type.
    /// </summary>
    public static string? GetSlotId(Type componentType)
    {
        // Check all mappings
        foreach (var mapping in new[] { StandardMappings, DionaMappings, SlimeMappings })
        {
            foreach (var (slotId, type) in mapping)
            {
                if (type == componentType)
                    return slotId;
            }
        }

        return null;
    }

    /// <summary>
    /// Checks if a body is a Slime species.
    /// </summary>
    public static bool IsSlime(EntityUid body, IEntityManager entManager, IComponentFactory componentFactory)
    {
        if (!entManager.TryGetComponent(body, out HumanoidAppearanceComponent? appearance))
            return false;

        var speciesId = appearance.Species.ToString();
        if (string.IsNullOrEmpty(speciesId))
            return false;

        // Check if it's a slime species (could be "Slime" or "SlimePerson" depending on codebase)
        return speciesId.Contains("Slime", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Checks if a body is a Diona species.
    /// </summary>
    public static bool IsDiona(EntityUid body, IEntityManager entManager, IComponentFactory componentFactory)
    {
        if (!entManager.TryGetComponent(body, out HumanoidAppearanceComponent? appearance))
            return false;

        var speciesId = appearance.Species.ToString();
        return !string.IsNullOrEmpty(speciesId) && speciesId.Equals("Diona", StringComparison.OrdinalIgnoreCase);
    }
}
