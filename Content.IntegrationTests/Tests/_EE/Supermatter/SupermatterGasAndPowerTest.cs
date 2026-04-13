// SPDX-FileCopyrightText: 2026 Funkystation Contributors
//
// SPDX-License-Identifier: MIT

using Content.Server.Atmos.Components;
using Content.Server.Atmos.EntitySystems;
using Content.Shared._EE.Supermatter.Components;
using Content.Shared.Atmos;
using Content.Shared.Atmos.Components;
using Content.Shared.Radiation.Components;
using Robust.Shared.Maths;
using Robust.Shared.Random;

namespace Content.IntegrationTests.Tests._EE.Supermatter;

[TestFixture]
public sealed class SupermatterGasAndPowerTest
{
    [Test]
    public async Task SupermatterProcessingAbsorbsTileGasAndChangesPower()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Dirty = true });
        var server = pair.Server;
        server.ResolveDependency<IRobustRandom>().SetSeed(12345);

        await pair.CreateTestMap();
        // Need a positive power mix ratio after normalization; ~20/80 air clamps to 0 in ProcessAtmos.
        var mix = new GasMixture(Atmospherics.CellVolume) { Temperature = Atmospherics.T20C };
        mix.AdjustMoles(Gas.Oxygen, 70f);
        mix.AdjustMoles(Gas.Nitrogen, 30f);

        var sm = await SupermatterIntegrationTestHelpers.PrepareGridAndSpawn(pair, "Supermatter", mix, settleTicksAfterSpawn: 5);

        float molesBefore = 0;
        await server.WaitPost(() =>
        {
            var entMan = server.EntMan;
            var atmos = entMan.System<AtmosphereSystem>();
            var gridUid = pair.TestMap!.Grid.Owner;
            var ga = entMan.GetComponent<GridAtmosphereComponent>(gridUid);
            var ov = entMan.GetComponent<GasTileOverlayComponent>(gridUid);
            var tileMix = atmos.GetTileMixture((gridUid, ga, ov), null, Vector2i.Zero, excite: false);
            Assert.That(tileMix, Is.Not.Null);
            molesBefore = tileMix!.TotalMoles;
        });

        await SupermatterIntegrationTestHelpers.RunAtmosDevices(pair, 5);

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var atmos = entMan.System<AtmosphereSystem>();
            var gridUid = pair.TestMap!.Grid.Owner;
            var ga = entMan.GetComponent<GridAtmosphereComponent>(gridUid);
            var ov = entMan.GetComponent<GasTileOverlayComponent>(gridUid);
            var tileMix = atmos.GetTileMixture((gridUid, ga, ov), null, Vector2i.Zero, excite: false);
            Assert.That(tileMix, Is.Not.Null);
            Assert.That(tileMix!.TotalMoles, Is.LessThan(molesBefore));

            var comp = entMan.GetComponent<SupermatterComponent>(sm);
            Assert.That(comp.Power, Is.GreaterThan(0f));
            Assert.That(comp.HasBeenPowered, Is.True);

            var rad = entMan.GetComponent<RadiationSourceComponent>(sm);
            Assert.That(rad.Intensity, Is.GreaterThan(0f));
        });

        await pair.CleanReturnAsync();
    }
}
