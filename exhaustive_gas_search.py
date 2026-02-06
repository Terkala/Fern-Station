#!/usr/bin/env python3
"""Exhaustive search for all gas combinations with O2 mix variations."""

import random
from typing import Dict, List, Tuple, Optional
from maxcap_explosion_simulator import (
    Gas, GasMixture, TANK_VOLUME, R, THERMOMACHINE_MAX_TEMP,
    simulate_maxcap_explosion, calculate_explosion_range
)

# Flammable gases to test
FLAMMABLE_GASES = [
    ([Gas.Plasma], "Pure Plasma"),
    ([Gas.Tritium], "Pure Tritium"),
    ([Gas.Hydrogen], "Pure Hydrogen"),
    ([Gas.Plasma, Gas.Tritium], "Plasma + Tritium"),
    ([Gas.Plasma, Gas.Hydrogen], "Plasma + Hydrogen"),
    ([Gas.Tritium, Gas.Hydrogen], "Tritium + Hydrogen"),
    ([Gas.Plasma, Gas.Tritium, Gas.Hydrogen], "Plasma + Tritium + Hydrogen"),
]

# Atmospheric gases to test in canister
ATMOSPHERIC_GASES = [
    ([], "No atmospheric"),
    ([Gas.Nitrogen], "N2"),
    ([Gas.CarbonDioxide], "CO2"),
    ([Gas.WaterVapor], "Water Vapor"),
    ([Gas.Nitrogen, Gas.CarbonDioxide], "N2 + CO2"),
    ([Gas.Nitrogen, Gas.WaterVapor], "N2 + Water Vapor"),
    ([Gas.CarbonDioxide, Gas.WaterVapor], "CO2 + Water Vapor"),
    ([Gas.Nitrogen, Gas.CarbonDioxide, Gas.WaterVapor], "N2 + CO2 + Water Vapor"),
]


