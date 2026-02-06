#!/usr/bin/env python3
"""Test how long it takes for reactions to reach explosion threshold."""

from maxcap_explosion_simulator import (
    Gas, GasMixture, TANK_VOLUME, R, TankFragmentPressure,
    simulate_maxcap_explosion, run_reactions
)

def test_burn_time_to_threshold(min_burn_time_seconds=0.0):
    """
    Test how many reaction cycles it takes to reach explosion threshold.
    
    Args:
        min_burn_time_seconds: Minimum required burn time in seconds (default 0.0).
                               If set, only combinations that take at least this long
                               will be considered valid.
    """
    TimerDelay = 0.5  # seconds per cycle (from game code)
    min_cycles_required = int(min_burn_time_seconds / TimerDelay) if min_burn_time_seconds > 0 else 0
    
    # Parameters - using the best Pure Tritium result from exhaustive search
    # But user asked about 441.12K result - that was from pure plasma search
    # Let me test both to be thorough
    print("Testing two scenarios:")
    print("1. Best Pure Tritium (498.23 K)")
    print("2. Pure Plasma at 441.12 K (from earlier search)")
    if min_burn_time_seconds > 0:
        print(f"\nMinimum burn time required: {min_burn_time_seconds:.1f} seconds ({min_cycles_required} cycles)")
    print()
    
    # Scenario 1: Best Pure Tritium
    explosive_temp_1 = 498.23  # K
    explosive_pressure_1 = 770.99  # kPa
    o2_temp_1 = 293.15  # K
    
    # Scenario 2: Pure Plasma (the 441.12K result user mentioned)
    explosive_temp_2 = 441.12  # K
    explosive_pressure_2 = 469.78  # kPa
    o2_temp_2 = 347.13  # K
    
    scenarios = [
        ("Best Pure Tritium", explosive_temp_1, explosive_pressure_1, o2_temp_1, Gas.Tritium),
        ("Pure Plasma (441.12K)", explosive_temp_2, explosive_pressure_2, o2_temp_2, Gas.Plasma)
    ]
    
    for scenario_name, explosive_temp, explosive_pressure, o2_temp, fuel_gas in scenarios:
        print("=" * 70)
        print(f"SCENARIO: {scenario_name}")
        print("=" * 70)
        print()
        print(f"Explosive Mix: {fuel_gas.name} at {explosive_temp:.2f} K, {explosive_pressure:.2f} kPa")
        print(f"O2 Mix: Pure O2 at {o2_temp:.2f} K")
        print(f"Explosion Threshold: {TankFragmentPressure:.2f} kPa")
        print()
        
        # Create the initial canister mix
        canister = GasMixture(volume=TANK_VOLUME, temperature=explosive_temp)
        fuel_moles = (explosive_pressure * TANK_VOLUME) / (R * explosive_temp)
        canister.set_moles(fuel_gas, fuel_moles)
        
        # Simulate the mixing process to get initial pressure after adding O2
        final_pressure, explosion_range, stats = simulate_maxcap_explosion(
            canister,
            explosive_temp,
            target_explosive_pressure=explosive_pressure,
            target_total_pressure=1013.0,
            oxygen_temp=o2_temp,
            canister_nitrogen_pct=0.0,
            o2_mix_nitrogen_pct=0.0
        )
        
        if stats.get('below_ignition_temp', False):
            print("ERROR: Mixed temperature is below ignition threshold!")
            print()
            continue
        
        initial_pressure = stats['initial_pressure']
        initial_temp = stats['initial_temp']
        
        print(f"After mixing (before reactions):")
        print(f"  Pressure: {initial_pressure:.2f} kPa")
        print(f"  Temperature: {initial_temp:.2f} K")
        print()
        
        # Check if already above threshold
        if initial_pressure > TankFragmentPressure:
            print(f"Already above threshold! Explosion happens immediately.")
            print()
            continue
        
        # Recreate the mix using the exact same process as simulate_maxcap_explosion
        # This is complex, so let's use a simpler approach - recreate from the stats
        # Actually, we need to recreate it properly. Let me use the iterative approach from simulate_maxcap_explosion
        
        # Start fresh
        canister2 = GasMixture(volume=TANK_VOLUME, temperature=explosive_temp)
        fuel_moles2 = (explosive_pressure * TANK_VOLUME) / (R * explosive_temp)
        canister2.set_moles(fuel_gas, fuel_moles2)
        
        # Scale to target pressure
        current_p = canister2.pressure
        if current_p > 0:
            scale = explosive_pressure / current_p
            for gas in Gas:
                canister2.set_moles(gas, canister2.get_moles(gas) * scale)
        
        # Calculate O2 with iterative refinement (simplified)
        explosive_heat_cap = canister2.get_heat_capacity()
        explosive_moles = canister2.total_moles
        remaining = 1013.0 - explosive_pressure
        pressure_delta = min(remaining, explosive_pressure / 2.0)
        o2_moles_approx = pressure_delta * TANK_VOLUME / (R * explosive_temp)
        
        # Refine
        for _ in range(10):
            temp_o2_mix = GasMixture(volume=TANK_VOLUME, temperature=o2_temp)
            temp_o2_mix.set_moles(Gas.Oxygen, o2_moles_approx)
            o2_heat_cap = temp_o2_mix.get_heat_capacity()
            combined_heat_cap = explosive_heat_cap + o2_heat_cap
            if combined_heat_cap > 0.0003:
                mixed_temp = (explosive_temp * explosive_heat_cap + o2_temp * o2_heat_cap) / combined_heat_cap
            else:
                mixed_temp = (explosive_temp + o2_temp) / 2.0
            total_moles = explosive_moles + o2_moles_approx
            actual_p = total_moles * R * mixed_temp / TANK_VOLUME
            pressure_diff = 1013.0 - actual_p
            if abs(pressure_diff) < 0.01:
                break
            o2_adjustment = pressure_diff * TANK_VOLUME / (R * explosive_temp)
            o2_moles_approx += o2_adjustment
            o2_moles_approx = max(0.0, o2_moles_approx)
        
        o2_moles = o2_moles_approx
        
        # Create O2 mix
        o2_mix = GasMixture(volume=TANK_VOLUME, temperature=o2_temp)
        o2_mix.set_moles(Gas.Oxygen, o2_moles)
        
        # Mix temperatures
        o2_heat_cap = o2_mix.get_heat_capacity()
        combined_heat_cap = explosive_heat_cap + o2_heat_cap
        if combined_heat_cap > 0.0003:
            mixed_temp = (explosive_temp * explosive_heat_cap + o2_temp * o2_heat_cap) / combined_heat_cap
        else:
            mixed_temp = (explosive_temp + o2_temp) / 2.0
        
        # Create final mix
        final_mix = GasMixture(volume=TANK_VOLUME, temperature=mixed_temp)
        for gas in Gas:
            final_mix.set_moles(gas, canister2.get_moles(gas) + o2_mix.get_moles(gas))
        
        initial_pressure_actual = final_mix.pressure
        
        # Verify it matches
        if abs(initial_pressure_actual - initial_pressure) > 10.0:
            print(f"Warning: Pressure mismatch - stats: {initial_pressure:.2f}, recreated: {initial_pressure_actual:.2f}")
            print(f"Using stats value: {initial_pressure:.2f} kPa")
            # Recreate to match stats
            total_moles_needed = initial_pressure * TANK_VOLUME / (R * initial_temp)
            # Scale the mix to match
            current_total = final_mix.total_moles
            if current_total > 0:
                scale_factor = total_moles_needed / current_total
                for gas in Gas:
                    final_mix.set_moles(gas, final_mix.get_moles(gas) * scale_factor)
                final_mix.temperature = initial_temp
                initial_pressure_actual = final_mix.pressure
        
        print(f"Running reactions until pressure reaches {TankFragmentPressure:.2f} kPa...")
        print()
        
        cycle = 0
        max_cycles = 100
        
        while final_mix.pressure < TankFragmentPressure and cycle < max_cycles:
            cycle += 1
            pressure_before = final_mix.pressure
            temp_before = final_mix.temperature
            
            # Run reactions (this runs until they stop, which is typically 1-100 iterations)
            run_reactions(final_mix, max_iterations=100)
            
            pressure_after = final_mix.pressure
            temp_after = final_mix.temperature
            
            if cycle <= 5 or cycle % 5 == 0 or pressure_after >= TankFragmentPressure:
                print(f"Cycle {cycle:3d}: Pressure {pressure_before:8.2f} -> {pressure_after:8.2f} kPa "
                      f"(+{pressure_after - pressure_before:6.2f}), Temp {temp_after:.1f} K")
            
            if pressure_after >= TankFragmentPressure:
                break
        
        print()
        print(f"Cycles to reach threshold: {cycle}")
        print(f"Final pressure: {final_mix.pressure:.2f} kPa")
        print(f"Final temperature: {final_mix.temperature:.1f} K")
        print()
        
        # TimerDelay = 0.5 seconds from game code
        time_to_explosion = cycle * TimerDelay
        print(f"Time to explosion: {time_to_explosion:.1f} seconds")
        print(f"  (TimerDelay = {TimerDelay} seconds per cycle)")
        print()
        
        # Check if it meets minimum burn time requirement
        if min_burn_time_seconds > 0:
            if time_to_explosion >= min_burn_time_seconds:
                print(f"[VALID] Meets minimum burn time requirement ({min_burn_time_seconds:.1f}s)")
            else:
                print(f"[INVALID] Does not meet minimum burn time requirement")
                print(f"  Required: {min_burn_time_seconds:.1f}s, Actual: {time_to_explosion:.1f}s")
                print(f"  This combination would explode too quickly to arm safely in-game")
        print()
        print()
    
    return
    
    print("=" * 70)
    print("TESTING BURN TIME TO EXPLOSION THRESHOLD")
    print("=" * 70)
    print()
    print(f"Explosive Mix: Pure Tritium at {explosive_temp:.2f} K, {explosive_pressure:.2f} kPa")
    print(f"O2 Mix: Pure O2 at {o2_temp:.2f} K")
    print(f"Explosion Threshold: {TankFragmentPressure:.2f} kPa")
    print()
    
    # Create the initial canister mix
    canister = GasMixture(volume=TANK_VOLUME, temperature=explosive_temp)
    tritium_moles = (explosive_pressure * TANK_VOLUME) / (R * explosive_temp)
    canister.set_moles(Gas.Tritium, tritium_moles)
    
    # Simulate the mixing process to get initial pressure after adding O2
    final_pressure, explosion_range, stats = simulate_maxcap_explosion(
        canister,
        explosive_temp,
        target_explosive_pressure=explosive_pressure,
        target_total_pressure=1013.0,
        oxygen_temp=o2_temp,
        canister_nitrogen_pct=0.0,
        o2_mix_nitrogen_pct=0.0
    )
    
    if stats.get('below_ignition_temp', False):
        print("ERROR: Mixed temperature is below ignition threshold!")
        return
    
    initial_pressure = stats['initial_pressure']
    initial_temp = stats['initial_temp']
    
    print(f"After mixing (before reactions):")
    print(f"  Pressure: {initial_pressure:.2f} kPa")
    print(f"  Temperature: {initial_temp:.2f} K")
    print()
    
    # Now simulate reactions cycle by cycle until we reach threshold
    # Recreate the mixture at initial state
    test_mix = GasMixture(volume=TANK_VOLUME, temperature=initial_temp)
    
    # Get the initial composition from the stats
    # We need to recreate what the mix was after merging but before reactions
    # The stats don't have this, so let's recreate it from the simulation
    
    # Actually, let's just run the simulation step by step
    # Recreate canister
    canister2 = GasMixture(volume=TANK_VOLUME, temperature=explosive_temp)
    tritium_moles2 = (explosive_pressure * TANK_VOLUME) / (R * explosive_temp)
    canister2.set_moles(Gas.Tritium, tritium_moles2)
    
    # Calculate O2 to add (same as in simulate_maxcap_explosion)
    explosive_heat_cap = canister2.get_heat_capacity()
    explosive_moles = canister2.total_moles
    remaining_pressure_capacity = 1013.0 - explosive_pressure
    pressure_delta = min(remaining_pressure_capacity, (explosive_pressure - 0.0) / 2.0)
    o2_moles = pressure_delta * TANK_VOLUME / (R * explosive_temp)
    
    # Create O2 mix
    o2_mix = GasMixture(volume=TANK_VOLUME, temperature=o2_temp)
    o2_mix.set_moles(Gas.Oxygen, o2_moles)
    
    # Mix temperatures
    o2_heat_cap = o2_mix.get_heat_capacity()
    combined_heat_cap = explosive_heat_cap + o2_heat_cap
    if combined_heat_cap > 0.0003:  # MinimumHeatCapacity
        mixed_temp = (explosive_temp * explosive_heat_cap + o2_temp * o2_heat_cap) / combined_heat_cap
    else:
        mixed_temp = (explosive_temp + o2_temp) / 2.0
    
    # Create final mix
    final_mix = GasMixture(volume=TANK_VOLUME, temperature=mixed_temp)
    for gas in Gas:
        final_mix.set_moles(gas, canister2.get_moles(gas) + o2_mix.get_moles(gas))
    
    initial_pressure_actual = final_mix.pressure
    initial_temp_actual = final_mix.temperature
    
    print(f"Initial state after mixing:")
    print(f"  Pressure: {initial_pressure_actual:.2f} kPa")
    print(f"  Temperature: {initial_temp_actual:.2f} K")
    print(f"  Tritium: {final_mix.get_moles(Gas.Tritium):.4f} mol")
    print(f"  Oxygen: {final_mix.get_moles(Gas.Oxygen):.4f} mol")
    print()
    
    # Check if already above threshold
    if initial_pressure_actual > TankFragmentPressure:
        print(f"Already above threshold! Explosion happens immediately.")
        return
    
    print(f"Running reactions until pressure reaches {TankFragmentPressure:.2f} kPa...")
    print()
    
    cycle = 0
    max_cycles = 1000  # Safety limit
    
    pressures = []
    temperatures = []
    
    while final_mix.pressure < TankFragmentPressure and cycle < max_cycles:
        cycle += 1
        pressure_before = final_mix.pressure
        temp_before = final_mix.temperature
        
        # Run reactions (this runs until they stop, which is typically 1-100 iterations)
        run_reactions(final_mix, max_iterations=100)
        
        pressure_after = final_mix.pressure
        temp_after = final_mix.temperature
        
        pressures.append(pressure_after)
        temperatures.append(temp_after)
        
        if cycle <= 10 or cycle % 10 == 0 or pressure_after >= TankFragmentPressure:
            print(f"Cycle {cycle:3d}: Pressure {pressure_before:8.2f} -> {pressure_after:8.2f} kPa "
                  f"(+{pressure_after - pressure_before:6.2f}), Temp {temp_after:.1f} K")
        
        if pressure_after >= TankFragmentPressure:
            break
    
    print()
    print("=" * 70)
    print("RESULTS")
    print("=" * 70)
    print()
    print(f"Cycles to reach threshold: {cycle}")
    print(f"Final pressure: {final_mix.pressure:.2f} kPa")
    print(f"Final temperature: {final_mix.temperature:.1f} K")
    print()
    
    # From game code, reactions run once per Update cycle
    # TimerDelay appears to be around 0.5-1.0 seconds based on typical game update rates
    # But let me check if we can find the actual value
    print("Note: In-game, reactions run once per gas tank update cycle.")
    print("The update frequency depends on TimerDelay (typically 0.5-1.0 seconds per cycle).")
    print()
    print(f"Estimated time to explosion: {cycle * 0.5:.1f} - {cycle * 1.0:.1f} seconds")
    print(f"  (assuming 0.5-1.0 second update interval)")

if __name__ == "__main__":
    import sys
    
    # Check for command line argument for minimum burn time
    min_burn_time = 0.0
    if len(sys.argv) > 1:
        try:
            min_burn_time = float(sys.argv[1])
        except ValueError:
            print(f"Invalid argument: {sys.argv[1]}. Using default: 0.0 seconds")
    
    test_burn_time_to_threshold(min_burn_time_seconds=min_burn_time)
