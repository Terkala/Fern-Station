#!/usr/bin/env python3
"""
Test the specific case: 54% plasma, 46% tritium at 380.70K, 700kPa,
inserted into O2 canister at 20°C, opened to 1013kPa.
"""

import sys
import io

# Fix Unicode encoding for Windows
if sys.platform == 'win32':
    sys.stdout = io.TextIOWrapper(sys.stdout.buffer, encoding='utf-8', errors='replace')
from maxcap_explosion_simulator import (
    GasMixture, Gas, simulate_maxcap_explosion,
    TANK_VOLUME, R
)

# 20°C in Kelvin
T20C = 293.15

def test_specific_case():
    """Test the exact case the user specified"""
    
    # Create explosive mix: 54% plasma, 46% tritium at 382.7K (user's test case)
    explosive_mix = GasMixture(volume=TANK_VOLUME, temperature=382.7)
    
    # Target pressure: 700 kPa (will test different pressures)
    target_explosive_pressure = 700.0  # kPa
    
    # Calculate moles needed for 700 kPa at 380.70K
    total_moles_needed = target_explosive_pressure * TANK_VOLUME / (R * 380.70)
    
    # 54% plasma, 46% tritium
    plasma_moles = total_moles_needed * 0.54
    tritium_moles = total_moles_needed * 0.46
    
    explosive_mix.set_moles(Gas.Plasma, plasma_moles)
    explosive_mix.set_moles(Gas.Tritium, tritium_moles)
    
    print(f"=== Testing 54% Plasma / 46% Tritium at 382.7K ===")
    print(f"Target explosive pressure: {target_explosive_pressure:.2f} kPa")
    print(f"Explosive mix temperature: {explosive_mix.temperature:.2f} K")
    print(f"Plasma moles: {plasma_moles:.4f}")
    print(f"Tritium moles: {tritium_moles:.4f}")
    print(f"Total moles: {explosive_mix.total_moles:.4f}")
    print(f"Actual pressure: {explosive_mix.pressure:.2f} kPa")
    print()
    
    # O2 at 20°C (293.15 K)
    o2_temp = T20C
    
    # Target total pressure: 1013 kPa
    target_total_pressure = 1013.0  # kPa
    
    print(f"O2 temperature: {o2_temp:.2f} K (20°C)")
    print(f"Target total pressure: {target_total_pressure:.2f} kPa")
    print()
    
    # Run simulation
    final_pressure, explosion_range, stats = simulate_maxcap_explosion(
        canister_mix=explosive_mix,
        canister_temp=382.7,
        target_explosive_pressure=target_explosive_pressure,
        target_total_pressure=target_total_pressure,
        oxygen_temp=o2_temp,
        o2_mix_nitrogen_pct=0.0,
        canister_nitrogen_pct=0.0
    )
    
    print("=== Simulation Results ===")
    print(f"Final pressure: {final_pressure:.2f} kPa")
    print(f"Explosion range: {explosion_range:.2f} tiles")
    print(f"Initial pressure: {stats['initial_pressure']:.2f} kPa")
    print(f"Initial temperature: {stats['initial_temp']:.2f} K")
    print(f"Final temperature: {stats['final_temp']:.2f} K")
    print(f"Cycles to threshold: {stats['cycles_to_threshold']}")
    print(f"Burn time: {stats['burn_time_seconds']:.2f} seconds")
    print(f"Reached threshold: {stats['reached_threshold']}")
    print(f"Below ignition temp: {stats.get('below_ignition_temp', False)}")
    print()
    
    # Check if explosion occurred
    if explosion_range > 0:
        print("✓ EXPLOSION OCCURRED")
        if explosion_range >= 25.99:  # Account for floating point
            print("✓ MAXIMUM EXPLOSION RANGE (26 tiles)")
        else:
            print(f"  Explosion range: {explosion_range:.2f} tiles")
    else:
        print("✗ NO EXPLOSION")
        if stats.get('below_ignition_temp', False):
            print("  Reason: Mixed temperature below ignition threshold")
        elif not stats['reached_threshold']:
            print("  Reason: Did not reach explosion pressure threshold")
    
    print()
    
    # Detailed breakdown
    print("=== Detailed Breakdown ===")
    print(f"O2 moles added: {stats.get('o2_moles_to_add', 0):.4f}")
    print(f"O2 temperature: {stats.get('o2_temp_k', 0):.2f} K")
    print(f"Mixed temperature (after adding O2): {stats.get('initial_temp', 0):.2f} K")
    print(f"Mixed pressure (after adding O2): {stats.get('initial_pressure', 0):.2f} kPa")
    
    # Check composition
    print(f"\nExplosive mix composition:")
    print(f"  Plasma: {stats.get('plasma_percent', 0):.2f}%")
    print(f"  Tritium: {stats.get('tritium_percent', 0):.2f}%")
    
    return {'explosion_range': explosion_range, 'final_pressure': final_pressure, **stats}

if __name__ == "__main__":
    result = test_specific_case()
    sys.exit(0 if result['explosion_range'] > 0 else 1)
