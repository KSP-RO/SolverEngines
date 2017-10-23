using System;
using System.Collections.Generic;

// Parts of this file are taken from FerramAerospaceResearch, Copyright 2015, Michael Ferrara, aka Ferram4, used with permission

namespace SolverEngines
{
    public class SolverFlightSys : VesselModule
    {
        public double InletArea { get; private set; }
        public double EngineArea { get; private set; }
        public double AreaRatio { get; private set; }
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

        protected override void OnStart()
        {
            base.OnStart();

            this.enabled = true;
            UpdatePartsList();

            AmbientTherm = new EngineThermodynamics();
            InletTherm = new EngineThermodynamics();
        }

        private void FixedUpdate()
        {
            if (!HighLogic.LoadedSceneIsFlight || !vessel)
                return;
            if (vessel.altitude > vessel.mainBody.atmosphereDepth)
                return;
            int newCount = vessel.Parts.Count;
            if (partsCount != newCount)
            {
                partsCount = newCount;
                UpdatePartsList();
            }
            

            InletArea = 0d;
            EngineArea = 0d;
            OverallTPR = 0d;
            AreaRatio = 0d;

            for (int j = engineList.Count - 1; j >= 0; --j)
            {
                ModuleEnginesSolver e = engineList[j];
                if ((object)e != null && e.EngineIgnited)
                {
                    EngineArea += e.Need_Area;
                }
            }

            for (int j = inletList.Count -1; j >= 0; --j)
            {
                AJEInlet i = inletList[j];
                if ((object)i != null)
                {
                    double area = i.UsableArea();
                    InletArea += area;
                    OverallTPR += area * i.overallTPR;
                }
            }

            if (InletArea > 0d)
            {
                if (EngineArea > 0d)
                {
                    AreaRatio = Math.Min(1d, InletArea / EngineArea);
                    OverallTPR /= InletArea;
                    OverallTPR *= AreaRatio;
                }
                else
                {
                    AreaRatio = 1d;
                }
            }

            AmbientTherm = EngineThermodynamics.VesselAmbientConditions(vessel);
            Mach = vessel.mach;

            // Transform from static frame to vessel frame, increasing total pressure and temperature
            if (vessel.srfSpeed < 0.01d)
                InletTherm = AmbientTherm;
            else
                InletTherm = AmbientTherm.ChangeReferenceFrame(vessel.srfSpeed);
            InletTherm.P *= OverallTPR; // TPR accounts for loss of total pressure by inlet

            // Push parameters to each engine

            for (int i = engineList.Count - 1; i >= 0 ; --i)
            {
                engineList[i].UpdateInletEffects(InletTherm, AreaRatio, OverallTPR);
            }
        }

        private void UpdatePartsList()
        {
            engineList.Clear();
            inletList.Clear();
            allEngines.Clear();
            for (int i = partsCount - 1; i >= 0; --i)
            {
                Part p = vessel.parts[i];
                for (int j = p.Modules.Count - 1; j >= 0; --j)
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
