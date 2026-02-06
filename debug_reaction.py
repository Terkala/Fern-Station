#!/usr/bin/env python3
"""Debug why reactions stop prematurely."""

from maxcap_explosion_simulator import (
    Gas, GasMixture, TANK_VOLUME, R, TankFragmentPressure,
    MinimumHeatCapacity, PlasmaMinimumBurnTemperature, PlasmaUpperTemperature,
    PlasmaOxygenFullburn, PlasmaBurnRateDelta, OxygenBurnRateBase,
    run_reactions
)

def debug_reaction_step(mixture, cycle_num):
    """Debug a single reaction step."""
    initial_plasma = mixture.get_moles(Gas.Plasma)
    initial_tritium = mixture.get_moles(Gas.Tritium)
    initial_oxygen = mixture.get_moles(Gas.Oxygen)
    initial_temp = mixture.temperature
    initial_pressure = mixture.pressure
    
    print(f"\nCycle {cycle_num} - Before reactions:")
    print(f"  Plasma: {initial_plasma:.6f} mol")
    print(f"  Tritium: {initial_tritium:.6f} mol")
    print(f"  Oxygen: {initial_oxygen:.6f} mol")
    print(f"  Temperature: {initial_temp:.2f} K")
    print(f"  Pressure: {initial_pressure:.2f} kPa")
    
    # Check plasma reaction
    if initial_oxygen >= 0.01 and initial_plasma >= 0.01:
        if initial_temp >= PlasmaMinimumBurnTemperature:
            if initial_temp > PlasmaUpperTemperature:
                temp_scale = 1.0
            else:
                temp_scale = (initial_temp - PlasmaMinimumBurnTemperature) / \
                           (PlasmaUpperTemperature - PlasmaMinimumBurnTemperature)
            
            if temp_scale > 0:
                oxygen_burn_rate = OxygenBurnRateBase - temp_scale
                
                if initial_oxygen > initial_plasma * PlasmaOxygenFullburn:
                    plasma_burn_rate = initial_plasma * temp_scale / PlasmaBurnRateDelta
                else:
                    plasma_burn_rate = temp_scale * (initial_oxygen / PlasmaOxygenFullburn) / PlasmaBurnRateDelta
                
                print(f"  Plasma reaction calc:")
                print(f"    Temp scale: {temp_scale:.6f}")
                print(f"    O2 burn rate: {oxygen_burn_rate:.6f}")
                print(f"    Plasma burn rate (before limit): {plasma_burn_rate:.6f}")
                
                if plasma_burn_rate > MinimumHeatCapacity:
                    limited = min(
                        plasma_burn_rate,
                        initial_plasma,
                        initial_oxygen / oxygen_burn_rate
                    )
                    print(f"    Plasma burn rate (after limit): {limited:.6f}")
                    print(f"    Limited by: plasma={initial_plasma:.6f}, o2={initial_oxygen/oxygen_burn_rate:.6f}")
                else:
                    print(f"    Plasma burn rate too small: {plasma_burn_rate:.6f} <= {MinimumHeatCapacity}")
        else:
            print(f"  Temperature too low: {initial_temp:.2f} < {PlasmaMinimumBurnTemperature:.2f}")
    else:
        print(f"  Not enough reactants: O2={initial_oxygen:.6f}, Plasma={initial_plasma:.6f}")
    
    # Run reactions
    run_reactions(mixture, max_iterations=100)
    
    final_plasma = mixture.get_moles(Gas.Plasma)
    final_tritium = mixture.get_moles(Gas.Tritium)
    final_oxygen = mixture.get_moles(Gas.Oxygen)
    final_temp = mixture.temperature
    final_pressure = mixture.pressure
    
    print(f"\nCycle {cycle_num} - After reactions:")
    print(f"  Plasma: {final_plasma:.6f} mol (delta: {final_plasma - initial_plasma:.6f})")
    print(f"  Tritium: {final_tritium:.6f} mol (delta: {final_tritium - initial_tritium:.6f})")
    print(f"  Oxygen: {final_oxygen:.6f} mol (delta: {final_oxygen - initial_oxygen:.6f})")
    print(f"  Temperature: {final_temp:.2f} K (delta: {final_temp - initial_temp:.2f})")
    print(f"  Pressure: {final_pressure:.2f} kPa (delta: {final_pressure - initial_pressure:.2f})")
    
    if abs(final_pressure - initial_pressure) < 0.01:
        print(f"  *** REACTIONS STOPPED ***")
        if final_oxygen < 0.01:
            print(f"    Reason: Oxygen too low ({final_oxygen:.6f} < 0.01)")
        if final_plasma < 0.01:
            print(f"    Reason: Plasma too low ({final_plasma:.6f} < 0.01)")
        if final_tritium < 0.01:
            print(f"    Reason: Tritium too low ({final_tritium:.6f} < 0.01)")

# Test the specific combination
print("=" * 70)
print("DEBUGGING REACTION STOPPAGE")
print("=" * 70)

# Recreate the mix from the test
canister = GasMixture(volume=TANK_VOLUME, temperature=382.7)
explosive_pressure = 800.0
total_moles = (explosive_pressure * TANK_VOLUME) / (R * 382.7)
plasma_moles = total_moles * 0.54
tritium_moles = total_moles * 0.46
canister.set_moles(Gas.Plasma, plasma_moles)
canister.set_moles(Gas.Tritium, tritium_moles)

# Calculate O2
remaining = 1013.0 - explosive_pressure
pressure_delta = min(remaining, explosive_pressure / 2.0)
o2_moles = pressure_delta * TANK_VOLUME / (R * 382.7)

# Mix
o2_mix = GasMixture(volume=TANK_VOLUME, temperature=293.15)
o2_mix.set_moles(Gas.Oxygen, o2_moles)

explosive_heat_cap = canister.get_heat_capacity()
o2_heat_cap = o2_mix.get_heat_capacity()
combined_heat_cap = explosive_heat_cap + o2_heat_cap
from maxcap_explosion_simulator import MinimumHeatCapacity as MinHC
if combined_heat_cap > MinHC:
    mixed_temp = (382.7 * explosive_heat_cap + 293.15 * o2_heat_cap) / combined_heat_cap
else:
    mixed_temp = (382.7 + 293.15) / 2.0

final_mix = GasMixture(volume=TANK_VOLUME, temperature=mixed_temp)
final_mix.set_moles(Gas.Plasma, canister.get_moles(Gas.Plasma))
final_mix.set_moles(Gas.Tritium, canister.get_moles(Gas.Tritium))
final_mix.set_moles(Gas.Oxygen, o2_moles)

print(f"\nInitial mix:")
print(f"  Plasma: {final_mix.get_moles(Gas.Plasma):.6f} mol")
print(f"  Tritium: {final_mix.get_moles(Gas.Tritium):.6f} mol")
print(f"  Oxygen: {final_mix.get_moles(Gas.Oxygen):.6f} mol")
print(f"  Temperature: {final_mix.temperature:.2f} K")
print(f"  Pressure: {final_mix.pressure:.2f} kPa")
print(f"\nTarget threshold: {TankFragmentPressure:.2f} kPa")
print()

# Run a few cycles with detailed debugging
for cycle in range(1, 8):
    debug_reaction_step(final_mix, cycle)
    if final_mix.pressure >= TankFragmentPressure:
        print(f"\n*** REACHED THRESHOLD at cycle {cycle} ***")
        break
    if abs(final_mix.pressure - final_mix.pressure) < 0.01:  # This won't work, but you get the idea
        pass
