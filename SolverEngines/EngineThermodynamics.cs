using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;
using KSP;

namespace SolverEngines
{
    // Right now this only works for a mixture of mostly air and some fuel (presumably kerosene-based)
    // To work well for other engine types, it should be changed to be composed of an arbitrary mixture of exhaust products, and may or may not have air
    // The exhaust products would presumably have their thermodynamic parameters read from a cfg somewhere, including possible temperature dependence

    // Please note that all conditions (pressure, temperature, density) are total conditions as opposed to real conditions

    // Also note that this is still WIP and likely to change in the future
    public class EngineThermodynamics
    {
        private double _T; // Total temperature
        private double _Far;
        private double _FF;

        /// <summary>
        /// Total (stagnation) pressure
        /// </summary>
        public double P { get; set; } // Total pressure
        /// <summary>
        /// Total (stagnation) temperature
        /// </summary>
        public double T
        {
            get
            {
                return _T;
            }
            set
            {
                _T = value;
                Recalculate();
            }
        }
        /// <summary>
        /// Total (stagnation) density
        /// </summary>
        public double Rho { get; set; }
        /// <summary>
        /// Specific heat capacity at constant pressure, joules / kg / K
        /// </summary>
        public double Cp { get; private set; }
        /// <summary>
        /// Specific heat capacity at constant volume, joules / kg / K
        /// </summary>
        public double Cv { get; private set; }
        /// <summary>
        /// Specific heat capacity ratio Cp / Cv
        /// </summary>
        public double Gamma { get; private set; }
        /// <summary>
        /// Specific gas constant
        /// Equal to (boltzmann constant) / (molecular mass) = (ideal gas constant) / (molar mass)
        /// </summary>
        public double R { get; private set; }
        /// <summary>
        /// Fuel-air ratio
        /// </summary>
        public double Far
        {
            get
            {
                return _Far;
            }
            set
            {
                _Far = value;
                _FF = _Far / (_Far + 1);
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
                return _FF;
            }
            set
            {
                _FF = value;
                _Far = _FF / (_FF - 1);
                Recalculate();
            }
        }
        /// <summary>
        /// Ratio of mass flow relative to some reference flow
        /// This is increased when fuel is added
        /// </summary>
        public double MassRatio { get; set; }

        public EngineThermodynamics()
        {
            Zero();
        }

        /// <summary>
        /// Sets all values to their defaults
        /// </summary>
        public void Zero()
        {
            P = 0d;
            _T = 0d;
            Rho = 0d;
            _Far = 0d;
            _FF = 0d;
            Cp = 0d;
            Cv = 0d;
            Gamma = 0d;
            R = 0d;
            MassRatio = 1d;
        }

        /// <summary>
        /// Copies all values from another instance
        /// </summary>
        /// <param name="t">Thermodynamics to copy from</param>
        public void CopyFrom(EngineThermodynamics t)
        {
            if (this == t) // lol
                return;
            P = t.P;
            _T = t.T;
            Rho = t.Rho;
            _Far = t.Far;
            _FF = t.FF;
            Cp = t.Cp;
            Cv = t.Cv;
            Gamma = t.Gamma;
            R = t.R;
            MassRatio = t.MassRatio;
        }

        private void Recalculate()
        {
            double tDelt = Math.Max((T - 300d) * 0.0005d, 0d);
            Cp = 1004.5d + 250d * tDelt * (1d + 10d * FF);
            Gamma = 1.4d - 0.1d * tDelt * (1d + FF);
            Cv = Cp / Gamma;
            R = Cv * (Gamma - 1d);
            Rho = P / R / T;
        }

        // This odd system of copying from another Thermodynamics and then modifying is so that new ones don't have to be allocated every frame, which is bad for GC

        /// <summary>
        /// Takes input conditions and performs an adiabatic expansion or compression, storing the result in self
        /// </summary>
        /// <param name="t">Input thermodynamics</param>
        /// <param name="tempRatio">Total temperature ratio for this process</param>
        /// <param name="efficiency">Adiabatic efficiency for this process</param>
        /// <returns>Work done in the process, per unit reference mass.  Positive if the process does work, negative if the process requires work.</returns>
        public double FromAdiabaticProcessWithTempRatio(EngineThermodynamics t, double tempRatio, double efficiency = 1d)
        {
            CopyFrom(t);
            double oldT = t.T;
            double oldCp = t.Cp;
            _T *= tempRatio;
            //double pressureExponent = Gamma / (Gamma - 1d) * efficiency;
            double pressureExponent = Cp / R * efficiency; // One less step
            P *= Math.Pow(tempRatio, pressureExponent);
            Rho *= Math.Pow(tempRatio, pressureExponent - 1d);
            Recalculate();

            return oldCp * (oldT - T) * MassRatio;
        }

