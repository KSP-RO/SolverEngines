using System;
using UnityEngine;

namespace SolverEngines
{
    public class AJEInlet : ModuleResourceIntake, IModuleInfo
    {
        [KSPField(isPersistant = false, guiActive = true, guiActiveEditor = true)]
        public float Area;
        [KSPField(isPersistant = false, guiActive = false)]
        public FloatCurve TPRCurve = new FloatCurve();
        [KSPField(isPersistant = false, guiActive = false)]
        public bool useTPRCurve = true;
        [KSPField(isPersistant = false, guiActive = false)]
        public string inletTitle;
        [KSPField(isPersistant = false, guiActive = false)]
        public string inletDescription;

        [KSPField(isPersistant = false, guiActive = true, guiFormat = "F3")]
        public float cosine = 1f;
        [KSPField(isPersistant = false, guiActive = true, guiName = "Overall TPR", guiFormat = "P2")]
        public float overallTPR = 1f;

        // replace some original things
        new public float airFlow = 0f;
        new public float airSpeedGui;

        virtual public float GetTPR(double Mach)
        {
            if (useTPRCurve)
            {
                return TPRCurve.Evaluate((float)Mach);
            }
            else
            {
                if (Mach <= 1d)
                    return 1f;
                else
                    return 1.0f - .075f * (float)Math.Pow(Mach - 1.0d, 1.35d);
            }

        }

        virtual public void UpdateOverallTPR(Vector3 velocity, double mach)
        {
            // blowfish - avoid TPR variations at standstill
            if (velocity.IsSmallerThan(0.05f))
            {
                cosine = 0.987654f;
                mach = 0d;
            }
            else
            {
                //by Virindi
                float realcos = Math.Max(0f, Vector3.Dot(velocity.normalized, intakeTransform.forward));
                float fakecos = FakeCosine(velocity.magnitude);

                cosine = Math.Max(realcos, fakecos); //by Virindi
            }

            overallTPR = cosine * cosine * GetTPR(mach);
        }

        public override void OnStart(StartState state)
        {
            base.OnStart(state);
            if (intakeTransform == null)
            {
                intakeTransform = part.FindModelTransform(intakeTransformName);
            }
        }

        new public void FixedUpdate()
        {
            base.FixedUpdate();
            if (HighLogic.LoadedSceneIsEditor)
                return;
            if (part.vessel.altitude > vessel.mainBody.atmosphereDepth)
            {
                return;
            }

            if (IntakeActive())
            {
                UpdateOverallTPR(vessel.srf_velocity, vessel.mach);
            }
            else
            {
                cosine = 1f;
                overallTPR = 0f;
            }
        }

        virtual public bool IntakeActive()
        {
            return intakeEnabled && InAtmosphere() && !part.ShieldedFromAirstream && (intakeTransform != null) && !(CheckUnderwater() && disableUnderwater);
        }

        virtual public float UsableArea()
        {
            if (IntakeActive())
                return Area;
            else
                return 0f;
        }

        protected bool InAtmosphere()
        {
            return part.vessel.altitude < vessel.mainBody.atmosphereDepth;
        }

        protected bool CheckUnderwater()
        {
            return FlightGlobals.getAltitudeAtPos(intakeTransform.position) < 0.0f && vessel.mainBody.ocean;
        }

        public Callback<Rect> GetDrawModulePanelCallback()
        {
            return null;
        }

        public string GetModuleTitle()
        {
            return "AJE Inlet";
        }

        public string GetPrimaryField()
        {
            return "<b>Intake Area: </b>" + (Area).ToString("N4") + " m^2";
        }

        public override string GetInfo()
        {
            string output = "";

            if (inletTitle != null && inletTitle.Length > 0)
            {
                output += "<b>" + inletTitle + "</b>\n";

                if (inletDescription != null && inletDescription.Length > 0)
                {
                    output += inletDescription + "\n";
                }

                output += "\n";
            }
            else
            {
                if (inletDescription != null && inletDescription.Length > 0)
                    Debug.LogWarning("AJEInlet on part " + part.name + " has inletDescription but no inletTitle.  inletDescription will not be displayed.");
            }

            output += "<b>Intake Resource: </b>" + resourceName + "\n";
            output += "<b>Intake Area: </b>" + (Area).ToString("N4") + " m^2";

            return output;
        }


        public static float FakeCosine(float speed)
        {
            //by Virindi
            float fakecos = (-0.000123f * speed * speed + 0.002469f * speed + 0.987654f);
            return Mathf.Min(fakecos, 1f);
        }

        public static float OverallStaticTPR(float TPR)
        {
            float fakecos = FakeCosine(0f);
            return TPR * fakecos * fakecos;
        }
    }
}