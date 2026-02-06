#!/usr/bin/env python3
"""
Test baseline combination and variations around it.
"""

from maxcap_explosion_simulator import (
    test_specific_combination, Gas, GasMixture, simulate_maxcap_explosion,
    TANK_VOLUME, R
)

def calculate_moles_for_pressure_and_ratio(pressure_kpa, temp_k, plasma_percent, tritium_percent):
    """Calculate moles needed for given pressure and ratio."""
    total_moles = pressure_kpa * TANK_VOLUME / (R * temp_k)
    plasma_moles = total_moles * (plasma_percent / 100.0)
    tritium_moles = total_moles * (tritium_percent / 100.0)
    return plasma_moles, tritium_moles, total_moles

def test_baseline():
    """Test the baseline combination."""
    print("=" * 70)
    print("BASELINE TEST")
    print("=" * 70)
    print("Plasma: 54.56%")
    print("Tritium: 45.44%")
    print("Temperature: 382.7 K")
    print("Explosive Pressure: 700 kPa")
    print()
    
    plasma_moles, tritium_moles, total_moles = calculate_moles_for_pressure_and_ratio(
        700.0, 382.7, 54.56, 45.44
    )
    
    print(f"Calculated moles:")
    print(f"  Total: {total_moles:.4f} mol")
    print(f"  Plasma: {plasma_moles:.4f} mol")
    print(f"  Tritium: {tritium_moles:.4f} mol")
    print()
    
    final_pressure, explosion_range, stats = test_specific_combination(
        {Gas.Plasma: plasma_moles, Gas.Tritium: tritium_moles},
        382.7,
        target_explosive_pressure=700.0,
        target_total_pressure=1013.0,
        verbose=True
    )
    
    return stats

