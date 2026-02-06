#!/usr/bin/env python3
"""
SpaceStation 14 MaxCap Explosion Simulator

This script simulates the MaxCap explosion mechanic where a gas mixture is heated
and inserted into a pre-prepared oxygen tank at 1013 kPa, causing a chain reaction
that builds up pressure until it explodes at the maximum cap (26 tiles).

Based on the actual game code from:
- GasTankSystem.cs (explosion calculation)
- PlasmaFireReaction.cs and TritiumFireReaction.cs (combustion reactions)
- Atmospherics.cs (constants)
"""

import math
from dataclasses import dataclass
from typing import Dict, List, Tuple, Optional
from enum import IntEnum


# ============================================================================
# Constants from Atmospherics.cs
# ============================================================================

class Gas(IntEnum):
    """Gas IDs matching the game's Gas enum"""
    Oxygen = 0
    Nitrogen = 1
    CarbonDioxide = 2
    Plasma = 3
    Tritium = 4
    WaterVapor = 5
    Ammonia = 6
    NitrousOxide = 7
    Frezon = 8
    BZ = 9
    Healium = 10
    Nitrium = 11
    Pluoxium = 12
    Hydrogen = 13
    HyperNoblium = 14
    ProtoNitrate = 15
    Zauker = 16
    Halon = 17
    Helium = 18
    AntiNoblium = 19


# Gas constant in kPa*L/(K*mol)
R = 8.314462618
OneAtmosphere = 101.325

# Tank properties
TANK_VOLUME = 5.0  # liters
TankFragmentPressure = 50 * OneAtmosphere  # 5066.25 kPa
TankFragmentScale = 2 * OneAtmosphere  # 202.65 kPa
MaxExplosionRange = 26.0  # tiles

# Reaction constants
FirePlasmaEnergyReleased = 160e3  # kJ/mol
FireHydrogenEnergyReleased = 284e3  # kJ/mol
PlasmaMinimumBurnTemperature = 100 + 273.15  # 373.15 K
PlasmaUpperTemperature = 1370 + 273.15  # 1643.15 K
FireMinimumTemperatureToExist = 273.15 + 100  # 373.15 K
MinimumTritiumOxyburnEnergy = 143000
OxygenBurnRateBase = 1.4
PlasmaOxygenFullburn = 10.0
PlasmaBurnRateDelta = 9.0
SuperSaturationThreshold = 96.0
SuperSaturationEnds = SuperSaturationThreshold / 3  # 32.0
TritiumBurnOxyFactor = 100.0
TritiumBurnTritFactor = 10.0
TritiumBurnFuelRatio = 2.0
MinimumHydrogenOxyburnEnergy = 143000  # kJ/mol
HydrogenBurnOxyFactor = 100.0
HydrogenBurnH2Factor = 10.0
MinimumHeatCapacity = 0.0003
N2ODecompositionRate = 2.0  # From Atmospherics.cs
N2ODecompositionMinTemp = 850.0  # K - from reactions.yml

# Specific heats (J/(mol*K)) - from gases.yml
# NOTE: The game divides these by HeatScale (8.0) when initializing,
# so we store the raw values and divide by HEAT_SCALE in get_heat_capacity()
# to match the game's behavior exactly
GAS_SPECIFIC_HEATS_RAW = {
    Gas.Oxygen: 20,
    Gas.Nitrogen: 30,
    Gas.CarbonDioxide: 30,
    Gas.Plasma: 200,
    Gas.Tritium: 10,
    Gas.WaterVapor: 40,
    Gas.Ammonia: 20,
    Gas.NitrousOxide: 40,
    Gas.Frezon: 600,
    Gas.BZ: 20,
    Gas.Healium: 10,
    Gas.Nitrium: 10,
    Gas.Pluoxium: 80,
    Gas.Hydrogen: 15,
    Gas.HyperNoblium: 2000,
    Gas.ProtoNitrate: 30,
    Gas.Zauker: 350,
    Gas.Halon: 1.4,
    Gas.Helium: 15,
    Gas.AntiNoblium: 1,
}

# Heat capacity ratios (gamma = Cp/Cv) - from gases.yml
# These affect how much the mixture heats up from fire reactions
GAS_HEAT_CAPACITY_RATIOS = {
    Gas.Oxygen: 1.4,
    Gas.Nitrogen: 1.4,
    Gas.CarbonDioxide: 1.3,
    Gas.Plasma: 1.7,
    Gas.Tritium: 1.3,
    Gas.WaterVapor: 1.33,
    Gas.Ammonia: 1.4,
    Gas.NitrousOxide: 1.33,
    Gas.Frezon: 1.6,
    Gas.BZ: 1.4,
    Gas.Healium: 1.4,
    Gas.Nitrium: 1.4,
    Gas.Pluoxium: 1.33,
    Gas.Hydrogen: 1.4,
    Gas.HyperNoblium: 20.0,
    Gas.ProtoNitrate: 1.3,
    Gas.Zauker: 1.3,
    Gas.Halon: 1.3,
    Gas.Helium: 1.4,
    Gas.AntiNoblium: 1.0,
}

# Heat scale (from AtmosphereSystem.CVars - default is 8.0)
# The game divides SpecificHeat by HeatScale when initializing gas specific heats
# Reactions use GetHeatCapacity(mixture, true) which returns the scaled value
# Energy is divided by heatScale before being used in temperature calculations
HEAT_SCALE = 8.0

# Thermomachine temperature limits (from GasThermoMachineComponent)
THERMOMACHINE_MIN_TEMP = 73.15  # K (-200°C)
THERMOMACHINE_MAX_TEMP = 593.15  # K (320°C) - Maximum reachable by thermomachine


# ============================================================================
# Gas Mixture Class
# ============================================================================

@dataclass
class GasMixture:
    """Represents a gas mixture with moles of each gas, temperature, and volume"""
    moles: Dict[Gas, float]
    temperature: float  # Kelvin
    volume: float  # liters
    
    def __init__(self, volume: float = TANK_VOLUME, temperature: float = 293.15):
        self.moles = {gas: 0.0 for gas in Gas}
        self.temperature = temperature
        self.volume = volume
    
    def get_moles(self, gas: Gas) -> float:
        """Get moles of a specific gas"""
        return self.moles.get(gas, 0.0)
    
    def set_moles(self, gas: Gas, amount: float):
        """Set moles of a specific gas"""
        self.moles[gas] = max(0.0, amount)
    
    def adjust_moles(self, gas: Gas, delta: float):
        """Adjust moles of a specific gas"""
        self.moles[gas] = max(0.0, self.moles.get(gas, 0.0) + delta)
    
    @property
    def total_moles(self) -> float:
        """Total moles in the mixture"""
        return sum(self.moles.values())
    
    @property
    def pressure(self) -> float:
        """Calculate pressure using ideal gas law: P = nRT/V"""
        if self.volume <= 0:
            return 0.0
        return self.total_moles * R * self.temperature / self.volume
    
    def get_heat_capacity(self, apply_scaling: bool = True) -> float:
        """
        Calculate heat capacity: sum(moles[i] * specific_heat[i] / HeatScale)
        
        This mirrors the game's GetHeatCapacity function exactly:
        - The game divides SpecificHeat by HeatScale when initializing
        - When applyScaling=True (used by reactions), returns the scaled value
        - When applyScaling=False, multiplies by HeatScale to get un-scaled value
        
        Args:
            apply_scaling: If True, returns scaled heat capacity (as used by reactions).
                          If False, returns un-scaled heat capacity.
        """
        # Calculate heat capacity with HeatScale division (matching game's initialization)
        heat_cap = sum(
            self.moles.get(gas, 0.0) * (GAS_SPECIFIC_HEATS_RAW.get(gas, 0.0) / HEAT_SCALE)
            for gas in Gas
        )
        heat_cap = max(heat_cap, MinimumHeatCapacity)
        
        # If not applying scaling, multiply by HeatScale to get un-scaled value
        # (matching game's GetHeatCapacity(mixture, false))
        if not apply_scaling:
            heat_cap = heat_cap * HEAT_SCALE
        
        return heat_cap
    
    def get_effective_heat_capacity_ratio(self) -> float:
        """
        Calculate the effective heat capacity ratio (gamma) of the mixture.
        This is a mole-fraction weighted average of individual gas heat capacity ratios.
        Heat capacity ratio affects how much the mixture heats up from fire reactions.
        """
        total_moles = self.total_moles
        if total_moles <= 0:
            return 1.4  # Default value
        
        weighted_gamma = sum(
            (self.moles.get(gas, 0.0) / total_moles) * GAS_HEAT_CAPACITY_RATIOS.get(gas, 1.4)
            for gas in Gas
        )
        return weighted_gamma
    
    def copy(self) -> 'GasMixture':
        """Create a copy of this mixture"""
        new_mix = GasMixture(self.volume, self.temperature)
        new_mix.moles = self.moles.copy()
        return new_mix


# ============================================================================
# Reaction Functions
# ============================================================================

