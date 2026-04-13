// SPDX-FileCopyrightText: 2026 Funkystation Contributors
//
// SPDX-License-Identifier: MIT

using Content.Server._EE.Supermatter.Systems;
using Content.Shared._EE.CCVars;
using Content.Shared._EE.Supermatter.Components;
using Content.Shared.Atmos;
using Robust.Shared.Configuration;
using Robust.Shared.Random;

namespace Content.IntegrationTests.Tests._EE.Supermatter;

[TestFixture]
public sealed class SupermatterDelamTypeTest
{
    [Test]
    public async Task ChooseDelamType_ForcedCVar()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Dirty = true });
        var server = pair.Server;
        var cfg = server.ResolveDependency<IConfigurationManager>();
        var oldForce = cfg.GetCVar(ECCVars.SupermatterDoForceDelam);
        var oldType = cfg.GetCVar(ECCVars.SupermatterForcedDelamType);

        try
        {
            cfg.SetCVar(ECCVars.SupermatterDoForceDelam, true);
            cfg.SetCVar(ECCVars.SupermatterForcedDelamType, DelamType.Tesla);

            server.ResolveDependency<IRobustRandom>().SetSeed(9001);
            await pair.CreateTestMap();
            var mix = new GasMixture(Atmospherics.CellVolume) { Temperature = Atmospherics.T20C };
            mix.AdjustMoles(Gas.Oxygen, 20f);
            mix.AdjustMoles(Gas.Nitrogen, 80f);

            var sm = await SupermatterIntegrationTestHelpers.PrepareGridAndSpawn(pair, "Supermatter", mix, 5);

            await server.WaitAssertion(() =>
            {
                var entMan = server.EntMan;
                var sys = entMan.System<SupermatterSystem>();
                var comp = entMan.GetComponent<SupermatterComponent>(sm);
                Assert.That(sys.ChooseDelamType(sm, comp), Is.EqualTo(DelamType.Tesla));
            });
        }
        finally
        {
            cfg.SetCVar(ECCVars.SupermatterDoForceDelam, oldForce);
            cfg.SetCVar(ECCVars.SupermatterForcedDelamType, oldType);
        }

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task ChooseDelamType_SingulooseWhenDense()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Dirty = true });
        var server = pair.Server;
        var cfg = server.ResolveDependency<IConfigurationManager>();
        var oldForce = cfg.GetCVar(ECCVars.SupermatterDoForceDelam);
        var oldSing = cfg.GetCVar(ECCVars.SupermatterDoSingulooseDelam);
        var oldTes = cfg.GetCVar(ECCVars.SupermatterDoTeslooseDelam);
        var oldMod = cfg.GetCVar(ECCVars.SupermatterSingulooseMolesModifier);

        try
        {
            cfg.SetCVar(ECCVars.SupermatterDoForceDelam, false);
            cfg.SetCVar(ECCVars.SupermatterDoSingulooseDelam, true);
            cfg.SetCVar(ECCVars.SupermatterDoTeslooseDelam, false);
            cfg.SetCVar(ECCVars.SupermatterSingulooseMolesModifier, 1f);

            server.ResolveDependency<IRobustRandom>().SetSeed(9002);
            await pair.CreateTestMap();
            var mix = new GasMixture(Atmospherics.CellVolume) { Temperature = Atmospherics.T20C };
            var threshold = cfg.GetCVar(ECCVars.SupermatterMolePenaltyThreshold);
            // ChooseDelamType uses one GasEfficiency absorb sample (~0.15 * tile moles by default).
            const float gasEff = 0.15f;
            mix.AdjustMoles(Gas.Oxygen, threshold / gasEff + 500f);

            var sm = await SupermatterIntegrationTestHelpers.PrepareGridAndSpawn(pair, "Supermatter", mix, 5);

            await server.WaitAssertion(() =>
            {
                var entMan = server.EntMan;
                var sys = entMan.System<SupermatterSystem>();
                var comp = entMan.GetComponent<SupermatterComponent>(sm);
                Assert.That(sys.ChooseDelamType(sm, comp), Is.EqualTo(DelamType.Singulo));
            });
        }
        finally
        {
            cfg.SetCVar(ECCVars.SupermatterDoForceDelam, oldForce);
            cfg.SetCVar(ECCVars.SupermatterDoSingulooseDelam, oldSing);
            cfg.SetCVar(ECCVars.SupermatterDoTeslooseDelam, oldTes);
            cfg.SetCVar(ECCVars.SupermatterSingulooseMolesModifier, oldMod);
        }

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task ChooseDelamType_TeslooseWhenPowerHigh()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Dirty = true });
        var server = pair.Server;
        var cfg = server.ResolveDependency<IConfigurationManager>();
        var oldForce = cfg.GetCVar(ECCVars.SupermatterDoForceDelam);
        var oldSing = cfg.GetCVar(ECCVars.SupermatterDoSingulooseDelam);
        var oldTes = cfg.GetCVar(ECCVars.SupermatterDoTeslooseDelam);
        var oldPowMod = cfg.GetCVar(ECCVars.SupermatterTesloosePowerModifier);

        try
        {
            cfg.SetCVar(ECCVars.SupermatterDoForceDelam, false);
            cfg.SetCVar(ECCVars.SupermatterDoSingulooseDelam, false);
            cfg.SetCVar(ECCVars.SupermatterDoTeslooseDelam, true);
            cfg.SetCVar(ECCVars.SupermatterTesloosePowerModifier, 1f);

            server.ResolveDependency<IRobustRandom>().SetSeed(9003);
            await pair.CreateTestMap();
            var mix = new GasMixture(Atmospherics.CellVolume) { Temperature = Atmospherics.T20C };
            mix.AdjustMoles(Gas.Oxygen, 20f);
            mix.AdjustMoles(Gas.Nitrogen, 80f);

            var sm = await SupermatterIntegrationTestHelpers.PrepareGridAndSpawn(pair, "Supermatter", mix, 5);

            await server.WaitAssertion(() =>
            {
                var entMan = server.EntMan;
                var sys = entMan.System<SupermatterSystem>();
                var comp = entMan.GetComponent<SupermatterComponent>(sm);
                var threshold = cfg.GetCVar(ECCVars.SupermatterPowerPenaltyThreshold);
                comp.Power = threshold + 100f;
                Assert.That(sys.ChooseDelamType(sm, comp), Is.EqualTo(DelamType.Tesla));
            });
        }
        finally
        {
            cfg.SetCVar(ECCVars.SupermatterDoForceDelam, oldForce);
            cfg.SetCVar(ECCVars.SupermatterDoSingulooseDelam, oldSing);
            cfg.SetCVar(ECCVars.SupermatterDoTeslooseDelam, oldTes);
            cfg.SetCVar(ECCVars.SupermatterTesloosePowerModifier, oldPowMod);
        }

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task ChooseDelamType_DefaultExplosionWhenBranchesDisabled()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Dirty = true });
        var server = pair.Server;
        var cfg = server.ResolveDependency<IConfigurationManager>();
        var oldForce = cfg.GetCVar(ECCVars.SupermatterDoForceDelam);
        var oldSing = cfg.GetCVar(ECCVars.SupermatterDoSingulooseDelam);
        var oldTes = cfg.GetCVar(ECCVars.SupermatterDoTeslooseDelam);

        try
        {
            cfg.SetCVar(ECCVars.SupermatterDoForceDelam, false);
            cfg.SetCVar(ECCVars.SupermatterDoSingulooseDelam, false);
            cfg.SetCVar(ECCVars.SupermatterDoTeslooseDelam, false);

            server.ResolveDependency<IRobustRandom>().SetSeed(9004);
            await pair.CreateTestMap();
            var mix = new GasMixture(Atmospherics.CellVolume) { Temperature = Atmospherics.T20C };
            mix.AdjustMoles(Gas.Oxygen, 20f);
            mix.AdjustMoles(Gas.Nitrogen, 80f);

            var sm = await SupermatterIntegrationTestHelpers.PrepareGridAndSpawn(pair, "Supermatter", mix, 5);

            await server.WaitAssertion(() =>
            {
                var entMan = server.EntMan;
                var sys = entMan.System<SupermatterSystem>();
                var comp = entMan.GetComponent<SupermatterComponent>(sm);
                comp.Power = 9000f;
                Assert.That(sys.ChooseDelamType(sm, comp), Is.EqualTo(DelamType.Explosion));
            });
        }
        finally
        {
            cfg.SetCVar(ECCVars.SupermatterDoForceDelam, oldForce);
            cfg.SetCVar(ECCVars.SupermatterDoSingulooseDelam, oldSing);
            cfg.SetCVar(ECCVars.SupermatterDoTeslooseDelam, oldTes);
        }

        await pair.CleanReturnAsync();
    }
}
