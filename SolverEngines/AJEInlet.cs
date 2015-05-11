﻿using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using KSP;


namespace SolverEngines
{
    public class AJEInlet : ModuleResourceIntake
    {
        [KSPField(isPersistant = false, guiActive = true, guiActiveEditor = true)]
        public float Area;
        [KSPField(isPersistant = false, guiActive = false)]
        public FloatCurve TPRCurve = new FloatCurve();
        [KSPField(isPersistant = false, guiActive = false)]
        public bool useTPRCurve = true;
        [KSPField(isPersistant = false, guiActive = true)]
        public double cosine = 1d;
        [KSPField(isPersistant = false, guiActive = true, guiName = "Overall TPR", guiFormat = "P2")]
        public double overallTPR = 1d;

        // replace some original things
        protected Transform intakeTransform = null;
        new public float airFlow = 0f;
        new public float intakeDrag;
        new public float airSpeedGui;

        public double GetTPR(double Mach)
        {
            if (useTPRCurve)
            {
                return (double)TPRCurve.Evaluate((float)Mach);
            }
            else
            {
                if (Mach <= 1d)
                    return 1d;
                else
                    return 1.0d - .075d * Math.Pow(Mach - 1.0d, 1.35d);
            }

        }

        public void UpdateOverallTPR(Vector3 velocity, double mach)
        {
            // blowfish - avoid TPR variations at standstill
            if (velocity.IsSmallerThan(0.05f))
            {
                cosine = 0.987654d;
                mach = 0d;
            }
            else
            {
                //by Virindi
                double realcos = Math.Max(0d, Vector3.Dot(velocity.normalized, intakeTransform.forward));
                double fakecos = (-0.000123d * velocity.sqrMagnitude + 0.002469d * velocity.magnitude + 0.987654d);
                if (fakecos > 1d)
                    fakecos = 1d;

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
            if (vessel.mainBody.atmosphereContainsOxygen == false || part.vessel.altitude > vessel.mainBody.atmosphereDepth)
            {
                return;
            }

            if (intakeEnabled)
            {
                UpdateOverallTPR(vessel.srf_velocity, vessel.mach);
            }
            else
            {
                cosine = 1d;
                overallTPR = 0d;
            }
        }

        public override string GetInfo()
        {
            string output = "";

            output += "<b>Intake Resource: </b>" + resourceName + "\n";
            output += "<b>Intake Area: </b>" + (Area).ToString("N4") + " m^2";

            return output;
        }

    }
}