def plasma_fire_reaction(mixture: GasMixture) -> bool:
    """
    Simulates plasma fire reaction.
    Returns True if reaction occurred.
    """
    # Check for HyperNoblium suppression
    if mixture.temperature > 20.0 and mixture.get_moles(Gas.HyperNoblium) >= 5.0:
        return False
    
    if mixture.temperature < PlasmaMinimumBurnTemperature:
        return False
    
    initial_oxygen = mixture.get_moles(Gas.Oxygen)
    initial_plasma = mixture.get_moles(Gas.Plasma)
    
    # Check minimum requirements - use slightly lower threshold to account for floating point precision
    # The game checks if (!(mixture.GetMoles(i) < req)), so we need moles >= 0.01
    if initial_oxygen < 0.00999 or initial_plasma < 0.00999:
        return False
    
    # Calculate temperature scale
    if mixture.temperature > PlasmaUpperTemperature:
        temperature_scale = 1.0
    else:
        temperature_scale = (mixture.temperature - PlasmaMinimumBurnTemperature) / \
                           (PlasmaUpperTemperature - PlasmaMinimumBurnTemperature)
    
    if temperature_scale <= 0:
        return False
    
    old_heat_capacity = mixture.get_heat_capacity()
    temperature = mixture.temperature
    
    oxygen_burn_rate = OxygenBurnRateBase - temperature_scale
    
    # Calculate plasma burn rate
    if initial_oxygen > initial_plasma * PlasmaOxygenFullburn:
        plasma_burn_rate = initial_plasma * temperature_scale / PlasmaBurnRateDelta
    else:
        plasma_burn_rate = temperature_scale * (initial_oxygen / PlasmaOxygenFullburn) / PlasmaBurnRateDelta
    
    # Check if burn rate is too small - but allow very small burns to continue
    # The game checks if (plasmaBurnRate > MinimumHeatCapacity), so we should allow
    # burns that are greater than MinimumHeatCapacity, even if very small
    if plasma_burn_rate <= MinimumHeatCapacity:
        return False
    
    # Limit burn rate by available reactants
    plasma_burn_rate = min(
        plasma_burn_rate,
        initial_plasma,
        initial_oxygen / oxygen_burn_rate
    )
    
    # Supersaturation calculation (produces tritium)
    oxy_ratio = initial_oxygen / initial_plasma if initial_plasma > 0 else 0
    supersaturation = max(0.0, min(1.0,
        (oxy_ratio - SuperSaturationEnds) / (SuperSaturationThreshold - SuperSaturationEnds)
    ))
    
    # Apply reaction
    mixture.set_moles(Gas.Plasma, initial_plasma - plasma_burn_rate)
    mixture.set_moles(Gas.Oxygen, initial_oxygen - plasma_burn_rate * oxygen_burn_rate)
    mixture.adjust_moles(Gas.Tritium, plasma_burn_rate * supersaturation)
    mixture.adjust_moles(Gas.CarbonDioxide, plasma_burn_rate * (1.0 - supersaturation))
    
    # Calculate energy released
    energy_released = FirePlasmaEnergyReleased * plasma_burn_rate / HEAT_SCALE
    
    # Update temperature
    if energy_released > 0:
        new_heat_capacity = mixture.get_heat_capacity()
        if new_heat_capacity > MinimumHeatCapacity:
            mixture.temperature = (temperature * old_heat_capacity + energy_released) / new_heat_capacity
    
    return True


def hydrogen_fire_reaction(mixture: GasMixture) -> bool:
    """
    Simulates hydrogen fire reaction.
    Returns True if reaction occurred.
    """
    # Check for HyperNoblium suppression
    if mixture.temperature > 20.0 and mixture.get_moles(Gas.HyperNoblium) >= 5.0:
        return False
    
    initial_h2 = mixture.get_moles(Gas.Hydrogen)
    initial_oxygen = mixture.get_moles(Gas.Oxygen)
    
    # Check minimum requirements - use slightly lower threshold to account for floating point precision
    if initial_h2 < 0.00999 or initial_oxygen < 0.00999:
        return False
    
    old_heat_capacity = mixture.get_heat_capacity()
    temperature = mixture.temperature
    energy_released = 0.0
    
    # Check if we're in the low-energy burn mode
    if initial_oxygen < initial_h2 or \
       MinimumHydrogenOxyburnEnergy > (temperature * old_heat_capacity * HEAT_SCALE):
        # Low-energy burn
        burned_fuel = initial_oxygen / HydrogenBurnOxyFactor
        if burned_fuel > initial_h2:
            burned_fuel = initial_h2
    else:
        # High-energy burn
        burned_fuel = min(initial_h2, initial_oxygen / TritiumBurnFuelRatio) / HydrogenBurnH2Factor
    
    if burned_fuel <= 0:
        return False
    
    oxygen_consumed = burned_fuel / TritiumBurnFuelRatio
    if initial_h2 - burned_fuel < 0 or initial_oxygen - oxygen_consumed < 0:
        return False
    
    mixture.adjust_moles(Gas.Hydrogen, -burned_fuel)
    mixture.adjust_moles(Gas.Oxygen, -oxygen_consumed)
    mixture.adjust_moles(Gas.WaterVapor, burned_fuel)
    
    energy_released = FireHydrogenEnergyReleased * burned_fuel / HEAT_SCALE
    
    # Update temperature
    if energy_released > 0:
        new_heat_capacity = mixture.get_heat_capacity()
        if new_heat_capacity > MinimumHeatCapacity:
            mixture.temperature = (temperature * old_heat_capacity + energy_released) / new_heat_capacity
    
    return True


def tritium_fire_reaction(mixture: GasMixture) -> bool:
    """
    Simulates tritium fire reaction.
    Returns True if reaction occurred.
    """
    # Check for HyperNoblium suppression
    if mixture.temperature > 20.0 and mixture.get_moles(Gas.HyperNoblium) >= 5.0:
        return False
    
    initial_trit = mixture.get_moles(Gas.Tritium)
    initial_oxygen = mixture.get_moles(Gas.Oxygen)
    
    # Check minimum requirements - use slightly lower threshold to account for floating point precision
    if initial_trit < 0.00999 or initial_oxygen < 0.00999:
        return False
    
    old_heat_capacity = mixture.get_heat_capacity()
    temperature = mixture.temperature
    energy_released = 0.0
    
    # Check if we're in the low-energy burn mode
    if initial_oxygen < initial_trit or \
       MinimumTritiumOxyburnEnergy > (temperature * old_heat_capacity * HEAT_SCALE):
        # Low-energy burn
        burned_fuel = initial_oxygen / TritiumBurnOxyFactor
        if burned_fuel > initial_trit:
            burned_fuel = initial_trit
    else:
        # High-energy burn
        burned_fuel = min(initial_trit, initial_oxygen / TritiumBurnFuelRatio) / TritiumBurnTritFactor
        energy_released += FireHydrogenEnergyReleased * burned_fuel * (TritiumBurnTritFactor - 1)
    
    if burned_fuel > 0:
        mixture.adjust_moles(Gas.Tritium, -burned_fuel)
        mixture.adjust_moles(Gas.Oxygen, -burned_fuel / TritiumBurnFuelRatio)
        mixture.adjust_moles(Gas.WaterVapor, burned_fuel)
        
        energy_released += FireHydrogenEnergyReleased * burned_fuel
        energy_released /= HEAT_SCALE
        
        # Update temperature
        if energy_released > 0:
            new_heat_capacity = mixture.get_heat_capacity()
            if new_heat_capacity > MinimumHeatCapacity:
                mixture.temperature = (temperature * old_heat_capacity + energy_released) / new_heat_capacity
        
        return True
    
    return False


def n2o_decomposition_reaction(mixture: GasMixture) -> bool:
    """
    Simulates N2O decomposition reaction.
    Decomposes Nitrous Oxide into Nitrogen and Oxygen at high temperatures.
    Returns True if reaction occurred.
    """
    # Check for HyperNoblium suppression
    if mixture.temperature > 20.0 and mixture.get_moles(Gas.HyperNoblium) >= 5.0:
        return False
    
    # Check minimum temperature requirement (850 K)
    if mixture.temperature < N2ODecompositionMinTemp:
        return False
    
    cache_n2o = mixture.get_moles(Gas.NitrousOxide)
    
    if cache_n2o <= 0:
        return False
    
    # Decompose 50% of N2O per reaction cycle
    burned_fuel = cache_n2o / N2ODecompositionRate
    
    if burned_fuel <= 0 or cache_n2o - burned_fuel < 0:
        return False
    
    # N2O -> N2 + 0.5*O2
    mixture.adjust_moles(Gas.NitrousOxide, -burned_fuel)
    mixture.adjust_moles(Gas.Nitrogen, burned_fuel)
    mixture.adjust_moles(Gas.Oxygen, burned_fuel / 2.0)
    
    return True


def run_reaction_cycle(mixture: GasMixture) -> bool:
    """
    Run one reaction cycle (one call to React() in the game).
    This processes all reactions once and returns True if any reaction occurred.
    Matches AtmosphereSystem.React() behavior.
    """
    # N2O decomposition happens first (priority 0, before fire reactions which are -2, -1)
    n2o_reacted = n2o_decomposition_reaction(mixture)
    plasma_reacted = plasma_fire_reaction(mixture)
    tritium_reacted = tritium_fire_reaction(mixture)
    hydrogen_reacted = hydrogen_fire_reaction(mixture)
    
    return n2o_reacted or plasma_reacted or tritium_reacted or hydrogen_reacted


def run_reactions(mixture: GasMixture, max_iterations: int = 100) -> int:
    """
    Run gas reactions until they stop or max iterations reached.
    This simulates multiple calls to React() until reactions complete.
    Returns number of iterations performed.
    """
    for i in range(max_iterations):
        if not run_reaction_cycle(mixture):
            break
    
    return i + 1


# ============================================================================
# Explosion Calculation
# ============================================================================

def calculate_explosion_range(pressure: float) -> float:
    """
    Calculate explosion range from pressure.
    Matches GasTankSystem.CheckStatus calculation.
    """
    if pressure <= TankFragmentPressure:
        return 0.0
    
    range_val = math.sqrt((pressure - TankFragmentPressure) / TankFragmentScale)
    return min(range_val, MaxExplosionRange)


