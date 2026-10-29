using System;
using KSP.Localization;
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
        [KSPField(isPersistant = false, guiActive = true, guiName = "#SolverEngines_OverallTPR", guiFormat = "P2")]
        public float overallTPR = 1f;

        // replace some original things
        new public float airFlow = 0f;
        new public float airSpeedGui;

        private const int tprSamplesPerMach = 100;
        private const int tprMaxSampledMach = 20;

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
            return $"<b>{LocalizationStrings.str_IntakeArea}: </b>" + (Area).ToString("N4") + " m²\n";
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

            output += $"<b>{LocalizationStrings.str_IntakeResource}: </b>" + resourceName + "\n";
            output += $"<b>{LocalizationStrings.str_IntakeArea}: </b>" + (Area).ToString("N4") + " m²\n";
            output += $"\n{GetTPRInfo()}";

            return output;
        }

        // TPR from the curve only, without the off-axis cosine losses applied in flight.
        private string GetTPRInfo()
        {
            float[] samples = SampleTPR();
            int peak = FindTPRPeakIndex(samples);

            string output = $"<b>{LocalizationStrings.str_PressureRecovery}</b>\n";
            output += $"{FormatTPRPeak(samples, peak)}\n";
            output += FormatTPRBand(samples, peak, 0.9f);
            output += FormatTPRBand(samples, peak, 0.5f);

            for (int i = peak + 1; i < samples.Length; i++)
            {
                if (samples[i] <= 0f)
                {
                    output += Localizer.Format("#SolverEngines_TPRZeroAt", FormatSampleMach(i));
                    break;
                }
            }

            return output.TrimEnd('\n');
        }

        private float[] SampleTPR()
        {
            float[] samples = new float[tprMaxSampledMach * tprSamplesPerMach + 1];
            for (int i = 0; i < samples.Length; i++)
                samples[i] = GetTPR((double)i / tprSamplesPerMach);
            return samples;
        }

        private static int FindTPRPeakIndex(float[] samples)
        {
            int peak = 0;
            for (int i = 1; i < samples.Length; i++)
            {
                if (samples[i] > samples[peak])
                    peak = i;
            }
            return peak;
        }

        // Shows a Mach range when the peak is flat, e.g. isentropic inlets at 100% up to Mach 1.
        private static string FormatTPRPeak(float[] samples, int peak)
        {
            return Localizer.Format("#SolverEngines_TPRPeak", $"{samples[peak]:P0}", FormatTPRRange(samples, peak, samples[peak] - 1e-5f));
        }

        private static string FormatTPRBand(float[] samples, int peak, float threshold)
        {
            if (samples[peak] < threshold)
                return string.Empty;
            return $"{Localizer.Format("#SolverEngines_TPRAbove", $"{threshold:P0}", FormatTPRRange(samples, peak, threshold))}\n";
        }

        // Contiguous Mach range around the peak where TPR stays at or above the threshold.
        private static string FormatTPRRange(float[] samples, int peak, float threshold)
        {
            int min = peak;
            int max = peak;
            while (min > 0 && samples[min - 1] >= threshold)
                min--;
            while (max < samples.Length - 1 && samples[max + 1] >= threshold)
                max++;

            string minMach = FormatSampleMach(min);
            string maxMach = FormatSampleMach(max);
            return minMach == maxMach ? minMach : $"{minMach} - {maxMach}";
        }

        private static string FormatSampleMach(int index)
        {
            return ((float)index / tprSamplesPerMach).ToString("F1");
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