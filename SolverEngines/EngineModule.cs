using KSP;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;
using System.Reflection;


namespace SolverEngines
{
    /// <summary>
    /// Base module for AJE engines
    /// Derive from this for a real engine; this *will not work* alone.
    /// </summary>
    public class ModuleEnginesSolver : ModuleEnginesFX, IModuleInfo
    {
        // base fields

        [KSPField(isPersistant = false, guiActiveEditor = true)]
        public double Need_Area;

        [KSPField(isPersistant = false, guiActive = true, guiName = "Current Throttle", guiUnits = "%")]
        public int actualThrottle;

        [KSPField(isPersistant = false)]
        public double thrustUpperLimit = double.MaxValue;

        [KSPField(isPersistant = false)]
        public bool multiplyThrustByFuelFrac = true;

        [KSPField]
        public bool useZaxis = true;

        [KSPField]
        public bool useExtTemp = false;

        // Testflight interaction
        public double flowMult = 1d;
        public double ispMult = 1d;

        // engine temp stuff
        // fields
        [KSPField(isPersistant = false)]
        public double maxEngineTemp;
        [KSPField(isPersistant = false, guiActive = true, guiName = "Eng. Internal Temp")]
        public string engineTempString;
        [KSPField]
        public double tempGaugeMin = 0.8d;

        // internals
        protected double tempRatio = 0d, engineTemp = 288.15d;
        protected double lastPropellantFraction = 1d;
        protected VInfoBox overheatBox = null;

        protected List<ModuleAnimateEmissive> emissiveAnims;


        // protected internals
        protected EngineSolver engineSolver = null;

        protected EngineThermodynamics ambientTherm = new EngineThermodynamics();
        protected EngineThermodynamics inletTherm = new EngineThermodynamics();

        protected double areaRatio = 1d;

        protected Vector3 thrustOffset = Vector3.zero;
        protected Quaternion thrustRot = Quaternion.identity;

        protected const double invg0 = 1d / 9.80665d;

        #region Overridable Methods

        virtual public void CreateEngine()
        {
            engineSolver = new EngineSolver();
        }

        virtual public void Start()
        {
            CreateEngine();
            if(ambientTherm == null)
                ambientTherm = new EngineThermodynamics();
            if(inletTherm == null)
                inletTherm = new EngineThermodynamics();
            Need_Area = engineSolver.GetArea();
            Fields["Need_Area"].guiActiveEditor = Need_Area > 0;

            currentThrottle = 0f;
            flameout = false;
            SetUnflameout();
            Fields["fuelFlowGui"].guiUnits = " kg/sec";
        }

        public override void OnStart(PartModule.StartState state)
        {
            base.OnStart(state);
            flameout = false;
            SetUnflameout();
            // set initial params
            engineTemp = 288.15d;
            currentThrottle = 0f;

            if (ambientTherm == null)
                ambientTherm = new EngineThermodynamics();
            if (inletTherm == null)
                inletTherm = new EngineThermodynamics();

            // Get emissives
            emissiveAnims = new List<ModuleAnimateEmissive>();
            int mCount = part.Modules.Count;
            for (int i = 0; i < mCount; ++i)
                if (part.Modules[i] is ModuleAnimateEmissive)
                    emissiveAnims.Add(part.Modules[i] as ModuleAnimateEmissive);
        }

        public override void OnLoad(ConfigNode node)
        {
            base.OnLoad(node);

            thrustTransforms = new List<Transform>(part.FindModelTransforms(thrustVectorTransformName));
            // -- will be done on Start - CreateEngine();
        }