def simulate_maxcap_explosion(
    canister_mix: GasMixture,
    canister_temp: float,
    target_explosive_pressure: Optional[float] = None,  # Target pressure for explosive mix (if None, use current)
    target_total_pressure: float = 1013.0,  # Target total pressure after adding O2
    oxygen_temp: float = 293.15,  # O2 temperature (20°C)
    canister_nitrogen_pct: float = 0.0,  # Percentage of canister mix that is nitrogen (0-100)
    o2_mix_nitrogen_pct: float = 0.0  # Percentage of O2 mix that is nitrogen (0-100)
) -> Tuple[float, float, Dict]:
    """
    Simulate the MaxCap explosion process with space constraints.
    
    Process:
    1. Heat the canister mixture to canister_temp
    2. If target_explosive_pressure is specified, scale gas amounts to reach that pressure
    3. Calculate how much O2 can be added at oxygen_temp to reach target_total_pressure
    4. Merge O2 into canister (mixing gases and temperatures)
    5. Run 3 reaction cycles to build pressure (as per game code)
    6. Calculate final explosion range
    
    Args:
        canister_mix: Gas mixture with relative amounts (will be scaled to target pressure)
        canister_temp: Temperature to heat explosive mix to
        target_explosive_pressure: Target pressure for explosive mix (kPa). If None, uses current pressure.
        target_total_pressure: Target total pressure after adding O2 (kPa, default 1013)
        oxygen_temp: Temperature of O2 being added (K, default 293.15 = 20°C)
    
    Returns: (final_pressure, explosion_range, stats_dict)
    """
    # Step 1: Heat the canister mixture
    canister_mix.temperature = canister_temp
    
    # Step 2: Scale to target explosive pressure if specified
    if target_explosive_pressure is not None:
        current_pressure = canister_mix.pressure
        if current_pressure > 0:
            # Scale all gas amounts proportionally
            scale_factor = target_explosive_pressure / current_pressure
            for gas in Gas:
                current_moles = canister_mix.get_moles(gas)
                canister_mix.set_moles(gas, current_moles * scale_factor)
        else:
            # If no pressure, we can't scale - this shouldn't happen
            pass
    
    # Calculate explosive mix pressure
    explosive_pressure = canister_mix.pressure
    explosive_temp = canister_temp
    
    # Step 3: Calculate how much O2 to add
    # We need to account for the actual gas composition for accurate heat capacity
    explosive_heat_cap = canister_mix.get_heat_capacity()
    explosive_moles = canister_mix.total_moles
    
    # Calculate remaining pressure capacity
    remaining_pressure_capacity = target_total_pressure - explosive_pressure
    
    if remaining_pressure_capacity <= 0:
        # Canister is already at or above target pressure
        o2_moles = 0.0
        nitrogen_moles = 0.0
    else:
        # Calculate O2 moles needed
        # IMPORTANT: The game's ReleaseGasTo uses the SOURCE (canister) temperature to calculate moles,
        # not the target temperature. See: transferMoles = pressureDelta * output.Volume / (mixture.Temperature * R)
        # where mixture.Temperature is the canister temperature.
        
        # Game also limits pressure delta to half the difference: min(target - output, (input - output) / 2)
        output_starting_pressure = 0.0  # Tank starts empty in our case
        pressure_delta = min(remaining_pressure_capacity, (explosive_pressure - output_starting_pressure) / 2.0)
        
        # Calculate moles using CANISTER temperature (as per game code)
        o2_moles_approx = pressure_delta * TANK_VOLUME / (R * explosive_temp)
        
        # Refine by accounting for temperature mixing after Merge
        # This is critical: when gases of different temperatures mix, the heat capacity
        # of each gas affects how the temperature mixes, which affects the final pressure.
        # We need to iteratively refine to account for this non-linear relationship.
        for iteration in range(20):  # Increased iterations for better convergence
            # Calculate nitrogen if specified
            if o2_mix_nitrogen_pct > 0:
                nitrogen_moles_approx = o2_moles_approx * (o2_mix_nitrogen_pct / 100.0) / (1.0 - o2_mix_nitrogen_pct / 100.0)
            else:
                nitrogen_moles_approx = 0.0
            
            # Create temporary O2 mix to calculate heat capacity
            temp_o2_mix = GasMixture(volume=TANK_VOLUME, temperature=oxygen_temp)
            temp_o2_mix.set_moles(Gas.Oxygen, o2_moles_approx)
            if nitrogen_moles_approx > 0:
                temp_o2_mix.set_moles(Gas.Nitrogen, nitrogen_moles_approx)
            
            o2_heat_cap = temp_o2_mix.get_heat_capacity()
            combined_heat_cap = explosive_heat_cap + o2_heat_cap
            
            # Calculate mixed temperature (as per Merge function in game)
            # This accounts for heat capacity ratios - gases with higher heat capacity
            # have more "thermal inertia" and affect the final temperature more
            if combined_heat_cap > MinimumHeatCapacity:
                mixed_temp = (
                    explosive_temp * explosive_heat_cap +
                    oxygen_temp * o2_heat_cap
                ) / combined_heat_cap
            else:
                mixed_temp = (explosive_temp + oxygen_temp) / 2.0
            
            # Calculate actual pressure after mixing
            total_moles = explosive_moles + o2_moles_approx + nitrogen_moles_approx
            actual_pressure = total_moles * R * mixed_temp / TANK_VOLUME
            
            pressure_diff = target_total_pressure - actual_pressure
            if abs(pressure_diff) < 0.01:
                break
            
            # Adjust O2 moles - but use explosive_temp since that's what the game uses for ReleaseGasTo
            # However, we need to account for the fact that adding more O2 changes the mixed temperature,
            # which changes pressure non-linearly. Use a damped adjustment for stability.
            o2_adjustment = pressure_diff * TANK_VOLUME / (R * explosive_temp)
            # Damp the adjustment to avoid oscillation
            damping_factor = 0.7 if iteration > 5 else 1.0
            o2_moles_approx += o2_adjustment * damping_factor
            o2_moles_approx = max(0.0, o2_moles_approx)
        
        o2_moles = o2_moles_approx
        
        # Calculate final nitrogen moles
        if o2_mix_nitrogen_pct > 0:
            nitrogen_moles = o2_moles * (o2_mix_nitrogen_pct / 100.0) / (1.0 - o2_mix_nitrogen_pct / 100.0)
        else:
            nitrogen_moles = 0.0
    
    # Step 4: Create O2 mixture and merge into canister
    o2_mix = GasMixture(volume=TANK_VOLUME, temperature=oxygen_temp)
    o2_mix.set_moles(Gas.Oxygen, o2_moles)
    if nitrogen_moles > 0:
        o2_mix.set_moles(Gas.Nitrogen, nitrogen_moles)
    
    # Mix temperatures based on heat capacity
    o2_heat_cap = o2_mix.get_heat_capacity()
    combined_heat_cap = explosive_heat_cap + o2_heat_cap
    
    if combined_heat_cap > MinimumHeatCapacity:
        mixed_temp = (
            explosive_temp * explosive_heat_cap +
            oxygen_temp * o2_heat_cap
        ) / combined_heat_cap
    else:
        mixed_temp = (explosive_temp + oxygen_temp) / 2.0
    
    # Add all moles together
    final_mix = GasMixture(volume=TANK_VOLUME, temperature=mixed_temp)
    for gas in Gas:
        final_mix.set_moles(gas, 
            canister_mix.get_moles(gas) + o2_mix.get_moles(gas))
    
    initial_pressure = final_mix.pressure
    initial_temp = final_mix.temperature
    
    # Check if mixed temperature is above ignition threshold for flammable gases
    # From reactions.yml: minimumTemperature: 373.149 K for both PlasmaFire and TritiumFire
    ignition_threshold = 373.149  # K
    has_plasma = final_mix.get_moles(Gas.Plasma) > 0.01
    has_tritium = final_mix.get_moles(Gas.Tritium) > 0.01
    has_hydrogen = final_mix.get_moles(Gas.Hydrogen) > 0.01
    
    # Calculate O2 mix pressure equivalent for stats
    o2_mix_total_moles = o2_moles + nitrogen_moles
    o2_pressure_equivalent = o2_mix_total_moles * R * oxygen_temp / TANK_VOLUME if o2_moles > 0 else 0.0
    
    if (has_plasma or has_tritium or has_hydrogen) and initial_temp < ignition_threshold:
        # Mixed temperature is below ignition threshold - reactions won't start
        # Return a failure result
        plasma_moles = canister_mix.get_moles(Gas.Plasma)
        tritium_moles = canister_mix.get_moles(Gas.Tritium)
        total_fuel = plasma_moles + tritium_moles
        
        if total_fuel > 0:
            plasma_percent = (plasma_moles / total_fuel) * 100.0
            tritium_percent = (tritium_moles / total_fuel) * 100.0
        else:
            plasma_percent = 0.0
            tritium_percent = 0.0
        
        stats = {
            'explosive_pressure_kpa': explosive_pressure,
            'explosive_temp_k': explosive_temp,
            'plasma_percent': plasma_percent,
            'tritium_percent': tritium_percent,
            'canister_nitrogen_pct': canister_nitrogen_pct,
            'o2_mix_nitrogen_pct': o2_mix_nitrogen_pct,
            'o2_temp_k': oxygen_temp,
            'o2_moles_to_add': o2_moles,
            'nitrogen_moles_in_o2_mix': nitrogen_moles,
            'o2_pressure_equivalent_kpa': o2_pressure_equivalent,
            'target_total_pressure_kpa': target_total_pressure,
            'initial_pressure': initial_pressure,
            'final_pressure': initial_pressure,  # No reactions occurred
            'initial_temp': initial_temp,
            'final_temp': initial_temp,
            'pressure_increase': 0.0,
            'explosion_range': 0.0,
            'hits_max_cap': False,
            'below_ignition_temp': True,  # Flag to indicate this combination is invalid
            'reached_threshold': False,  # Cannot reach threshold if below ignition
            'mixed_temp': initial_temp,
            'ignition_threshold': ignition_threshold,
            'cycles_to_threshold': 0,
            'burn_time_seconds': 0.0,
        }
        return initial_pressure, 0.0, stats
    
    # Step 5: Run reactions until threshold is reached (or 3 cycles if already above)
    # Calculate how many cycles it takes to reach explosion threshold
    # IMPORTANT: In the game:
    # - Reactions run every AtmosTime = 1/15 = 0.0667 seconds (AtmosTickRate = 15 TPS)
    # - CheckStatus checks every TimerDelay = 0.5 seconds
    # - So between each check, reactions have run ~7-8 times
    # - We need to simulate continuous reactions, checking pressure every 0.5 seconds
    
    AtmosTickRate = 15.0  # TPS (from CCVars.AtmosTickRate)
    AtmosTime = 1.0 / AtmosTickRate  # ~0.0667 seconds per reaction cycle
    TimerDelay = 0.5  # seconds between CheckStatus calls (from GasTankSystem.cs)
    
    cycles_to_threshold = 0
    burn_time_seconds = 0.0
    
    if initial_pressure < TankFragmentPressure:
        # Need to run reactions until threshold is reached
        # Simulate continuous reactions: run reactions every AtmosTime, check pressure every TimerDelay
        test_mix = GasMixture(volume=TANK_VOLUME, temperature=initial_temp)
        for gas in Gas:
            test_mix.set_moles(gas, final_mix.get_moles(gas))
        
        max_check_cycles = 1000  # Safety limit for check cycles
        reached_threshold = False
        time_elapsed = 0.0
        
        # Simulate continuous reactions
        # Each "check cycle" represents 0.5 seconds, during which reactions run continuously
        while test_mix.pressure < TankFragmentPressure and cycles_to_threshold < max_check_cycles:
            cycles_to_threshold += 1
            time_elapsed += TimerDelay
            
            # During this 0.5-second period, reactions run continuously
            # Reactions run every AtmosTime, so we have TimerDelay/AtmosTime reaction cycles
            reactions_per_check = int(TimerDelay / AtmosTime)  # ~7-8 reactions per 0.5s check
            
            # Run reactions continuously during this check period
            # Each atmos update calls React() once, which processes all reactions once
            for reaction_cycle in range(reactions_per_check):
                # Run one reaction cycle (one call to React() in the game)
                run_reaction_cycle(test_mix)
                
                # Check if we've reached threshold (can happen mid-check)
                if test_mix.pressure >= TankFragmentPressure:
                    reached_threshold = True
                    # Calculate exact time within this check cycle
                    reactions_run = reaction_cycle + 1
                    time_in_cycle = reactions_run * AtmosTime
                    burn_time_seconds = (cycles_to_threshold - 1) * TimerDelay + time_in_cycle
                    break
            
            if reached_threshold:
                break
            
            if cycles_to_threshold >= max_check_cycles:
                break
        
        if not reached_threshold:
            burn_time_seconds = cycles_to_threshold * TimerDelay
        
        # Now run the actual reactions on final_mix to get the final explosion result
        # We need to run the same number of reaction cycles that test_mix ran
        reactions_per_check = int(TimerDelay / AtmosTime)  # ~7-8 reactions per 0.5s check
        
        if not reached_threshold:
            # Still run some reactions to get final state, but it won't explode
            # Run the same number of cycles we tested
            total_reaction_cycles = cycles_to_threshold * reactions_per_check
            for cycle in range(min(total_reaction_cycles, 100)):
                run_reaction_cycle(final_mix)
        else:
            # Run the same number of cycles that brought test_mix to threshold
            # Then add 3 more cycles as per game code (CheckStatus runs 3 cycles when threshold is reached)
            total_reaction_cycles = cycles_to_threshold * reactions_per_check
            for cycle in range(total_reaction_cycles + 3):
                run_reaction_cycle(final_mix)
    else:
        # Already above threshold, run 3 cycles as per game code
        for cycle in range(3):
            run_reaction_cycle(final_mix)
        cycles_to_threshold = 0  # Explodes immediately
        burn_time_seconds = 0.0
    
    final_pressure = final_mix.pressure
    final_temp = final_mix.temperature
    
    # Step 6: Calculate explosion range
    explosion_range = calculate_explosion_range(final_pressure)
    
    # Calculate plasma/tritium ratio
    plasma_moles = canister_mix.get_moles(Gas.Plasma)
    tritium_moles = canister_mix.get_moles(Gas.Tritium)
    total_fuel = plasma_moles + tritium_moles
    
    if total_fuel > 0:
        plasma_percent = (plasma_moles / total_fuel) * 100.0
        tritium_percent = (tritium_moles / total_fuel) * 100.0
    else:
        plasma_percent = 0.0
        tritium_percent = 0.0
    
    # Calculate O2 mix pressure equivalent (what pressure the O2 mix would be at if alone)
    o2_mix_total_moles = o2_moles + nitrogen_moles
    o2_pressure_equivalent = o2_mix_total_moles * R * oxygen_temp / TANK_VOLUME
    
    stats = {
        'explosive_pressure_kpa': explosive_pressure,  # kPa of explosive tank before adding O2
        'explosive_temp_k': explosive_temp,  # Temperature of explosive tank before adding O2
        'plasma_percent': plasma_percent,  # Percentage of plasma in fuel mix
        'tritium_percent': tritium_percent,  # Percentage of tritium in fuel mix
        'canister_nitrogen_pct': canister_nitrogen_pct,  # Percentage of nitrogen in canister mix
        'o2_mix_nitrogen_pct': o2_mix_nitrogen_pct,  # Percentage of nitrogen in O2 mix
        'o2_temp_k': oxygen_temp,  # Temperature of O2 mix
        'o2_moles_to_add': o2_moles,  # Moles of O2 to add
        'nitrogen_moles_in_o2_mix': nitrogen_moles,  # Moles of nitrogen in O2 mix
        'o2_pressure_equivalent_kpa': o2_pressure_equivalent,  # Pressure equivalent of O2 mix
        'target_total_pressure_kpa': target_total_pressure,  # Target total pressure (1013 kPa)
        'initial_pressure': initial_pressure,  # Pressure after mixing but before reactions
        'final_pressure': final_pressure,  # Final pressure after reactions
        'initial_temp': initial_temp,  # Temperature after mixing but before reactions
        'final_temp': final_temp,  # Final temperature after reactions
        'pressure_increase': final_pressure - initial_pressure,
        'explosion_range': explosion_range,
        'hits_max_cap': explosion_range >= MaxExplosionRange - 0.01,
        'cycles_to_threshold': cycles_to_threshold,
        'burn_time_seconds': burn_time_seconds,
        'reached_threshold': reached_threshold if initial_pressure < TankFragmentPressure else True,
    }
    
    return final_pressure, explosion_range, stats


