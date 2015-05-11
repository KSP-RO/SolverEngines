using KSP;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;
using System.Reflection;


/// <summary>
/// Base module for AJE engines
/// Derive from this for a real engine; this *will not work* alone.
/// </summary>
public class ModuleEnginesSolver : ModuleEnginesFX, IModuleInfo
{
    // base fields
    [KSPField(isPersistant = false, guiActive = true)]
    public String Environment;

    [KSPField(isPersistant = false, guiActive = true)]
    public String Inlet;

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
    // internals
    protected double tempRatio = 0d, engineTemp = 288.15d;
    protected double lastPropellantFraction = 1d;
    protected VInfoBox overheatBox = null;

    protected List<ModuleAnimateEmissive> emissiveAnims;


    // protected internals
    protected EngineSolver engineSolver = null;
    protected List<ModuleEnginesSolver> engineList;
    protected List<AJEInlet> inletList;
    protected double OverallTPR = 1d, Arearatio = 1d;

    protected Vector3 thrustOffset = Vector3.zero;
    protected Quaternion thrustRot = Quaternion.identity;

    protected int partsCount = 0;

    protected const double invg0 = 1d / 9.80665d;

    #region Overridable Methods

    virtual public void CreateEngine()
    {
        engineSolver = new EngineSolver();
    }

    virtual public void Start()
    {
        CreateEngine();
        Need_Area = engineSolver.GetArea();
        Fields["Need_Area"].guiActiveEditor = Need_Area > 0;

        List<Part> parts = null;
        engineList = new List<ModuleEnginesSolver>();
        inletList = new List<AJEInlet>();

        if (HighLogic.LoadedSceneIsEditor)
            parts = EditorLogic.fetch.getSortedShipList();
        else if (HighLogic.LoadedSceneIsFlight)
            parts = vessel.Parts;
        if (parts != null)
        {
            partsCount = parts.Count;
            GetLists(parts);
        }
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
            List<Part> eParts = EditorLogic.fetch.getSortedShipList();
            int eNewCount = eParts.Count;
            if (eNewCount != partsCount)
            {
                partsCount = eNewCount;
                GetLists(eParts);
            }
            return;
        }
        // so we must be in flight
        if (TimeWarping())
        {
            currentThrottle = 0f;
            return;
        }

        // update our links
        List<Part> parts = vessel.Parts;
        int newCount = parts.Count;
        if (newCount != partsCount)
        {
            partsCount = newCount;
            GetLists(parts);
        }
            
        UpdateInletEffects();
        if(EngineIgnited && !flameout)
            requestedThrottle = vessel.ctrlState.mainThrottle;
        UpdateThrottle();
        UpdateFlightCondition(vessel.altitude,
            vessel.srfSpeed,
            vessel.staticPressurekPa,
            useExtTemp ? vessel.externalTemperature : vessel.atmosphericTemperature,
            vessel.atmDensity,
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
        if (EngineIgnited)
        {
            part.Effect(directThrottleEffectName, requestedThrottle);
        }
        else
        {
            part.Effect(directThrottleEffectName, 0f);
        }

        if (EngineIgnited && !flameout)
        {
            /*if (useEngineResponseTime)
            {
                engineSpool = Mathf.Lerp(engineSpool, Mathf.Max(engineSpoolIdle, currentThrottle), engineSpoolTime * TimeWarp.fixedDeltaTime);
            }
            else
            {*/
                engineSpool = currentThrottle;
            //}

            part.Effect(spoolEffectName, engineSpool);
            

            if (finalThrust == 0f)
            {
                part.Effect(powerEffectName, 0f);
            }
            else
            {
                float pow = finalThrust / maxThrust;
                part.Effect(runningEffectName, pow);
                part.Effect(powerEffectName, pow);
            }
        }
        else
        {
            /*if (useEngineResponseTime)
            {
                engineSpool = Mathf.Lerp(engineSpool, flameout ? 0f : currentThrottle, engineSpoolTime * TimeWarp.fixedDeltaTime);
            }
            else
            {*/
                engineSpool = 0f;
            //}

            part.Effect(spoolEffectName, engineSpool);
            part.Effect(runningEffectName, 0f);
            part.Effect(powerEffectName, 0f);
        }


    }

    virtual protected void UpdateTemp()
    {
        if (tempRatio > 1d)
        {
            FlightLogger.eventLog.Add("[" + FormatTime(vessel.missionTime) + "] " + part.partInfo.title + " melted its internals from heat.");
            part.explode();
        }
        else
            UpdateOverheatBox(tempRatio, 0.8d, 2f);
    }

    //ferram4: separate out so function can be called separately for editor sims
    virtual public void UpdateInletEffects()
    {
        double EngineArea = 0, InletArea = 0;
        OverallTPR = 0;

        if (engineSolver == null)
        {
            Debug.Log("HOW?!");
            return;
        }
        double M0 = engineSolver.GetM0();
        int eCount = engineList.Count;
        for (int j = 0; j < eCount; ++j)
        {
            ModuleEnginesSolver e = engineList[j];
            if ((object)e != null) // probably unneeded because I'm updating the lists now
            {
                EngineArea += e.engineSolver.GetArea();
            }
        }

        for (int j = 0; j < inletList.Count; j++)
        {
            AJEInlet i = inletList[j];
            if ((object)i != null) // probably unneeded because I'm updating the lists now
            {
                InletArea += i.Area;
                OverallTPR += i.overallTPR * i.Area; ;
            }
        }

        if (InletArea > 0)
            OverallTPR /= InletArea;
        if (EngineArea > 0d)
            Arearatio = Math.Min(InletArea / EngineArea, 1d);
        else
            Arearatio = 1d;
        Inlet = "Area:" + Arearatio.ToString("P2") + " TPR:" + OverallTPR.ToString("P2");

    }

    new virtual public void UpdateThrottle()
    {
        currentThrottle = Mathf.Max(0.01f, currentThrottle);
        actualThrottle = Mathf.RoundToInt(currentThrottle * 100f);
    }

    virtual public void UpdateFlightCondition(double altitude, double vel, double pressure, double temperature, double rho, double mach, bool oxygen)
    {
        Environment = pressure.ToString("N2") + " kPa; " + temperature.ToString("N2") + " K ";

        engineSolver.SetTPR(OverallTPR);
        engineSolver.SetEngineState(EngineIgnited, lastPropellantFraction);
        engineSolver.SetFreestream(altitude, pressure, temperature, rho, mach, vel, oxygen);
        engineSolver.CalculatePerformance(Arearatio, currentThrottle, flowMult, ispMult);
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

        if (thrustIn <= 0d || double.IsNaN(thrustIn))
        {
            if (EngineIgnited && !double.IsNaN(thrustIn) && currentThrottle > 0f)
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

    protected void GetLists(List<Part> parts)
    {
        engineList.Clear();
        inletList.Clear();
        for (int j = 0; j < partsCount; ++j)        //reduces garbage produced compared to foreach due to Unity Mono issues
        {
            Part p = parts[j];
            int mCount = p.Modules.Count;
            for (int i = 0; i < mCount; ++i)
            {
                PartModule m = p.Modules[i];
                if (m is ModuleEnginesSolver)
                    engineList.Add(m as ModuleEnginesSolver);
                if (m is AJEInlet)
                    inletList.Add(m as AJEInlet);
            }
        }
    }
    protected void UpdateOverheatBox(double val, double minVal, float scalar)
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
            float scaleFac = 1f  - scalar;
            float gaugeMin = scalar * (float)minVal + scaleFac;
            overheatBox.SetValue((float)val * scalar + scaleFac, gaugeMin, 1.0f);
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