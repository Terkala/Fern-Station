#!/usr/bin/env python3
"""
Test N2O combinations for MaxCap explosions.
Tests Plasma+N2O, Tritium+N2O, and Plasma+Tritium+N2O combinations.
"""

import sys
import random
from maxcap_explosion_simulator import (
    Gas, GasMixture, TANK_VOLUME, R, simulate_maxcap_explosion,
    THERMOMACHINE_MAX_TEMP, TankFragmentPressure
)

# Search parameters
min_temp = 373.15
max_temp = THERMOMACHINE_MAX_TEMP
min_pressure = 400.0
max_pressure = 900.0
min_o2_temp = 293.15
max_o2_temp = THERMOMACHINE_MAX_TEMP
min_burn_time = 5.0

def test_n2o_combination(flammable_gases, num_candidates=100, num_generations=10):
    """Test a combination of flammable gases with N2O"""
    print(f"\n{'='*70}")
    print(f"Testing: {' + '.join([g.name for g in flammable_gases])} + N2O")
    print(f"{'='*70}\n")
    
    best_range = 0.0
    best_result = None
    all_results = []
    
    for generation in range(num_generations):
        candidates = []
        
        if generation == 0:
            # Initial random candidates
            for _ in range(num_candidates):
                temp = random.uniform(min_temp, max_temp)
                pressure = random.uniform(min_pressure, max_pressure)
                o2_temp = random.uniform(min_o2_temp, max_o2_temp)
                
                # Random fuel ratios
                if len(flammable_gases) == 1:
                    fuel_ratio = 1.0
                else:
                    fuel_ratio = random.uniform(0.1, 0.9)
                
                # Random N2O percentage (0-50% of total)
                n2o_pct = random.uniform(0.0, 0.5)
                
                candidates.append({
                    'temp': temp,
                    'pressure': pressure,
                    'o2_temp': o2_temp,
                    'fuel_ratio': fuel_ratio,
                    'n2o_pct': n2o_pct
                })
        else:
            # Mutate best candidates from previous generation
            for base in best_candidates[:10]:
                temp = base['temp'] + random.uniform(-20.0, 20.0)
                temp = max(min_temp, min(max_temp, temp))
                
                pressure = base['pressure'] + random.uniform(-50.0, 50.0)
                pressure = max(min_pressure, min(max_pressure, pressure))
                
                o2_temp = base.get('o2_temp', min_o2_temp) + random.uniform(-20.0, 20.0)
                o2_temp = max(min_o2_temp, min(max_o2_temp, o2_temp))
                
                fuel_ratio = base.get('fuel_ratio', 0.5) + random.uniform(-0.1, 0.1)
                fuel_ratio = max(0.1, min(0.9, fuel_ratio))
                
                n2o_pct = base.get('n2o_pct', 0.2) + random.uniform(-0.1, 0.1)
                n2o_pct = max(0.0, min(0.5, n2o_pct))
                
                candidates.append({
                    'temp': temp,
                    'pressure': pressure,
                    'o2_temp': o2_temp,
                    'fuel_ratio': fuel_ratio,
                    'n2o_pct': n2o_pct
                })
        
        generation_results = []
        
        for candidate in candidates:
            try:
                # Create explosive mix
                explosive_mix = GasMixture(volume=TANK_VOLUME, temperature=candidate['temp'])
                
                # Calculate total moles needed for target pressure
                target_pressure = candidate['pressure']
                total_moles = target_pressure * TANK_VOLUME / (R * candidate['temp'])
                
                # Calculate fuel moles (split between flammable gases if multiple)
                fuel_total_pct = 1.0 - candidate['n2o_pct']
                if len(flammable_gases) == 1:
                    fuel_moles = total_moles * fuel_total_pct
                    explosive_mix.set_moles(flammable_gases[0], fuel_moles)
                else:
                    # Split between two fuels
                    fuel1_moles = total_moles * fuel_total_pct * candidate['fuel_ratio']
                    fuel2_moles = total_moles * fuel_total_pct * (1.0 - candidate['fuel_ratio'])
                    explosive_mix.set_moles(flammable_gases[0], fuel1_moles)
                    explosive_mix.set_moles(flammable_gases[1], fuel2_moles)
                
                # Add N2O
                n2o_moles = total_moles * candidate['n2o_pct']
                explosive_mix.set_moles(Gas.NitrousOxide, n2o_moles)
                
                # Test explosion
                o2_temp = candidate.get('o2_temp', min_o2_temp)
                final_pressure, explosion_range, stats = simulate_maxcap_explosion(
                    canister_mix=explosive_mix,
                    canister_temp=candidate['temp'],
                    target_explosive_pressure=candidate['pressure'],
                    target_total_pressure=1013.0,
                    oxygen_temp=o2_temp,
                    canister_nitrogen_pct=0.0,
                    o2_mix_nitrogen_pct=0.0
                )
                
                # Skip combinations that don't meet requirements
                if stats.get('below_ignition_temp', False):
                    continue
                if stats.get('burn_time_seconds', 0) < min_burn_time:
                    continue
                if not stats.get('reached_threshold', False):
                    continue
                
                result = {
                    'temp': candidate['temp'],
                    'pressure': candidate['pressure'],
                    'o2_temp': o2_temp,
                    'explosion_range': explosion_range,
                    'final_pressure': final_pressure,
                    'fuel_ratio': candidate.get('fuel_ratio', 1.0),
                    'n2o_pct': candidate['n2o_pct'],
                    'generation': generation,
                    **stats
                }
                
                generation_results.append(result)
                all_results.append(result)
                
                if explosion_range > best_range:
                    best_range = explosion_range
                    best_result = result
                    
            except Exception as e:
                continue
        
        # Sort and select best for next generation
        generation_results.sort(key=lambda x: x['explosion_range'], reverse=True)
        best_candidates = generation_results[:10] if generation_results else []
        
        if generation_results:
            gen_best = generation_results[0]
            print(f"  Generation {generation}: Best = {gen_best['explosion_range']:.2f} tiles "
                  f"(temp={gen_best['temp']:.1f}K, press={gen_best['pressure']:.1f}kPa, "
                  f"O2={gen_best.get('o2_temp', min_o2_temp):.1f}K, "
                  f"burn={gen_best.get('burn_time_seconds', 0):.2f}s, "
                  f"N2O={gen_best['n2o_pct']*100:.1f}%)")
        else:
            print(f"  Generation {generation}: No valid results found")
    
    return best_result, all_results


