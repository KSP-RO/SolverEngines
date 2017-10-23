using System;
using UnityEngine;

namespace SolverEngines
{
    // Right now this only works for a mixture of mostly air and some fuel (presumably kerosene-based)
    // To work well for other engine types, it should be changed to be composed of an arbitrary mixture of exhaust products, and may or may not have air
    // The exhaust products would presumably have their thermodynamic parameters read from a cfg somewhere, including possible temperature dependence

    // Please note that all conditions (pressure, temperature, density) are total conditions as opposed to real conditions

    // Also note that this is still WIP and likely to change in the future
    public struct EngineThermodynamics
    {
        public struct GasParameters
        {
            public double T;
            public double P;
            public double Rho;
            public double Cp;
            public double Cv;
            public double Gamma;
            public double R;
            public double fuelAirRatio;
            public double fuelFraction;
        }

        private GasParameters gas;

        /// <summary>
        /// Total (stagnation) pressure
        /// </summary>
        public double P
        {
            get
            {
                return gas.P;
            }
            set
            {
                gas.P = value;
                RecalculateDensity();
            }
        }
        /// <summary>
        /// Total (stagnation) temperature
        /// </summary>
        public double T
        {
            get
            {
                return gas.T;
            }
            set
            {
                gas.T = value;
                Recalculate();
            }
        }
        /// <summary>
        /// Total (stagnation) density
        /// </summary>
        public double Rho => gas.Rho;
        /// <summary>
        /// Specific heat capacity at constant pressure, joules / kg / K
        /// </summary>
        public double Cp => gas.Cp;
        /// <summary>
        /// Specific heat capacity at constant volume, joules / kg / K
        /// </summary>
        public double Cv => gas.Cv;
        /// <summary>
        /// Specific heat capacity ratio Cp / Cv
        /// </summary>
        public double Gamma => gas.Gamma;
        /// <summary>
        /// Specific gas constant
        /// Equal to (boltzmann constant) / (molecular mass) = (ideal gas constant) / (molar mass)
        /// </summary>
        public double R => gas.R;
        /// <summary>
        /// Fuel-air ratio
        /// </summary>
        public double Far
        {
            get
            {
                return gas.fuelAirRatio;
            }
            set
            {
                gas.fuelAirRatio = value;
                gas.fuelFraction = gas.fuelAirRatio / (gas.fuelAirRatio + 1);
                Recalculate();
            }
        }
        /// <summary>
        /// Fuel fraction ( mfuel / (mair + mfuel) )
        /// </summary>
        public double FF
        {
            get
            {
                return gas.fuelFraction;
            }
            set
            {
                gas.fuelFraction = value;
                gas.fuelAirRatio = gas.fuelFraction / (gas.fuelFraction - 1);
                Recalculate();
            }
        }
        /// <summary>
        /// Ratio of mass flow relative to some reference flow
        /// This is increased when fuel is added
        /// </summary>
        public double MassRatio { get; set; }

        public EngineThermodynamics(
            double P = 0d,
            double T = 0d,
            double far = 0d,
            double massRatio = 1d)
        {
            gas = new GasParameters
            {
                P = P,
                T = T
            };
            MassRatio = massRatio;
            Far = far;

            Recalculate();
        }

        private void Recalculate()
        {
            double tDelt = Math.Max((T - 300d) * 0.0005d, 0d);
            gas.Cp = 1004.5d + 250d * tDelt * (1d + 10d * FF);
            gas.Gamma = 1.4d - 0.1d * tDelt * (1d + FF);
            gas.Cv = Cp / Gamma;
            gas.R = Cv * (Gamma - 1d);
            RecalculateDensity();
        }

        private void RecalculateDensity()
        {
            gas.Rho = P / R / T;
        }

