// SPDX-FileCopyrightText: 2026 Funkystation Contributors
//
// SPDX-License-Identifier: MIT

using Content.Shared._EE.CCVars;
using Content.Shared._EE.Supermatter.Components;
using Content.Shared.Atmos;
using Robust.Shared.Configuration;
using Robust.Shared.GameObjects;
using Robust.Shared.Random;
using Robust.UnitTesting;

namespace Content.IntegrationTests.Tests._EE.Supermatter;

[TestFixture]
public sealed class SupermatterDelamTimelineTest
{
    [TestPrototypes]
    private const string DelamTestPrototypes = @"
- type: entity
  id: SupermatterIntegrationSpawnMarker
  components:
  - type: Transform
  - type: MetaData
    name: sm-spawn-marker

- type: entity
  id: SupermatterIntegrationDelamTest
  parent: Supermatter
  components:
  - type: Supermatter
    delamTimer: 0.25
    singularitySpawnPrototype: SupermatterIntegrationSpawnMarker
";

    [Test]
    public async Task DelaminationTimerSpawnsConfiguredEntity()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Dirty = true });
        var server = pair.Server;
        var cfg = server.ResolveDependency<IConfigurationManager>();
        var oldForce = cfg.GetCVar(ECCVars.SupermatterDoForceDelam);
        var oldType = cfg.GetCVar(ECCVars.SupermatterForcedDelamType);

        try
        {
            cfg.SetCVar(ECCVars.SupermatterDoForceDelam, true);
            cfg.SetCVar(ECCVars.SupermatterForcedDelamType, DelamType.Singulo);

            server.ResolveDependency<IRobustRandom>().SetSeed(42);
            await pair.CreateTestMap();
            var mix = new GasMixture(Atmospherics.CellVolume) { Temperature = Atmospherics.T20C };
            mix.AdjustMoles(Gas.Oxygen, 20f);
            mix.AdjustMoles(Gas.Nitrogen, 80f);

            var sm = await SupermatterIntegrationTestHelpers.PrepareGridAndSpawn(pair, "SupermatterIntegrationDelamTest", mix, 5);

            await server.WaitAssertion(() =>
            {
                var comp = server.EntMan.GetComponent<SupermatterComponent>(sm);
                comp.Damage = comp.DamageDelaminationPoint + 100f;
            });

            await SupermatterIntegrationTestHelpers.RunManyTicks(pair, 240);

            await server.WaitAssertion(() =>
            {
                var entMan = server.EntMan;
                var found = false;
                var query = entMan.AllEntityQueryEnumerator<MetaDataComponent>();
                while (query.MoveNext(out var uid, out var meta))
                {
                    if (meta.EntityPrototype?.ID == "SupermatterIntegrationSpawnMarker")
                    {
                        found = true;
                        break;
                    }
                }

                Assert.That(found, Is.True);
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
    public async Task DamageAtThresholdStartsDelaminationState()
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

            server.ResolveDependency<IRobustRandom>().SetSeed(43);
            await pair.CreateTestMap();
            var mix = new GasMixture(Atmospherics.CellVolume) { Temperature = Atmospherics.T20C };
            mix.AdjustMoles(Gas.Oxygen, 20f);
            mix.AdjustMoles(Gas.Nitrogen, 80f);

            var sm = await SupermatterIntegrationTestHelpers.PrepareGridAndSpawn(pair, "Supermatter", mix, 5);

            // Stay above the delam threshold after HandleDamage runs (cool standard mix can heal slightly).
            await server.WaitAssertion(() =>
            {
                var comp = server.EntMan.GetComponent<SupermatterComponent>(sm);
                comp.Damage = comp.DamageDelaminationPoint + 50f;
            });

            await SupermatterIntegrationTestHelpers.RunAtmosDevices(pair, 2);

            await server.WaitAssertion(() =>
            {
                var comp = server.EntMan.GetComponent<SupermatterComponent>(sm);
                Assert.That(comp.Delamming, Is.True);
                Assert.That(comp.PreferredDelamType, Is.EqualTo(DelamType.Explosion));
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
