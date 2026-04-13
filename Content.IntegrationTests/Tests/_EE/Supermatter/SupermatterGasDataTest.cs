// SPDX-FileCopyrightText: 2026 Funkystation Contributors
//
// SPDX-License-Identifier: MIT

using Content.Shared._EE.Supermatter.Components;
using Content.Shared.Atmos;

namespace Content.IntegrationTests.Tests._EE.Supermatter;

[TestFixture]
[TestOf(typeof(SupermatterGasData))]
public sealed class SupermatterGasDataTest
{
    [Test]
    public void PureOxygenPowerMixRatioMatchesMoleWeightedCoefficient()
    {
        // GetPowerMixRatios sums moles * PowerMixRatio per gas (not a0–1 fraction).
        var mix = new GasMixture(Atmospherics.CellVolume) { Temperature = Atmospherics.T20C };
        mix.AdjustMoles(Gas.Oxygen, 1f);
        var ratio = SupermatterGasData.GetPowerMixRatios(mix);
        Assert.That(ratio, Is.EqualTo(1f).Within(0.001f));
    }

    [Test]
    public void NitrogenDominantPowerMixRatioClampedLow()
    {
        var mix = new GasMixture(Atmospherics.CellVolume) { Temperature = Atmospherics.T20C };
        mix.AdjustMoles(Gas.Nitrogen, 100f);
        var ratio = SupermatterGasData.GetPowerMixRatios(mix);
        Assert.That(ratio, Is.LessThanOrEqualTo(1f));
        Assert.That(ratio, Is.LessThan(0.5f));
    }

    [Test]
    public void PlasmaIncreasesHeatPenaltyContribution()
    {
        var baseline = new GasMixture(Atmospherics.CellVolume) { Temperature = Atmospherics.T20C };
        baseline.SetMoles((int)Gas.Oxygen, 100f);

        var withPlasma = new GasMixture(Atmospherics.CellVolume) { Temperature = Atmospherics.T20C };
        withPlasma.SetMoles((int)Gas.Oxygen, 100f);
        withPlasma.SetMoles((int)Gas.Plasma, 50f);

        var h1 = SupermatterGasData.GetHeatPenalties(baseline);
        var h2 = SupermatterGasData.GetHeatPenalties(withPlasma);
        Assert.That(h2, Is.GreaterThan(h1));
    }
}
