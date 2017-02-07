using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;

namespace SolverEngines.EngineFitting
{
    public class EngineFitter
    {
        public static void FitIfNecessary(object engine, bool saveToDatabase)
        {
            if (engine is IFittableEngine)
            {
                EngineFitter fitter = new EngineFitter((IFittableEngine)engine);
                fitter.FitEngineIfNecessary(saveToDatabase);
            }
        }

        private IFittableEngine engine;
        private List<EngineParameterInfo> engineFitParameters = new List<EngineParameterInfo>();

        public EngineFitter(IFittableEngine engine)
        {
            this.engine = engine;

            SetupFitParameters();
        }

        public virtual bool EngineHasFitResults => engineFitParameters.Any(param => param.IsFitResult());

        protected virtual bool ShouldFitEngine => EngineHasFitResults && engine.CanFitEngine;

        public virtual void FitEngineIfNecessary(bool saveToDatabase)
        {
            if (!ShouldFitEngine) return;

            bool doFit = false;

            ConfigNode node = EngineDatabase.GetNodeForEngine(engine);
            if (node == null)
            {
                doFit = true;
            }
            else
            {
                doFit = EngineDatabase.PluginUpdateCheck(engine, node);

                // Check for changes
                foreach (EngineParameterInfo entry in engineFitParameters)
                {
                    if (entry.IsFitResult()) continue;
                    if (!entry.EqualsValueInNode(node))
                    {
                        doFit = true;
                        break;
                    }
                }
                if (!doFit)
                {
                    Debug.Log("[" + engine.EngineTypeName + "] Reading engine params from cache for engine " + engine.EnginePartName);

                    foreach (EngineParameterInfo entry in engineFitParameters)
                    {
                        // Only copy things that would be fitted
                        if (entry.IsFitResult())
                            entry.SetValueFromNode(node);
                    }
                    engine.PushFitParamsToSolver();
                }
            }

            if (!doFit) return;

            Debug.Log("[" + engine.EngineTypeName + "] Fitting params for engine " + engine.EnginePartName);

            // Make sure everything has the correct value
            engine.PushFitParamsToSolver();
            engine.DoEngineFit();

            ConfigNode newNode = new ConfigNode();

            foreach (EngineParameterInfo entry in engineFitParameters)
            {
                newNode.SetValue(entry.Name, entry.GetValueStr(), true);
            }

            if (saveToDatabase)
            {
#if DEBUG
                Debug.Log("Saving fitted engine parameters to database");
#endif
                EngineDatabase.SetNodeForEngine(engine, newNode);
            }
        }

#region Private Methods

        protected virtual void SetupFitParameters()
        {
            FieldInfo[] fields = engine.GetType().GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            foreach (FieldInfo field in fields)
            {
                object[] attributes = field.GetCustomAttributes(true);
                foreach (object attribute in attributes)
                {
                    if (attribute is EngineParameter)
                        engineFitParameters.Add(new EngineParameterInfo(engine, field, (EngineParameter)attribute));
                }
            }
        }

#endregion
    }
}
