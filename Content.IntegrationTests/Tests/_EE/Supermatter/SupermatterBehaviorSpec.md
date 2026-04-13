# Supermatter behavior specification (reference implementation)

This document describes the supermatter implementation in this repository as of the integration test suite addition. Use it alongside tests under this folder when porting or rewriting the system.

## Source map

| Topic | Location |
|-------|----------|
| Tick order | `Content.Server/_EE/Supermatter/Systems/SupermatterSystem.cs` — `OnSupermatterUpdated` |
| Atmos, damage, delam, vision, announcements | `Content.Server/_EE/Supermatter/Systems/SupermatterSystem.Processing.cs` |
| Component state, gas table | `Content.Shared/_EE/Supermatter/Components/SupermatterComponent.cs` — `SupermatterGasData` |
| CVars | `Content.Shared/_EE/CCVars/ECCVars.Supermatter.cs` |
| Entity prototype | `Resources/Prototypes/_DV/Entities/Structures/Power/Generation/Supermatter/supermatter.yml` |
| Monitoring console | `Content.Server/_EE/Supermatter/Consoles/SupermatterConsoleSystem.cs` |

## Processing pipeline (each `AtmosDeviceUpdateEvent`)

1. `ProcessAtmos` — absorb fraction `GasEfficiency` of tile gas; compute gas composition modifiers; ammonia consumption; CO2 powerloss dynamics; matter→power; temperature→power; radiation; heat/gas release; power decay; gravity well range; first-power log when `Power > 0`.
2. `HandleDamage` — environmental damage/healing; space exposure rules; crystal appearance.
3. `HandleDelamination` — if `Damage >= DamageDelaminationPoint` or `Delamming`.
4. `HandleLight`, `HandleVision`, `HandleStatus`, `HandleSoundLoop`, `HandleAccent`.
5. If `Power > power_penalty_threshold` CVar or `Damage > DamagePenaltyPoint`: `SupermatterZap`, `GenerateAnomalies`.

## Gas table

Per-gas contributions are in `SupermatterGasData.GasData` (transmit modifier, heat penalty, power mix ratio, heat resistance). Unknown gases default via `GetValueOrDefault` to zero contribution for missing keys.

`GetPowerMixRatios` applies those coefficients to **per-gas mole fractions** (after normalizing the absorbed mix to proportions); the result is then clamped to 0–1 for power scaling. Standard ~20/80 O₂/N₂ air clamps to **0** (N₂ has a negative power-mix coefficient).

## MapInit atmosphere top-up

On `MapInit`, the supermatter adjusts the **containing tile** toward `Atmospherics.OxygenMolesStandard` / `NitrogenMolesStandard` and invokes the inactive device-link port. Integration tests that need a controlled mix re-apply tile gas **after** spawn (see `SupermatterIntegrationTestHelpers.PrepareGridAndSpawn`).

## Status (`SupermatterStatusType`)

Derived in `GetStatus` (server): Error (no mix), Delaminating, Emergency, Danger, Warning, Caution (ambient temp > ~80% of heat penalty CVar above0°C), Normal (`Power > 5`), Inactive.

`HandleStatus` invokes `DeviceLink` **source** ports when `Status` **changes**.

## Delamination

- `ChooseDelamType` (public): optional CVar force; else singuloose if moles ≥ mole penalty threshold × modifier and singuloose enabled; else tesloose if power threshold met and enabled; else explosion.
- `HandleDelamination`: sets `PreferredDelamType`, countdown, announcements; on timeout: global message, paracusia on eligible mobs, then singulo/tesla spawn or `TriggerExplosive`.

## Damage thresholds (component defaults)

- `DamageWarningThreshold` 50, `DamageEmergencyThreshold` 500, `DamageDelamAlertPoint` 300, `DamagePenaltyPoint` 550, `DamageDelaminationPoint` 900.

## Interactions

- Hand / item use / collision paths grant `SupermatterImmune` to prevent feedback loops.
- Static bodies and SM-in-container skip collision consumption.
- Projectiles add power from damage without normal deletion popup path.

## Known gap: sliver tampering

`SupermatterDoAfterEvent` is handled by `OnGetSliver` in `SupermatterSystem.cs`, but **no YAML or system in this repo starts that do-after** (no construction/tool step references it). Treat as unwired unless added later.

## Porting tests to a clean-room rewrite

1. Keep this spec and the test names/assertions as the behavioral contract.
2. Copy `Content.IntegrationTests/Tests/_EE/Supermatter/*.cs` and `SupermatterLinkTestListenerSystem.cs`; re-register the listener in the new repo’s test host if needed.
3. Adjust only prototype IDs, namespaces, or APIs that legitimately changed; file bugs if behavior diverges without an intentional design change.