        /// <summary>
        /// Takes input conditions and performs an adiabatic expansion or compression, storing the result in self
        /// </summary>
        /// <param name="t">Input thermodynamics</param>
        /// <param name="pressureRatio">Total pressure ratio for this process</param>
        /// <param name="efficiency">Adiabatic efficiency for this process</param>
        /// <returns>Work done in the process, per unit reference mass.  Positive if the process does work, negative if the process requires work.</returns>
        public double FromAdiabaticProcessWithPressureRatio(EngineThermodynamics t, double pressureRatio, double efficiency = 1d)
        {
            CopyFrom(t);
            double oldT = t.T;
            double oldCp = t.Cp;
            //_T *= Math.Pow(pressureRatio, (Gamma - 1d) / Gamma / efficiency);
            // equivalent: tempExponent = (Gamma - 1) / Gamma
            double tempExponent = R / Cp / efficiency;
            _T *= Math.Pow(pressureRatio, tempExponent);
            P *= pressureRatio;
            Rho *= Math.Pow(pressureRatio, 1d - tempExponent);
            Recalculate();

            return oldCp * (oldT - T) * MassRatio;
        }

        /// <summary>
        /// Takes input conditions and performs an adiabatic expansion or compression, storing the result in self
        /// Positive work corresponds to work being done on the gas (i.e. compression), and negative work corresponds to the gas doing work (i.e. expansion)
        /// </summary>
        /// <param name="t">Input thermodynamics</param>
        /// <param name="work">Work to do in this process, per unit reference mass (controlled by MassRatio).  Positive = compression, negative = expansion</param>
        /// <param name="efficiency">Adiabatic efficiency for this process</param>
        public void FromAdiabaticProcessWithWork(EngineThermodynamics t, double work, double efficiency = 1d)
        {
            CopyFrom(t);
            _T += work / Cp / MassRatio;
            double pressureExponent = t.Cp / t.R * efficiency;
            P *= Math.Pow(T / t.T, pressureExponent);
            Recalculate();
        }

        /// <summary>
        /// Changes the reference frame of the gas, adjusting total conditions to account for the change
        /// </summary>
        /// <param name="t">Input thermodynamics</param>
        /// <param name="speed">Speed to change reference by.  Positive = speed up, negative = slow down</param>
        public void FromChangeReferenceFrame(EngineThermodynamics t, double speed, bool forward=true)
        {
            FromAdiabaticProcessWithWork(t, speed * speed / 2d * Math.Sign(speed));
        }

        /// <summary>
        /// Changes the reference frame of the gas, adjusting total conditions to account for the change
        /// </summary>
        /// <param name="t">Input thermodynamics</param>
        /// <param name="mach">Mach to change reference by.  Positive = speed up, negative = slow down</param>
        public void FromChangeReferenceFrameMach(EngineThermodynamics t, double mach, bool forward = true)
        {
            CopyFrom(t);
            _T += 0.5 * (Gamma - 1) * mach * mach * Math.Sign(mach);
            double pressureExponent = t.Cp / t.R;
            P *= Math.Pow(T / t.T, pressureExponent);
            Recalculate();
        }

        /// <summary>
        /// Add fuel to a gas (and burn it)
        /// </summary>
        /// <param name="t">Input thermodynamics</param>
        /// <param name="maxTemp">Maximum allowed temperature</param>
        /// <param name="heatOfFuel">Heat of combustion of fuel, measured in joules/kg/K</param>
        /// <param name="throttle">Throttle for adding fuel.  0 corresponds to no fuel added, 1 corresponds to max fuel added</param>
        /// <param name="maxFar">Maximum fuel-air ratio (usually defined by stoichiometry).  This will be handled automatically in the future</param>
        public void FromAddFuelToTemperature(EngineThermodynamics t, double maxTemp, double heatOfFuel, double throttle = 1d, double maxFar = 0d)
        {
            System.Diagnostics.Debug.Assert(throttle >= 0d && throttle <= 1d);
            CopyFrom(t);
            // Max fuel-air ratio - don't want to inject more fuel than can be burnt in air
            if (maxFar > 0d)
                maxTemp = Math.Min(maxTemp, (maxFar - Far) * heatOfFuel / Cp + T);
            double delta = (maxTemp - T) * throttle;

            _T += delta;
            double addedFuel = delta * Cp / heatOfFuel;
            // Order is important here - want old Far in this line not new Far
            MassRatio += addedFuel / (1d + Far);
            Far += addedFuel;
            Rho = P / R / T;
        }

        /// <summary>
        /// Mix two or more streams
        /// </summary>
        /// <param name="streams">Gas streams to mix.  MassRatio will be respected in mixing</param>
        public void FromMixStreams(params EngineThermodynamics[] streams)
        {
            Zero();
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

            _T = temperatureSum / temperatureWeightSum;
            P = pressureSum / MassRatio;
            MassRatio = newMassRatio;
            Far = fuelSum / fuelWeightSum; // Recalculates
        }

