using System;

namespace SolverEngines
{
    public class ThrustTransformInfo
    {
        public readonly string transformName;
        public readonly float overallMultiplier = 1;
        public readonly float[] multipliers = null;

        public ThrustTransformInfo(ConfigNode node)
        {
            if (node == null) throw new ArgumentNullException(nameof(node));

            transformName = node.GetValue("name");
            if (string.IsNullOrEmpty(transformName)) throw new ArgumentException("Missing or blank name");

            string overallMultiplierStr = node.GetValue("overallMultiplier");
            if (!string.IsNullOrEmpty(overallMultiplierStr))
            {
                if (!float.TryParse(overallMultiplierStr, out overallMultiplier))
                {
                    throw new ArgumentException("Could not parse overallMultiplier as float: " + overallMultiplierStr);
                }
                else if (overallMultiplier <= 0)
                {
                    throw new ArgumentException("overallMultiplier must be positive: " + overallMultiplierStr);
                }
            }

            if (node.HasValue("multiplier"))
            {
                string[] strMultipliers = node.GetValues("multiplier");

                multipliers = new float[strMultipliers.Length];
                for (int i = 0; i < strMultipliers.Length; i++)
                {
                    if (!float.TryParse(strMultipliers[i], out multipliers[i]))
                    {
                        throw new ArgumentException("Could not parse multiplier as flooat: " + strMultipliers[i]);
                    }
                    else if (multipliers[i] <= 0)
                    {
                        throw new ArgumentException("multiplier must be positive: " + strMultipliers[i]);
                    }
                }

                if (multipliers.Length == 1)
                {
                    overallMultiplier *= multipliers[0];
                    multipliers = null;
                }
            }
        }
    }
}