        new virtual public void FixedUpdate()
        {
            realIsp = 0f;
            finalThrust = 0f;
            fuelFlowGui = 0f;
            requestedThrottle = 0f;

            SetUnflameout();
            if (!HighLogic.LoadedSceneIsEditor && !HighLogic.LoadedSceneIsFlight)
                return;

            if (HighLogic.LoadedSceneIsEditor)
            {
                // nothing to see here
                return;
            }
            // so we must be in flight
            if (TimeWarping())
            {
                currentThrottle = 0f;
                return;
            }

            if (EngineIgnited && !flameout)
                requestedThrottle = vessel.ctrlState.mainThrottle;
            UpdateThrottle();

            ambientTherm.FromVesselAmbientConditions(vessel, useExtTemp);

            UpdateFlightCondition(ambientTherm,
                vessel.altitude,
                vessel.srf_velocity,
                vessel.mach,
                vessel.mainBody.atmosphereContainsOxygen);
            CalculateEngineParams();
            UpdateTemp();

            if (finalThrust > 0f)
            {
                // now apply the thrust
                if (part.Rigidbody != null)
                {
                    int tCount = thrustTransforms.Count;
                    float thrustPortion = finalThrust / tCount;
                    Transform t;
                    for (int i = 0; i < tCount; ++i)
                    {
                        t = thrustTransforms[i];
                        Vector3 axis = useZaxis ? -t.forward : -t.up;
                        part.Rigidbody.AddForceAtPosition(thrustRot * (axis * thrustPortion), t.position + t.rotation * thrustOffset, ForceMode.Force);
                    }
                }
                EngineExhaustDamage();

                double thermalFlux = tempRatio * heatProduction * vessel.VesselValues.HeatProduction.value * PhysicsGlobals.InternalHeatProductionFactor * part.thermalMass;
                part.AddThermalFlux(thermalFlux);
            }
            FXUpdate();
            if (flameout || !EngineIgnited)
            {
                SetFlameout();
            }
        }

        public override void FXUpdate()
        {
            part.Effect(directThrottleEffectName, engineSolver.GetFXThrottle());
            part.Effect(spoolEffectName, engineSolver.GetFXSpool());
            part.Effect(runningEffectName, engineSolver.GetFXRunning());
            part.Effect(powerEffectName, engineSolver.GetFXPower());
        }

        virtual protected void UpdateTemp()
        {
            if (tempRatio > 1d)
            {
                FlightLogger.eventLog.Add("[" + FormatTime(vessel.missionTime) + "] " + part.partInfo.title + " melted its internals from heat.");
                part.explode();
            }
            else
                UpdateOverheatBox(tempRatio, tempGaugeMin);
        }

        //ferram4: separate out so function can be called separately for editor sims
        virtual public void UpdateInletEffects(EngineThermodynamics inletTherm, double areaRatio = 1d, double TPR = 1d)
        {
            if (engineSolver == null)
            {
                Debug.Log("*ERROR* EngineSolver on this part is null!");
                return;
            }

            // CopyFrom avoids GC associated with allocating a new one every frame
            // Could probably just assign since this *shouldn't* be changed, but just to be sure
            this.inletTherm.CopyFrom(inletTherm);
            this.areaRatio = areaRatio;

        }

        new virtual public void UpdateThrottle()
        {
            currentThrottle = Mathf.Max(0.00f, currentThrottle);
            actualThrottle = Mathf.RoundToInt(currentThrottle * 100f);
        }

        virtual public void UpdateFlightCondition(EngineThermodynamics ambientTherm, double altitude, Vector3d vel, double mach, bool oxygen)
        {
            // In flight, these are the same and this will just return
            this.ambientTherm.CopyFrom(ambientTherm);

            engineSolver.SetEngineState(EngineIgnited, lastPropellantFraction);
            engineSolver.SetFreestreamAndInlet(ambientTherm, inletTherm, altitude, mach, vel, oxygen);
            engineSolver.CalculatePerformance(areaRatio, currentThrottle, flowMult, ispMult);
        }

