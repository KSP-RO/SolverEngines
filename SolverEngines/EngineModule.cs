using System;
using System.Collections.Generic;
using UnityEngine;
using KSP.UI.Screens;
using SolverEngines.EngineFitting;
using UnityEngine.Profiling;

namespace SolverEngines
{
    /// <summary>
    /// Base module for AJE engines
    /// Derive from this for a real engine; this *will not work* alone.
    /// </summary>
    public abstract class ModuleEnginesSolver : ModuleEnginesFX, IModuleInfo, IEngineStatus, IEngineIdentifier
    {
        // base fields

        [KSPField(guiActiveEditor = true, guiFormat = "F3")]
        public float Need_Area;

        [KSPField(guiActive = true, guiName = "Current Throttle", guiFormat = "N2", guiUnits = "%")]
        public float actualThrottle;

        [KSPField(guiActive = true, guiName = "Mass Flow", guiUnits = " kg/s", guiFormat = "F5")]
        public float massFlowGui;

        [KSPField]
        public double thrustUpperLimit = double.MaxValue;

        [KSPField]
        public bool multiplyThrustByFuelFrac = true;

        [KSPField]
        public bool useZaxis = true;

        [KSPField]
        public bool useExtTemp = false;

        [KSPField]
        public float autoignitionTemp = float.PositiveInfinity;

        // Just in case we need a double set of multipliers beyond the stock engine module's
        public double flowMult = 1d;
        public double ispMult = 1d;

        // engine temp stuff
        // fields
        [KSPField]
        public double maxEngineTemp;
        [KSPField(guiActive = true, guiName = "Eng. Internal Temp", guiFormat = "N0")]
        public double engineTemp = 288.15d;
        [KSPField]
        public double tempGaugeMin = 0.8d;

        // internals
        protected double tempRatio = 0d;

        public double GetEngineTemp => engineTemp;

        protected double lastPropellantFraction = 1d;
        protected ProtoStageIconInfo overheatBox = null;

        protected List<ModuleAnimateHeat> emissiveAnims;

        protected List<ThrustTransformInfo> thrustTransformInfos;


        // protected internals
        protected EngineSolver engineSolver = null;

        protected EngineThermodynamics ambientTherm = new EngineThermodynamics();
        protected EngineThermodynamics inletTherm = new EngineThermodynamics();

        protected double areaRatio = 1d;

        protected Vector3 thrustOffset = Vector3.zero;
        protected Quaternion thrustRot = Quaternion.identity;

        protected const double invg0 = 1d / 9.80665d;

        protected bool flowKG = true;

        #region Properties

        public virtual string EnginePartName => part.name;
        public virtual string EngineTypeName => moduleName;
        public virtual string EngineID => engineID;
        public virtual string EngineConfigName => string.Empty;

        #endregion

        #region Overridable Methods

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
            Need_Area = RequiredIntakeArea();
            Fields[nameof(Need_Area)].guiActiveEditor = Need_Area > 0f;
            currentThrottle = 0f;
            flameout = false;
            SetUnflameout();
            Fields[nameof(fuelFlowGui)].guiActive = false;
            Fields[nameof(massFlowGui)].guiUnits = " kg/s";
            flowKG = true;
            Fields[nameof(engineTemp)].guiUnits = $" K / {maxEngineTemp:N0} K";
        }

        public override void OnStart(PartModule.StartState state)
        {
            base.OnStart(state);
            flameout = false;
            SetUnflameout();
            // set initial params
            engineTemp = 288.15d;
            currentThrottle = 0f;

            InitializeThrustTransforms();

            // Get emissives
            emissiveAnims = new List<ModuleAnimateHeat>();
            foreach (var pm in part.Modules)
                if (pm is ModuleAnimateHeat)
                    emissiveAnims.Add(pm as ModuleAnimateHeat);

            CreateEngineIfNecessary();
        }

        public override void OnLoad(ConfigNode node)
        {
            base.OnLoad(node);

            if (node.HasNode("THRUST_TRANSFORM"))
            {
                thrustTransformInfos = new List<ThrustTransformInfo>();

                foreach (ConfigNode trfNode in node.nodes)
                {
                    if (trfNode.name != "THRUST_TRANSFORM") continue;

                    try
                    {
                        thrustTransformInfos.Add(new ThrustTransformInfo(trfNode));
                    }
                    catch (Exception e)
                    {
                        Debug.LogError($"[{GetType().Name}] exception while attempting to parse THRUST_TRANSFORM: {e}");
                    }
                }
            }

            InitializeThrustTransforms();

            CreateEngine();
            FitEngineIfNecessary();
        }

