using System;
using System.Reflection;
using UnityEngine;

// This class uses DaMichel FAR wrapping code from Kerbal Flight Data, used with permission

namespace SolverEngines
{
    public static class FlightDataWrapper
    {
        private static bool haveFAR = false;
        private static Type FARAPI = null;
        private static MethodInfo FARVesselDynPres = null;
        private static MethodInfo FARVesselLiftCoeff = null;
        private static MethodInfo FARVesselDragCoeff = null;
        private static MethodInfo FARVesselRefArea = null;
        private static MethodInfo FARVesselTermVelEst = null;
        private static MethodInfo FARVesselBallisticCoeff = null;
        private static MethodInfo FARVesselAoA = null;
        private static MethodInfo FARVesselSideslip = null;
        private static MethodInfo FARVesselTSFC = null;
        private static MethodInfo FARVesselStallFrac = null;

        public static void Init()
        {
            haveFAR = false;

            for (int i = 0; i < AssemblyLoader.loadedAssemblies.Count; i++)
            {
                var assembly = AssemblyLoader.loadedAssemblies[i];
                if (assembly.name == "FerramAerospaceResearch")
                {
                    var types = assembly.assembly.GetExportedTypes();
                    for (int j = 0; j < types.Length; j++)
                    {
                        Type t = types[j];
                        if (t.FullName.Equals("FerramAerospaceResearch.FARAPI"))
                        {
                            FARAPI = t;
                            haveFAR = true;
                            break;
                        }
                    }
                    break;
                }
            }

            if (haveFAR)
            {
                foreach (var method in FARAPI.GetMethods(BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance | BindingFlags.Public))
                {
                    if (method.Name == "VesselDynPres")
                        FARVesselDynPres = method;
                    else if (method.Name == "VesselLiftCoeff")
                        FARVesselLiftCoeff = method;
                    else if (method.Name == "VesselDragCoeff")
                        FARVesselDragCoeff = method;
                    else if (method.Name == "VesselRefArea")
                        FARVesselRefArea = method;
                    else if (method.Name == "VesselTermVelEst")
                        FARVesselTermVelEst = method;
                    else if (method.Name == "VesselBallisticCoeff")
                        FARVesselBallisticCoeff = method;
                    else if (method.Name == "VesselAoA")
                        FARVesselAoA = method;
                    else if (method.Name == "VesselSideslip")
                        FARVesselSideslip = method;
                    else if (method.Name == "VesselTSFC")
                        FARVesselTSFC = method;
                    else if (method.Name == "VesselStallFrac")
                        FARVesselStallFrac = method;
                }
            }
        }

        public static double VesselDynPreskPa(Vessel vessel)
        {
            if (haveFAR && FARVesselDynPres != null)
            {
                var arg = new object[] { vessel };
                return (double)FARVesselDynPres.Invoke(null, arg);
            }
            else
            {
                return vessel.dynamicPressurekPa;
            }
        }

        public static double VesselTotalDragkN(Vessel vessel)
        {
            if (haveFAR && FARVesselDynPres != null && FARVesselRefArea != null && FARVesselLiftCoeff != null)
            {
                var arg = new object[] { vessel };
                return (double)FARVesselDynPres.Invoke(null, arg) * (double)FARVesselRefArea.Invoke(null, arg) * (double)FARVesselDragCoeff.Invoke(null, arg);
            }
            else if (haveFAR)
            {
                Debug.LogWarning("FAR was found but some of its methods are null.  How did this happen?");
                return 0d;
            }
            else
            {
                return -Vector3.Dot(VesselStockAeroForces(vessel), vessel.srf_velocity.normalized);
            }
        }

        // Stock aero force calculation by NathanKell

        private static Vector3 VesselStockAeroForces(Vessel vessel)
        {
            Vector3 aeroForce = Vector3.zero;

            for (int i = 0; i < vessel.parts.Count; i++)
            {
                Part part = vessel.parts[i];
                aeroForce += -part.dragVectorDir * part.dragScalar;
                if (!part.hasLiftModule)
                {
                    Vector3 bodyLift = part.transform.rotation * (part.bodyLiftScalar * part.DragCubes.LiftForce);
                    aeroForce += Vector3.ProjectOnPlane(bodyLift, -part.dragVectorDir);
                }
                else
                {
                    for (int j = 0; j < part.Modules.Count; j++)
                    {
                        ModuleLiftingSurface module = part.Modules[j] as ModuleLiftingSurface;
                        if (module == null) continue;
                        aeroForce += module.liftForce;
                        aeroForce += module.dragForce;
                    }
                }
            }

            return aeroForce;
        }
    }
}
