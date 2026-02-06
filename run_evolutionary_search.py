#!/usr/bin/env python3
"""Run the evolutionary search for optimal MaxCap combinations."""

from maxcap_explosion_simulator import evolutionary_search, export_results_to_csv, calculate_o2_mix_preparation, print_o2_mix_preparation, calculate_explosive_mix_preparation, print_explosive_mix_preparation

if __name__ == "__main__":
    import sys
    
    # Check for command line argument for minimum burn time
    min_burn_time = 0.0
    if len(sys.argv) > 1:
        try:
            min_burn_time = float(sys.argv[1])
            print(f"Minimum burn time requirement: {min_burn_time:.1f} seconds")
            print()
        except ValueError:
            print(f"Invalid argument: {sys.argv[1]}. Using default: 0.0 seconds")
            print()
    
    all_results, best_per_gen = evolutionary_search(
        initial_candidates=100,
        top_n=10,
        variations_per_candidate=10,
        max_generations=20,
        improvement_threshold=0.05,
        min_plasma_pct=30.0,
        max_plasma_pct=70.0,
        min_temp=373.15,
        max_temp=593.15,  # THERMOMACHINE_MAX_TEMP
        min_pressure=400.0,
        max_pressure=900.0,
        min_o2_temp=293.15,  # 20°C
        max_o2_temp=593.15,  # THERMOMACHINE_MAX_TEMP
        max_canister_n2_pct=30.0,  # Up to 30% nitrogen in canister
        max_o2_mix_n2_pct=30.0,  # Up to 30% nitrogen in O2 mix
        min_burn_time_seconds=min_burn_time
    )
    
    print("=" * 70)
    print("FINAL RESULTS")
    print("=" * 70)
    print()
    print("Best overall:")
    best = best_per_gen[-1]
    print(f"  Explosion Range: {best['explosion_range']:.2f} tiles")
    print(f"  Plasma: {best['plasma_pct']:.2f}%")
    print(f"  Tritium: {best['tritium_pct']:.2f}%")
    print(f"  Temperature: {best['temp']:.2f} K ({best['temp'] - 273.15:.2f}°C)")
    print(f"  Explosive Pressure: {best['pressure']:.2f} kPa")
    print(f"  O2 Temperature: {best['o2_temp']:.2f} K ({best['o2_temp'] - 273.15:.2f}°C)")
    print(f"  Canister N2: {best['canister_n2_pct']:.2f}%")
    print(f"  O2 Mix N2: {best['o2_mix_n2_pct']:.2f}%")
    print(f"  Final Pressure: {best['final_pressure']:.2f} kPa")
    print(f"  O2 Mix to Add: {best['o2_pressure_equivalent_kpa']:.2f} kPa")
    if 'burn_time_seconds' in best:
        print(f"  Burn Time to Threshold: {best['burn_time_seconds']:.2f} seconds ({best.get('cycles_to_threshold', 0)} cycles)")
    print()
    
    # Calculate and print O2 mix preparation instructions
    # Use o2_temp_k and o2_mix_n2_pct from stats, or fall back to o2_temp if available
    o2_temp = best.get('o2_temp_k', best.get('o2_temp', 293.15))
    o2_mix_n2_pct = best.get('o2_mix_n2_pct', best.get('o2_mix_nitrogen_pct', 0.0))
    
    prep_info = calculate_o2_mix_preparation(
        target_o2_mix_temp=o2_temp,
        target_n2_pct=o2_mix_n2_pct
    )
    print_o2_mix_preparation(prep_info)
    
    # Calculate and print explosive mix preparation instructions
    explosive_prep_info = calculate_explosive_mix_preparation(
        target_plasma_pct=best['plasma_pct'],
        target_tritium_pct=best['tritium_pct'],
        target_n2_pct=best['canister_n2_pct'],
        target_final_temp=best['temp']
    )
    print_explosive_mix_preparation(explosive_prep_info)
    
    print("Top 10 overall:")
    all_results.sort(key=lambda x: x['explosion_range'], reverse=True)
    print(f"{'Rank':<6} {'Range':<8} {'Plasma%':<9} {'Tritium%':<10} {'Temp':<7} {'Press':<8} {'O2Temp':<8} {'CanN2':<7} {'O2N2':<7} {'Gen':<5}")
    print("-" * 85)
    for i, result in enumerate(all_results[:10], 1):
        print(f"{i:<6} {result['explosion_range']:>7.2f} {result['plasma_pct']:>8.2f}% "
              f"{result['tritium_pct']:>9.2f}% {result['temp']:>6.1f} "
              f"{result['pressure']:>7.1f} {result['o2_temp']:>7.1f} "
              f"{result['canister_n2_pct']:>6.1f}% {result['o2_mix_n2_pct']:>6.1f}% {result['generation']:>4}")
    print()
    
    print("Best per generation:")
    print(f"{'Gen':<6} {'Range':<8} {'Plasma%':<9} {'Tritium%':<10} {'Temp':<7} {'Press':<8} {'O2Temp':<8} {'CanN2':<7} {'O2N2':<7}")
    print("-" * 80)
    for i, result in enumerate(best_per_gen):
        print(f"{i:<6} {result['explosion_range']:>7.2f} {result['plasma_pct']:>8.2f}% "
              f"{result['tritium_pct']:>9.2f}% {result['temp']:>6.1f} "
              f"{result['pressure']:>7.1f} {result['o2_temp']:>7.1f} "
              f"{result['canister_n2_pct']:>6.1f}% {result['o2_mix_n2_pct']:>6.1f}%")
    print()
    
    # Export results
    export_results_to_csv(all_results, "evolutionary_search_results.csv")
    print(f"\nExported {len(all_results)} results to evolutionary_search_results.csv")