# ============================================================================
# Search Functions
# ============================================================================

def search_plasma_tritium_combinations(
    min_plasma: float = 0.1,
    max_plasma: float = 10.0,
    min_tritium: float = 0.1,
    max_tritium: float = 10.0,
    plasma_step: float = 0.1,
    tritium_step: float = 0.1,
    min_temp: float = 373.15,
    max_temp: float = THERMOMACHINE_MAX_TEMP,  # Limited by thermomachine max
    temp_step: float = 10.0,
    oxygen_tank_pressure: float = 1013.0
) -> List[Dict]:
    """
    Search through plasma/tritium combinations to find MaxCap explosions.
    
    Returns list of successful combinations sorted by explosion range.
    """
    results = []
    
    plasma_range = int((max_plasma - min_plasma) / plasma_step) + 1
    tritium_range = int((max_tritium - min_tritium) / tritium_step) + 1
    temp_range = int((max_temp - min_temp) / temp_step) + 1
    
    total_combinations = plasma_range * tritium_range * temp_range
    print(f"Searching {total_combinations} combinations...")
    print(f"Plasma: {min_plasma}-{max_plasma} (step {plasma_step})")
    print(f"Tritium: {min_tritium}-{max_tritium} (step {tritium_step})")
    print(f"Temperature: {min_temp}-{max_temp} K (step {temp_step})")
    print()
    
    count = 0
    for plasma_moles in [min_plasma + i * plasma_step for i in range(plasma_range)]:
        for tritium_moles in [min_tritium + i * tritium_step for i in range(tritium_range)]:
            for temp in [min_temp + i * temp_step for i in range(temp_range)]:
                count += 1
                if count % 1000 == 0:
                    print(f"Progress: {count}/{total_combinations} ({100*count/total_combinations:.1f}%)")
                
                # Create canister mixture (relative amounts, will be scaled to target pressure)
                canister = GasMixture(volume=TANK_VOLUME, temperature=temp)
                canister.set_moles(Gas.Plasma, plasma_moles)
                canister.set_moles(Gas.Tritium, tritium_moles)
                # Don't add oxygen here - it will be added separately to fill remaining space
                
                # Try different explosive pressures (e.g., 400-900 kPa in 100 kPa steps)
                for explosive_pressure in [400.0, 500.0, 600.0, 700.0, 800.0, 900.0]:
                    if explosive_pressure >= oxygen_tank_pressure:
                        continue
                    
                    try:
                        # Create a fresh canister for each pressure
                        test_canister = GasMixture(volume=TANK_VOLUME, temperature=temp)
                        test_canister.set_moles(Gas.Plasma, plasma_moles)
                        test_canister.set_moles(Gas.Tritium, tritium_moles)
                        
                        final_pressure, explosion_range, stats = simulate_maxcap_explosion(
                            test_canister, temp, 
                            target_explosive_pressure=explosive_pressure,
                            target_total_pressure=oxygen_tank_pressure
                        )
                    
                        if stats['hits_max_cap']:
                            results.append({
                                'plasma_moles': plasma_moles,
                                'tritium_moles': tritium_moles,
                                'temperature': temp,
                                'explosion_range': explosion_range,
                                'final_pressure': final_pressure,
                                **stats
                            })
                            # Found a working combination, no need to try other pressures
                            break
                    except (ValueError, ZeroDivisionError) as e:
                        # Skip invalid combinations
                        continue
    
    # Sort by explosion range (descending)
    results.sort(key=lambda x: x['explosion_range'], reverse=True)
    
    return results


