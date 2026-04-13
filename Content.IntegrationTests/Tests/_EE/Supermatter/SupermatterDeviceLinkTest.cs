// SPDX-FileCopyrightText: 2026 Funkystation Contributors
//
// SPDX-License-Identifier: MIT

using System.Collections.Generic;
using Content.Server.DeviceLinking.Systems;
using Content.Shared.Atmos;
using Content.Shared.DeviceLinking;
using Robust.Shared.GameObjects;
using Robust.Shared.Random;
using Robust.UnitTesting;

namespace Content.IntegrationTests.Tests._EE.Supermatter;

[TestFixture]
public sealed class SupermatterDeviceLinkTest
{
    [TestPrototypes]
    private const string LinkTestPrototypes = @"
- type: sinkPort
  id: SmLinkTestInactive
  name: sm-link-test-inactive
  description: test

- type: sinkPort
  id: SmLinkTestNormal
  name: sm-link-test-normal
  description: test

- type: entity
  id: SupermatterLinkTestSink
  components:
  - type: Transform
  - type: DeviceLinkSink
    ports:
    - SmLinkTestInactive
    - SmLinkTestNormal
";

    [Test]
    public async Task SourcePortsReachLinkedSink()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Dirty = true });
        var server = pair.Server;
        server.ResolveDependency<IRobustRandom>().SetSeed(12001);
        var listener = server.ResolveDependency<IEntitySystemManager>().GetEntitySystem<SupermatterLinkTestListenerSystem>();
        listener.Clear();

        await pair.CreateTestMap();
        var mix = new GasMixture(Atmospherics.CellVolume) { Temperature = Atmospherics.T20C };
        mix.AdjustMoles(Gas.Oxygen, 20f);
        mix.AdjustMoles(Gas.Nitrogen, 80f);

        var sm = await SupermatterIntegrationTestHelpers.PrepareGridAndSpawn(pair, "Supermatter", mix, 5);
        EntityUid sink = default;

        await server.WaitPost(() =>
        {
            var entMan = server.EntMan;
            var map = pair.TestMap!;
            var link = entMan.System<DeviceLinkSystem>();
            sink = entMan.SpawnEntity("SupermatterLinkTestSink", map.GridCoords);
            var links = new List<(string source, string sink)>
            {
                ("SupermatterInactive", "SmLinkTestInactive"),
                ("SupermatterNormal", "SmLinkTestNormal"),
            };
            link.SaveLinks(null, sm, sink, links);
            link.InvokePort(sm, "SupermatterInactive");
            link.InvokePort(sm, "SupermatterNormal");
        });

        await server.WaitAssertion(() =>
        {
            Assert.That(listener.Received.Exists(r => r is { Sink: var s, Port: "SmLinkTestInactive" } && s == sink), Is.True);
            Assert.That(listener.Received.Exists(r => r is { Sink: var s, Port: "SmLinkTestNormal" } && s == sink), Is.True);
        });

        await pair.CleanReturnAsync();
    }
}
