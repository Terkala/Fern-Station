// SPDX-FileCopyrightText: 2026 Funkystation Contributors
//
// SPDX-License-Identifier: MIT

using Content.Shared._EE.Supermatter.Components;
using Content.Shared.Atmos;
using Robust.Shared.GameObjects;
using Robust.Shared.Random;

namespace Content.IntegrationTests.Tests._EE.Supermatter;

[TestFixture]
public sealed class SupermatterConsoleTest
{
    [Test]
    public async Task MonitoringConsoleTracksSupermatterOnGrid()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Dirty = true });
        var server = pair.Server;
        server.ResolveDependency<IRobustRandom>().SetSeed(8001);

        await pair.CreateTestMap();
        var mix = new GasMixture(Atmospherics.CellVolume) { Temperature = 5000f };
        mix.AdjustMoles(Gas.Oxygen, 400f);
        mix.AdjustMoles(Gas.Plasma, 100f);

        var sm = await SupermatterIntegrationTestHelpers.PrepareGridAndSpawn(pair, "Supermatter", mix, 5);

        await server.WaitPost(() =>
        {
            var entMan = server.EntMan;
            var map = pair.TestMap!;
            entMan.SpawnEntity("ComputerSupermatter", map.GridCoords);
        });

        await SupermatterIntegrationTestHelpers.RunAtmosDevices(pair, 6);

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var smComp = entMan.GetComponent<SupermatterComponent>(sm);
            Assert.That(smComp.Damage, Is.GreaterThan(0f));

            var consoleFound = false;
            var query = entMan.AllEntityQueryEnumerator<SupermatterConsoleComponent, TransformComponent>();
            while (query.MoveNext(out _, out _, out var xform))
            {
                if (xform.GridUid != pair.TestMap!.Grid.Owner)
                    continue;

                consoleFound = true;
                break;
            }

            Assert.That(consoleFound, Is.True);
        });

        await pair.CleanReturnAsync();
    }
}