def search_all_gas_combinations(
    gas_list: List[Gas],
    min_moles: float = 0.1,
    max_moles: float = 10.0,
    mole_step: float = 0.5,
    min_temp: float = 373.15,
    max_temp: float = THERMOMACHINE_MAX_TEMP,  # Limited by thermomachine max
    temp_step: float = 20.0,
    oxygen_tank_pressure: float = 1013.0,
    target_range: Optional[float] = None
) -> List[Dict]:
    """
    Search through combinations of multiple gases.
    
    Args:
        gas_list: List of gases to vary (others will be set to 0)
        min_moles, max_moles, mole_step: Range for mole amounts
        min_temp, max_temp, temp_step: Range for temperatures
        oxygen_tank_pressure: Pressure of the oxygen tank
        target_range: If specified, only return results with explosion_range >= target_range
    
    Returns list of successful combinations.
    """
    results = []
    
    print(f"Searching combinations of: {[g.name for g in gas_list]}")
    print(f"Moles: {min_moles}-{max_moles} (step {mole_step})")
    print(f"Temperature: {min_temp}-{max_temp} K (step {temp_step})")
    if target_range:
        print(f"Target explosion range: >= {target_range} tiles")
    print()
    
    # For 2 gases, do a grid search
    if len(gas_list) == 2:
        gas1, gas2 = gas_list
        mole_range1 = int((max_moles - min_moles) / mole_step) + 1
        mole_range2 = int((max_moles - min_moles) / mole_step) + 1
        temp_range = int((max_temp - min_temp) / temp_step) + 1
        
        total = mole_range1 * mole_range2 * temp_range
        print(f"Total combinations to test: {total}")
        print()
        
        count = 0
        for temp in [min_temp + i * temp_step for i in range(temp_range)]:
            for moles1 in [min_moles + i * mole_step for i in range(mole_range1)]:
                for moles2 in [min_moles + i * mole_step for i in range(mole_range2)]:
                    count += 1
                    if count % 100 == 0:
                        print(f"Progress: {count}/{total} ({100*count/total:.1f}%)")
                    
                    canister = GasMixture(volume=TANK_VOLUME, temperature=temp)
                    canister.set_moles(gas1, moles1)
                    canister.set_moles(gas2, moles2)
                    # Add oxygen for reactions
                    canister.set_moles(Gas.Oxygen, (moles1 + moles2) * 2)
                    
                    try:
                        final_pressure, explosion_range, stats = simulate_maxcap_explosion(
                            canister, temp, target_total_pressure=oxygen_tank_pressure
                        )
                        
                        if target_range is None or explosion_range >= target_range:
                            result = {
                                'temperature': temp,
                                'explosion_range': explosion_range,
                                'final_pressure': final_pressure,
                                **stats
                            }
                            result[f'{gas1.name.lower()}_moles'] = moles1
                            result[f'{gas2.name.lower()}_moles'] = moles2
                            results.append(result)
                    except (ValueError, ZeroDivisionError):
                        continue
    else:
        # For more gases, use a simpler approach
        mole_range = int((max_moles - min_moles) / mole_step) + 1
        temp_range = int((max_temp - min_temp) / temp_step) + 1
        
        total = mole_range * temp_range
        print(f"Total combinations to test: {total}")
        print()
        
        count = 0
        for temp in [min_temp + i * temp_step for i in range(temp_range)]:
            for base_moles in [min_moles + i * mole_step for i in range(mole_range)]:
                count += 1
                if count % 100 == 0:
                    print(f"Progress: {count}/{total} ({100*count/total:.1f}%)")
                
                canister = GasMixture(volume=TANK_VOLUME, temperature=temp)
                for gas in gas_list:
                    canister.set_moles(gas, base_moles)
                
                # Add oxygen for reactions
                canister.set_moles(Gas.Oxygen, base_moles * len(gas_list) * 2)
                
                try:
                    final_pressure, explosion_range, stats = simulate_maxcap_explosion(
                        canister, temp, target_total_pressure=oxygen_tank_pressure
                    )
                    
                    if target_range is None or explosion_range >= target_range:
                        result = {
                            'temperature': temp,
                            'explosion_range': explosion_range,
                            'final_pressure': final_pressure,
                            **stats
                        }
                        for gas in gas_list:
                            result[f'{gas.name.lower()}_moles'] = base_moles
                        results.append(result)
                except (ValueError, ZeroDivisionError):
                    continue
    
    results.sort(key=lambda x: x['explosion_range'], reverse=True)
    return results


# ============================================================================
# Evolutionary Search Function
# ============================================================================

