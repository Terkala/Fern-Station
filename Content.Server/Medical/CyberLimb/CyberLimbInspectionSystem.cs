// SPDX-FileCopyrightText: 2025 terkala <appleorange64@gmail.com>
//
// SPDX-License-Identifier: MIT

using Content.Shared.Body.Part;
using Content.Shared.Damage.Prototypes;
using Content.Shared.Examine;
using Content.Shared.Medical.CyberLimb;
using Content.Shared.Overlays;
using Content.Shared.Verbs;
using Robust.Shared.Prototypes;
using Robust.Shared.Utility;

namespace Content.Server.Medical.CyberLimb;

/// <summary>
/// System that handles inspection of cyber limbs with diagnostic goggles.
/// </summary>
public sealed class CyberLimbInspectionSystem : EntitySystem
{
    [Dependency] private readonly ExamineSystemShared _examine = default!;
    [Dependency] private readonly CyberLimbStatsSystem _stats = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<CyberLimbInspectableComponent, GetVerbsEvent<ExamineVerb>>(OnGetExamineVerbs);
    }

    private void OnGetExamineVerbs(EntityUid uid, CyberLimbInspectableComponent component, ref GetVerbsEvent<ExamineVerb> args)
    {
        // Check if examiner has diagnostic goggles (ShowHealthBarsComponent with Silicon damage container)
        if (!HasDiagnosticGoggles(args.User))
            return;

        var detailsRange = _examine.IsInDetailsRange(args.User, uid);
        var stats = GetLimbStats(uid, args.User);

        var user = args.User;
        var verb = new ExamineVerb
        {
            Act = () =>
            {
                var markup = new FormattedMessage();
                markup.AddMarkupOrThrow(stats);
                _examine.SendExamineTooltip(user, uid, markup, false, false);
            },
            Text = Loc.GetString("cyberlimb-inspect-verb-text"),
            Category = VerbCategory.Examine,
            Disabled = !detailsRange,
            Message = detailsRange ? null : Loc.GetString("cyberlimb-inspect-verb-disabled"),
            Icon = new SpriteSpecifier.Texture(new("/Textures/Interface/VerbIcons/examine.svg.192dpi.png"))
        };

        args.Verbs.Add(verb);
    }

    /// <summary>
    /// Checks if the entity has diagnostic goggles equipped.
    /// Diagnostic goggles have ShowHealthBarsComponent with Silicon in DamageContainers.
    /// </summary>
    private bool HasDiagnosticGoggles(EntityUid examiner)
    {
        if (!TryComp<ShowHealthBarsComponent>(examiner, out var showHealthBars))
            return false;

        // Check if Silicon is in the damage containers list
        return showHealthBars.DamageContainers.Contains(new ProtoId<DamageContainerPrototype>("Silicon"));
    }

    /// <summary>
    /// Gets formatted stats string for a cyber limb.
    /// </summary>
    private string GetLimbStats(EntityUid limb, EntityUid examiner)
    {
        if (!TryComp<CyberLimbStorageComponent>(limb, out var storage))
            return Loc.GetString("cyberlimb-inspect-no-storage");

        // Get body stats for battery (shared) and limb stats for service time (per-limb)
        string batteryInfo = Loc.GetString("cyberlimb-inspect-battery-unknown");
        string serviceTimeInfo = Loc.GetString("cyberlimb-inspect-service-time-unknown");
        string efficiencyInfo = $"{storage.CachedEfficiency * 100:F0}%";

        // Get battery info from body (shared)
        float batteryPenalty = 1.0f;
        if (TryComp<BodyPartComponent>(limb, out var part) && part.Body != null)
        {
            if (TryComp<CyberLimbStatsComponent>(part.Body.Value, out var bodyStats))
            {
                var batteryPercent = bodyStats.CachedAverageBatteryCapacity > 0
                    ? (bodyStats.CurrentBatteryCharge / bodyStats.CachedAverageBatteryCapacity * 100)
                    : 0;
                var batteryMinutes = bodyStats.CurrentBatteryCharge > 0 && bodyStats.CachedAverageBatteryCapacity > 0
                    ? (bodyStats.CurrentBatteryCharge / bodyStats.CachedAverageBatteryCapacity * 20)
                    : 0;

                batteryInfo = Loc.GetString("cyberlimb-inspect-battery",
                    ("percent", batteryPercent.ToString("F0")),
                    ("minutes", batteryMinutes.ToString("F1")));

                batteryPenalty = bodyStats.CachedEfficiencyPenalty;
            }
        }

        // Get service time info from this specific limb
        if (storage.MaxServiceTime > 0f)
        {
            var serviceMinutes = storage.ServiceTimeRemaining / 60f;
            serviceTimeInfo = Loc.GetString("cyberlimb-inspect-service-time",
                ("minutes", serviceMinutes.ToString("F1")));
        }

        // Apply efficiency penalties: battery (shared) and service time (per-limb)
        var serviceTimePenalty = storage.IsServiceTimeExpired ? 0.5f : 1.0f;
        var finalEfficiency = storage.CachedEfficiency * batteryPenalty * serviceTimePenalty;
        efficiencyInfo = $"{finalEfficiency * 100:F0}%";

        var moduleList = GetModuleList(limb, storage);

        return Loc.GetString("cyberlimb-inspect-stats",
            ("battery", batteryInfo),
            ("serviceTime", serviceTimeInfo),
            ("efficiency", efficiencyInfo),
            ("modules", moduleList));
    }

    /// <summary>
    /// Gets a formatted list of installed modules.
    /// </summary>
    private string GetModuleList(EntityUid limb, CyberLimbStorageComponent storage)
    {
        var modules = new List<string>();

        if (storage.CachedBatteryCount > 0)
            modules.Add(Loc.GetString("cyberlimb-inspect-module-battery", ("count", storage.CachedBatteryCount)));

        if (storage.CachedMatterBinCount > 0)
            modules.Add(Loc.GetString("cyberlimb-inspect-module-matter-bin", ("count", storage.CachedMatterBinCount)));

        if (storage.CachedManipulatorCount > 0)
            modules.Add(Loc.GetString("cyberlimb-inspect-module-manipulator", ("count", storage.CachedManipulatorCount)));

        if (modules.Count == 0)
            return Loc.GetString("cyberlimb-inspect-modules-none");

        return string.Join(", ", modules);
    }
}

