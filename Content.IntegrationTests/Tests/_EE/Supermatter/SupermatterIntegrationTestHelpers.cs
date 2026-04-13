// SPDX-FileCopyrightText: 2026 Funkystation Contributors
//
// SPDX-License-Identifier: MIT

using Content.IntegrationTests.Pair;
using Content.Server.Atmos.Components;
using Content.Server.Atmos.EntitySystems;
using Content.Shared.Atmos;
using Content.Shared.Atmos.Components;
using NUnit.Framework;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Maths;

namespace Content.IntegrationTests.Tests._EE.Supermatter;

internal static class SupermatterIntegrationTestHelpers
{
    /// <summary>
    /// Ensures the test grid has simulated atmosphere, optionally sets the (0,0) tile mix, and spawns the entity.
    /// </summary>
    public static async Task<EntityUid> PrepareGridAndSpawn(
        TestPair pair,
        string prototype,
        GasMixture? originMix = null,
        int settleTicksAfterSpawn = 15)
    {
        var map = pair.TestMap ?? throw new InvalidOperationException("Call CreateTestMap first.");
        var server = pair.Server;
        EntityUid ent = default;

        await server.WaitPost(() =>
        {
            var entMan = server.EntMan;
            var gridUid = map.Grid.Owner;

            if (!entMan.HasComponent<GridAtmosphereComponent>(gridUid))
                entMan.AddComponent<GridAtmosphereComponent>(gridUid);
            // AtmosphereSystem only simulates grids that also have this component (see UpdateProcessing query).
            if (!entMan.HasComponent<GasTileOverlayComponent>(gridUid))
                entMan.AddComponent<GasTileOverlayComponent>(gridUid);

            var atmos = entMan.System<AtmosphereSystem>();
            var gridAtmos = entMan.GetComponent<GridAtmosphereComponent>(gridUid);
            atmos.InvalidateTile((gridUid, gridAtmos), Vector2i.Zero);
        });

        // Let Revalidate create the (0,0) tile atmosphere entry before we merge or spawn devices.
        await server.WaitRunTicks(30);

        await server.WaitPost(() =>
        {
            var entMan = server.EntMan;
            // Prototypes like Supermatter already have anchored transform; do not re-anchor (snap grid assert).
            ent = entMan.SpawnEntity(prototype, map.GridCoords);
        });

        // Supermatter MapInit tops the tile up toward standard O2/N2; re-apply the scenario mix afterward.
        await server.WaitRunTicks(1);

        if (originMix != null)
        {
            await server.WaitPost(() =>
            {
                var entMan = server.EntMan;
                var atmos = entMan.System<AtmosphereSystem>();
                var gridUid = map.Grid.Owner;
                var gridAtmos = entMan.GetComponent<GridAtmosphereComponent>(gridUid);
                var overlay = entMan.GetComponent<GasTileOverlayComponent>(gridUid);
                var mix = atmos.GetTileMixture((gridUid, gridAtmos, overlay), null, Vector2i.Zero, excite: true);
                Assert.That(mix, Is.Not.Null, "Tile (0,0) should exist on the test grid after invalidate + ticks.");
                mix!.Clear();
                atmos.Merge(mix, originMix.Clone());
            });
        }

        await server.WaitRunTicks(settleTicksAfterSpawn);

        return ent;
    }

    /// <summary>
    /// Advances the simulation so grid-linked <see cref="Content.Server.Atmos.Piping.Components.AtmosDeviceComponent"/> entities
    /// (including supermatter) receive <c>AtmosDeviceUpdateEvent</c>. Those are processed in <see cref="AtmosphereSystem"/>'s
    /// AtmosDevices state, not in <c>AtmosDeviceSystem.Update</c> (which only runs devices stuck in the off-grid join set).
    /// </summary>
    public static async Task RunAtmosDevices(TestPair pair, int cycles = 3)
    {
        var ticks = Math.Clamp(cycles * 20, 40, 400);
        await pair.Server.WaitRunTicks(ticks);
    }

    public static async Task RunManyTicks(TestPair pair, int ticks = 60)
    {
        await pair.Server.WaitRunTicks(ticks);
    }
}