        /// <summary>
        /// Performs an adiabatic expansion or compression
        /// </summary>
        /// <param name="tempRatio">Total temperature ratio for this process</param>
        /// <param name="efficiency">Adiabatic efficiency for this process</param>
        /// <returns>Resulting thermodynamic state</returns>
        public EngineThermodynamics AdiabaticProcessWithTempRatio(double tempRatio, double efficiency = 1d)
        {
            double pressureExponent = Cp / R * efficiency;
            EngineThermodynamics result = this;
            result.T *= tempRatio;
            result.P *= Math.Pow(tempRatio, pressureExponent);
            return result;
        }

        /// <summary>
        /// Performs an adiabatic expansion or compression
        /// </summary>
        /// <param name="tempRatio">Total temperature ratio for this process</param>
        /// <param name="efficiency">Adiabatic efficiency for this process</param>
        /// <param name="work">Work done in the process, per unit reference mass.  Positive if the process does work, negative if the process requires work.</param>
        /// <returns>Resulting thermodynamic state</returns>
        public EngineThermodynamics AdiabaticProcessWithTempRatio(double tempRatio, out double work, double efficiency = 1d)
        {
            EngineThermodynamics result = this.AdiabaticProcessWithTempRatio(tempRatio, efficiency: efficiency);
            work = Cp * (T - result.T) * MassRatio;
            return result;
        }

        /// <summary>
        /// Takes input conditions and performs an adiabatic expansion or compression
        /// </summary>
        /// <param name="pressureRatio">Total pressure ratio for this process</param>
        /// <param name="efficiency">Adiabatic efficiency for this process</param>
        /// <returns>Resulting thermodynamic state</returns>
        public EngineThermodynamics AdiabaticProcessWithPressureRatio(double pressureRatio, double efficiency = 1d)
        {
            //_T *= Math.Pow(pressureRatio, (Gamma - 1d) / Gamma / efficiency);
            // equivalent: tempExponent = (Gamma - 1) / Gamma
            EngineThermodynamics result = this;
            double tempExponent = R / Cp / efficiency;
            result.T *= Math.Pow(pressureRatio, tempExponent);
            result.P *= pressureRatio;

            return result;
        }

        /// <summary>
        /// Takes input conditions and performs an adiabatic expansion or compression
        /// </summary>
        /// <param name="pressureRatio">Total pressure ratio for this process</param>
        /// <param name="work">Work done in the process, per unit reference mass.  Positive if the process does work, negative if the process requires work.</param>
        /// <param name="efficiency">Adiabatic efficiency for this process</param>
        /// <returns>Resulting thermodynamic state</returns>
        public EngineThermodynamics AdiabaticProcessWithPressureRatio(double pressureRatio, out double work, double efficiency = 1d)
        {
            //_T *= Math.Pow(pressureRatio, (Gamma - 1d) / Gamma / efficiency);
            // equivalent: tempExponent = (Gamma - 1) / Gamma
            EngineThermodynamics result = AdiabaticProcessWithPressureRatio(pressureRatio, efficiency: efficiency);

            work = Cp * (T - result.T) * MassRatio;

            return result;
        }

        /// <summary>
        /// Takes input conditions and performs an adiabatic expansion or compression
        /// Positive work corresponds to work being done on the gas (i.e. compression), and negative work corresponds to the gas doing work (i.e. expansion)
        /// </summary>
        /// <param name="work">Work to do in this process, per unit reference mass (controlled by MassRatio).  Positive = compression, negative = expansion</param>
        /// <param name="efficiency">Adiabatic efficiency for this process</param>
        /// <returns>Resulting thermodynamic state</returns>
        public EngineThermodynamics AdiabaticProcessWithWork(double work, double efficiency = 1d)
        {
            EngineThermodynamics result = this;
            result.T += work / Cp / MassRatio;
            double pressureExponent = Cp / R * efficiency;
            result.P *= Math.Pow(result.T / T, pressureExponent);
            return result;
        }

