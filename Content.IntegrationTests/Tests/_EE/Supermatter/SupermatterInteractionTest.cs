// SPDX-FileCopyrightText: 2026 Funkystation Contributors
//
// SPDX-License-Identifier: MIT

using Content.Shared._EE.Supermatter.Components;
using Content.Shared.Atmos;
using Content.Shared.Interaction;
using Robust.Shared.GameObjects;
using Robust.Shared.Random;
using Robust.UnitTesting;

namespace Content.IntegrationTests.Tests._EE.Supermatter;

[TestFixture]
public sealed class SupermatterInteractionTest
{
    [TestPrototypes]
    private const string ItemPrototypes = @"
- type: entity
  id: SupermatterIntegrationTestTrash
  parent: BaseItem
  name: sm-test-trash
  components:
  - type: Sprite
    sprite: Objects/Misc/bureaucracy.rsi
    state: paper
  - type: Item
    size: Tiny
";

    [Test]
    public async Task HandTouchDestroysUserAndAddsMatterPower()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Dirty = true });
        var server = pair.Server;
        server.ResolveDependency<IRobustRandom>().SetSeed(7001);

        await pair.CreateTestMap();
        var mix = new GasMixture(Atmospherics.CellVolume) { Temperature = Atmospherics.T20C };
        mix.AdjustMoles(Gas.Oxygen, 20f);
        mix.AdjustMoles(Gas.Nitrogen, 80f);

        var sm = await SupermatterIntegrationTestHelpers.PrepareGridAndSpawn(pair, "Supermatter", mix, 5);
        EntityUid victim = default;

        await server.WaitPost(() =>
        {
            var entMan = server.EntMan;
            var map = pair.TestMap!;
            victim = entMan.SpawnEntity("MobHumanPathDummy", map.GridCoords);
            var ev = new InteractHandEvent(victim, sm);
            entMan.EventBus.RaiseLocalEvent(sm, ev);
        });

        await server.WaitRunTicks(1);

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            Assert.That(entMan.Deleted(victim), Is.True);
            var comp = entMan.GetComponent<SupermatterComponent>(sm);
            Assert.That(comp.MatterPower, Is.GreaterThanOrEqualTo(200f));
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task HandTouchIgnoredWhenImmune()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Dirty = true });
        var server = pair.Server;
        server.ResolveDependency<IRobustRandom>().SetSeed(7002);

        await pair.CreateTestMap();
        var mix = new GasMixture(Atmospherics.CellVolume) { Temperature = Atmospherics.T20C };
        mix.AdjustMoles(Gas.Oxygen, 20f);
        mix.AdjustMoles(Gas.Nitrogen, 80f);

        var sm = await SupermatterIntegrationTestHelpers.PrepareGridAndSpawn(pair, "Supermatter", mix, 5);
        EntityUid victim = default;

        await server.WaitPost(() =>
        {
            var entMan = server.EntMan;
            var map = pair.TestMap!;
            victim = entMan.SpawnEntity("MobHumanPathDummy", map.GridCoords);
            entMan.AddComponent<SupermatterImmuneComponent>(victim);
            var ev = new InteractHandEvent(victim, sm);
            entMan.EventBus.RaiseLocalEvent(sm, ev);
        });

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            Assert.That(entMan.Deleted(victim), Is.False);
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task ItemInteractionDestroysItem()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Dirty = true });
        var server = pair.Server;
        server.ResolveDependency<IRobustRandom>().SetSeed(7003);

        await pair.CreateTestMap();
        var mix = new GasMixture(Atmospherics.CellVolume) { Temperature = Atmospherics.T20C };
        mix.AdjustMoles(Gas.Oxygen, 20f);
        mix.AdjustMoles(Gas.Nitrogen, 80f);

        var sm = await SupermatterIntegrationTestHelpers.PrepareGridAndSpawn(pair, "Supermatter", mix, 5);
        EntityUid user = default;
        EntityUid item = default;

        await server.WaitPost(() =>
        {
            var entMan = server.EntMan;
            var map = pair.TestMap!;
            user = entMan.SpawnEntity("MobHumanPathDummy", map.GridCoords);
            // Prevent StartCollide from deleting the user before InteractUsing runs (same tile as the crystal).
            entMan.AddComponent<SupermatterImmuneComponent>(user);
            item = entMan.SpawnEntity("SupermatterIntegrationTestTrash", map.GridCoords);
            var coords = entMan.GetComponent<TransformComponent>(sm).Coordinates;
            var ev = new InteractUsingEvent(user, item, sm, coords);
            entMan.EventBus.RaiseLocalEvent(sm, ev);
        });

        await server.WaitRunTicks(1);

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            Assert.That(entMan.Deleted(item), Is.True);
            Assert.That(entMan.Deleted(user), Is.False);
        });

        await pair.CleanReturnAsync();
    }
}