        protected void InitializeThrustTransforms()
        {
            if (thrustTransformInfos == null && part.partInfo != null && part.partInfo.partPrefab != null && part.partInfo.partPrefab != this)
            {
                ModuleEnginesSolver prefabModule = null;

                for (int i = 0; i < part.partInfo.partPrefab.Modules.Count; i++)
                {
                    ModuleEnginesSolver m = part.partInfo.partPrefab.Modules[i] as ModuleEnginesSolver;
                    if (m == null) continue;
                    if (m.engineID != engineID) continue;

                    prefabModule = m;
                    break;
                }

                if (prefabModule == null)
                {
                    Debug.LogError($"[{GetType().Name}] unable to find prefab module");
                    return;
                }

                thrustTransformInfos = prefabModule.thrustTransformInfos;
            }

            if (thrustTransformInfos == null) return;

            thrustTransforms.Clear();
            thrustTransformMultipliers.Clear();
            float normalization = 0;

            foreach(ThrustTransformInfo info in thrustTransformInfos)
            {
                Transform[] transforms = part.FindModelTransforms(info.transformName);

                if (transforms.Length == 0)
                {
                    Debug.LogError($"[{GetType().Name}] no transforms named {info.transformName} found");
                    continue;
                }

                if (info.multipliers != null && transforms.Length != info.multipliers.Length)
                {
                    Debug.LogError($"[{GetType().Name}] found {transforms.Length} transforms named {info.transformName} but got {info.multipliers.Length} multipliers");
                    continue;
                }

                for (int i = 0; i < transforms.Length; i++)
                {
                    thrustTransforms.Add(transforms[i]);

                    float multiplier = info.overallMultiplier;
                    if (info.multipliers != null) multiplier *= info.multipliers[i];
                    thrustTransformMultipliers.Add(multiplier);
                    normalization += multiplier;
                }
            }

            if (normalization == 0) normalization = 1;
            for (int i = 0; i < thrustTransformMultipliers.Count; i++)
            {
                thrustTransformMultipliers[i] /= normalization;
            }
        }

