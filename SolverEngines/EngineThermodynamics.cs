using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace SolverEngines
{
    // Right now this only works for a mixture of mostly air and some fuel (presumably kerosene-based)
    // To work well for other engine types, it should be changed to be composed of an arbitrary mixture of exhaust products, and may or may not have air
    // The exhaust products would presumably have their thermodynamic parameters read from a cfg somewhere, including possible temperature dependence
    public class EngineThermodynamics
    {
        private double _T; // Total temperature
        private double _Far;
        private double _FF;

        public double P { get; set; } // Total pressure
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
        public double Rho { get; set; }
        public double Cp { get; private set; }
        public double Cv { get; private set; }
        public double Gamma { get; private set; }
        public double R { get; private set; }
        public double Vs { get; private set; } // Sound speed
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

        public EngineThermodynamics()
        {
            P = 0;
            _T = 0;
            Rho = 0;
            _Far = 0;
            _FF = 0;
            Cp = 0;
            Cv = 0;
            Gamma = 0;
            R = 0;
        }

        public void Zero()
        {
            P = 0;
            _T = 0;
            Rho = 0;
            _Far = 0;
            _FF = 0;
            Cp = 0;
            Cv = 0;
            Gamma = 0;
            R = 0;
        }

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
            Gamma =t.Gamma;
            R = t.R;
        }

        private void Recalculate()
        {
            double tDelt = Math.Max((T - 300d) * 0.0005d, 0d);
            Cp = 1004.5d + 250d * tDelt * (1d + 10d * FF);
            Gamma = 1.4d - 0.1d * tDelt * (1d + FF);
            Cv = Cp / Gamma;
            R = Cv * (Gamma - 1d);
            Vs = Math.Sqrt(Gamma * R * T);
        }

        // This odd system of copying from another Thermodynamics and then modifying is so that new ones don't have to be allocated every frame, which is bad for GC

        public void FromAdiabaticProcessWithTempRatio(EngineThermodynamics t, double tempRatio, double efficiency = 1d)
        {
            CopyFrom(t);
            _T *= tempRatio;
            //double pressureExponent = Gamma / (Gamma - 1d) * efficiency;
            double pressureExponent = Cp / R * efficiency; // One less step
            P *= Math.Pow(tempRatio, pressureExponent);
            Rho *= Math.Pow(tempRatio, pressureExponent - 1d);
            Recalculate();
        }

        public void FromAdiabaticProcessWithPressureRatio(EngineThermodynamics t, double pressureRatio, double efficiency = 1d)
        {
            CopyFrom(t);
            //_T *= Math.Pow(pressureRatio, (Gamma - 1d) / Gamma / efficiency);
            double tempExponent = R / Cp / efficiency;
            _T *= Math.Pow(pressureRatio, tempExponent);
            P *= pressureRatio;
            Rho *= Math.Pow(pressureRatio, 1d - tempExponent);
            Recalculate();
        }

        public void FromAdiabaticProcessWithWork(EngineThermodynamics t, double work, double efficiency = 1d)
        {
            CopyFrom(t);
            _T += work / Cp;
            double pressureExponent = t.Cp / t.R * efficiency;
            P *= Math.Pow(T / t.T, pressureExponent);
            Rho *= Math.Pow(T / t.T, pressureExponent - 1d);
            Recalculate();
        }

        public void FromChangeReferenceFrame(EngineThermodynamics t, double speed, bool forward=true)
        {
            FromAdiabaticProcessWithWork(t, speed * speed / 2d * (forward? 1d : -1d));
        }

        public void FromAddFuelToTemperature(EngineThermodynamics t, double maxTemp, double heatOfFuel, float throttle = 1f, float maxFar = 0f)
        {
            CopyFrom(t);
            double delta = (maxTemp - T) * throttle;

            // Max fuel-air ratio - don't want to inject more fuel than can be burnt in air
            if (maxFar > 0f)
            {
                if (Far >= maxFar)
                    delta = 0f;
                else
                    delta = Math.Max(delta, (maxFar - Far) * heatOfFuel / Cp);
            }
            _T += delta;
            Far += delta * Cp / heatOfFuel;
            Rho = P / R / (T + delta);
            Recalculate();
        }

        public void FromVesselAmbientConditions(Vessel v, bool useExternalTemp = false)
        {
            P = v.staticPressurekPa * 1000d;
            Rho = v.atmDensity;
            if (useExternalTemp)
                _T = v.externalTemperature;
            else
                _T = v.atmosphericTemperature;
            Far = 0d; // Recalculates, so no need to do by hand
        }

        public void FromAmbientAtAltitude(double altitude, CelestialBody body = null)
        {
            P = FlightGlobals.getStaticPressure(altitude, body) * 1000d;
            _T = FlightGlobals.getExternalTemperature(altitude, body);
            Rho = FlightGlobals.getAtmDensity(P, T, body);
            Far = 0d; // Recalculates, so no need to do by hand
        }

        public void FromAmbientAtLocation(Vector3d position, CelestialBody body)
        {
            P = FlightGlobals.getStaticPressure(position, body) * 1000d;
            _T = FlightGlobals.getExternalTemperature(position, body);
            Rho = FlightGlobals.getAtmDensity(P, T, body);
            Far = 0d; // Recalculates, so no need to do by hand
        }

        public void FromAmbientConditions(double pressurekPa, double temperatureK, double density)
        {
            P = pressurekPa * 1000d;
            _T = temperatureK;
            Rho = density;
            Far = 0d; // Recalculates, so no need to do by hand
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
