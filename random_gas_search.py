#!/usr/bin/env python3
"""
Random Gas Combination Search for MaxCap Explosions

Tests evolutionary search on a wide variety of gas combinations
and reports the best results for each unique gas type.
"""

import sys
import random
from typing import List, Dict, Tuple, Optional
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

# Define all possible gas combinations to test
FLAMMABLE_GASES = [Gas.Plasma, Gas.Tritium, Gas.Hydrogen]
ATMOSPHERIC_GASES = [Gas.Nitrogen, Gas.CarbonDioxide, Gas.WaterVapor, Gas.NitrousOxide]

def generate_combinations():
    """Generate all possible gas combinations to test"""
    combinations = []
    
    # Pure flammable gases
    for gas in FLAMMABLE_GASES:
        combinations.append({
            'name': f"Pure {gas.name}",
            'flammable': [gas],
            'atmospheric': []
        })
    
    # Flammable + each atmospheric
    for flam in FLAMMABLE_GASES:
        for atm in ATMOSPHERIC_GASES:
            combinations.append({
                'name': f"{flam.name} + {atm.name}",
                'flammable': [flam],
                'atmospheric': [atm]
            })
    
    # Flammable pairs
    for i, flam1 in enumerate(FLAMMABLE_GASES):
        for flam2 in FLAMMABLE_GASES[i+1:]:
            combinations.append({
                'name': f"{flam1.name} + {flam2.name}",
                'flammable': [flam1, flam2],
                'atmospheric': []
            })
            
            # Flammable pairs + each atmospheric
            for atm in ATMOSPHERIC_GASES:
                combinations.append({
                    'name': f"{flam1.name} + {flam2.name} + {atm.name}",
                    'flammable': [flam1, flam2],
                    'atmospheric': [atm]
                })
    
    # All three flammable
    combinations.append({
        'name': f"{Gas.Plasma.name} + {Gas.Tritium.name} + {Gas.Hydrogen.name}",
        'flammable': [Gas.Plasma, Gas.Tritium, Gas.Hydrogen],
        'atmospheric': []
    })
    
    # All three flammable + each atmospheric
    for atm in ATMOSPHERIC_GASES:
        combinations.append({
            'name': f"{Gas.Plasma.name} + {Gas.Tritium.name} + {Gas.Hydrogen.name} + {atm.name}",
            'flammable': [Gas.Plasma, Gas.Tritium, Gas.Hydrogen],
            'atmospheric': [atm]
        })
    
    return combinations