def test_variations(baseline_stats):
    """Test variations around the baseline."""
    print("\n" + "=" * 70)
    print("VARIATION TESTS")
    print("=" * 70)
    print()
    
    baseline_plasma_pct = 54.56
    baseline_tritium_pct = 45.44
    baseline_temp = 382.7
    baseline_pressure = 700.0
    
    variations = []
    
    # Test ratio variations (±5%, ±10%)
    for plasma_delta in [-10, -5, -2, -1, 0, 1, 2, 5, 10]:
        plasma_pct = baseline_plasma_pct + plasma_delta
        tritium_pct = 100.0 - plasma_pct
        
        if plasma_pct < 0 or tritium_pct < 0:
            continue
        
        plasma_moles, tritium_moles, _ = calculate_moles_for_pressure_and_ratio(
            baseline_pressure, baseline_temp, plasma_pct, tritium_pct
        )
        
        canister = GasMixture(volume=TANK_VOLUME, temperature=baseline_temp)
        canister.set_moles(Gas.Plasma, plasma_moles)
        canister.set_moles(Gas.Tritium, tritium_moles)
        
        try:
            final_pressure, explosion_range, stats = simulate_maxcap_explosion(
                canister, baseline_temp,
                target_explosive_pressure=baseline_pressure,
                target_total_pressure=1013.0
            )
            
            variations.append({
                'plasma_pct': plasma_pct,
                'tritium_pct': tritium_pct,
                'temp': baseline_temp,
                'pressure': baseline_pressure,
                'explosion_range': explosion_range,
                'final_pressure': final_pressure,
                'pressure_increase': stats['pressure_increase'],
                **stats
            })
        except Exception as e:
            print(f"Error testing {plasma_pct:.2f}%/{tritium_pct:.2f}%: {e}")
            continue
    
    # Test temperature variations (±10K, ±20K)
    for temp_delta in [-20, -10, -5, 0, 5, 10, 20]:
        temp = baseline_temp + temp_delta
        if temp < 373.15 or temp > 593.15:  # Stay within valid range
            continue
        
        plasma_moles, tritium_moles, _ = calculate_moles_for_pressure_and_ratio(
            baseline_pressure, temp, baseline_plasma_pct, baseline_tritium_pct
        )
        
        canister = GasMixture(volume=TANK_VOLUME, temperature=temp)
        canister.set_moles(Gas.Plasma, plasma_moles)
        canister.set_moles(Gas.Tritium, tritium_moles)
        
        try:
            final_pressure, explosion_range, stats = simulate_maxcap_explosion(
                canister, temp,
                target_explosive_pressure=baseline_pressure,
                target_total_pressure=1013.0
            )
            
            variations.append({
                'plasma_pct': baseline_plasma_pct,
                'tritium_pct': baseline_tritium_pct,
                'temp': temp,
                'pressure': baseline_pressure,
                'explosion_range': explosion_range,
                'final_pressure': final_pressure,
                'pressure_increase': stats['pressure_increase'],
                **stats
            })
        except Exception as e:
            print(f"Error testing temp {temp:.1f}K: {e}")
            continue
    
    # Test pressure variations (±50 kPa, ±100 kPa)
    for pressure_delta in [-100, -50, -25, 0, 25, 50, 100]:
        pressure = baseline_pressure + pressure_delta
        if pressure < 100 or pressure >= 1013:  # Must leave room for O2
            continue
        
        plasma_moles, tritium_moles, _ = calculate_moles_for_pressure_and_ratio(
            pressure, baseline_temp, baseline_plasma_pct, baseline_tritium_pct
        )
        
        canister = GasMixture(volume=TANK_VOLUME, temperature=baseline_temp)
        canister.set_moles(Gas.Plasma, plasma_moles)
        canister.set_moles(Gas.Tritium, tritium_moles)
        
        try:
            final_pressure, explosion_range, stats = simulate_maxcap_explosion(
                canister, baseline_temp,
                target_explosive_pressure=pressure,
                target_total_pressure=1013.0
            )
            
            variations.append({
                'plasma_pct': baseline_plasma_pct,
                'tritium_pct': baseline_tritium_pct,
                'temp': baseline_temp,
                'pressure': pressure,
                'explosion_range': explosion_range,
                'final_pressure': final_pressure,
                'pressure_increase': stats['pressure_increase'],
                **stats
            })
        except Exception as e:
            print(f"Error testing pressure {pressure:.0f} kPa: {e}")
            continue
    
    # Sort by explosion range (descending)
    variations.sort(key=lambda x: x['explosion_range'], reverse=True)
    
    print(f"Tested {len(variations)} variations")
    print()
    print("Top 20 Results (sorted by explosion range):")
    print("-" * 70)
    print(f"{'Plasma%':<8} {'Tritium%':<10} {'Temp(K)':<10} {'Press(kPa)':<12} {'Range':<8} {'Final Press':<12} {'Press Inc':<12}")
    print("-" * 70)
    
    for i, var in enumerate(variations[:20], 1):
        print(f"{var['plasma_pct']:>7.2f}% {var['tritium_pct']:>9.2f}% {var['temp']:>9.1f} {var['pressure']:>11.1f} "
              f"{var['explosion_range']:>7.2f} {var['final_pressure']:>11.2f} {var['pressure_increase']:>11.2f}")
    
    print()
    print("Baseline comparison:")
    baseline_range = baseline_stats['explosion_range']
    print(f"  Baseline explosion range: {baseline_range:.2f} tiles")
    
    better = [v for v in variations if v['explosion_range'] > baseline_range + 0.01]
    worse = [v for v in variations if v['explosion_range'] < baseline_range - 0.01]
    same = [v for v in variations if abs(v['explosion_range'] - baseline_range) <= 0.01]
    
    print(f"  Better: {len(better)} variations")
    print(f"  Worse: {len(worse)} variations")
    print(f"  Same: {len(same)} variations")
    
    if better:
        print()
        print("Best improvements:")
        for var in better[:5]:
            improvement = var['explosion_range'] - baseline_range
            print(f"  +{improvement:.2f} tiles: {var['plasma_pct']:.2f}%/{var['tritium_pct']:.2f}% "
                  f"at {var['temp']:.1f}K, {var['pressure']:.1f}kPa")
    
    if worse:
        print()
        print("Worst reductions:")
        for var in worse[:5]:
            reduction = baseline_range - var['explosion_range']
            print(f"  -{reduction:.2f} tiles: {var['plasma_pct']:.2f}%/{var['tritium_pct']:.2f}% "
                  f"at {var['temp']:.1f}K, {var['pressure']:.1f}kPa")
    
    return variations

if __name__ == "__main__":
    baseline_stats = test_baseline()
    variations = test_variations(baseline_stats)