        /// <summary>
        /// Get ambient conditions for a vessel
        /// Density is calculated automatically according to gas law.  If the density KSP gives differs by more than 1% from this, then a warning is printed
        /// </summary>
        /// <param name="v">Vessel to find conditions for</param>
        /// <param name="useExternalTemp">If true, use externalTemperature (nominally shock temp).  Otherwise use atmosphericTemperature (ambient temp)</param>
        public void FromVesselAmbientConditions(Vessel v, bool useExternalTemp = false)
        {
            Zero();
            P = v.staticPressurekPa * 1000d;
            Rho = v.atmDensity;
            if (useExternalTemp)
                _T = v.externalTemperature;
            else
                _T = v.atmosphericTemperature;

            Far = 0d; // Recalculates, so no need to do by hand
            if (Math.Abs(v.atmDensity / Rho - 1d) > 0.01d)
                Debug.LogWarning("Ambient density does not obey the gas law for vessel " + v.name);
        }

        /// <summary>
        /// Get ambient conditions at altitude
        /// Density is calculated automatically according to gas law.  If the density KSP gives differs by more than 1% from this, then a warning is printed
        /// </summary>
        /// <param name="altitude">Altitude, in meters</param>
        /// <param name="body">Which body to use.  Can be null, in which case home body is probably used</param>
        public void FromAmbientAtAltitude(double altitude, CelestialBody body = null)
        {
            Zero();
            P = FlightGlobals.getStaticPressure(altitude, body) * 1000d;
            _T = FlightGlobals.getExternalTemperature(altitude, body);
            Far = 0d; // Recalculates, so no need to do by hand

            if (Math.Abs(FlightGlobals.getAtmDensity(P, T, body) / Rho - 1d) > 0.01d)
                if (body != null)
                    Debug.LogWarning("Ambient density does not obey the gas law on body " + body.name + " at altitude " + altitude.ToString());
                else
                    Debug.LogWarning("Ambient density does not obey the gas law on body at altitude " + altitude.ToString());
        }

        /// <summary>
        /// Get ambient conditions at a particular location
        /// Density is calculated automatically according to gas law.  If the density KSP gives differs by more than 1% from this, then a warning is printed
        /// </summary>
        /// <param name="position">3D position at which to get conditions</param>
        /// <param name="body">Which body to use.  Can be null, in which case home body is probably used</param>
        public void FromAmbientAtLocation(Vector3d position, CelestialBody body)
        {
            Zero();
            P = FlightGlobals.getStaticPressure(position, body) * 1000d;
            _T = FlightGlobals.getExternalTemperature(position, body);
            Far = 0d; // Recalculates, so no need to do by hand

            if (Math.Abs(FlightGlobals.getAtmDensity(P, T, body) / Rho - 1d) > 0.01d)
                if (body != null)
                    Debug.LogWarning("Ambient density does not obey the gas law on body " + body.name + " at position " + position.ToString());
                else
                    Debug.LogWarning("Ambient density does not obey the gas law on body at position " + position.ToString());
        }

        /// <summary>
        /// Initialize ambient conditions
        /// </summary>
        /// <param name="pressurekPa">Pressure, in kPa</param>
        /// <param name="temperatureK">Temperature, in kelvins</param>
        /// <param name="Far">Fuel-air ratio</param>
        public void FromAmbientConditions(double pressurekPa, double temperatureK, double Far = 0d)
        {
            Zero();
            P = pressurekPa * 1000d;
            _T = temperatureK;
            this.Far = Far; // Recalculates, so no need to do by hand
        }

        /// <summary>
        /// Initialize standard conditions
        /// Standard pressure is 101325 Pa, temperature is 288.15 K
        /// </summary>
        /// <param name="usePlanetarium">If true (and planeterium available), will get conditions from sea level at home body</param>
        public void FromStandardConditions(bool usePlanetarium = false)
        {
            Zero();
            P = 101325d;
            _T = 288.15d;
            if (usePlanetarium && Planetarium.fetch != null)
            {
                CelestialBody home = Planetarium.fetch.Home;
                if (home != null)
                {
                    P = home.GetPressure(0d) * 1000d;
                    _T = home.GetTemperature(0d);
                }
            }

            Far = 0d;
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

        public override string ToString()
        {
            string returnString = "";
            returnString += "T: " + EnginesGUI.GUIUnitsSettings.TemperatureUnits.Format(T);
            returnString += " P: " + EnginesGUI.GUIUnitsSettings.PressureUnits.Format(P);
            returnString += " Rho: " + Rho.ToString("F2");
            returnString += "\n FF: " + FF.ToString("F3");
            returnString += "\n Cp: " + Cp.ToString("F2");
            returnString += " Cv: " + Cv.ToString("F2");
            returnString += "\nGamma: " + Gamma.ToString("F2");
            returnString += " R: " + R.ToString("F2");
            return returnString;
        }
    }
}
