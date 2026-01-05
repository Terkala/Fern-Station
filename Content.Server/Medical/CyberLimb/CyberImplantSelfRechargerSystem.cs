// SPDX-FileCopyrightText: 2025 terkala <appleorange64@gmail.com>
//
// SPDX-License-Identifier: MIT

using Content.Shared.Implants.Components;
using Content.Shared.Medical.CyberLimb;
using Content.Server.Power.Components;
using Content.Server.Power.EntitySystems;
using Robust.Shared.Timing;

namespace Content.Server.Medical.CyberLimb;

/// <summary>
/// System that handles self-recharging implants that recharge the body's battery.
/// </summary>
public sealed class CyberImplantSelfRechargerSystem : EntitySystem
{
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly BatterySystem _battery = default!;

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        // Query all implants with CyberImplantSelfRechargerComponent and SubdermalImplantComponent
        var query = EntityQueryEnumerator<CyberImplantSelfRechargerComponent, SubdermalImplantComponent>();
        while (query.MoveNext(out var implantUid, out var recharger, out var implant))
        {
            // Get the body this implant is implanted in
            if (implant.ImplantedEntity == null)
                continue;

            var body = implant.ImplantedEntity.Value;

            // Check if body has BatteryComponent
            if (!TryComp<BatteryComponent>(body, out var battery))
                continue;

            // Check if battery is full
            if (battery.CurrentCharge >= battery.MaxCharge)
                continue;

            // Handle pause logic if enabled
            if (recharger.AutoRechargePause)
            {
                // Check if we're in pause period (would need to track this, but for now we'll skip pause logic)
                // The pause logic would need a NextAutoRecharge field similar to BatterySelfRechargerComponent
                // For simplicity, we'll implement basic pause logic if needed later
            }

            // Recharge the body's battery
            var rechargeAmount = recharger.AutoRechargeRate * frameTime;
            _battery.SetCharge(body, battery.CurrentCharge + rechargeAmount, battery);
        }
    }
}