def evolutionary_search(
    initial_candidates: int = 100,
    top_n: int = 10,
    variations_per_candidate: int = 10,
    max_generations: int = 20,
    improvement_threshold: float = 0.05,  # 5% improvement threshold
    min_plasma_pct: float = 30.0,
    max_plasma_pct: float = 70.0,
    min_temp: float = 373.15,
    max_temp: float = THERMOMACHINE_MAX_TEMP,
    min_pressure: float = 400.0,
    max_pressure: float = 900.0,
    min_o2_temp: float = 293.15,  # Minimum O2 temperature (20°C)
    max_o2_temp: float = THERMOMACHINE_MAX_TEMP,  # Maximum O2 temperature
    max_canister_n2_pct: float = 30.0,  # Maximum nitrogen percentage in canister mix
    max_o2_mix_n2_pct: float = 30.0,  # Maximum nitrogen percentage in O2 mix
    target_total_pressure: float = 1013.0,
    min_burn_time_seconds: float = 0.0  # Minimum burn time required (0 = no requirement)
) -> Tuple[List[Dict], List[Dict]]:
    """
    Evolutionary search for optimal MaxCap explosion combinations.
    
    Process:
    1. Generate initial_candidates random combinations
    2. Test all and select top_n best
    3. Create variations_per_candidate variations of each top candidate
    4. Test new generation
    5. Repeat until improvement < improvement_threshold or max_generations reached
    
    Returns: (all_results, best_per_generation)
    """
    import random
    
    all_results = []
    best_per_generation = []
    
    # Generation 0: Random initial candidates
    print("=" * 70)
    print("EVOLUTIONARY SEARCH FOR MAXCAP EXPLOSIONS")
    print("=" * 70)
    print(f"Initial candidates: {initial_candidates}")
    print(f"Top candidates per generation: {top_n}")
    print(f"Variations per candidate: {variations_per_candidate}")
    print(f"Improvement threshold: {improvement_threshold*100:.1f}%")
    print(f"Search dimensions:")
    print(f"  - Plasma/Tritium ratio: {min_plasma_pct:.1f}%-{max_plasma_pct:.1f}%")
    print(f"  - Canister temperature: {min_temp:.1f}-{max_temp:.1f} K")
    print(f"  - Explosive pressure: {min_pressure:.1f}-{max_pressure:.1f} kPa")
    print(f"  - O2 temperature: {min_o2_temp:.1f}-{max_o2_temp:.1f} K")
    print(f"  - Canister N2: 0-{max_canister_n2_pct:.1f}%")
    print(f"  - O2 mix N2: 0-{max_o2_mix_n2_pct:.1f}%")
    if min_burn_time_seconds > 0:
        print(f"  - Minimum burn time: {min_burn_time_seconds:.1f} seconds")
    print()
    
    current_generation = []
    
    # Generate initial random candidates
    print(f"Generation 0: Generating {initial_candidates} random candidates...")
    for i in range(initial_candidates):
        plasma_pct = random.uniform(min_plasma_pct, max_plasma_pct)
        tritium_pct = 100.0 - plasma_pct
        temp = random.uniform(min_temp, max_temp)
        pressure = random.uniform(min_pressure, max_pressure)
        o2_temp = random.uniform(min_o2_temp, max_o2_temp)
        canister_n2_pct = random.uniform(0.0, max_canister_n2_pct)
        o2_mix_n2_pct = random.uniform(0.0, max_o2_mix_n2_pct)
        
        plasma_moles, tritium_moles, _ = calculate_moles_for_pressure_and_ratio(
            pressure, temp, plasma_pct, tritium_pct
        )
        
        canister = GasMixture(volume=TANK_VOLUME, temperature=temp)
        canister.set_moles(Gas.Plasma, plasma_moles)
        canister.set_moles(Gas.Tritium, tritium_moles)
        
        try:
            final_pressure, explosion_range, stats = simulate_maxcap_explosion(
                canister, temp,
                target_explosive_pressure=pressure,
                target_total_pressure=target_total_pressure,
                oxygen_temp=o2_temp,
                canister_nitrogen_pct=canister_n2_pct,
                o2_mix_nitrogen_pct=o2_mix_n2_pct
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
            
            candidate = {
                'plasma_pct': plasma_pct,
                'tritium_pct': tritium_pct,
                'temp': temp,
                'pressure': pressure,
                'o2_temp': o2_temp,
                'canister_n2_pct': canister_n2_pct,
                'o2_mix_n2_pct': o2_mix_n2_pct,
                'explosion_range': explosion_range,
                'final_pressure': final_pressure,
                'generation': 0,
                **stats
            }
            current_generation.append(candidate)
            all_results.append(candidate)
        except Exception as e:
            continue
    
    # Sort and select top candidates
    current_generation.sort(key=lambda x: x['explosion_range'], reverse=True)
    top_candidates = current_generation[:top_n]
    best_per_generation.append(top_candidates[0].copy())
    
    print(f"Generation 0 complete: {len(current_generation)} valid candidates")
    best = top_candidates[0]
    print(f"Best: {best['explosion_range']:.2f} tiles "
          f"({best['plasma_pct']:.2f}%/{best['tritium_pct']:.2f}% "
          f"at {best['temp']:.1f}K, {best['pressure']:.1f}kPa, "
          f"O2: {best['o2_temp']:.1f}K, "
          f"CanN2: {best['canister_n2_pct']:.1f}%, O2N2: {best['o2_mix_n2_pct']:.1f}%)")
    print()
    
    # Evolutionary loop
    for generation in range(1, max_generations + 1):
        previous_best = best_per_generation[-1]['explosion_range']
        
        # Create variations of top candidates
        new_generation = []
        print(f"Generation {generation}: Creating {top_n * variations_per_candidate} variations...")
        
        for candidate in top_candidates:
            for _ in range(variations_per_candidate):
                # Create variation with small random changes
                plasma_pct = candidate['plasma_pct'] + random.uniform(-5.0, 5.0)
                plasma_pct = max(min_plasma_pct, min(max_plasma_pct, plasma_pct))
                tritium_pct = 100.0 - plasma_pct
                
                temp = candidate['temp'] + random.uniform(-10.0, 10.0)
                temp = max(min_temp, min(max_temp, temp))
                
                pressure = candidate['pressure'] + random.uniform(-50.0, 50.0)
                pressure = max(min_pressure, min(max_pressure, pressure))
                
                o2_temp = candidate.get('o2_temp', 293.15) + random.uniform(-20.0, 20.0)
                o2_temp = max(min_o2_temp, min(max_o2_temp, o2_temp))
                
                canister_n2_pct = candidate.get('canister_n2_pct', 0.0) + random.uniform(-5.0, 5.0)
                canister_n2_pct = max(0.0, min(max_canister_n2_pct, canister_n2_pct))
                
                o2_mix_n2_pct = candidate.get('o2_mix_n2_pct', 0.0) + random.uniform(-5.0, 5.0)
                o2_mix_n2_pct = max(0.0, min(max_o2_mix_n2_pct, o2_mix_n2_pct))
                
                plasma_moles, tritium_moles, _ = calculate_moles_for_pressure_and_ratio(
                    pressure, temp, plasma_pct, tritium_pct
                )
                
                canister = GasMixture(volume=TANK_VOLUME, temperature=temp)
                canister.set_moles(Gas.Plasma, plasma_moles)
                canister.set_moles(Gas.Tritium, tritium_moles)
                
                try:
                    final_pressure, explosion_range, stats = simulate_maxcap_explosion(
                        canister, temp,
                        target_explosive_pressure=pressure,
                        target_total_pressure=target_total_pressure,
                        oxygen_temp=o2_temp,
                        canister_nitrogen_pct=canister_n2_pct,
                        o2_mix_nitrogen_pct=o2_mix_n2_pct
                    )
                    
                    new_candidate = {
                        'plasma_pct': plasma_pct,
                        'tritium_pct': tritium_pct,
                        'temp': temp,
                        'pressure': pressure,
                        'o2_temp': o2_temp,
                        'canister_n2_pct': canister_n2_pct,
                        'o2_mix_n2_pct': o2_mix_n2_pct,
                        'explosion_range': explosion_range,
                        'final_pressure': final_pressure,
                        'generation': generation,
                        **stats
                    }
                    new_generation.append(new_candidate)
                    all_results.append(new_candidate)
                except Exception as e:
                    continue
        
        # Sort and select top candidates for next generation
        # Only consider candidates that meet minimum burn time requirement and actually explode
        valid_candidates = [
            c for c in new_generation
            if c.get('reached_threshold', True)  # Must actually reach explosion threshold
        ]
        
        if min_burn_time_seconds > 0:
            valid_candidates = [
                c for c in valid_candidates
                if c.get('burn_time_seconds', 0.0) >= min_burn_time_seconds
            ]
            if not valid_candidates:
                print(f"  Warning: No valid candidates found in generation {generation} that meet burn time requirement!")
                print(f"  Continuing with best available candidates that explode...")
                # Fall back to candidates that at least explode
                valid_candidates = [c for c in new_generation if c.get('reached_threshold', True)]
        
        valid_candidates.sort(key=lambda x: x['explosion_range'], reverse=True)
        top_candidates = valid_candidates[:top_n]
        
        if not top_candidates:
            print(f"  No valid candidates in generation {generation}, stopping search.")
            break
        
        current_best = top_candidates[0]
        best_per_generation.append(current_best.copy())
        
        improvement = (current_best['explosion_range'] - previous_best) / previous_best if previous_best > 0 else 0
        
        print(f"Generation {generation} complete: {len(new_generation)} valid candidates")
        print(f"Best: {current_best['explosion_range']:.2f} tiles "
              f"({current_best['plasma_pct']:.2f}%/{current_best['tritium_pct']:.2f}% "
              f"at {current_best['temp']:.1f}K, {current_best['pressure']:.1f}kPa, "
              f"O2: {current_best['o2_temp']:.1f}K, "
              f"CanN2: {current_best['canister_n2_pct']:.1f}%, O2N2: {current_best['o2_mix_n2_pct']:.1f}%)")
        print(f"Improvement: {improvement*100:+.2f}%")
        print()
        
        # Check stopping condition
        if improvement < improvement_threshold and generation > 1:
            print(f"Stopping: Improvement ({improvement*100:.2f}%) below threshold ({improvement_threshold*100:.1f}%)")
            break
    
    return all_results, best_per_generation


def calculate_moles_for_pressure_and_ratio(pressure_kpa, temp_k, plasma_percent, tritium_percent):
    """Calculate moles needed for given pressure and ratio."""
    total_moles = pressure_kpa * TANK_VOLUME / (R * temp_k)
    plasma_moles = total_moles * (plasma_percent / 100.0)
    tritium_moles = total_moles * (tritium_percent / 100.0)
    return plasma_moles, tritium_moles, total_moles


# ============================================================================
# O2 Mix Preparation Calculator
# ============================================================================

def calculate_o2_mix_preparation(
    target_o2_mix_temp: float,
    target_n2_pct: float,
    n2_source_temp: float = 293.15
) -> Dict:
    """
    Calculate how to prepare the O2 mix using a gas mixer, accounting for temperature effects.
    
    The game mixer calculates: transferMoles = concentration * generalTransfer / temperature
    So temperature differences affect the actual output ratio!
    
    Args:
        target_o2_mix_temp: Target temperature of the final O2 mix (K)
        target_n2_pct: Target percentage of N2 in the final mix (0-100)
        n2_source_temp: Temperature of N2 source (default 293.15 K = 20°C)
    
    Returns:
        Dictionary with preparation instructions
    """
    target_o2_pct = 100.0 - target_n2_pct
    target_o2_to_n2_ratio = target_o2_pct / target_n2_pct if target_n2_pct > 0 else float('inf')
    
    o2_specific_heat = GAS_SPECIFIC_HEATS_RAW[Gas.Oxygen]
    n2_specific_heat = GAS_SPECIFIC_HEATS_RAW[Gas.Nitrogen]
    
    # Calculate heat capacities for final mix
    o2_moles_frac = target_o2_pct / 100.0
    n2_moles_frac = target_n2_pct / 100.0
    o2_heat_cap_final = o2_moles_frac * o2_specific_heat
    n2_heat_cap_final = n2_moles_frac * n2_specific_heat
    total_heat_cap_final = o2_heat_cap_final + n2_heat_cap_final
    
    # Option 1: Heat both to same temperature (simplest)
    o2_temp_option1 = target_o2_mix_temp
    n2_temp_option1 = target_o2_mix_temp
    mixer_o2_pct_option1 = target_o2_pct
    mixer_n2_pct_option1 = target_n2_pct
    
    # Option 2: N2 at room temp, adjust O2 and mixer
    required_o2_temp_option2 = (total_heat_cap_final * target_o2_mix_temp - n2_heat_cap_final * n2_source_temp) / o2_heat_cap_final
    required_conc_ratio = target_o2_to_n2_ratio * (required_o2_temp_option2 / n2_source_temp)
    mixer_n2_pct_option2 = 100.0 / (1.0 + required_conc_ratio)
    mixer_o2_pct_option2 = 100.0 - mixer_n2_pct_option2
    
    return {
        'target_o2_mix_temp': target_o2_mix_temp,
        'target_o2_pct': target_o2_pct,
        'target_n2_pct': target_n2_pct,
        'option1_same_temp': {
            'o2_temp_k': o2_temp_option1,
            'o2_temp_c': o2_temp_option1 - 273.15,
            'n2_temp_k': n2_temp_option1,
            'n2_temp_c': n2_temp_option1 - 273.15,
            'mixer_o2_pct': mixer_o2_pct_option1,
            'mixer_n2_pct': mixer_n2_pct_option1,
            'description': 'Heat both gases to same temperature'
        },
        'option2_room_temp_n2': {
            'o2_temp_k': required_o2_temp_option2,
            'o2_temp_c': required_o2_temp_option2 - 273.15,
            'n2_temp_k': n2_source_temp,
            'n2_temp_c': n2_source_temp - 273.15,
            'mixer_o2_pct': mixer_o2_pct_option2,
            'mixer_n2_pct': mixer_n2_pct_option2,
            'description': 'N2 at room temperature, adjust O2 and mixer'
        }
    }


def print_o2_mix_preparation(prep_info: Dict):
    """
    Print formatted O2 mix preparation instructions.
    """
    print("=" * 70)
    print("O2 MIX PREPARATION INSTRUCTIONS")
    print("=" * 70)
    print()
    print(f"Target: {prep_info['target_o2_pct']:.2f}% O2, {prep_info['target_n2_pct']:.2f}% N2")
    print(f"Target temperature: {prep_info['target_o2_mix_temp']:.2f} K ({prep_info['target_o2_mix_temp'] - 273.15:.2f}°C)")
    print()
    
    opt1 = prep_info['option1_same_temp']
    print("OPTION 1 (Recommended - Simplest):")
    print("-" * 70)
    print(f"  {opt1['description']}")
    print(f"  Heat O2 to: {opt1['o2_temp_k']:.2f} K ({opt1['o2_temp_c']:.2f}°C)")
    print(f"  Heat N2 to: {opt1['n2_temp_k']:.2f} K ({opt1['n2_temp_c']:.2f}°C)")
    print(f"  Set mixer to: {opt1['mixer_o2_pct']:.2f}% O2, {opt1['mixer_n2_pct']:.2f}% N2")
    print(f"  Result: Exact target ratio and temperature")
    print()
    
    opt2 = prep_info['option2_room_temp_n2']
    print("OPTION 2 (N2 at room temperature):")
    print("-" * 70)
    print(f"  {opt2['description']}")
    print(f"  Heat O2 to: {opt2['o2_temp_k']:.2f} K ({opt2['o2_temp_c']:.2f}°C)")
    print(f"  Keep N2 at: {opt2['n2_temp_k']:.2f} K ({opt2['n2_temp_c']:.2f}°C)")
    print(f"  Set mixer to: {opt2['mixer_o2_pct']:.2f}% O2, {opt2['mixer_n2_pct']:.2f}% N2")
    print(f"  Result: Target ratio and temperature (mixer adjusted for temp difference)")
    print()
    print("=" * 70)
    print()


def calculate_explosive_mix_preparation(
    target_plasma_pct: float,
    target_tritium_pct: float,
    target_n2_pct: float,
    target_final_temp: float,
    n2_source_temp: float = 293.15
) -> Dict:
    """
    Calculate how to prepare the explosive mix using gas mixers.
    
    Process:
    1. Mix Plasma and Tritium at the same temperature
    2. Add N2 at 20°C to the Plasma/Tritium mix
    
    The game mixer calculates: transferMoles = concentration * generalTransfer / temperature
    So temperature differences affect the actual output ratio!
    
    Args:
        target_plasma_pct: Target percentage of Plasma in final mix (0-100)
        target_tritium_pct: Target percentage of Tritium in final mix (0-100)
        target_n2_pct: Target percentage of N2 in final mix (0-100)
        target_final_temp: Target temperature of the final explosive mix (K)
        n2_source_temp: Temperature of N2 source (default 293.15 K = 20°C)
    
    Returns:
        Dictionary with preparation instructions
    """
    # Normalize percentages (should sum to 100, but allow for small errors)
    total_pct = target_plasma_pct + target_tritium_pct + target_n2_pct
    if abs(total_pct - 100.0) > 0.01:
        # Normalize
        target_plasma_pct = target_plasma_pct * 100.0 / total_pct
        target_tritium_pct = target_tritium_pct * 100.0 / total_pct
        target_n2_pct = target_n2_pct * 100.0 / total_pct
    
    plasma_specific_heat = GAS_SPECIFIC_HEATS_RAW[Gas.Plasma]
    tritium_specific_heat = GAS_SPECIFIC_HEATS_RAW[Gas.Tritium]
    n2_specific_heat = GAS_SPECIFIC_HEATS_RAW[Gas.Nitrogen]
    
    # Step 1: Calculate Plasma/Tritium ratio (before adding N2)
    # If final mix is P% plasma, T% tritium, N% N2
    # Then the Plasma/Tritium mix (before N2) should be:
    # Plasma: P/(P+T) of the fuel mix
    # Tritium: T/(P+T) of the fuel mix
    fuel_total_pct = target_plasma_pct + target_tritium_pct
    if fuel_total_pct < 0.01:
        # No fuel, can't calculate
        return {
            'error': 'No fuel gases (Plasma/Tritium) in mix'
        }
    
    plasma_in_fuel_pct = (target_plasma_pct / fuel_total_pct) * 100.0
    tritium_in_fuel_pct = (target_tritium_pct / fuel_total_pct) * 100.0
    
    # Step 2: Calculate what temperature the Plasma/Tritium mix should be at
    # so that when we add N2 at n2_source_temp, we get target_final_temp
    
    # Final mix heat capacities (per unit total moles)
    plasma_heat_cap = (target_plasma_pct / 100.0) * plasma_specific_heat
    tritium_heat_cap = (target_tritium_pct / 100.0) * tritium_specific_heat
    n2_heat_cap = (target_n2_pct / 100.0) * n2_specific_heat
    total_heat_cap = plasma_heat_cap + tritium_heat_cap + n2_heat_cap
    
    # Fuel mix heat capacity (per unit fuel moles)
    fuel_heat_cap = (plasma_in_fuel_pct / 100.0) * plasma_specific_heat + (tritium_in_fuel_pct / 100.0) * tritium_specific_heat
    
    # If we have 1 unit of fuel mix and add X units of N2:
    # Final mix: (1/(1+X)) fuel, (X/(1+X)) N2
    # We want: X/(1+X) = target_n2_pct / 100
    # So: X = target_n2_pct / (100 - target_n2_pct)
    n2_to_fuel_ratio = target_n2_pct / (100.0 - target_n2_pct) if target_n2_pct < 100.0 else float('inf')
    
    # Energy balance for mixing:
    # fuel_heat_cap * T_fuel + n2_heat_cap_per_unit * n2_to_fuel_ratio * n2_source_temp
    # = total_heat_cap * target_final_temp
    # But we need to account for the fact that n2_heat_cap is per unit total moles
    # Let's work with per-unit-fuel basis:
    # 1 unit fuel at T_fuel + X units N2 at n2_source_temp
    # = (1+X) units final mix at target_final_temp
    
    # Heat capacity of N2 per unit N2
    n2_heat_cap_per_unit_n2 = n2_specific_heat
    
    # Total heat capacity: 1 * fuel_heat_cap + X * n2_heat_cap_per_unit_n2
    total_heat_cap_mixing = fuel_heat_cap + n2_to_fuel_ratio * n2_heat_cap_per_unit_n2
    
    # Energy balance:
    # fuel_heat_cap * T_fuel + n2_to_fuel_ratio * n2_heat_cap_per_unit_n2 * n2_source_temp
    # = total_heat_cap_mixing * target_final_temp
    
    # Solve for T_fuel:
    if total_heat_cap_mixing > 0.001:
        required_fuel_temp = (
            total_heat_cap_mixing * target_final_temp -
            n2_to_fuel_ratio * n2_heat_cap_per_unit_n2 * n2_source_temp
        ) / fuel_heat_cap
    else:
        required_fuel_temp = target_final_temp
    
    # Step 3: Calculate mixer settings for adding N2
    # We want to mix: fuel mix at required_fuel_temp with N2 at n2_source_temp
    # To get: target_n2_pct% N2 in final mix
    
    # The mixer calculates: transferMoles = concentration * generalTransfer / temperature
    # So if we want N2% in output, we need:
    # (conc_n2 / n2_source_temp) / ((conc_fuel / required_fuel_temp) + (conc_n2 / n2_source_temp)) = target_n2_pct / 100
    
    # Let's work with ratios:
    # If conc_fuel = 1 - conc_n2, then:
    # transfer_fuel = (1 - conc_n2) / required_fuel_temp
    # transfer_n2 = conc_n2 / n2_source_temp
    # We want: transfer_n2 / (transfer_fuel + transfer_n2) = target_n2_pct / 100
    
    # Solving: transfer_n2 = (target_n2_pct / 100) * (transfer_fuel + transfer_n2)
    # transfer_n2 = (target_n2_pct / 100) * transfer_fuel + (target_n2_pct / 100) * transfer_n2
    # transfer_n2 * (1 - target_n2_pct / 100) = (target_n2_pct / 100) * transfer_fuel
    # transfer_n2 / transfer_fuel = (target_n2_pct / 100) / (1 - target_n2_pct / 100)
    # = target_n2_pct / (100 - target_n2_pct)
    
    # transfer_n2 / transfer_fuel = (conc_n2 / n2_source_temp) / ((1 - conc_n2) / required_fuel_temp)
    # = (conc_n2 * required_fuel_temp) / ((1 - conc_n2) * n2_source_temp)
    
    # So: (conc_n2 * required_fuel_temp) / ((1 - conc_n2) * n2_source_temp) = target_n2_pct / (100 - target_n2_pct)
    # conc_n2 * required_fuel_temp = (target_n2_pct / (100 - target_n2_pct)) * (1 - conc_n2) * n2_source_temp
    # conc_n2 * required_fuel_temp = (target_n2_pct / (100 - target_n2_pct)) * n2_source_temp - (target_n2_pct / (100 - target_n2_pct)) * n2_source_temp * conc_n2
    # conc_n2 * (required_fuel_temp + (target_n2_pct / (100 - target_n2_pct)) * n2_source_temp) = (target_n2_pct / (100 - target_n2_pct)) * n2_source_temp
    # conc_n2 = ((target_n2_pct / (100 - target_n2_pct)) * n2_source_temp) / (required_fuel_temp + (target_n2_pct / (100 - target_n2_pct)) * n2_source_temp)
    
    n2_ratio = target_n2_pct / (100.0 - target_n2_pct) if target_n2_pct < 100.0 else float('inf')
    if n2_ratio == float('inf'):
        mixer_n2_pct = 100.0
        mixer_fuel_pct = 0.0
    else:
        denominator = required_fuel_temp + n2_ratio * n2_source_temp
        if denominator > 0.001:
            mixer_n2_pct = (n2_ratio * n2_source_temp) / denominator * 100.0
            mixer_fuel_pct = 100.0 - mixer_n2_pct
        else:
            mixer_n2_pct = 0.0
            mixer_fuel_pct = 100.0
    
    return {
        'target_plasma_pct': target_plasma_pct,
        'target_tritium_pct': target_tritium_pct,
        'target_n2_pct': target_n2_pct,
        'target_final_temp': target_final_temp,
        'step1_plasma_tritium_mix': {
            'plasma_pct': plasma_in_fuel_pct,
            'tritium_pct': tritium_in_fuel_pct,
            'temperature_k': required_fuel_temp,
            'temperature_c': required_fuel_temp - 273.15,
            'description': 'Mix Plasma and Tritium at same temperature'
        },
        'step2_add_n2': {
            'fuel_mix_temp_k': required_fuel_temp,
            'fuel_mix_temp_c': required_fuel_temp - 273.15,
            'n2_temp_k': n2_source_temp,
            'n2_temp_c': n2_source_temp - 273.15,
            'mixer_fuel_pct': mixer_fuel_pct,
            'mixer_n2_pct': mixer_n2_pct,
            'description': 'Add N2 at room temperature to Plasma/Tritium mix'
        }
    }


def print_explosive_mix_preparation(prep_info: Dict):
    """
    Print formatted explosive mix preparation instructions.
    """
    if 'error' in prep_info:
        print("=" * 70)
        print("EXPLOSIVE MIX PREPARATION INSTRUCTIONS")
        print("=" * 70)
        print()
        print(f"ERROR: {prep_info['error']}")
        print()
        print("=" * 70)
        print()
        return
    
    print("=" * 70)
    print("EXPLOSIVE MIX PREPARATION INSTRUCTIONS")
    print("=" * 70)
    print()
    print(f"Target: {prep_info['target_plasma_pct']:.2f}% Plasma, {prep_info['target_tritium_pct']:.2f}% Tritium, {prep_info['target_n2_pct']:.2f}% N2")
    print(f"Target temperature: {prep_info['target_final_temp']:.2f} K ({prep_info['target_final_temp'] - 273.15:.2f}°C)")
    print()
    
    step1 = prep_info['step1_plasma_tritium_mix']
    print("STEP 1: Mix Plasma and Tritium")
    print("-" * 70)
    print(f"  {step1['description']}")
    print(f"  Heat Plasma to: {step1['temperature_k']:.2f} K ({step1['temperature_c']:.2f}°C)")
    print(f"  Heat Tritium to: {step1['temperature_k']:.2f} K ({step1['temperature_c']:.2f}°C)")
    print(f"  Set mixer to: {step1['plasma_pct']:.2f}% Plasma, {step1['tritium_pct']:.2f}% Tritium")
    print(f"  Result: {step1['plasma_pct']:.2f}% Plasma, {step1['tritium_pct']:.2f}% Tritium at {step1['temperature_k']:.2f} K")
    print()
    
    step2 = prep_info['step2_add_n2']
    print("STEP 2: Add N2 to Plasma/Tritium Mix")
    print("-" * 70)
    print(f"  {step2['description']}")
    print(f"  Fuel mix temperature: {step2['fuel_mix_temp_k']:.2f} K ({step2['fuel_mix_temp_c']:.2f}°C)")
    print(f"  N2 temperature: {step2['n2_temp_k']:.2f} K ({step2['n2_temp_c']:.2f}°C)")
    print(f"  Set mixer to: {step2['mixer_fuel_pct']:.2f}% Fuel Mix, {step2['mixer_n2_pct']:.2f}% N2")
    print(f"  Result: {prep_info['target_plasma_pct']:.2f}% Plasma, {prep_info['target_tritium_pct']:.2f}% Tritium, {prep_info['target_n2_pct']:.2f}% N2")
    print(f"           at {prep_info['target_final_temp']:.2f} K ({prep_info['target_final_temp'] - 273.15:.2f}°C)")
    print()
    print("=" * 70)
    print()


# ============================================================================
# Helper Functions
# ============================================================================

def test_specific_combination(
    gas_amounts: Dict[Gas, float],
    temperature: float,
    target_explosive_pressure: Optional[float] = None,
    target_total_pressure: float = 1013.0,
    verbose: bool = True
) -> Tuple[float, float, Dict]:
    """
    Test a specific gas combination.
    
    Args:
        gas_amounts: Dictionary mapping Gas to mole amounts (relative ratios)
        temperature: Temperature to heat the canister to (Kelvin)
        target_explosive_pressure: Target pressure for explosive mix (kPa). If None, uses current pressure.
        target_total_pressure: Target total pressure after adding O2 (kPa, default 1013)
        verbose: If True, print detailed results
    
    Returns: (final_pressure, explosion_range, stats_dict)
    """
    canister = GasMixture(volume=TANK_VOLUME, temperature=temperature)
    for gas, moles in gas_amounts.items():
        canister.set_moles(gas, moles)
    
    final_pressure, explosion_range, stats = simulate_maxcap_explosion(
        canister, temperature, 
        target_explosive_pressure=target_explosive_pressure,
        target_total_pressure=target_total_pressure
    )
    
    if verbose:
        print(f"Gas Combination:")
        for gas, moles in gas_amounts.items():
            print(f"  {gas.name}: {moles:.3f} mol")
        print(f"Temperature: {temperature:.2f} K")
        print(f"Target Total Pressure: {target_total_pressure:.2f} kPa")
        print()
        print(f"Explosive Tank (before adding O2):")
        print(f"  Pressure: {stats['explosive_pressure_kpa']:.2f} kPa")
        print(f"  Temperature: {stats['explosive_temp_k']:.2f} K")
        if stats['plasma_percent'] > 0 or stats['tritium_percent'] > 0:
            print(f"  Plasma: {stats['plasma_percent']:.1f}%")
            print(f"  Tritium: {stats['tritium_percent']:.1f}%")
        print()
        print(f"O2 to Add:")
        print(f"  Moles: {stats['o2_moles_to_add']:.3f} mol")
        print(f"  Pressure Equivalent (at 20°C): {stats['o2_pressure_equivalent_kpa']:.2f} kPa")
        print()
        print(f"After Mixing (before reactions):")
        print(f"  Initial Pressure: {stats['initial_pressure']:.2f} kPa")
        print(f"  Initial Temperature: {stats['initial_temp']:.2f} K")
        print()
        print(f"After Reactions:")
        print(f"  Final Pressure: {stats['final_pressure']:.2f} kPa")
        print(f"  Final Temperature: {stats['final_temp']:.2f} K")
        print(f"  Pressure Increase: {stats['pressure_increase']:.2f} kPa")
        print(f"  Explosion Range: {stats['explosion_range']:.2f} tiles")
        print(f"  Hits Max Cap: {stats['hits_max_cap']}")
        print()
    
    return final_pressure, explosion_range, stats


def export_results_to_csv(results: List[Dict], filename: str = "maxcap_results.csv"):
    """
    Export search results to a CSV file.
    
    Args:
        results: List of result dictionaries from search functions
        filename: Output CSV filename
    """
    import csv
    
    if not results:
        print("No results to export.")
        return
    
    # Get all unique keys from results
    fieldnames = set()
    for result in results:
        fieldnames.update(result.keys())
    fieldnames = sorted(fieldnames)
    
    with open(filename, 'w', newline='') as csvfile:
        writer = csv.DictWriter(csvfile, fieldnames=fieldnames)
        writer.writeheader()
        writer.writerows(results)
    
    print(f"Exported {len(results)} results to {filename}")


# ============================================================================
# Main / Example Usage
# ============================================================================

def main():
    """Example usage: Test traditional plasma+tritium MaxCap"""
    
    print("=" * 70)
    print("SpaceStation 14 MaxCap Explosion Simulator")
    print("=" * 70)
    print()
    
    # Example 1: Traditional plasma + tritium mix
    print("Example 1: Traditional Plasma + Tritium MaxCap")
    print("-" * 70)
    
    canister = GasMixture(volume=TANK_VOLUME, temperature=THERMOMACHINE_MAX_TEMP)
    canister.set_moles(Gas.Plasma, 2.0)
    canister.set_moles(Gas.Tritium, 1.0)
    # No oxygen - it will be added to fill remaining space
    
    final_pressure, explosion_range, stats = simulate_maxcap_explosion(
        canister, THERMOMACHINE_MAX_TEMP, 
        target_explosive_pressure=800.0,  # Fill explosive mix to 800 kPa
        target_total_pressure=1013.0
    )
    
    print(f"Explosive Tank (before adding O2):")
    print(f"  Pressure: {stats['explosive_pressure_kpa']:.2f} kPa")
    print(f"  Temperature: {stats['explosive_temp_k']:.2f} K")
    print(f"  Plasma: {stats['plasma_percent']:.1f}%")
    print(f"  Tritium: {stats['tritium_percent']:.1f}%")
    print()
    print(f"O2 to Add:")
    print(f"  Moles: {stats['o2_moles_to_add']:.3f} mol")
    print(f"  Pressure Equivalent (at 20°C): {stats['o2_pressure_equivalent_kpa']:.2f} kPa")
    print()
    print(f"After Mixing (before reactions):")
    print(f"  Initial Pressure: {stats['initial_pressure']:.2f} kPa")
    print(f"  Initial Temperature: {stats['initial_temp']:.2f} K")
    print()
    print(f"After Reactions:")
    print(f"  Final Pressure: {stats['final_pressure']:.2f} kPa")
    print(f"  Final Temperature: {stats['final_temp']:.2f} K")
    print(f"  Pressure Increase: {stats['pressure_increase']:.2f} kPa")
    print(f"  Explosion Range: {stats['explosion_range']:.2f} tiles")
    print(f"  Hits Max Cap (26 tiles): {stats['hits_max_cap']}")
    print()
    
    # Example 2: Search for plasma/tritium combinations
    print("Example 2: Searching for Plasma/Tritium MaxCap combinations...")
    print("-" * 70)
    print("(This may take a while)")
    print()
    
    results = search_plasma_tritium_combinations(
        min_plasma=0.5,
        max_plasma=5.0,
        min_tritium=0.5,
        max_tritium=5.0,
        plasma_step=0.5,
        tritium_step=0.5,
        min_temp=373.15,  # Fire minimum temperature
        max_temp=THERMOMACHINE_MAX_TEMP,  # Limited by thermomachine
        temp_step=20.0,  # Smaller step for more precision in limited range
        oxygen_tank_pressure=1013.0
    )
    
    print(f"\nFound {len(results)} MaxCap combinations:")
    print()
    
    # Show top 10
    for i, result in enumerate(results[:10], 1):
        print(f"{i}. Plasma: {result['plasma_moles']:.2f} mol ({result['plasma_percent']:.1f}%), "
              f"Tritium: {result['tritium_moles']:.2f} mol ({result['tritium_percent']:.1f}%), "
              f"Temp: {result['temperature']:.1f} K")
        print(f"   Explosive Pressure: {result['explosive_pressure_kpa']:.2f} kPa, "
              f"O2 to add: {result['o2_pressure_equivalent_kpa']:.2f} kPa")
        print(f"   -> Range: {result['explosion_range']:.2f} tiles, "
              f"Final Pressure: {result['final_pressure']:.2f} kPa")
        print()
    
    # Example 3: Search other gas combinations
    print("\nExample 3: Searching for other gas combinations...")
    print("-" * 70)
    print("Testing Plasma + Hydrogen...")
    print()
    
    results2 = search_all_gas_combinations(
        gas_list=[Gas.Plasma, Gas.Hydrogen],
        min_moles=0.5,
        max_moles=3.0,
        mole_step=0.5,
        min_temp=373.15,  # Fire minimum temperature
        max_temp=THERMOMACHINE_MAX_TEMP,  # Limited by thermomachine
        temp_step=20.0,  # Smaller step for more precision in limited range
        oxygen_tank_pressure=1013.0,
        target_range=MaxExplosionRange - 0.01
    )
    
    print(f"\nFound {len(results2)} MaxCap combinations with Plasma + Hydrogen:")
    for i, result in enumerate(results2[:5], 1):
        print(f"{i}. Plasma: {result['plasma_moles']:.2f} mol, "
              f"Hydrogen: {result['hydrogen_moles']:.2f} mol, "
              f"Temp: {result['temperature']:.1f} K")
        print(f"   -> Range: {result['explosion_range']:.2f} tiles")
    print()
    
    # Example 4: Test a specific combination
    print("\nExample 4: Testing a specific combination")
    print("-" * 70)
    test_specific_combination(
        gas_amounts={
            Gas.Plasma: 1.5,
            Gas.Tritium: 0.8
        },
        temperature=THERMOMACHINE_MAX_TEMP,  # Use thermomachine max
        target_explosive_pressure=800.0,  # Fill to 800 kPa before adding O2
        target_total_pressure=1013.0
    )
    
    # Example 5: Evolutionary search
    print("\nExample 5: Evolutionary Search for Optimal MaxCap")
    print("-" * 70)
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
        max_temp=THERMOMACHINE_MAX_TEMP,
        min_pressure=400.0,
        max_pressure=900.0
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
    print(f"  Final Pressure: {best['final_pressure']:.2f} kPa")
    print(f"  O2 to Add: {best['o2_pressure_equivalent_kpa']:.2f} kPa")
    print()
    
    print("Top 10 overall:")
    all_results.sort(key=lambda x: x['explosion_range'], reverse=True)
    print(f"{'Rank':<6} {'Range':<8} {'Plasma%':<10} {'Tritium%':<11} {'Temp(K)':<10} {'Press(kPa)':<12} {'Gen':<5}")
    print("-" * 70)
    for i, result in enumerate(all_results[:10], 1):
        print(f"{i:<6} {result['explosion_range']:>7.2f} {result['plasma_pct']:>9.2f}% "
              f"{result['tritium_pct']:>10.2f}% {result['temp']:>9.2f} "
              f"{result['pressure']:>11.2f} {result['generation']:>4}")
    print()
    
    print("\n" + "=" * 70)
    print("Usage:")
    print("  - Modify search parameters in main() to explore different combinations")
    print("  - Use search_plasma_tritium_combinations() for plasma/tritium searches")
    print("  - Use search_all_gas_combinations() for any gas combination")
    print("  - Use evolutionary_search() for optimal combination finding")
    print("  - Use test_specific_combination() to test exact values")
    print("  - Use export_results_to_csv() to save results for analysis")
    print("  - All functions return results sorted by explosion range")
    print("=" * 70)


if __name__ == "__main__":
    main()
