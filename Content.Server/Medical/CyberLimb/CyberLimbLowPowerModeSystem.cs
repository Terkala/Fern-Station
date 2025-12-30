// SPDX-FileCopyrightText: 2025 terkala <appleorange64@gmail.com>
//
// SPDX-License-Identifier: MIT

using Content.Shared.Medical.CyberLimb;
using Content.Shared.Movement.Events;
using Content.Shared.Interaction;
using Content.Shared.Interaction.Events;
using Content.Shared.Hands;
using Content.Shared.Hands.Components;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Speech;
using Robust.Shared.Timing;

namespace Content.Server.Medical.CyberLimb;

/// <summary>
/// System that tracks player inactivity for cyberlimb low power mode.
/// When inactive for 10 seconds, cyberlimbs consume 50% power and accumulate 50% maintenance time.
/// If 50% is too high, change the percentage even lower. The intent is for AFK players to not come back to all their cyberwear being broken.
/// </summary>
public sealed class CyberLimbLowPowerModeSystem : EntitySystem
{
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly SharedHandsSystem _hands = default!;

    private const float UpdateInterval = 10.0f; // Check every 10 seconds, not every tick

    public override void Initialize()
    {
        base.Initialize();

        // Subscribe to movement events
        SubscribeLocalEvent<CyberLimbLowPowerModeComponent, MoveInputEvent>(OnMoveInput);
        
        // Subscribe to interaction events (but not speech)
        SubscribeLocalEvent<CyberLimbLowPowerModeComponent, InteractHandEvent>(OnInteract);
        SubscribeLocalEvent<CyberLimbLowPowerModeComponent, InteractUsingEvent>(OnInteractUsing);
        SubscribeLocalEvent<CyberLimbLowPowerModeComponent, UseInHandEvent>(OnUseInHand);
        SubscribeLocalEvent<CyberLimbLowPowerModeComponent, HandSelectedEvent>(OnHandSelected);
        SubscribeLocalEvent<CyberLimbLowPowerModeComponent, HandDeselectedEvent>(OnHandDeselected);
        
        // Note: We intentionally do NOT subscribe to EntitySpokeEvent - speaking doesn't count as activity
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var curTime = _timing.CurTime;
        var query = EntityQueryEnumerator<CyberLimbLowPowerModeComponent>();

        while (query.MoveNext(out var uid, out var lowPower))
        {
            // Only check every 10 second for performance
            var timeSinceLastCheck = (curTime - lowPower.LastActivityTime).TotalSeconds;
            
            // Check if we should enter/exit low power mode
            bool shouldBeLowPower = timeSinceLastCheck >= lowPower.InactivityThreshold;
            
            if (shouldBeLowPower != lowPower.IsLowPowerMode)
            {
                lowPower.IsLowPowerMode = shouldBeLowPower;
                Dirty(uid, lowPower);
            }
        }
    }

    /// <summary>
    /// Marks the entity as active (resets inactivity timer).
    /// </summary>
    private void MarkActive(EntityUid uid, CyberLimbLowPowerModeComponent component)
    {
        component.LastActivityTime = _timing.CurTime;
        
        // If we were in low power mode, exit it immediately
        if (component.IsLowPowerMode)
        {
            component.IsLowPowerMode = false;
            Dirty(uid, component);
        }
    }

    private void OnMoveInput(Entity<CyberLimbLowPowerModeComponent> ent, ref MoveInputEvent args)
    {
        MarkActive(ent, ent.Comp);
    }

    private void OnInteract(Entity<CyberLimbLowPowerModeComponent> ent, ref InteractHandEvent args)
    {
        if (args.Handled)
            return;
        MarkActive(ent, ent.Comp);
    }

    private void OnInteractUsing(Entity<CyberLimbLowPowerModeComponent> ent, ref InteractUsingEvent args)
    {
        if (args.Handled)
            return;
        MarkActive(ent, ent.Comp);
    }

    private void OnUseInHand(Entity<CyberLimbLowPowerModeComponent> ent, ref UseInHandEvent args)
    {
        if (args.Handled)
            return;
        MarkActive(ent, ent.Comp);
    }

    private void OnHandSelected(Entity<CyberLimbLowPowerModeComponent> ent, ref HandSelectedEvent args)
    {
        MarkActive(ent, ent.Comp);
    }

    private void OnHandDeselected(Entity<CyberLimbLowPowerModeComponent> ent, ref HandDeselectedEvent args)
    {
        MarkActive(ent, ent.Comp);
    }

    /// <summary>
    /// Gets the power consumption multiplier for low power mode (0.5 if in low power mode, 1.0 otherwise).
    /// </summary>
    public float GetPowerConsumptionMultiplier(EntityUid body)
    {
        if (!TryComp<CyberLimbLowPowerModeComponent>(body, out var lowPower))
            return 1.0f;

        return lowPower.IsLowPowerMode ? 0.5f : 1.0f;
    }

    /// <summary>
    /// Gets the maintenance time accumulation multiplier for low power mode (0.5 if in low power mode, 1.0 otherwise).
    /// </summary>
    public float GetMaintenanceTimeMultiplier(EntityUid body)
    {
        if (!TryComp<CyberLimbLowPowerModeComponent>(body, out var lowPower))
            return 1.0f;

        return lowPower.IsLowPowerMode ? 0.5f : 1.0f;
    }
}

