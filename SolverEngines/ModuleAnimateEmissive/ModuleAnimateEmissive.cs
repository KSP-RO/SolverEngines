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
            double powTerm = inputVal * lerpInnerScalar;
            if (lerpPow != 1d)
                powTerm = Math.Pow(powTerm, lerpPow);
            animState = (float)UtilMath.Clamp(
                (powTerm * lerpOuterScalar + lerpOffset) * lerpDivRecip,
                0d,
                1d);
        }

        protected void SetDefaults()
        {
            if (useHeat)
            {
                if (double.IsNaN(lerpMin))
                    lerpMin = 0d;
                if (double.IsNaN(lerpOffset))
                    lerpOffset = -draperPoint;
                if (double.IsNaN(lerpMax))
                    lerpMax = part.maxTemp;
                lerpInnerScalar = lerpPow = lerpOuterScalar = 1d;
            }
            else
            {
                if (double.IsNaN(lerpMin))
                    lerpMin = 0d;
                if (double.IsNaN(lerpOffset))
                    lerpOffset = 0d;
                if (double.IsNaN(lerpMax))
                    lerpMax = 1d;
            }
            UpdateLerpVals();
        }

        public override void OnLoad(ConfigNode node)
        {
            base.OnLoad(node);
            SetDefaults();
        }

        public override void OnStart(PartModule.StartState state)
        {
            base.OnStart(state); // sets up the animation etc.
            stateCount = heatAnimStates.Length;

            SetDefaults();
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