        new virtual public void FixedUpdate()
        {
            realIsp = 0f;
            finalThrust = 0f;
            fuelFlowGui = 0f;
            massFlowGui = 0f;
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
            {
                float controlThrottle = independentThrottle ? independentThrottlePercentage * 0.01f : vessel.ctrlState.mainThrottle;
                float thrustLimiter = 0.01f * thrustPercentage;
                requestedThrottle = controlThrottle * thrustLimiter;
                UpdatePropellantStatus();
                lastPropellantFraction = PropellantAvailable() ? 1d : 0d;
            }
            UpdateThrottle();

            UpdateFlightCondition();

            UpdateSolver(ambientTherm,
                vessel.altitude,
                vessel.srf_velocity,
                vessel.mach,
                EngineIgnited,
                vessel.mainBody.atmosphereContainsOxygen,
                CheckTransformsUnderwater());
            CalculateEngineParams();
            UpdateTemp();

            if (finalThrust > 0f)
            {
                // now apply the thrust
                if (part.Rigidbody != null)
                {
                    Transform t;
                    for (int i = thrustTransforms.Count - 1; i >= 0; --i)
                    {
                        t = thrustTransforms[i];
                        Vector3 axis = useZaxis ? -t.forward : -t.up;
                        part.AddForceAtPosition(thrustRot * (axis * thrustTransformMultipliers[i] * finalThrust), t.position + t.rotation * thrustOffset);
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

        public override bool CanStart()
        {
            return base.CanStart() || flameout;
        }

        public override void FXUpdate()
        {
            Profiler.BeginSample("EngineSolver.FXUpdate");
            part.Effect(directThrottleEffectName, engineSolver.GetFXThrottle());
            part.Effect(spoolEffectName, engineSolver.GetFXSpool());
            part.Effect(runningEffectName, engineSolver.GetFXRunning());
            Profiler.BeginSample("EngineSolver.FXUpdate.GetFXPower");
            part.Effect(powerEffectName, engineSolver.GetFXPower());
            Profiler.EndSample();
            Profiler.EndSample();
        }

        virtual protected void UpdateTemp()
        {
            if (tempRatio > 1d && !CheatOptions.IgnoreMaxTemperature)
            {
                FlightLogger.eventLog.Add($"[{FormatTime(vessel.missionTime)}] {part.partInfo.title} melted its internals from heat.");
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

            this.inletTherm = inletTherm;
            this.areaRatio = areaRatio;
        }

        public override void UpdateThrottle()
        {
            currentThrottle = Mathf.Max(0.00f, currentThrottle);
            actualThrottle = currentThrottle * 100f;
        }

        virtual public void UpdateFlightCondition()
        {
            ambientTherm = EngineThermodynamics.VesselAmbientConditions(vessel, useExtTemp);
        }

        virtual public void UpdateSolver(EngineThermodynamics ambientTherm, double altitude, Vector3d vel, double mach, bool ignited, bool oxygen, bool underwater)
        {
            // In flight, these are the same and this will just return
            Profiler.BeginSample("EngineSolver.UpdateSolver");
            this.ambientTherm = ambientTherm;

            engineSolver.SetEngineState(ignited, lastPropellantFraction);
            engineSolver.SetFreestreamAndInlet(ambientTherm, inletTherm, altitude, mach, vel, oxygen, underwater);
            Profiler.BeginSample("EngineSolver.UpdateSolver.CalculatePerformance");
            engineSolver.CalculatePerformance(areaRatio, currentThrottle, flowMult * multFlow, ispMult * multIsp);
            Profiler.EndSample();
            Profiler.EndSample();
        }

        virtual public void CalculateEngineParams()
        {
            Profiler.BeginSample("EngineSolver.CalculateEngineParams");
            SetEmissive(engineSolver.GetEmissive());
            // Heat
            engineTemp = engineSolver.GetEngineTemp();
            tempRatio = engineTemp / maxEngineTemp;

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
                massFlowGui = 0f;
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
                Profiler.BeginSample("EngineSolver.CalculateEngineParams.RunningEngine");
                // calc flow
                double vesselValue = vessel.VesselValues.FuelUsage.value;
                if (vesselValue == 0d)
                    vesselValue = 1d;
                fuelFlow *= vesselValue;

                massFlow = fuelFlow * 0.001d * TimeWarp.fixedDeltaTime; // in tons

                if (CheatOptions.InfinitePropellant == true)
                {
                    lastPropellantFraction = 1d;
                    UnFlameout();
                }
                else
                {
                    if (massFlow > 0d)
                    {
                        Profiler.BeginSample("EngineSolver.CalculateEngineParams.RunningEngine.RequestPropellant");
                        lastPropellantFraction = RequestPropellant(massFlow);
                        Profiler.EndSample();
                    }
                    else
                    {
                        lastPropellantFraction = PropellantAvailable() ? 1d : 0d;
                    }
                }
                this.propellantReqMet = (float)this.lastPropellantFraction * 100;
                Profiler.EndSample();

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
                fuelFlowGui = (float)(fuelFlow * 0.001d * mixtureDensityRecip / ratioSum); // Also in tons
                // If we're displaying in the wrong mode, swap
                if (flowKG != (fuelFlow <= 1000))
                {
                    flowKG = fuelFlow <= 1000;
                    Fields[nameof(massFlowGui)].guiUnits = flowKG ? " kg/s" : " ton/s";
                }
                if (fuelFlow > 1000d)
                    fuelFlow *= 0.001d;
                massFlowGui = (float)fuelFlow;
                realIsp = (float)isp;
            }

            finalThrust = (float)producedThrust * vessel.VesselValues.EnginePower.value;
            Profiler.EndSample();
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
                float tmpOutput = currentThrottle;

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

        virtual public void FitEngineIfNecessary()
        {
            EngineFitter.FitIfNecessary(this, !part.HasParsedPrefab());
        }

        #endregion

        #region Events and Actions
        public override void Activate()
        {
            base.Activate();

            flameout = false;

            UpdatePropellantStatus();
            lastPropellantFraction = PropellantAvailable() ? 1d : 0d;
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
            flameoutBar = float.MaxValue;
        }
        protected void SetUnflameout()
        {
            // hack to get around my not making CanStart() virtual
            flameoutBar = 0f;
        }

        protected void SetEmissive(double val)
        {
            int eCount = emissiveAnims.Count;
            for (int i = 0; i < eCount; ++i)
                emissiveAnims[i].SetScalar((float)val);
        }

        protected void UpdateOverheatBox(double val, double minVal)
        {
            if (!vessel.isActiveVessel)
            {
                overheatBox = null;
                return;
            }

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