        virtual public void CalculateEngineParams()
        {
            SetEmissive(engineSolver.GetEmissive());
            // Heat
            engineTemp = engineSolver.GetEngineTemp();
            tempRatio = engineTemp / maxEngineTemp;
            engineTempString = engineTemp.ToString("N0") + " K / " + maxEngineTemp.ToString("n0") + " K";

            if (EngineIgnited) // slow, so only do this if we have to.
                UpdatePropellantStatus();

            double thrustIn = engineSolver.GetThrust(); //in N
            double isp = engineSolver.GetIsp();
            double producedThrust = 0d;
            double fuelFlow = engineSolver.GetFuelFlow();
            double massFlow = 0d;

            if (double.IsNaN(thrustIn) || !engineSolver.GetRunning())
            {
                if (EngineIgnited && (currentThrottle > 0f || throttleLocked))
                {
                    Flameout(engineSolver.GetStatus());
                }
                realIsp = 0f;
                fuelFlowGui = 0f;
                producedThrust = 0d;
            }
            else
            {
                // calc flow
                double vesselValue = vessel.VesselValues.FuelUsage.value;
                if (vesselValue == 0d)
                    vesselValue = 1d;
                fuelFlow *= vesselValue;

                massFlow = fuelFlow * 0.001d * TimeWarp.fixedDeltaTime; // in tons

                if (CheatOptions.InfiniteFuel == true)
                {
                    lastPropellantFraction = 1d;
                    UnFlameout();
                }
                else
                {
                    lastPropellantFraction = RequestPropellant(massFlow);
                }

                // set produced thrust
                if (multiplyThrustByFuelFrac)
                {
                    thrustIn *= lastPropellantFraction;
                    fuelFlow *= lastPropellantFraction;
                }
                producedThrust = thrustIn * 0.001d; // to kN
                // soft cap
                if (producedThrust > thrustUpperLimit)
                    producedThrust = thrustUpperLimit + (producedThrust - thrustUpperLimit) * 0.1d;

                // set fuel flow
                if (fuelFlow > 1000d)
                {
                    fuelFlow *= 0.001d;
                    Fields["fuelFlowGui"].guiUnits = " ton/s";
                }
                else
                    Fields["fuelFlowGui"].guiUnits = " kg/s";
                fuelFlowGui = (float)fuelFlow;


                realIsp = (float)isp;
            }

            finalThrust = (float)producedThrust * vessel.VesselValues.EnginePower.value;
        }


        new virtual public float normalizedOutput
        {
            get
            {
                // should this just be actualThrottle ?
                // or should we get current thrust divided max possible thrust here?
                // or what? FIXME
                return finalThrust / maxThrust;
            }
        }
        new virtual public string GetModuleTitle()
        {
            return "EngineSolver Engine";
        }
        new virtual public string GetPrimaryField()
        {
            return "";
        }

        public override string GetInfo()
        {
            return "<b>Unconfigured</b>";
        }

        #endregion

        #region Base Methods
        protected void SetFlameout()
        {
            CLAMP = 0f;
            flameoutBar = float.MaxValue;
        }
        protected void SetUnflameout()
        {
            // hack to get around my not making CanStart() virtual
            CLAMP = float.MaxValue;
            flameoutBar = 0f;
        }

        protected void SetEmissive(double val)
        {
            int eCount = emissiveAnims.Count;
            for (int i = 0; i < eCount; ++i)
                emissiveAnims[i].SetState(val);
        }

        protected void UpdateOverheatBox(double val, double minVal)
        {
            if (val >= (minVal - 0.00001d))
            {
                if (overheatBox == null)
                {
                    overheatBox = part.stackIcon.DisplayInfo();
                    overheatBox.SetMsgBgColor(XKCDColors.DarkRed.A(0.6f));
                    overheatBox.SetMsgTextColor(XKCDColors.OrangeYellow.A(0.6f));
                    overheatBox.SetMessage("Eng. Int.");
                    overheatBox.SetProgressBarBgColor(XKCDColors.DarkRed.A(0.6f));
                    overheatBox.SetProgressBarColor(XKCDColors.OrangeYellow.A(0.6f));
                }
                double scalar = 1d / (1d - minVal);
                double scaleFac = 1f - scalar;
                float gaugeMin = (float)(scalar * minVal + scaleFac);
                overheatBox.SetValue(Mathf.Clamp01((float)(val * scalar + scaleFac)), gaugeMin, 1.0f);
            }
            else
            {
                if (overheatBox != null)
                {
                    part.stackIcon.RemoveInfo(overheatBox);
                    overheatBox = null;
                }
            }
        }

        protected static string FormatTime(double time)
        {
            int iTime = (int)time % 3600;
            int seconds = iTime % 60;
            int minutes = (iTime / 60) % 60;
            int hours = (iTime / 3600);
            return hours.ToString("D2")
                + ":" + minutes.ToString("D2") + ":" + seconds.ToString("D2");
        }

        #endregion
    }
}