        /// <summary>
        /// Changes the reference frame of the gas, adjusting total conditions to account for the change
        /// </summary>
        /// <param name="speed">Speed to change reference by.  Positive = speed up, negative = slow down</param>
        public EngineThermodynamics ChangeReferenceFrame(double speed)
        {
            return AdiabaticProcessWithWork(speed * speed / 2d * Math.Sign(speed));
        }

        /// <summary>
        /// Changes the reference frame of the gas, adjusting total conditions to account for the change
        /// </summary>
        /// <param name="mach">Mach to change reference by.  Positive = speed up, negative = slow down</param>
        public EngineThermodynamics ChangeReferenceFrameMach(double mach)
        {
            EngineThermodynamics result = this;
            double machFactor = 0.5 * (Gamma - 1) * mach * mach + 1d;
            result.T *= (mach > 0d) ? machFactor : 1d / machFactor;
            double pressureExponent = Cp / R;
            result.P *= Math.Pow(result.T / T, pressureExponent);
            return result;
        }

        /// <summary>
        /// Add fuel to a gas (and burn it)
        /// </summary>
        /// <param name="maxTemp">Maximum allowed temperature</param>
        /// <param name="heatOfFuel">Heat of combustion of fuel, measured in joules/kg/K</param>
        /// <param name="throttle">Throttle for adding fuel.  0 corresponds to no fuel added, 1 corresponds to max fuel added</param>
        /// <param name="maxFar">Maximum fuel-air ratio (usually defined by stoichiometry).  This will be handled automatically in the future</param>
        public EngineThermodynamics AddFuelToTemperature(double maxTemp, double heatOfFuel, double throttle = 1d, double maxFar = 0d)
        {
            EngineThermodynamics result = this;
            System.Diagnostics.Debug.Assert(throttle >= 0d && throttle <= 1d);
            // Max fuel-air ratio - don't want to inject more fuel than can be burnt in air
            if (maxFar > 0d)
                maxTemp = Math.Min(maxTemp, (maxFar - Far) * heatOfFuel / Cp + T);
            double delta = (maxTemp - T) * throttle;

            result.T += delta;
            double addedFuel = delta * Cp / heatOfFuel;
            result.Far += addedFuel;
            // Order is important here - want old Far in this line not new Far
            result.MassRatio *= (addedFuel / (1d + Far) + 1d);

            return result;
        }

        /// <summary>
        /// Mix two or more streams
        /// </summary>
        /// <param name="streams">Gas streams to mix.  MassRatio will be respected in mixing</param>
        public static EngineThermodynamics MixStreams(params EngineThermodynamics[] streams)
        {
            double temperatureSum = 0d;
            double temperatureWeightSum = 0d;
            double pressureSum = 0d;

            double fuelSum = 0d;
            double fuelWeightSum = 0d;

            double newMassRatio = 0d;

            foreach (EngineThermodynamics t in streams)
            {
                temperatureSum += t.Cp * t.MassRatio * t.T;
                temperatureWeightSum += t.Cp * t.MassRatio;
                pressureSum += t.MassRatio * t.P;
                fuelSum += t.MassRatio * t.FF;
                fuelWeightSum += t.MassRatio / (1d + t.Far);
                newMassRatio += t.MassRatio;
            }

            return new EngineThermodynamics(
                T: temperatureSum / temperatureWeightSum,
                P: pressureSum / newMassRatio,
                far: fuelSum / fuelWeightSum,
                massRatio: newMassRatio);
        }

        /// <summary>
        /// Get ambient conditions for a vessel
        /// Density is calculated automatically according to gas law.  If the density KSP gives differs by more than 1% from this, then a warning is printed
        /// UPDATE - warning not printed because it causes debug spam on other planets.  Have to implement real gas composition before this can be used
        /// </summary>
        /// <param name="v">Vessel to find conditions for</param>
        /// <param name="useExternalTemp">If true, use externalTemperature (nominally shock temp).  Otherwise use atmosphericTemperature (ambient temp)</param>
        public static EngineThermodynamics VesselAmbientConditions(Vessel v, bool useExternalTemp = false)
        {
            double P = v.staticPressurekPa * 1000d;
            double T = useExternalTemp ? v.externalTemperature : v.atmosphericTemperature;

            return new EngineThermodynamics(P: P, T: T);

            // Rho = v.atmDensity;
            //if (Math.Abs(v.atmDensity / Rho - 1d) > 0.01d)
            //    Debug.LogWarning("Ambient density does not obey the gas law for vessel " + v.name);
        }

