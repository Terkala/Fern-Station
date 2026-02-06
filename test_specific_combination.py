#!/usr/bin/env python3
"""Test the specific combination the user mentioned."""

from maxcap_explosion_simulator import (
    Gas, GasMixture, TANK_VOLUME, R, TankFragmentPressure,
    simulate_maxcap_explosion, run_reactions, calculate_explosion_range
)

def test_specific_combination():
    """Test 54% plasma, 46% tritium at 382.7K."""
    
    print("=" * 70)
    print("TESTING SPECIFIC COMBINATION")
    print("=" * 70)
    print()
    print("Configuration:")
    print("  - 54% Plasma, 46% Tritium")
    print("  - Temperature: 382.7 K")
    print("  - Target total pressure: 1013 kPa")
    print()
    
    # Create canister mix
    # The user said 54% plasma, 46% tritium at 382.7K works and takes ~15 seconds
    # Let's test a range of pressures to find which one gives ~15 seconds
    # Lower pressure = more oxygen can be added = longer burn time
    test_pressures = [400.0, 450.0, 500.0, 550.0, 600.0, 650.0, 700.0, 750.0, 800.0]
    
    for explosive_pressure in test_pressures:
        print(f"\n{'='*70}")
        print(f"Testing with explosive pressure: {explosive_pressure:.1f} kPa")
        print(f"{'='*70}")
        
        # Calculate moles for 54% plasma, 46% tritium at target pressure
        total_moles = (explosive_pressure * TANK_VOLUME) / (R * 382.7)
        plasma_moles = total_moles * 0.54
        tritium_moles = total_moles * 0.46
        
        canister = GasMixture(volume=TANK_VOLUME, temperature=382.7)
        canister.set_moles(Gas.Plasma, plasma_moles)
        canister.set_moles(Gas.Tritium, tritium_moles)
        
        # Test with O2 at 20°C
        o2_temp = 293.15
        
        print(f"\nCanister mix:")
        print(f"  Plasma: {plasma_moles:.4f} mol ({plasma_moles/total_moles*100:.1f}%)")
        print(f"  Tritium: {tritium_moles:.4f} mol ({tritium_moles/total_moles*100:.1f}%)")
        print(f"  Temperature: 382.7 K")
        print(f"  Pressure: {canister.pressure:.2f} kPa")
        
        # Calculate how much O2 we can add
        remaining_pressure = 1013.0 - explosive_pressure
        pressure_delta = min(remaining_pressure, explosive_pressure / 2.0)
        o2_moles_approx = pressure_delta * TANK_VOLUME / (R * 382.7)
        print(f"  Available O2 capacity: {remaining_pressure:.2f} kPa")
        print(f"  O2 moles to add (approx): {o2_moles_approx:.4f} mol")
        print()
        
        # Simulate
        final_pressure, explosion_range, stats = simulate_maxcap_explosion(
            canister,
            382.7,
            target_explosive_pressure=explosive_pressure,
            target_total_pressure=1013.0,
            oxygen_temp=o2_temp,
            canister_nitrogen_pct=0.0,
            o2_mix_nitrogen_pct=0.0
        )
        
        print(f"Results:")
        print(f"  Below ignition temp: {stats.get('below_ignition_temp', False)}")
        print(f"  Reached threshold: {stats.get('reached_threshold', True)}")
        print(f"  Initial pressure: {stats['initial_pressure']:.2f} kPa")
        print(f"  Initial temperature: {stats['initial_temp']:.2f} K")
        print(f"  Final pressure: {final_pressure:.2f} kPa")
        print(f"  Explosion range: {explosion_range:.2f} tiles")
        print(f"  Cycles to threshold: {stats.get('cycles_to_threshold', 0)}")
        print(f"  Burn time: {stats.get('burn_time_seconds', 0.0):.2f} seconds")
        
        # Check if this is close to 15 seconds
        burn_time = stats.get('burn_time_seconds', 0.0)
        if 10.0 <= burn_time <= 20.0 and explosion_range > 0:
            print(f"  *** CLOSE TO 15 SECONDS! ***")
        if burn_time > 0 and explosion_range > 0:
            print(f"  ✓ Valid result: {burn_time:.1f}s burn, {explosion_range:.2f} tiles")
        elif not stats.get('reached_threshold', True):
            print(f"  ✗ Did not reach threshold - reactions stopped early")
        print()
        
        # If it didn't reach threshold, let's manually test the burn
        if not stats.get('reached_threshold', True):
            print("  WARNING: Did not reach threshold in simulation!")
            print("  Manually testing burn time...")
            
            # Recreate the mix
            test_mix = GasMixture(volume=TANK_VOLUME, temperature=stats['initial_temp'])
            # We need to get the actual composition after mixing
            # Let's recreate it from the stats or re-run the mixing
            
            # Actually, let's just re-run the simulation step by step
            canister2 = GasMixture(volume=TANK_VOLUME, temperature=382.7)
            canister2.set_moles(Gas.Plasma, plasma_moles)
            canister2.set_moles(Gas.Tritium, tritium_moles)
            
            # Scale to target pressure
            current_p = canister2.pressure
            if current_p > 0:
                scale = explosive_pressure / current_p
                canister2.set_moles(Gas.Plasma, canister2.get_moles(Gas.Plasma) * scale)
                canister2.set_moles(Gas.Tritium, canister2.get_moles(Gas.Tritium) * scale)
            
            # Calculate O2 to add
            explosive_heat_cap = canister2.get_heat_capacity()
            explosive_moles = canister2.total_moles
            remaining = 1013.0 - explosive_pressure
            pressure_delta = min(remaining, explosive_pressure / 2.0)
            o2_moles = pressure_delta * TANK_VOLUME / (R * 382.7)
            
            # Create O2 mix
            o2_mix = GasMixture(volume=TANK_VOLUME, temperature=o2_temp)
            o2_mix.set_moles(Gas.Oxygen, o2_moles)
            
            # Mix temperatures
            o2_heat_cap = o2_mix.get_heat_capacity()
            combined_heat_cap = explosive_heat_cap + o2_heat_cap
            from maxcap_explosion_simulator import MinimumHeatCapacity
            if combined_heat_cap > MinimumHeatCapacity:
                mixed_temp = (382.7 * explosive_heat_cap + o2_temp * o2_heat_cap) / combined_heat_cap
            else:
                mixed_temp = (382.7 + o2_temp) / 2.0
            
            # Create final mix
            final_mix = GasMixture(volume=TANK_VOLUME, temperature=mixed_temp)
            final_mix.set_moles(Gas.Plasma, canister2.get_moles(Gas.Plasma))
            final_mix.set_moles(Gas.Tritium, canister2.get_moles(Gas.Tritium))
            final_mix.set_moles(Gas.Oxygen, o2_moles)
            
            print(f"  Mixed temperature: {mixed_temp:.2f} K")
            print(f"  Mixed pressure: {final_mix.pressure:.2f} kPa")
            print(f"  Plasma: {final_mix.get_moles(Gas.Plasma):.4f} mol")
            print(f"  Tritium: {final_mix.get_moles(Gas.Tritium):.4f} mol")
            print(f"  Oxygen: {final_mix.get_moles(Gas.Oxygen):.4f} mol")
            print()
            
            # Test burn time manually
            print(f"  Running reactions until threshold ({TankFragmentPressure:.2f} kPa)...")
            cycles = 0
            max_cycles = 100
            while final_mix.pressure < TankFragmentPressure and cycles < max_cycles:
                cycles += 1
                pressure_before = final_mix.pressure
                temp_before = final_mix.temperature
                
                # Check reactants before reaction
                plasma_before = final_mix.get_moles(Gas.Plasma)
                tritium_before = final_mix.get_moles(Gas.Tritium)
                oxygen_before = final_mix.get_moles(Gas.Oxygen)
                
                run_reactions(final_mix, max_iterations=100)
                pressure_after = final_mix.pressure
                temp_after = final_mix.temperature
                
                # Check reactants after reaction
                plasma_after = final_mix.get_moles(Gas.Plasma)
                tritium_after = final_mix.get_moles(Gas.Tritium)
                oxygen_after = final_mix.get_moles(Gas.Oxygen)
                
                if cycles <= 5 or cycles % 5 == 0 or pressure_after >= TankFragmentPressure:
                    print(f"    Cycle {cycles}: {pressure_before:.2f} -> {pressure_after:.2f} kPa, "
                          f"Temp {temp_before:.1f} -> {temp_after:.1f} K")
                    print(f"      Reactants: P={plasma_after:.4f}, T={tritium_after:.4f}, O2={oxygen_after:.4f}")
                
                if pressure_after >= TankFragmentPressure:
                    break
                
                # If pressure didn't change and we still have reactants, something is wrong
                if abs(pressure_after - pressure_before) < 0.01 and (plasma_after > 0.01 or tritium_after > 0.01) and oxygen_after > 0.01:
                    print(f"    WARNING: Reactions stopped but reactants remain!")
                    print(f"      Plasma: {plasma_after:.4f}, Tritium: {tritium_after:.4f}, O2: {oxygen_after:.4f}")
                    print(f"      Temperature: {temp_after:.1f} K")
                    break
            
            if cycles < max_cycles and final_mix.pressure >= TankFragmentPressure:
                print(f"  Reached threshold in {cycles} cycles ({cycles * 0.5:.1f} seconds)")
                print(f"  Final pressure: {final_mix.pressure:.2f} kPa")
                explosion_range_manual = calculate_explosion_range(final_mix.pressure)
                print(f"  Explosion range: {explosion_range_manual:.2f} tiles")
            else:
                print(f"  Did not reach threshold after {cycles} cycles")
                print(f"  Final pressure: {final_mix.pressure:.2f} kPa")
                print(f"  Final reactants: P={final_mix.get_moles(Gas.Plasma):.4f}, "
                      f"T={final_mix.get_moles(Gas.Tritium):.4f}, "
                      f"O2={final_mix.get_moles(Gas.Oxygen):.4f}")

if __name__ == "__main__":
    test_specific_combination()