def evolutionary_search(
    flammable_gases: List[Gas],
    atmospheric_gases: List[Gas],
    combination_name: str,
    num_candidates: int = 50,
    num_generations: int = 5
) -> Optional[Dict]:
    """Run evolutionary search for a gas combination"""
    best_range = 0.0
    best_result = None
    best_candidates = []
    
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
                    fuel_ratios = [1.0]
                elif len(flammable_gases) == 2:
                    ratio1 = random.uniform(0.1, 0.9)
                    fuel_ratios = [ratio1, 1.0 - ratio1]
                else:  # 3 gases
                    r1 = random.uniform(0.1, 0.8)
                    r2 = random.uniform(0.1, 0.8 - r1)
                    r3 = 1.0 - r1 - r2
                    fuel_ratios = [r1, r2, r3]
                
                # Random atmospheric gas percentages
                if len(atmospheric_gases) == 0:
                    atm_total_pct = 0.0
                    atm_ratios = []
                elif len(atmospheric_gases) == 1:
                    atm_total_pct = random.uniform(0.0, 0.5)
                    atm_ratios = [1.0]
                else:
                    atm_total_pct = random.uniform(0.0, 0.5)
                    r1 = random.uniform(0.0, 1.0)
                    atm_ratios = [r1, 1.0 - r1] if len(atmospheric_gases) == 2 else [r1, (1.0-r1)/2, (1.0-r1)/2]
                
                candidates.append({
                    'temp': temp,
                    'pressure': pressure,
                    'o2_temp': o2_temp,
                    'fuel_ratios': fuel_ratios,
                    'atm_total_pct': atm_total_pct,
                    'atm_ratios': atm_ratios
                })
        else:
            # Mutate best candidates
            for base in best_candidates[:10]:
                temp = base['temp'] + random.uniform(-20.0, 20.0)
                temp = max(min_temp, min(max_temp, temp))
                
                pressure = base['pressure'] + random.uniform(-50.0, 50.0)
                pressure = max(min_pressure, min(max_pressure, pressure))
                
                o2_temp = base.get('o2_temp', min_o2_temp) + random.uniform(-20.0, 20.0)
                o2_temp = max(min_o2_temp, min(max_o2_temp, o2_temp))
                
                # Mutate fuel ratios
                if len(flammable_gases) == 1:
                    fuel_ratios = [1.0]
                elif len(flammable_gases) == 2:
                    base_ratio = base.get('fuel_ratios', [0.5, 0.5])[0]
                    ratio1 = base_ratio + random.uniform(-0.1, 0.1)
                    ratio1 = max(0.1, min(0.9, ratio1))
                    fuel_ratios = [ratio1, 1.0 - ratio1]
                else:
                    base_ratios = base.get('fuel_ratios', [0.33, 0.33, 0.34])
                    r1 = base_ratios[0] + random.uniform(-0.1, 0.1)
                    r2 = base_ratios[1] + random.uniform(-0.1, 0.1)
                    r1 = max(0.1, min(0.8, r1))
                    r2 = max(0.1, min(0.8 - r1, r2))
                    r3 = 1.0 - r1 - r2
                    fuel_ratios = [r1, r2, r3]
                
                # Mutate atmospheric percentages
                base_atm_pct = base.get('atm_total_pct', 0.0)
                atm_total_pct = base_atm_pct + random.uniform(-0.1, 0.1)
                atm_total_pct = max(0.0, min(0.5, atm_total_pct))
                
                if len(atmospheric_gases) == 0:
                    atm_ratios = []
                elif len(atmospheric_gases) == 1:
                    atm_ratios = [1.0]
                elif len(atmospheric_gases) == 2:
                    base_atm_ratios = base.get('atm_ratios', [0.5, 0.5])
                    if len(base_atm_ratios) >= 2:
                        r1 = base_atm_ratios[0] + random.uniform(-0.2, 0.2)
                    else:
                        r1 = random.uniform(0.0, 1.0)
                    r1 = max(0.0, min(1.0, r1))
                    atm_ratios = [r1, 1.0 - r1]
                else:
                    base_atm_ratios = base.get('atm_ratios', [0.33, 0.33, 0.34])
                    if len(base_atm_ratios) >= 3:
                        r1 = base_atm_ratios[0] + random.uniform(-0.1, 0.1)
                        r2 = base_atm_ratios[1] + random.uniform(-0.1, 0.1)
                    else:
                        r1 = random.uniform(0.0, 0.8)
                        r2 = random.uniform(0.0, 0.8 - r1)
                    r1 = max(0.0, min(1.0, r1))
                    r2 = max(0.0, min(1.0 - r1, r2))
                    r3 = 1.0 - r1 - r2
                    atm_ratios = [r1, r2, r3]
                
                candidates.append({
                    'temp': temp,
                    'pressure': pressure,
                    'o2_temp': o2_temp,
                    'fuel_ratios': fuel_ratios,
                    'atm_total_pct': atm_total_pct,
                    'atm_ratios': atm_ratios
                })
        
        generation_results = []
        
        for candidate in candidates:
            try:
                # Create explosive mix
                explosive_mix = GasMixture(volume=TANK_VOLUME, temperature=candidate['temp'])
                
                # Calculate total moles needed
                target_pressure = candidate['pressure']
                total_moles = target_pressure * TANK_VOLUME / (R * candidate['temp'])
                
                # Calculate fuel moles
                fuel_total_pct = 1.0 - candidate['atm_total_pct']
                fuel_moles_total = total_moles * fuel_total_pct
                
                # Distribute fuel gases
                for i, gas in enumerate(flammable_gases):
                    fuel_moles = fuel_moles_total * candidate['fuel_ratios'][i]
                    explosive_mix.set_moles(gas, fuel_moles)
                
                # Distribute atmospheric gases
                if len(atmospheric_gases) > 0:
                    atm_moles_total = total_moles * candidate['atm_total_pct']
                    for i, gas in enumerate(atmospheric_gases):
                        atm_moles = atm_moles_total * candidate['atm_ratios'][i]
                        explosive_mix.set_moles(gas, atm_moles)
                
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
                
                # Skip invalid combinations
                if stats.get('below_ignition_temp', False):
                    continue
                if stats.get('burn_time_seconds', 0) < min_burn_time:
                    continue
                if not stats.get('reached_threshold', False):
                    continue
                
                result = {
                    'combination': combination_name,
                    'temp': candidate['temp'],
                    'pressure': candidate['pressure'],
                    'o2_temp': o2_temp,
                    'explosion_range': explosion_range,
                    'final_pressure': final_pressure,
                    'fuel_ratios': candidate['fuel_ratios'],
                    'atm_total_pct': candidate['atm_total_pct'],
                    'atm_ratios': candidate['atm_ratios'],
                    'generation': generation,
                    **stats
                }
                
                generation_results.append(result)
                
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
            print(f"  Gen {generation}: {gen_best['explosion_range']:.2f} tiles "
                  f"(T={gen_best['temp']:.1f}K, P={gen_best['pressure']:.1f}kPa, "
                  f"O2={gen_best.get('o2_temp', min_o2_temp):.1f}K, "
                  f"burn={gen_best.get('burn_time_seconds', 0):.2f}s)")
        else:
            print(f"  Gen {generation}: No valid results")
    
    return best_result


