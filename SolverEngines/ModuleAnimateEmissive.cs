using System;
using UnityEngine;


namespace SolverEngines
{
    public class ModuleAnimateEmissive : ModuleAnimateHeat
    {
        public float animState = 0f;
        public int stateCount;
        
        [KSPField]
        public double lerpMin = double.NaN;
        [KSPField]
        public double lerpOffset = double.NaN;
        [KSPField]
        public double lerpMax = double.NaN;
        [KSPField]
        public double lerpInnerScalar = 1d;
        [KSPField]
        public double lerpPow = 1d;
        [KSPField]
        public double lerpOuterScalar = 1d;

        public double lerpDivRecip = 1d;

        [KSPField]
        public bool useHeat = true;

        /// <summary>
        /// If the lerp values have been changed, call this afterwards
        /// </summary>
        public void UpdateLerpVals()
        {
            lerpDivRecip = 1d / (lerpMax - lerpMin + lerpOffset);
        }

        public void SetState(double inputVal)
        {
            animState = (float)UtilMath.Clamp(
                (Math.Pow(inputVal * lerpInnerScalar, lerpPow) * lerpOuterScalar + lerpOffset) * lerpDivRecip,
                lerpMin,
                lerpMax);
        }

        public override void OnStart(PartModule.StartState state)
        {
            base.OnStart(state); // sets up the animation etc.
            stateCount = heatAnimStates.Length;

            if (useHeat)
            {
                if (lerpMin == double.NaN)
                    lerpMin = 0d;
                if (lerpOffset == double.NaN)
                    lerpOffset = -draperPoint;
                if (lerpMax == double.NaN)
                    lerpMax = part.maxTemp;
                lerpInnerScalar = lerpPow = lerpOuterScalar = 1d;
            }
            else
            {
                if (lerpMin == double.NaN)
                    lerpMin = 0d;
                if (lerpOffset == double.NaN)
                    lerpOffset = 0d;
                if (lerpMax == double.NaN)
                    lerpMax = 1d;
            }

            UpdateLerpVals();
        }
        new public void Update()
        {
            if (useHeat)
            {
                SetState(part.temperature);
            }
            UpdateEffect();
        }
        virtual public void UpdateEffect()
        {
		    if (heatAnimStates == null)
		    {
			    return;
		    }
		    for (int i = 0; i < stateCount; ++i)
		    {
			    heatAnimStates[i].normalizedTime = animState;
		    }
        }
    }
}