def main():
    """Test N2O combinations"""
    print("="*70)
    print("N2O COMBINATION TESTING")
    print("="*70)
    print(f"Minimum burn time requirement: {min_burn_time} seconds")
    print()
    
    results = {}
    
    # Test Plasma + N2O
    best, all_res = test_n2o_combination([Gas.Plasma])
    results['Plasma + N2O'] = (best, all_res)
    
    # Test Tritium + N2O
    best, all_res = test_n2o_combination([Gas.Tritium])
    results['Tritium + N2O'] = (best, all_res)
    
    # Test Plasma + Tritium + N2O
    best, all_res = test_n2o_combination([Gas.Plasma, Gas.Tritium])
    results['Plasma + Tritium + N2O'] = (best, all_res)
    
    # Print summary
    print("\n" + "="*70)
    print("SUMMARY")
    print("="*70)
    print(f"{'Combination':<30} {'Range':<10} {'Temp':<8} {'O2Temp':<8} {'BurnTime':<10} {'N2O%':<8}")
    print("-"*70)
    
    for name, (best, _) in results.items():
        if best:
            print(f"{name:<30} {best['explosion_range']:>8.2f}  {best['temp']:>6.1f}  "
                  f"{best.get('o2_temp', 293.15):>6.1f}  {best.get('burn_time_seconds', 0):>7.2f}s  "
                  f"{best['n2o_pct']*100:>5.1f}%")
        else:
            print(f"{name:<30} {'No valid results':<10}")
    
    # Print best overall
    best_overall = None
    best_range = 0.0
    for name, (best, _) in results.items():
        if best and best['explosion_range'] > best_range:
            best_range = best['explosion_range']
            best_overall = (name, best)
    
    if best_overall:
        name, best = best_overall
        print("\n" + "="*70)
        print("BEST OVERALL RESULT")
        print("="*70)
        print(f"Combination: {name}")
        print(f"Explosion Range: {best['explosion_range']:.2f} tiles")
        print(f"Temperature: {best['temp']:.2f} K ({best['temp'] - 273.15:.2f}°C)")
        print(f"Explosive Pressure: {best['pressure']:.2f} kPa")
        print(f"O2 Temperature: {best.get('o2_temp', 293.15):.2f} K ({best.get('o2_temp', 293.15) - 273.15:.2f}°C)")
        print(f"Final Pressure: {best['final_pressure']:.2f} kPa")
        print(f"Burn Time: {best.get('burn_time_seconds', 0):.2f} seconds")
        print(f"N2O Percentage: {best['n2o_pct']*100:.2f}%")
        if len([g for g in [Gas.Plasma, Gas.Tritium] if g in [Gas.Plasma, Gas.Tritium]]) > 1:
            print(f"Fuel Ratio (Plasma/Tritium): {best.get('fuel_ratio', 1.0)*100:.1f}% / {(1.0-best.get('fuel_ratio', 0.5))*100:.1f}%")


if __name__ == "__main__":
    main()
