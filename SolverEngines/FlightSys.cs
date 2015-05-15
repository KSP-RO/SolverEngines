using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;
using KSP;

// Parts of this file are taken from FerramAerospaceResearch, Copyright 2015, Michael Ferrara, aka Ferram4, used with permission

namespace SolverEngines
{
    public class SolverFlightSys : VesselModule
    {
        private Vessel vessel;

        public float InletArea { get; private set; }
        public float EngineArea { get; private set; }
        public float AreaRatio { get; private set; }
        public double OverallTPR { get; private set; }
        public List<ModuleEngines> EngineList { get { return allEngines; } }
        
        private int partsCount = 0;
        private List<ModuleEnginesSolver> engineList = new List<ModuleEnginesSolver>();
        private List<AJEInlet> inletList = new List<AJEInlet>();
        private List<ModuleEngines> allEngines = new List<ModuleEngines>();

        // Ambient conditions - real
        public EngineThermodynamics AmbientTherm;
        public double Mach { get; private set; }

        // Inlet conditions - stagnation

        public EngineThermodynamics InletTherm;

        private void Start()
        {
            vessel = gameObject.GetComponent<Vessel>();
            this.enabled = true;
            updatePartsList();

            AmbientTherm = new EngineThermodynamics();
            InletTherm = new EngineThermodynamics();
        }

        private void FixedUpdate()
        {
            if (!HighLogic.LoadedSceneIsFlight || !vessel)
                return;
            if (vessel.altitude > vessel.mainBody.atmosphereDepth)
                return;

            if (partsCount != vessel.Parts.Count)
            {
                updatePartsList();
                partsCount = vessel.Parts.Count;
            }
            

            InletArea = 0f;
            EngineArea = 0f;
            OverallTPR = 0f;
            AreaRatio = 0f;

            for (int j = 0; j < engineList.Count; j++)
            {
                ModuleEnginesSolver e = engineList[j];
                if (e && e.EngineIgnited)
                {
                    EngineArea += (float)e.Need_Area;
                }
            }

            for (int j = 0; j < inletList.Count; j++)
            {
                AJEInlet i = inletList[j];
                if (i && i.intakeEnabled)
                {
                    InletArea += i.Area;
                    OverallTPR += i.Area * i.overallTPR;
                }
            }

            if (InletArea > 0f)
            {
                if (EngineArea > 0f)
                {
                    AreaRatio = InletArea / EngineArea;
                    OverallTPR /= InletArea;
                }
                else
                {
                    AreaRatio = 1f;
                }
            }

            AmbientTherm.FromVesselAmbientConditions(vessel);
            Mach = vessel.mach;

            // Transform from static frame to vessel frame, increasing total pressure and temperature
            if (vessel.srfSpeed < 0.01d)
                InletTherm.CopyFrom(AmbientTherm);
            else
                InletTherm.FromChangeReferenceFrame(AmbientTherm, vessel.srfSpeed);
            InletTherm.P *= OverallTPR; // TPR accounts for loss of total pressure by inlet

            // Push parameters to each engine

            for (int i = 0; i < engineList.Count; i++)
            {
                engineList[i].UpdateInletEffects(InletTherm, Math.Min(AreaRatio, 1f), OverallTPR);
            }
        }

        private void updatePartsList()
        {
            engineList.Clear();
            inletList.Clear();
            allEngines.Clear();
            for (int i = 0; i < vessel.parts.Count; i++)
            {
                Part p = vessel.parts[i];
                for (int j = 0; j < p.Modules.Count; j++)
                {
                    PartModule m = p.Modules[j];
                    if (m is ModuleEngines)
                        allEngines.Add(m as ModuleEngines);
                        if (m is ModuleEnginesSolver)
                            engineList.Add(m as ModuleEnginesSolver);
                    else if (m is AJEInlet)
                        inletList.Add(m as AJEInlet);
                }
            }
        }
    }
}