        /// <summary>
        /// Get ambient conditions at altitude
        /// Density is calculated automatically according to gas law.  If the density KSP gives differs by more than 1% from this, then a warning is printed
        /// UPDATE - warning not printed because it causes debug spam on other planets.  Have to implement real gas composition before this can be used
        /// </summary>
        /// <param name="altitude">Altitude, in meters</param>
        /// <param name="body">Which body to use.  Can be null, in which case home body is probably used</param>
        public static EngineThermodynamics AmbientAtAltitude(double altitude, CelestialBody body = null)
        {
            return new EngineThermodynamics(
                P: FlightGlobals.getStaticPressure(altitude, body) * 1000d,
                T: FlightGlobals.getExternalTemperature(altitude, body));

            //if (Math.Abs(FlightGlobals.getAtmDensity(P, T, body) / Rho - 1d) > 0.01d)
            //    if (body != null)
            //        Debug.LogWarning("Ambient density does not obey the gas law on body " + body.name + " at altitude " + altitude.ToString());
            //    else
            //        Debug.LogWarning("Ambient density does not obey the gas law on body at altitude " + altitude.ToString());
        }

        /// <summary>
        /// Get ambient conditions at a particular location
        /// Density is calculated automatically according to gas law.  If the density KSP gives differs by more than 1% from this, then a warning is printed
        /// UPDATE - warning not printed because it causes debug spam on other planets.  Have to implement real gas composition before this can be used
        /// </summary>
        /// <param name="position">3D position at which to get conditions</param>
        /// <param name="body">Which body to use.  Can be null, in which case home body is probably used</param>
        public static EngineThermodynamics AmbientAtLocation(Vector3d position, CelestialBody body)
        {
            return new EngineThermodynamics(
                P: FlightGlobals.getStaticPressure(position, body) * 1000d,
                T: FlightGlobals.getExternalTemperature(position, body));

            //if (Math.Abs(FlightGlobals.getAtmDensity(P, T, body) / Rho - 1d) > 0.01d)
            //    if (body != null)
            //        Debug.LogWarning("Ambient density does not obey the gas law on body " + body.name + " at position " + position.ToString());
            //    else
            //        Debug.LogWarning("Ambient density does not obey the gas law on body at position " + position.ToString());
        }

        /// <summary>
        /// Initialize standard conditions
        /// Standard pressure is 101325 Pa, temperature is 288.15 K
        /// </summary>
        /// <param name="usePlanetarium">If true (and planeterium available), will get conditions from sea level at home body</param>
        public static EngineThermodynamics StandardConditions(bool usePlanetarium = false)
        {
            double P = 101325d;
            double T = 288.15d;
            if (usePlanetarium && Planetarium.fetch != null)
            {
                CelestialBody home = Planetarium.fetch.Home;
                if (home != null)
                {
                    P = home.GetPressure(0d) * 1000d;
                    T = home.GetTemperature(0d);
                }
            }

            return new EngineThermodynamics(P: P, T: T);
        }

        /// <summary>
        /// Initialize vacuum conditions
        /// </summary>
        /// <param name="usePlanetarium">If true (and planetarium available), get space temperature from planet (otherwise get from physics globals)</param>
        /// <returns></returns>
        public static EngineThermodynamics VacuumConditions(bool usePlanetarium = false)
        {
            double T = PhysicsGlobals.SpaceTemperature;

            if (usePlanetarium && Planetarium.fetch != null)
            {
                CelestialBody home = Planetarium.fetch.Home;
                if (home != null)
                {
                    T = home.GetTemperature(home.atmosphereDepth + 1d);
                }
            }

            return new EngineThermodynamics(P: 0d, T: T);
        }

