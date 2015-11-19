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
    public abstract class ModuleEnginesSolver : ModuleEnginesFX, IModuleInfo, IEngineStatus
    {
        // base fields

        [KSPField(isPersistant = false, guiActiveEditor = true, guiFormat = "F3")]
        public float Need_Area;

        [KSPField(isPersistant = false, guiActive = true, guiName = "Current Throttle", guiFormat = "N2", guiUnits = "%")]
        public float actualThrottle;

        [KSPField(isPersistant = false)]
        public double thrustUpperLimit = double.MaxValue;

        [KSPField(isPersistant = false)]
        public bool multiplyThrustByFuelFrac = true;

        [KSPField]
        public bool useZaxis = true;

        [KSPField]
        public bool useExtTemp = false;

        [KSPField]
        public float autoignitionTemp = float.PositiveInfinity;

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

        protected List<ModuleAnimateHeat> emissiveAnims;


        // protected internals
        protected EngineSolver engineSolver = null;

        protected EngineThermodynamics ambientTherm = new EngineThermodynamics();
        protected EngineThermodynamics inletTherm = new EngineThermodynamics();

        protected double areaRatio = 1d;

        protected Vector3 thrustOffset = Vector3.zero;
        protected Quaternion thrustRot = Quaternion.identity;

        protected const double invg0 = 1d / 9.80665d;

        // Engine fitting stuff
        protected List<EngineParameterInfo> engineFitParameters = new List<EngineParameterInfo>();

        #region Overridable Methods

        public override void OnAwake()
        {
            base.OnAwake();

            engineFitParameters.Clear();

            FieldInfo[] fields = this.GetType().GetFields();
            foreach (FieldInfo field in fields)
            {
                object[] attributes = field.GetCustomAttributes(true);
                foreach (object attribute in attributes)
                {
                    if (attribute is EngineParameter)
                        engineFitParameters.Add(new EngineParameterInfo(this, field, attribute as EngineParameter));
                }
            }
        }

        virtual public void CreateEngine()
        {
            throw new NotImplementedException("Must be implemented to create the correct engine solver");
        }

        virtual public void CreateEngineIfNecessary()
        {
            if (engineSolver == null)
                CreateEngine();
        }

        virtual public void Start()
        {
            CreateEngine();
            if (ambientTherm == null)
                ambientTherm = new EngineThermodynamics();
            if (inletTherm == null)
                inletTherm = new EngineThermodynamics();
            Need_Area = RequiredIntakeArea();
            Fields["Need_Area"].guiActiveEditor = Need_Area > 0f;

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
            emissiveAnims = new List<ModuleAnimateHeat>();
            int mCount = part.Modules.Count;
            for (int i = 0; i < mCount; ++i)
                if (part.Modules[i] is ModuleAnimateHeat)
                    emissiveAnims.Add(part.Modules[i] as ModuleAnimateHeat);

            FitEngineIfNecessary();
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
                vessel.mainBody.atmosphereContainsOxygen,
                CheckTransformsUnderwater());
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

                double thermalFlux = tempRatio * tempRatio * heatProduction * vessel.VesselValues.HeatProduction.value * PhysicsGlobals.InternalHeatProductionFactor * part.thermalMass;
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
            if (tempRatio > 1d && !CheatOptions.IgnoreMaxTemperature)
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

        public override void UpdateThrottle()
        {
            currentThrottle = Mathf.Max(0.00f, currentThrottle);
            actualThrottle = currentThrottle * 100f;
        }

        virtual public void UpdateFlightCondition(EngineThermodynamics ambientTherm, double altitude, Vector3d vel, double mach, bool oxygen, bool underwater)
        {
            // In flight, these are the same and this will just return
            this.ambientTherm.CopyFrom(ambientTherm);

            engineSolver.SetEngineState(EngineIgnited, lastPropellantFraction);
            engineSolver.SetFreestreamAndInlet(ambientTherm, inletTherm, altitude, mach, vel, oxygen, underwater);
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

                // If engine is not ignited, then UpdatePropellantStatus() will not be called so no point in updating propellant fraction
                if (EngineIgnited)
                {
                    if (flameout && lastPropellantFraction <= 0d && PropellantAvailable() && CanAutoRestart()) // CanAutoRestart() checks allowRestart
                    {
                        lastPropellantFraction = 1d;
                        UnFlameout();
                    }
                }
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
                    if (massFlow > 0d)
                    {
                        lastPropellantFraction = RequestPropellant(massFlow);
                    }
                    else
                    {
                        lastPropellantFraction = PropellantAvailable() ? 1d : 0d;
                    }
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

        virtual public bool PropellantAvailable()
        {
            for (int i = 0; i < propellants.Count; i++)
            {
                if (propellants[i].totalResourceAvailable <= 0d)
                {
                    return false;
                }
            }
            return true;
        }

        new public float normalizedOutput
        {
            get
            {
                // for now changed to throttle, but clamped...
                float tmpOutput = 0.25f + currentThrottle * 0.75f;

                //current throttle should never allow it to go out of 0 - 1 bounds, but if it does....
                if (tmpOutput < 0)
                    tmpOutput = 0;
                else if (tmpOutput > 1)
                    tmpOutput = 1;
                
                return tmpOutput;
            }
        }
        new public bool isOperational
        {
            get
            {
                if (engineSolver != null)
                    return engineSolver.GetRunning();
                return false;
            }
        }

        virtual public float RequiredIntakeArea()
        {
            return (float)engineSolver.GetArea();
        }

        virtual public bool CanAutoRestart()
        {
            return allowRestart && EngineIgnited && autoignitionTemp >= 0f && engineTemp > autoignitionTemp;
        }

        new protected bool CheckTransformsUnderwater()
        {
            if (!vessel || !vessel.mainBody.ocean)
                return false;

            for (int i = 0; i < thrustTransforms.Count; i++)
            {
                if (FlightGlobals.getAltitudeAtPos(thrustTransforms[i].position) < 0f)
                    return true;
            }
            return false;
        }

        #region Engine Fitting

        virtual public bool ShouldFitParameter(EngineParameterInfo info)
        {
            if (info.Param is EngineFitResult)
                return true;
            else
                return false;
        }

        virtual public void FitEngineIfNecessary()
        {
            bool doFit = false;

            foreach (EngineParameterInfo entry in engineFitParameters)
            {
                if (ShouldFitParameter(entry))
                {
                    doFit = true;
                    break;
                }

            }

            // No parameters can be fit
            if (!doFit)
                return;

            doFit = false;

            ConfigNode node = EngineDatabase.GetNodeForEngine(this);
            if (node != null)
            {
                doFit |= EngineDatabase.PluginUpdateCheck(this, node);

                // Check for changes
                foreach (EngineParameterInfo entry in engineFitParameters)
                {
                    // Don't check things we're going to fit
                    if (ShouldFitParameter(entry))
                        continue;

                    if (!entry.EqualsValueInNode(node))
                    {
                        doFit = true;
                        break;
                    }
                }
                if (!doFit && node != null)
                {
                    Debug.Log("[" + this.GetType().Name + "] Reading engine params from cache for engine " + part.name);

                    CreateEngineIfNecessary();

                    foreach (EngineParameterInfo entry in engineFitParameters)
                    {
                        // Only copy things that would be fitted
                        if (ShouldFitParameter(entry))
                            entry.SetValueFromNode(node);
                    }
                    PushFitParamsToSolver();
                }
            }
            else
            {
                doFit = true;
            }

            if (doFit)
            {
                Debug.Log("[" + this.GetType().Name + "] Fitting params for engine " + part.name);

                CreateEngineIfNecessary();

                // Copy valid fit results from database - they might still be correct
                if (node != null)
                {
                    foreach (EngineParameterInfo entry in engineFitParameters)
                    {
                        // Only copy things that would be fitted
                        if (ShouldFitParameter(entry))
                            entry.SetValueFromNode(node);
                    }
                }

                // Make sure everything has the correct value
                PushFitParamsToSolver();
                DoEngineFit();

                ConfigNode newNode = new ConfigNode();

                foreach (EngineParameterInfo entry in engineFitParameters)
                {
                    newNode.SetValue(entry.Name, entry.GetValueStr(), true);
                }

                EngineDatabase.SetNodeForEngine(this, newNode);
            }
        }

        virtual public void DoEngineFit()
        {
            throw new NotImplementedException();
        }

        virtual public void PushFitParamsToSolver()
        {
            throw new NotImplementedException();
        }

        #endregion

        #region Events and Actions
        public override void Activate()
        {
            base.Activate();
            
            flameout = false;

            UpdatePropellantStatus();
            if (PropellantAvailable())
                lastPropellantFraction = 1d;
            else
                lastPropellantFraction = 0d;
        }
        
        #endregion

        #region Info
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