def search_gas_combination_exhaustive(
    flammable_gases: List[Gas],
    atmospheric_gases: List[Gas],
    combination_name: str,
    num_candidates: int = 100,
    num_generations: int = 8,
    min_burn_time_seconds: float = 0.0
) -> Dict:
    """
    Exhaustive search for optimal explosion parameters including O2 mix variations.
    """
    print(f"\n{'='*70}")
    print(f"Searching: {combination_name}")
    print(f"{'='*70}")
    
    best_result = None
    best_range = 0.0
    all_results = []
    
    # Search parameters
    min_temp = 373.15
    max_temp = THERMOMACHINE_MAX_TEMP
    min_pressure = 400.0
    max_pressure = 900.0
    min_o2_temp = 293.15
    max_o2_temp = THERMOMACHINE_MAX_TEMP
    max_o2_n2_pct = 30.0  # Up to 30% N2 in O2 mix
    
    current_candidates = []
    
    for generation in range(num_generations):
        if generation == 0:
            # Random initial candidates
            candidates = []
            for _ in range(num_candidates):
                temp = random.uniform(min_temp, max_temp)
                pressure = random.uniform(min_pressure, max_pressure)
                o2_temp = random.uniform(min_o2_temp, max_o2_temp)
                o2_n2_pct = random.uniform(0.0, max_o2_n2_pct)
                
                # Random ratios for flammable gases
                if len(flammable_gases) == 1:
                    ratios = [1.0]
                else:
                    ratios = [random.random() for _ in flammable_gases]
                    total = sum(ratios)
                    ratios = [r / total for r in ratios]
                
                # Random ratios for atmospheric gases (0-30% total)
                if len(atmospheric_gases) > 0:
                    atm_total_pct = random.uniform(0.0, 30.0)
                    if len(atmospheric_gases) == 1:
                        atm_ratios = [1.0]
                    else:
                        atm_ratios = [random.random() for _ in atmospheric_gases]
                        atm_total = sum(atm_ratios)
                        atm_ratios = [r / atm_total for r in atm_ratios]
                else:
                    atm_total_pct = 0.0
                    atm_ratios = []
                
                candidates.append({
                    'temp': temp,
                    'pressure': pressure,
                    'o2_temp': o2_temp,
                    'o2_n2_pct': o2_n2_pct,
                    'fuel_ratios': ratios,
                    'atm_total_pct': atm_total_pct,
                    'atm_ratios': atm_ratios
                })
        else:
            # Create variations of best candidates
            candidates = []
            for base in current_candidates[:20]:  # Top 20
                for _ in range(num_candidates // 20):
                    temp = base['temp'] + random.uniform(-25.0, 25.0)
                    temp = max(min_temp, min(max_temp, temp))
                    
                    pressure = base['pressure'] + random.uniform(-50.0, 50.0)
                    pressure = max(min_pressure, min(max_pressure, pressure))
                    
                    o2_temp = base.get('o2_temp', 293.15) + random.uniform(-30.0, 30.0)
                    o2_temp = max(min_o2_temp, min(max_o2_temp, o2_temp))
                    
                    o2_n2_pct = base.get('o2_n2_pct', 0.0) + random.uniform(-5.0, 5.0)
                    o2_n2_pct = max(0.0, min(max_o2_n2_pct, o2_n2_pct))
                    
                    # Vary ratios slightly
                    if len(flammable_gases) == 1:
                        ratios = [1.0]
                    else:
                        ratios = [r + random.uniform(-0.1, 0.1) for r in base['fuel_ratios']]
                        ratios = [max(0.0, r) for r in ratios]
                        total = sum(ratios)
                        if total > 0:
                            ratios = [r / total for r in ratios]
                        else:
                            ratios = [1.0 / len(ratios)] * len(ratios)
                    
                    atm_total_pct = base['atm_total_pct'] + random.uniform(-5.0, 5.0)
                    atm_total_pct = max(0.0, min(30.0, atm_total_pct))
                    
                    if len(atmospheric_gases) > 0:
                        if len(atmospheric_gases) == 1:
                            atm_ratios = [1.0]
                        else:
                            atm_ratios = [r + random.uniform(-0.1, 0.1) for r in base['atm_ratios']]
                            atm_ratios = [max(0.0, r) for r in atm_ratios]
                            total = sum(atm_ratios)
                            if total > 0:
                                atm_ratios = [r / total for r in atm_ratios]
                            else:
                                atm_ratios = [1.0 / len(atmospheric_gases)] * len(atmospheric_gases)
                    else:
                        atm_ratios = []
                    
                    candidates.append({
                        'temp': temp,
                        'pressure': pressure,
                        'o2_temp': o2_temp,
                        'o2_n2_pct': o2_n2_pct,
                        'fuel_ratios': ratios,
                        'atm_total_pct': atm_total_pct,
                        'atm_ratios': atm_ratios
                    })
        
        # Test all candidates
        generation_results = []
        for candidate in candidates:
            try:
                # Create canister mixture
                canister = GasMixture(volume=TANK_VOLUME, temperature=candidate['temp'])
                
                # Calculate total fuel moles for target pressure
                base_fuel_moles = 1.0
                total_fuel_moles = base_fuel_moles
                
                # Add flammable gases
                for i, gas in enumerate(flammable_gases):
                    moles = base_fuel_moles * candidate['fuel_ratios'][i]
                    canister.set_moles(gas, moles)
                    total_fuel_moles += moles
                
                # Calculate atmospheric gas moles
                if len(atmospheric_gases) > 0 and candidate['atm_total_pct'] > 0:
                    atm_moles = total_fuel_moles * (candidate['atm_total_pct'] / 100.0) / (1.0 - candidate['atm_total_pct'] / 100.0)
                    for i, gas in enumerate(atmospheric_gases):
                        moles = atm_moles * candidate['atm_ratios'][i]
                        canister.set_moles(gas, moles)
                
                # Scale to target pressure
                current_pressure = canister.pressure
                if current_pressure > 0:
                    scale_factor = candidate['pressure'] / current_pressure
                    for gas in Gas:
                        current_moles = canister.get_moles(gas)
                        canister.set_moles(gas, current_moles * scale_factor)
                
                # Store actual gas amounts
                gas_amounts = {}
                for gas in Gas:
                    moles = canister.get_moles(gas)
                    if moles > 0.0001:
                        gas_amounts[gas] = moles
                
                # Test explosion with O2/N2 mix
                o2_temp = candidate.get('o2_temp', min_o2_temp)
                o2_n2_pct = candidate.get('o2_n2_pct', 0.0)
                
                final_pressure, explosion_range, stats = simulate_maxcap_explosion(
                    canister,
                    candidate['temp'],
                    target_explosive_pressure=candidate['pressure'],
                    target_total_pressure=1013.0,
                    oxygen_temp=o2_temp,
                    canister_nitrogen_pct=0.0,  # Already in canister if specified
                    o2_mix_nitrogen_pct=o2_n2_pct
                )
                
                # Skip combinations where mixed temperature is below ignition threshold
                if stats.get('below_ignition_temp', False):
                    continue
                
                # Skip combinations that don't meet minimum burn time requirement
                # Also skip if they never reach threshold (don't explode)
                if not stats.get('reached_threshold', True):
                    continue  # Never reaches explosion threshold
                
                if min_burn_time_seconds > 0:
                    burn_time = stats.get('burn_time_seconds', 0.0)
                    if burn_time < min_burn_time_seconds:
                        continue  # Explodes too quickly
                
                result = {
                    'combination': combination_name,
                    'temp': candidate['temp'],
                    'pressure': candidate['pressure'],
                    'o2_temp': o2_temp,
                    'o2_n2_pct': o2_n2_pct,
                    'explosion_range': explosion_range,
                    'final_pressure': final_pressure,
                    'fuel_ratios': candidate['fuel_ratios'],
                    'atm_total_pct': candidate['atm_total_pct'],
                    'atm_ratios': candidate['atm_ratios'],
                    'gas_amounts': gas_amounts,
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
        # Only consider candidates that meet minimum burn time requirement and actually explode
        valid_results = [
            r for r in generation_results
            if r.get('reached_threshold', True)  # Must actually reach explosion threshold
        ]
        
        if min_burn_time_seconds > 0:
            valid_results = [
                r for r in valid_results
                if r.get('burn_time_seconds', 0.0) >= min_burn_time_seconds
            ]
            if not valid_results:
                print(f"  Warning: No valid candidates found in generation {generation} that meet burn time requirement!")
                print(f"  Continuing with best available candidates that explode...")
                # Fall back to candidates that at least explode
                valid_results = [r for r in generation_results if r.get('reached_threshold', True)]
        
        valid_results.sort(key=lambda x: x['explosion_range'], reverse=True)
        current_candidates = valid_results[:20]
        
        if generation_results:
            gen_best = generation_results[0]
            burn_time_str = f", burn={gen_best.get('burn_time_seconds', 0.0):.1f}s" if min_burn_time_seconds > 0 else ""
            print(f"  Generation {generation}: Best = {gen_best['explosion_range']:.2f} tiles "
                  f"(temp={gen_best['temp']:.1f}K, press={gen_best['pressure']:.1f}kPa, "
                  f"O2={gen_best['o2_temp']:.1f}K, O2N2={gen_best['o2_n2_pct']:.1f}%{burn_time_str})")
    
    return {
        'combination_name': combination_name,
        'best_result': best_result,
        'best_range': best_range,
        'all_results': all_results
    }


def main():
    """Run exhaustive gas combination search."""
    import sys
    
    # Check for command line argument for minimum burn time
    min_burn_time = 5.0  # Default to 5 seconds
    if len(sys.argv) > 1:
        try:
            min_burn_time = float(sys.argv[1])
        except ValueError:
            print(f"Invalid argument: {sys.argv[1]}. Using default: 5.0 seconds")
    
    print("=" * 70)
    print("EXHAUSTIVE GAS COMBINATION SEARCH")
    print("=" * 70)
    print()
    if min_burn_time > 0:
        print(f"Minimum burn time requirement: {min_burn_time:.1f} seconds")
        print()
    print("Testing all combinations of:")
    print("  Flammable gases: Plasma, Tritium, Hydrogen (pure and combinations)")
    print("  Atmospheric gases: N2, CO2, Water Vapor (in canister)")
    print("  O2 mix: Variable temperature (293-593K) and N2 ratio (0-30%)")
    print()
    
    all_combination_results = []
    
    # Test each flammable gas combination with each atmospheric combination
    for flammable_gases, flammable_name in FLAMMABLE_GASES:
        for atmospheric_gases, atmospheric_name in ATMOSPHERIC_GASES:
            combination_name = f"{flammable_name}"
            if atmospheric_name != "No atmospheric":
                combination_name += f" + {atmospheric_name}"
            
            result = search_gas_combination_exhaustive(
                flammable_gases,
                atmospheric_gases,
                combination_name,
                num_candidates=100,
                num_generations=8,
                min_burn_time_seconds=min_burn_time
            )
            
            all_combination_results.append(result)
    
    # Sort all results by explosion range
    all_combination_results.sort(key=lambda x: x['best_range'], reverse=True)
    
    # Filter to only results that meet burn time requirement
    valid_results = [r for r in all_combination_results if r['best_result'] and r['best_result'].get('reached_threshold', True)]
    if min_burn_time > 0:
        valid_results = [r for r in valid_results if r['best_result'].get('burn_time_seconds', 0.0) >= min_burn_time]
    
    if not valid_results:
        print("\n" + "=" * 70)
        print("NO VALID RESULTS FOUND")
        print("=" * 70)
        print()
        print(f"No combinations found that meet the minimum burn time requirement of {min_burn_time:.1f} seconds.")
        print("Try reducing the minimum burn time requirement or adjusting search parameters.")
        print()
        return
    
    # Find best for each primary gas type
    best_tritium = None
    best_plasma = None
    best_hydrogen = None
    
    for result in valid_results:
        if result['best_result'] is None:
            continue
        
        combo_name = result['combination_name'].lower()
        explosion_range = result['best_range']
        
        # Check if this is tritium-based (and better than current best)
        if 'tritium' in combo_name and (best_tritium is None or explosion_range > best_tritium['best_range']):
            best_tritium = result
        
        # Check if this is plasma-based (and better than current best)
        if 'plasma' in combo_name and 'tritium' not in combo_name and 'hydrogen' not in combo_name:
            if best_plasma is None or explosion_range > best_plasma['best_range']:
                best_plasma = result
        
        # Check if this is hydrogen-based (and better than current best)
        if 'hydrogen' in combo_name and 'plasma' not in combo_name and 'tritium' not in combo_name:
            if best_hydrogen is None or explosion_range > best_hydrogen['best_range']:
                best_hydrogen = result
    
    # Print results
    print("\n" + "=" * 70)
    print("BEST 3 DIFFERENT MIXTURES")
    print("=" * 70)
    print()
    
    results_to_show = []
    if best_tritium:
        results_to_show.append(("BEST TRITIUM-BASED MIX", best_tritium))
    if best_plasma:
        results_to_show.append(("BEST PLASMA-BASED MIX", best_plasma))
    if best_hydrogen:
        results_to_show.append(("BEST HYDROGEN-BASED MIX", best_hydrogen))
    
    for title, result in results_to_show:
        if result['best_result']:
            best = result['best_result']
            print(f"{title}")
            print("-" * 70)
            print(f"Combination: {result['combination_name']}")
            print(f"Explosion Range: {best['explosion_range']:.2f} tiles")
            print(f"Explosive Mix Temperature: {best['temp']:.2f} K ({best['temp'] - 273.15:.2f}°C)")
            print(f"Explosive Pressure: {best['pressure']:.2f} kPa")
            print(f"O2 Temperature: {best['o2_temp']:.2f} K ({best['o2_temp'] - 273.15:.2f}°C)")
            print(f"O2 Mix N2: {best['o2_n2_pct']:.2f}%")
            print(f"Mixed Temperature: {best.get('initial_temp', 'N/A'):.2f} K" if isinstance(best.get('initial_temp'), (int, float)) else f"Mixed Temperature: {best.get('initial_temp', 'N/A')}")
            print(f"Final Pressure: {best['final_pressure']:.2f} kPa")
            if 'burn_time_seconds' in best:
                print(f"Burn Time to Threshold: {best['burn_time_seconds']:.2f} seconds ({best.get('cycles_to_threshold', 0)} cycles)")
            print()
            
            # Show gas composition
            print("Gas Composition:")
            if best.get('gas_amounts'):
                total_moles = sum(best['gas_amounts'].values())
                if total_moles > 0:
                    sorted_gases = sorted(best['gas_amounts'].items(), key=lambda x: x[1], reverse=True)
                    for gas, moles in sorted_gases:
                        pct = (moles / total_moles) * 100.0
                        print(f"  {gas.name}: {pct:.2f}%")
            print()
            print("=" * 70)
            print()
    
    # Also show top 10 overall (only valid ones)
    if valid_results:
        print("TOP 10 OVERALL COMBINATIONS:")
        print(f"{'Rank':<6} {'Range':<8} {'Burn Time':<12} {'Combination':<50}")
        print("-" * 80)
        valid_results.sort(key=lambda x: x['best_range'], reverse=True)
        for i, result in enumerate(valid_results[:10], 1):
            if result['best_result']:
                burn_time = result['best_result'].get('burn_time_seconds', 0.0)
                print(f"{i:<6} {result['best_range']:>7.2f} {burn_time:>10.2f}s {result['combination_name']:<50}")
    else:
        print("No valid combinations found.")


if __name__ == "__main__":
    main()
