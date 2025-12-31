// SPDX-FileCopyrightText: 2025 terkala <appleorange64@gmail.com>
//
// SPDX-License-Identifier: MIT

using Content.Shared.Body.Components;
using Content.Shared.Body.Systems;
using Content.Shared.Medical.CyberLimb;
using Content.Shared.Popups;
using Content.Shared._Shitmed.Cybernetics;
using Robust.Shared.Timing;

namespace Content.Server.Medical.CyberLimb;

/// <summary>
/// System that sends periodic reminders to players about their cybernetics maintenance status.
/// Only runs on entities with cybernetics for performance.
/// </summary>
public sealed class CyberneticsReminderSystem : EntitySystem
{
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly SharedBodySystem _body = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<CyberLimbStatsComponent, ComponentStartup>(OnCyberneticsStartup);
        SubscribeLocalEvent<CyberLimbStatsComponent, ComponentRemove>(OnCyberneticsRemoved);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var curTime = _timing.CurTime;

        // Only check entities with cybernetics reminder component (which means they have cybernetics)
        var query = EntityQueryEnumerator<CyberneticsReminderComponent, CyberLimbStatsComponent>();
        while (query.MoveNext(out var uid, out var reminder, out var stats))
        {
            // Check if enough time has passed since last reminder
            if ((curTime - reminder.LastReminderTime).TotalSeconds < reminder.ReminderInterval.TotalSeconds)
                continue;

            // Check for issues and send reminder
            CheckAndSendReminder(uid, reminder, stats, curTime);
        }
    }

    private void OnCyberneticsStartup(EntityUid uid, CyberLimbStatsComponent component, ComponentStartup args)
    {
        // Ensure reminder component exists when cybernetics are first added
        EnsureComp<CyberneticsReminderComponent>(uid);
    }

    private void OnCyberneticsRemoved(EntityUid uid, CyberLimbStatsComponent component, ComponentRemove args)
    {
        // Remove reminder component when all cybernetics are removed (for cleanup)
        RemComp<CyberneticsReminderComponent>(uid);
    }

    /// <summary>
    /// Checks for cybernetics issues and sends appropriate reminder message.
    /// Battery depletion has higher priority than service time expiration.
    /// </summary>
    private void CheckAndSendReminder(EntityUid body, CyberneticsReminderComponent reminder, CyberLimbStatsComponent stats, TimeSpan curTime)
    {
        // Priority 1: Check for battery depletion (higher priority)
        if (stats.IsBatteryDepleted)
        {
            _popup.PopupEntity("the power indicator on your cybernetics is dark", body, PopupType.Small);
            reminder.LastReminderTime = curTime;
            Dirty(body, reminder);
            return;
        }

        // Priority 2: Check for service time expiration on any cyber limb or organ
        if (HasExpiredServiceTime(body))
        {
            _popup.PopupEntity("the maintenance indicator on your cybernetics has turned red", body, PopupType.Small);
            reminder.LastReminderTime = curTime;
            Dirty(body, reminder);
            return;
        }

        // No issues, update last check time but don't send message
        reminder.LastReminderTime = curTime;
        Dirty(body, reminder);
    }

    /// <summary>
    /// Checks if any cyber limb or organ on the body has expired service time.
    /// </summary>
    private bool HasExpiredServiceTime(EntityUid body)
    {
        if (!TryComp<BodyComponent>(body, out var bodyComp))
            return false;

        // Check all body parts for cybernetics with expired service time
        var allParts = _body.GetBodyChildren(body, bodyComp);
        foreach (var (partUid, _) in allParts)
        {
            // Check if this is a cyber part (has CyberneticsComponent)
            if (!HasComp<CyberneticsComponent>(partUid))
                continue;

            // Check if service time is expired
            if (TryComp<CyberLimbStorageComponent>(partUid, out var storage) && storage.IsServiceTimeExpired)
            {
                return true;
            }
        }

        return false;
    }
}