        /// <summary>
        /// Gets the speed of sound in isentropic flow for a given mach number
        /// </summary>
        /// <param name="mach">Mach to get speed of sound for</param>
        /// <returns>Speed of sound, m/s</returns>
        public double SpeedOfSound(double mach)
        {
            return Math.Sqrt(Gamma * R * T / (0.5 * (Gamma - 1d) * mach * mach + 1d));
        }

        /// <summary>
        /// Gets the speed of sound for a given velocity in isentropic flow
        /// </summary>
        /// <param name="velocity">Speed to get the speed of sound for, m/s</param>
        /// <returns>Speed of sound, m/s</returns>
        public double SpeedOfSoundFromVelocity(double velocity)
        {
            double csq = Gamma * R * T - 0.5 * (Gamma - 1d) * velocity * velocity;
            if (csq <= 0.0)
            {
                Debug.LogWarning("[" + this.GetType().Name + "] Got a velocity which is too large for this isentropic flow.  Stack trace: \n" + System.Environment.StackTrace);
                return 0d;
            }
            return Math.Sqrt(csq);
        }

        /// <summary>
        /// Calculates the mach number in isentropic flow froma a given speed
        /// </summary>
        /// <param name="velocity">Speed to calculate mach number from, m/s</param>
        /// <returns>Mach number</returns>
        public double CalculateMach(double velocity)
        {
            return velocity / SpeedOfSoundFromVelocity(velocity);
        }

        /// <summary>
        /// Calculates isentropic mass flow for a given area and mach number
        /// </summary>
        /// <param name="area">Flow cross section, m^2</param>
        /// <param name="mach">Mach number of flow</param>
        /// <returns>Mass flow, kg/s</returns>
        public double CalculateMassFlow(double area, double mach)
        {
            return area * P * Math.Sqrt(Gamma / R / T) * mach * Math.Pow(0.5d * (Gamma - 1d) * mach * mach + 1d, -0.5 * (Gamma + 1d) / (Gamma - 1d));
        }

        /// <summary>
        /// Calculates the flow cross section for a given mass flow and mach
        /// </summary>
        /// <param name="mdot">Mass flow, kg/s</param>
        /// <param name="mach">Mach number</param>
        /// <returns>Cross section area, m^2</returns>
        public double CalculateFlowArea(double mdot, double mach)
        {
            return mdot / P * Math.Sqrt(T * R / Gamma) / mach * Math.Pow(0.5d * (Gamma - 1d) * mach * mach + 1d, 0.5 * (Gamma + 1d) / (Gamma - 1d));
        }

        /// <summary>
        /// Pressure at which isentropic flow will choke
        /// </summary>
        /// <returns>Pressure, pascals</returns>
        public double ChokedPressure()
        {
            return P / Math.Pow(0.5 * (Gamma + 1d), Cp / R);
        }

        /// <summary>
        /// Real temperature at a particular mach number in this isentropic flow.
        /// Note that real temperature is mach number dependent while total (stagnation) temperature is constant.
        /// </summary>
        /// <param name="mach"></param>
        /// <returns>Real temperature</returns>
        public double TemperatureAtMach(double mach)
        {
            double factor = 0.5d * (Gamma - 1d) * mach * mach + 1d;
            return T / factor;
        }

        public override string ToString()
        {
            return string.Concat(
                "EngineThermodynamics(",
                $"T={T:F2}K,",
                $"P={P:F2}Pa,",
                $"Rho={Rho:F2}kg/m3,",
                $"Fuel/Air={Far:F3},",
                $"Cp={Cp:F2}J/kg-K,",
                $"Cv={Cv:F2}J/kg-K,",
                $"Gamma={Gamma:F2},",
                $"R={R:F2}J/kg-K,",
                $"MassRatio={MassRatio:F1}",
                ")");
        }
    }
}
