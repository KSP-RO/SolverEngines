using System;
using System.Collections.Generic;
using UnityEngine;

namespace SolverEngines
{
    public abstract class ModuleAnimateSolverEngine<T> : PartModule where T : class, IEngineStatus
    {
        #region KSPFields

        [KSPField]
        public string animationName = "animation";

        [KSPField]
        public float responseSpeed = 1f;

        [KSPField]
        public int layer = 1;

        [KSPField]
        public string engineID;

        [KSPField]
        public float defaultState = 0f;

        [KSPField]
        public bool useAnimCurve = false;

        [KSPField]
        public FloatCurve animCurve = new FloatCurve();

        [KSPField(isPersistant = true)]
        public float animationState = 0f;

        #endregion

        #region Protected Fields

        protected AnimationState[] animStates;

        protected T engine;

        #endregion

        #region Setup

        public override void OnStart(PartModule.StartState state)
        {
            Animation[] anims = FindAnimations(animationName);
            animStates = SetupAnimations(anims, animationName);
            engine = FindEngine();

            if (engine == null)
                LogError("Engine module could not be found!");

#if DEBUG
            Fields["animationState"].guiActive = true;
#endif
        }

        public virtual Animation[] FindAnimations(string name)
        {
            Animation[] anims = part.FindModelAnimators(name);
            if (anims.Length == 0)
                Debug.LogError("Error: Cannot find animation named '" + name + "' on part " + part.name);

            return anims;
        }

        public virtual AnimationState[] SetupAnimations(Animation[] anims, string name)
        {
            List<AnimationState> states = new List<AnimationState>();
            for (int i = 0; i < anims.Length; i++)
            {
                Animation anim = anims[i];
                AnimationState animState = anim?[name];
                if (animState == null)
                    continue;
                animState.speed = 0;
                animState.enabled = true;
                animState.layer = layer;
                anim.Play(name);
                states.Add(animState);
            }

            return states.ToArray();
        }

        public T FindEngine()
        {
            if (String.IsNullOrEmpty(engineID))
            {
                T engineModule = part.FindModuleImplementing<T>();

                if (engineModule == null)
                    LogError("Cannot find engine module");

                return engineModule;
            }
            else
            {
                for (int i = 0; i < part.Modules.Count; i++)
                {
                    T engineModule = part.Modules[i] as T;

                    if (engineModule == null) continue;
                    if (engineModule.engineName != engineID) continue;

                    return engineModule;
                }

                LogError("Cannot find engine module with engineID '" + engineID + "'");

                return null;
            }
        }

        #endregion

        #region Update

        protected virtual void Update()
        {
            float target;
            if (engine != null)
                target = TargetAnimationState();
            else
                target = defaultState;

            target = HandleCurveAndClamping(target);
            animationState = HandleResponseSpeed(target);
            SetAnimationState(animationState);
        }

        public virtual float TargetAnimationState()
        {
            return defaultState;
        }

        public virtual float HandleCurveAndClamping(float target)
        {
            if (useAnimCurve)
                target = animCurve.Evaluate(target);

            return Mathf.Clamp01(target);
        }

        public virtual float GetDeltaTime()
        {
            return TimeWarp.fixedDeltaTime;
        }

        public virtual float HandleResponseSpeed(float target)
        {
            return Mathf.Lerp(animationState, target, responseSpeed * 25f * GetDeltaTime());
        }

        protected virtual void SetAnimationState(float state)
        {
            for (int i = 0; i < animStates.Length; i++)
            {
                animStates[i].normalizedTime = animationState;
            }
        }

        #endregion

        #region Debug

        public void LogError(string message)
        {
            string str = "Error on module " + moduleName;
            if (part?.partInfo != null)
                str += " on part " + part.partInfo.name;
            str += ": ";
            str += message;
            Debug.LogError(str);
        }

        #endregion
    }
}