def main():
    """Run random search across all gas combinations"""
    print("="*80)
    print("RANDOM GAS COMBINATION SEARCH")
    print("="*80)
    print(f"Minimum burn time requirement: {min_burn_time} seconds")
    print(f"Testing {len(generate_combinations())} different gas combinations")
    print()
    
    combinations = generate_combinations()
    results = {}
    
    for i, combo in enumerate(combinations, 1):
        print(f"[{i}/{len(combinations)}] Testing: {combo['name']}")
        print("-" * 80)
        
        try:
            best = evolutionary_search(
                combo['flammable'],
                combo['atmospheric'],
                combo['name'],
                num_candidates=30,  # Reduced for speed
                num_generations=3   # Reduced for speed
            )
            
            results[combo['name']] = best
            
            if best:
                print(f"  [OK] Best: {best['explosion_range']:.2f} tiles "
                      f"(burn={best.get('burn_time_seconds', 0):.2f}s)")
            else:
                print(f"  [FAIL] No valid results found")
        except Exception as e:
            print(f"  [ERROR] {e}")
            results[combo['name']] = None
        print()
    
    # Print summary by gas type
    print("\n" + "="*80)
    print("SUMMARY BY GAS TYPE")
    print("="*80)
    
    # Group results by primary gas
    gas_groups = {}
    for name, result in results.items():
        if not result:
            continue
        
        # Determine primary gas(es)
        if "Pure" in name:
            primary = name.split()[1]  # "Pure Plasma" -> "Plasma"
        elif " + " in name:
            parts = name.split(" + ")
            primary = parts[0]  # First gas
        else:
            primary = name
        
        if primary not in gas_groups:
            gas_groups[primary] = []
        gas_groups[primary].append((name, result))
    
    # Print best for each group
    print(f"\n{'Combination':<50} {'Range':<10} {'Temp':<8} {'O2Temp':<8} {'BurnTime':<10}")
    print("-" * 80)
    
    for primary, group_results in sorted(gas_groups.items()):
        if not group_results:
            continue
        
        # Sort by explosion range
        group_results.sort(key=lambda x: x[1]['explosion_range'], reverse=True)
        best_name, best_result = group_results[0]
        
        print(f"{best_name:<50} {best_result['explosion_range']:>8.2f}  "
              f"{best_result['temp']:>6.1f}  {best_result.get('o2_temp', 293.15):>6.1f}  "
              f"{best_result.get('burn_time_seconds', 0):>7.2f}s")
    
    # Overall best
    all_valid = [(name, res) for name, res in results.items() if res]
    if all_valid:
        all_valid.sort(key=lambda x: x[1]['explosion_range'], reverse=True)
        best_name, best_result = all_valid[0]
        
        print("\n" + "="*80)
        print("BEST OVERALL RESULT")
        print("="*80)
        print(f"Combination: {best_name}")
        print(f"Explosion Range: {best_result['explosion_range']:.2f} tiles")
        print(f"Temperature: {best_result['temp']:.2f} K ({best_result['temp'] - 273.15:.2f}°C)")
        print(f"Explosive Pressure: {best_result['pressure']:.2f} kPa")
        print(f"O2 Temperature: {best_result.get('o2_temp', 293.15):.2f} K "
              f"({best_result.get('o2_temp', 293.15) - 273.15:.2f}°C)")
        print(f"Final Pressure: {best_result['final_pressure']:.2f} kPa")
        print(f"Burn Time: {best_result.get('burn_time_seconds', 0):.2f} seconds")
        print(f"Cycles to Threshold: {best_result.get('cycles_to_threshold', 0)}")
        
        # Gas composition
        print(f"\nGas Composition:")
        if len(best_result.get('fuel_ratios', [])) > 0:
            for i, ratio in enumerate(best_result['fuel_ratios']):
                if i < len(FLAMMABLE_GASES):
                    print(f"  {FLAMMABLE_GASES[i].name}: {ratio*100:.2f}%")
        if best_result.get('atm_total_pct', 0) > 0:
            print(f"  Atmospheric gases: {best_result['atm_total_pct']*100:.2f}%")
    
    # Top 10 overall
    print("\n" + "="*80)
    print("TOP 10 COMBINATIONS")
    print("="*80)
    print(f"{'Rank':<6} {'Range':<10} {'Combination':<50}")
    print("-" * 80)
    
    for i, (name, result) in enumerate(all_valid[:10], 1):
        print(f"{i:<6} {result['explosion_range']:>8.2f}  {name:<50}")


if __name__ == "__main__":
    main()
