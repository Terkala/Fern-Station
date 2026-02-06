#!/usr/bin/env python3
"""Comprehensive search for MaxCap explosions with all gas combinations."""

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

# Atmospheric gases to test (easy to acquire)
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


def search_gas_combination(
    flammable_gases: List[Gas],
    atmospheric_gases: List[Gas],
    combination_name: str,
    num_candidates: int = 50,
    num_generations: int = 5,
    min_burn_time_seconds: float = 0.0
) -> Dict:
    """
    Search for optimal explosion parameters for a specific gas combination.
    
    Args:
        flammable_gases: List of flammable gases (Plasma, Tritium, Hydrogen)
        atmospheric_gases: List of atmospheric gases (N2, CO2, Water Vapor)
        combination_name: Human-readable name for this combination
        num_candidates: Number of candidates per generation
        num_generations: Number of generations to run
    
    Returns:
        Dictionary with best result and search info
    """
    print(f"\n{'='*70}")
    print(f"Searching: {combination_name}")
    print(f"{'='*70}")
    
    best_result = None
    best_range = 0.0
    all_results = []
    
    # Search parameters
    # For 5+ second burn times, we need slower reactions
    # This typically means lower temperatures (closer to ignition threshold)
    # and potentially lower fuel-to-oxygen ratios
    min_temp = 373.15  # Just above ignition threshold
    max_temp = 420.0  # Lower max temp to encourage slower burns
    min_pressure = 400.0
    max_pressure = 800.0  # Lower pressure range for slower burns
    min_o2_temp = 293.15  # Room temperature
    max_o2_temp = 350.0  # Allow some O2 heating but keep it moderate
    
    # For each generation
    current_candidates = []
    
    for generation in range(num_generations):
        # Generate candidates
        if generation == 0:
            # Random initial candidates
            candidates = []
            for _ in range(num_candidates):
                temp = random.uniform(min_temp, max_temp)
                pressure = random.uniform(min_pressure, max_pressure)
                
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
                
                # Random O2 temperature
                o2_temp = random.uniform(min_o2_temp, max_o2_temp)
                
                candidates.append({
                    'temp': temp,
                    'pressure': pressure,
                    'fuel_ratios': ratios,
                    'atm_total_pct': atm_total_pct,
                    'atm_ratios': atm_ratios,
                    'o2_temp': o2_temp
                })
        else:
            # Create variations of best candidates
            candidates = []
            for base in current_candidates[:10]:  # Top 10
                for _ in range(num_candidates // 10):
                    temp = base['temp'] + random.uniform(-20.0, 20.0)
                    temp = max(min_temp, min(max_temp, temp))
                    
                    pressure = base['pressure'] + random.uniform(-50.0, 50.0)
                    pressure = max(min_pressure, min(max_pressure, pressure))
                    
                    # Vary O2 temperature
                    base_o2_temp = base.get('o2_temp', min_o2_temp)
                    o2_temp = base_o2_temp + random.uniform(-20.0, 20.0)
                    o2_temp = max(min_o2_temp, min(max_o2_temp, o2_temp))
                    candidate['o2_temp'] = o2_temp
                    
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
                                atm_ratios = [1.0 / len(atm_ratios)] * len(atm_ratios)
                    else:
                        atm_ratios = []
                    
                    candidates.append({
                        'temp': temp,
                        'pressure': pressure,
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
                # We'll use a base amount and scale
                base_fuel_moles = 1.0
                total_fuel_moles = base_fuel_moles
                
                # Add flammable gases
                for i, gas in enumerate(flammable_gases):
                    moles = base_fuel_moles * candidate['fuel_ratios'][i]
                    canister.set_moles(gas, moles)
                    total_fuel_moles += moles
                
                # Calculate atmospheric gas moles
                if len(atmospheric_gases) > 0 and candidate['atm_total_pct'] > 0:
                    # Atmospheric gases as percentage of total
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
                
                # Store actual gas amounts from canister AFTER scaling
                gas_amounts = {}
                total_moles_before = 0
                for gas in Gas:
                    moles = canister.get_moles(gas)
                    if moles > 0.0001:  # Only store significant amounts
                        gas_amounts[gas] = moles
                        total_moles_before += moles
                
                # Test explosion
                # Also vary O2 temperature in search
                if 'o2_temp' not in candidate:
                    o2_temp = random.uniform(min_o2_temp, max_o2_temp)
                else:
                    o2_temp = candidate['o2_temp']
                final_pressure, explosion_range, stats = simulate_maxcap_explosion(
                    canister,
                    candidate['temp'],
                    target_explosive_pressure=candidate['pressure'],
                    target_total_pressure=1013.0,
                    oxygen_temp=o2_temp,
                    canister_nitrogen_pct=0.0,  # Already in canister if specified
                    o2_mix_nitrogen_pct=0.0
                )
                
                # Skip combinations where mixed temperature is below ignition threshold
                if stats.get('below_ignition_temp', False):
                    continue  # This combination won't ignite, skip it
                
                # Skip combinations that don't meet minimum burn time requirement
                if min_burn_time_seconds > 0 and stats.get('burn_time_seconds', 0) < min_burn_time_seconds:
                    continue  # Burn time too short, skip it
                
                # Skip combinations that don't reach threshold
                if not stats.get('reached_threshold', False):
                    continue  # Didn't reach explosion threshold, skip it
                
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
                    'gas_amounts': gas_amounts,  # Store actual gas amounts
                    'generation': generation,
                    **stats
                }
                
                generation_results.append(result)
                all_results.append(result)
                
                if explosion_range > best_range:
                    best_range = explosion_range
                    best_result = result
                    
            except Exception as e:
                # Skip invalid combinations
                continue
        
        # Sort and select best for next generation
        generation_results.sort(key=lambda x: x['explosion_range'], reverse=True)
        current_candidates = generation_results[:10]
        
        if generation_results:
            gen_best = generation_results[0]
            o2_temp_str = f", O2={gen_best.get('o2_temp', min_o2_temp):.1f}K" if 'o2_temp' in gen_best else ""
            burn_time_str = f", burn={gen_best.get('burn_time_seconds', 0):.2f}s" if 'burn_time_seconds' in gen_best else ""
            print(f"  Generation {generation}: Best = {gen_best['explosion_range']:.2f} tiles "
                  f"(temp={gen_best['temp']:.1f}K, press={gen_best['pressure']:.1f}kPa{o2_temp_str}{burn_time_str})")
        else:
            print(f"  Generation {generation}: No valid results found (all filtered by burn time requirement)")
    
    return {
        'combination_name': combination_name,
        'best_result': best_result,
        'best_range': best_range,
        'all_results': all_results
    }


def main():
    """Run comprehensive gas combination search."""
    print("=" * 70)
    print("COMPREHENSIVE GAS COMBINATION SEARCH")
    print("=" * 70)
    print()
    print("Testing all combinations of:")
    print("  Flammable gases: Plasma, Tritium, Hydrogen (pure and combinations)")
    print("  Atmospheric gases: N2, CO2, Water Vapor (all combinations)")
    print()
    print("Minimum burn time requirement: 5.0 seconds")
    print("(Results must take at least 5 seconds to reach explosion threshold)")
    print()
    
    all_combination_results = []
    
    # Test each flammable gas combination with each atmospheric combination
    for flammable_gases, flammable_name in FLAMMABLE_GASES:
        for atmospheric_gases, atmospheric_name in ATMOSPHERIC_GASES:
            combination_name = f"{flammable_name}"
            if atmospheric_name != "No atmospheric":
                combination_name += f" + {atmospheric_name}"
            
            result = search_gas_combination(
                flammable_gases,
                atmospheric_gases,
                combination_name,
                num_candidates=100,  # More candidates to find slower burns
                num_generations=10,  # More generations to refine
                min_burn_time_seconds=5.0  # Require at least 5 seconds burn time
            )
            
            all_combination_results.append(result)
    
    # Sort all results by explosion range
    all_combination_results.sort(key=lambda x: x['best_range'], reverse=True)
    
    # Print summary
    print("\n" + "=" * 70)
    print("SUMMARY - TOP 20 COMBINATIONS")
    print("=" * 70)
    print()
    print(f"{'Rank':<6} {'Range':<8} {'Temp':<7} {'O2Temp':<8} {'BurnTime':<10} {'Combination':<40}")
    print("-" * 85)
    
    for i, result in enumerate(all_combination_results[:20], 1):
        if result['best_result']:
            best = result['best_result']
            temp_str = f"{best['temp']:.1f}" if 'temp' in best else "N/A"
            o2_temp = best.get('o2_temp', best.get('o2_temp_k', 293.15))
            o2_temp_str = f"{o2_temp:.1f}"
            burn_time = best.get('burn_time_seconds', 0.0)
            burn_time_str = f"{burn_time:.2f}s" if burn_time > 0 else "N/A"
            print(f"{i:<6} {result['best_range']:>7.2f} {temp_str:<7} {o2_temp_str:<8} {burn_time_str:<10} {result['combination_name']:<40}")
    
    # Print detailed best result
    if all_combination_results and all_combination_results[0]['best_result']:
        best = all_combination_results[0]['best_result']
        print("\n" + "=" * 70)
        print("BEST OVERALL RESULT")
        print("=" * 70)
        print()
        print(f"Combination: {all_combination_results[0]['combination_name']}")
        print(f"Explosion Range: {best['explosion_range']:.2f} tiles")
        print(f"Temperature: {best['temp']:.2f} K ({best['temp'] - 273.15:.2f}°C)")
        print(f"Explosive Pressure: {best['pressure']:.2f} kPa")
        print(f"O2 Temperature: {best.get('o2_temp', best.get('o2_temp_k', 293.15)):.2f} K ({best.get('o2_temp', best.get('o2_temp_k', 293.15)) - 273.15:.2f}°C)")
        print(f"Final Pressure: {best['final_pressure']:.2f} kPa")
        print(f"Mixed Temperature: {best.get('mixed_temp', best.get('initial_temp', 'N/A')):.2f} K" if isinstance(best.get('mixed_temp', best.get('initial_temp', 'N/A')), (int, float)) else f"Mixed Temperature: {best.get('mixed_temp', best.get('initial_temp', 'N/A'))}")
        print(f"Burn Time: {best.get('burn_time_seconds', 0.0):.2f} seconds")
        print(f"Cycles to Threshold: {best.get('cycles_to_threshold', 0)}")
        print()
        
        # Show gas composition from actual gas amounts
        print("Gas Composition:")
        if best.get('gas_amounts'):
            total_moles = sum(best['gas_amounts'].values())
            if total_moles > 0:
                # Sort by amount (descending)
                sorted_gases = sorted(best['gas_amounts'].items(), key=lambda x: x[1], reverse=True)
                for gas, moles in sorted_gases:
                    pct = (moles / total_moles) * 100.0
                    print(f"  {gas.name}: {pct:.2f}%")
        else:
            # Fallback to ratios if gas_amounts not available
            print("  (Gas amounts not available, using ratios)")
            for flammable_gases, flammable_name in FLAMMABLE_GASES:
                for atmospheric_gases, atmospheric_name in ATMOSPHERIC_GASES:
                    combo_name = flammable_name
                    if atmospheric_name != "No atmospheric":
                        combo_name += f" + {atmospheric_name}"
                    
                    if combo_name == all_combination_results[0]['combination_name']:
                        fuel_total_pct = 100.0 - best.get('atm_total_pct', 0.0)
                        
                        if best.get('fuel_ratios') and len(flammable_gases) == len(best['fuel_ratios']):
                            for i, gas in enumerate(flammable_gases):
                                gas_name = gas.name
                                pct = best['fuel_ratios'][i] * fuel_total_pct
                                print(f"  {gas_name}: {pct:.2f}%")
                        
                        if len(atmospheric_gases) > 0 and best.get('atm_total_pct', 0) > 0:
                            print(f"  Atmospheric gases: {best['atm_total_pct']:.2f}% total")
                            if best.get('atm_ratios') and len(atmospheric_gases) == len(best['atm_ratios']):
                                for i, gas in enumerate(atmospheric_gases):
                                    gas_name = gas.name
                                    pct = best['atm_ratios'][i] * best['atm_total_pct']
                                    print(f"    {gas_name}: {pct:.2f}%")
                        break
    
    print("\n" + "=" * 70)


if __name__ == "__main__":
    main()
