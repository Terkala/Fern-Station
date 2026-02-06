#!/usr/bin/env python3
"""
Test pure tritium MaxCap explosion with optimal parameters from comprehensive search.
"""

import sys
import io
from maxcap_explosion_simulator import (
    GasMixture, Gas, simulate_maxcap_explosion,
    TANK_VOLUME, R
)

# Fix Unicode encoding for Windows
if sys.platform == 'win32':
    sys.stdout = io.TextIOWrapper(sys.stdout.buffer, encoding='utf-8', errors='replace')

def test_pure_tritium():
    """Test pure tritium with optimal parameters from comprehensive search"""
    
    # Best result from comprehensive search:
    # 17.06 tiles at 500.8K, 759.0 kPa
    optimal_temp = 500.8
    optimal_pressure = 759.0
    o2_temp = 293.15  # 20°C
    
    print("=" * 70)
    print("PURE TRITIUM MAXCAP EXPLOSION - OPTIMAL PARAMETERS")
    print("=" * 70)
    print()
    print(f"Explosive mix temperature: {optimal_temp:.2f} K ({optimal_temp - 273.15:.2f}°C)")
    print(f"Explosive mix pressure: {optimal_pressure:.2f} kPa")
    print(f"O2 temperature: {o2_temp:.2f} K (20°C)")
    print(f"Target total pressure: 1013.0 kPa")
    print()
    
    # Create pure tritium mixture
    explosive_mix = GasMixture(volume=TANK_VOLUME, temperature=optimal_temp)
    
    # Calculate moles needed for target pressure
    total_moles_needed = optimal_pressure * TANK_VOLUME / (R * optimal_temp)
    explosive_mix.set_moles(Gas.Tritium, total_moles_needed)
    
    print(f"Tritium moles: {total_moles_needed:.4f}")
    print(f"Actual pressure: {explosive_mix.pressure:.2f} kPa")
    print()
    
    # Run simulation
    final_pressure, explosion_range, stats = simulate_maxcap_explosion(
        canister_mix=explosive_mix,
        canister_temp=optimal_temp,
        target_explosive_pressure=optimal_pressure,
        target_total_pressure=1013.0,
        oxygen_temp=o2_temp,
        o2_mix_nitrogen_pct=0.0,
        canister_nitrogen_pct=0.0
    )
    
    print("=" * 70)
    print("SIMULATION RESULTS")
    print("=" * 70)
    print()
    print(f"Explosion Range: {explosion_range:.2f} tiles")
    print(f"  {'MAXIMUM CAP (26 tiles)' if explosion_range >= 25.99 else 'Below maximum cap'}")
    print()
    print(f"Initial Conditions (after mixing with O2):")
    print(f"  Pressure: {stats['initial_pressure']:.2f} kPa")
    print(f"  Temperature: {stats['initial_temp']:.2f} K ({stats['initial_temp'] - 273.15:.2f}°C)")
    print()
    print(f"Final Conditions (after reactions):")
    print(f"  Pressure: {stats['final_pressure']:.2f} kPa")
    print(f"  Temperature: {stats['final_temp']:.2f} K ({stats['final_temp'] - 273.15:.2f}°C)")
    print()
    print(f"Reaction Details:")
    print(f"  Cycles to threshold: {stats['cycles_to_threshold']}")
    print(f"  Burn time: {stats['burn_time_seconds']:.2f} seconds")
    print(f"  Reached threshold: {stats['reached_threshold']}")
    print(f"  Below ignition temp: {stats.get('below_ignition_temp', False)}")
    print()
    print(f"Gas Mixing Details:")
    print(f"  O2 moles added: {stats.get('o2_moles_to_add', 0):.4f}")
    print(f"  O2 pressure equivalent: {stats.get('o2_pressure_equivalent_kpa', 0):.2f} kPa")
    print()
    print(f"Composition:")
    print(f"  Tritium: 100.00%")
    print()
    
    # Show preparation instructions
    print("=" * 70)
    print("PREPARATION INSTRUCTIONS")
    print("=" * 70)
    print()
    print("1. Fill canister with pure Tritium")
    print(f"   - Target pressure: {optimal_pressure:.2f} kPa")
    print(f"   - Target temperature: {optimal_temp:.2f} K ({optimal_temp - 273.15:.2f}°C)")
    print()
    print("2. Prepare O2 canister")
    print(f"   - Temperature: {o2_temp:.2f} K (20°C)")
    print(f"   - Target pressure: 1013.0 kPa (after mixing)")
    print()
    print("3. Insert explosive canister into O2 canister and open valve")
    print()
    print(f"Expected result: {explosion_range:.2f} tile explosion")
    
    return {
        'explosion_range': explosion_range,
        'final_pressure': final_pressure,
        **stats
    }

if __name__ == "__main__":
    result = test_pure_tritium()
    sys.exit(0 if result['explosion_range'] > 0 else 1)
