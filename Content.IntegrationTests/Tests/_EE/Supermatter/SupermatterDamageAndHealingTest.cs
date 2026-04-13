// SPDX-FileCopyrightText: 2026 Funkystation Contributors
//
// SPDX-License-Identifier: MIT

using Content.Server._EE.Supermatter.Systems;
using Content.Shared._EE.Supermatter.Components;
using Content.Shared.Atmos;
using Robust.Shared.Random;

namespace Content.IntegrationTests.Tests._EE.Supermatter;

[TestFixture]
public sealed class SupermatterDamageAndHealingTest
{
    [Test]
    public async Task HotDenseGasIncreasesDamage()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Dirty = true });
        var server = pair.Server;
        server.ResolveDependency<IRobustRandom>().SetSeed(222);

        await pair.CreateTestMap();
        var mix = new GasMixture(Atmospherics.CellVolume) { Temperature = 5000f };
        mix.AdjustMoles(Gas.Oxygen, 400f);
        mix.AdjustMoles(Gas.Plasma, 100f);

        var sm = await SupermatterIntegrationTestHelpers.PrepareGridAndSpawn(pair, "Supermatter", mix, 5);
        await SupermatterIntegrationTestHelpers.RunAtmosDevices(pair, 8);

        await server.WaitAssertion(() =>
        {
            var comp = server.EntMan.GetComponent<SupermatterComponent>(sm);
            Assert.That(comp.Damage, Is.GreaterThan(0f));
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task VacuumDoesNotDamageInactiveCrystal()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Dirty = true });
        var server = pair.Server;
        server.ResolveDependency<IRobustRandom>().SetSeed(333);

        await pair.CreateTestMap();
        var mix = new GasMixture(Atmospherics.CellVolume) { Temperature = Atmospherics.T20C };
        // Near-vacuum tile
        mix.AdjustMoles(Gas.Oxygen, 0.001f);

        var sm = await SupermatterIntegrationTestHelpers.PrepareGridAndSpawn(pair, "Supermatter", mix, 5);

        await server.WaitAssertion(() =>
        {
            var comp = server.EntMan.GetComponent<SupermatterComponent>(sm);
            comp.Power = 0f;
            comp.Damage = 0f;
            // HandleDamage consults Status before HandleStatus runs this tick; sync to inactive after depowering.
            comp.Status = SupermatterStatusType.Inactive;
        });

        await SupermatterIntegrationTestHelpers.RunAtmosDevices(pair, 4);

        await server.WaitAssertion(() =>
        {
            var comp = server.EntMan.GetComponent<SupermatterComponent>(sm);
            Assert.That(comp.Status, Is.EqualTo(SupermatterStatusType.Inactive));
            Assert.That(comp.Damage, Is.EqualTo(0f));
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task VacuumDamagesPoweredCrystal()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Dirty = true });
        var server = pair.Server;
        server.ResolveDependency<IRobustRandom>().SetSeed(444);

        await pair.CreateTestMap();
        // True vacuum branch in HandleDamage requires no absorbable moles (not merely "sparse" gas).
        var mix = new GasMixture(Atmospherics.CellVolume) { Temperature = Atmospherics.T20C };

        var sm = await SupermatterIntegrationTestHelpers.PrepareGridAndSpawn(pair, "Supermatter", mix, 5);

        await server.WaitAssertion(() =>
        {
            var comp = server.EntMan.GetComponent<SupermatterComponent>(sm);
            comp.Power = 200f;
            comp.Damage = 0f;
        });

        await SupermatterIntegrationTestHelpers.RunAtmosDevices(pair, 4);

        await server.WaitAssertion(() =>
        {
            var comp = server.EntMan.GetComponent<SupermatterComponent>(sm);
            Assert.That(comp.Damage, Is.GreaterThan(0f));
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public void GetIntegrityMatchesReference()
    {
        var sm = new SupermatterComponent { Damage = 450f, DamageDelaminationPoint = 900 };
        var integrity = SupermatterSystem.GetIntegrity(sm);
        Assert.That(integrity, Is.EqualTo(50f).Within(0.01f));
    }
}
