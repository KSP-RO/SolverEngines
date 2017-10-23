using System;

namespace SolverEngines
{
    public static class SolverMathUtil
    {
        /// <summary>
        /// Basic clamping method
        /// </summary>
        /// <param name="min">If value is less than this, the return value will be equal to this</param>
        /// <param name="max">If value is greater than this, the return value will be equal to this</param>
        /// <param name="value">The value to clamp.  If it is between min and max, it will be returned</param>
        /// <returns>value, clamped between min and max</returns>
        public static double Clamp(double min, double max, double value)
        {
            value = Math.Max(0d, value);
            value = Math.Min(value, 1d);
            return value;
        }

        /// <summary>
        /// Basic lerp method
        /// Clamps t between 0 and 1 then lerps between min and max
        /// </summary>
        /// <param name="min">Minimum value (at t=0)</param>
        /// <param name="max">Maximum value (at t=1)</param>
        /// <param name="t">Normalized time between min and max</param>
        /// <returns>Lerped value: min + (max - min) * t</returns>
        public static double Lerp(double min, double max, double t)
        {
            t = Clamp(0d, 1d, t);

            return min + (max - min) * t;
        }

        // This method is written by ferram4 - used with permission

        /// <summary>
        /// Brent's method for finding zero of a function
        /// </summary>
        /// <param name="function">Function to find root of.  Takes one double as a parameter - the variable to find the root with respect to</param>
        /// <param name="a">Lower bound on the parameter</param>
        /// <param name="b">Upper bound on the parameter</param>
        /// <param name="epsilon">Uncertainty on parameter interval - used as a convergence condition</param>
        /// <param name="maxIter">Maximum number of iterations</param>
        /// <returns>Parameter which zeros the function</returns>
        public static double BrentsMethod(Func<double, double> function, double a, double b, double epsilon = 0.001, int maxIter = int.MaxValue)
        {
            double delta = epsilon * 100;
            double fa, fb;
            fa = function(a);
            fb = function(b);

            if (fa * fb >= 0)
                return 0;

            if(Math.Abs(fa) < Math.Abs(fb))
            {
                double tmp = fa;
                fa = fb;
                fb = tmp;

                tmp = a;
                a = b;
                b = tmp;
            }

            double c = a, d = a, fc = function(c);

            double s = b, fs = fb; 

            bool flag = true;
            int iter = 0;
            while(fs != 0 && Math.Abs(a - b) > epsilon && iter < maxIter)
            {
                if((fa - fc) > double.Epsilon && (fb - fc) > double.Epsilon)    //inverse quadratic interpolation
                {
                    s = a * fc * fb / ((fa - fb) * (fa - fc));
                    s += b * fc * fa / ((fb - fa) * (fb - fc));
                    s += c * fc * fb / ((fc - fa) * (fc - fb));
                }
                else
                {
                    s = (b - a) / (fb - fa);    //secant method
                    s *= fb;
                    s = b - s;
                }

                double b_s = Math.Abs(b - s), b_c = Math.Abs(b-c), c_d = Math.Abs(c - d);

                //Conditions for bisection method
                bool condition1;
                double a3pb_over4 = (3 * a + b) * 0.25;

                if (a3pb_over4 > b)
                    if (s < a3pb_over4 && s > b)
                        condition1 = false;
                    else
                        condition1 = true;
                else
                    if (s > a3pb_over4 && s < b)
                        condition1 = false;
                    else
                        condition1 = true;

                bool condition2;

                if (flag && b_s >= b_c * 0.5)
                    condition2 = true;
                else
                    condition2 = false;

                bool condition3;

                if (!flag && b_s >= c_d * 0.5)
                    condition3 = true;
                else
                    condition3 = false;

                bool condition4;

                if (flag && b_c <= delta)
                    condition4 = true;
                else
                    condition4 = false;

                bool conditon5;

                if (!flag && c_d <= delta)
                    conditon5 = true;
                else
                    conditon5 = false;

                if (condition1 || condition2 || condition3 || condition4 || conditon5)
                {
                    s = a + b;
                    s *= 0.5;
                    flag = true;
                }
                else
                    flag = false;

                fs = function(s);
                d = c;
                c = b;

                if (fa * fs < 0)
                {
                    b = s;
                    fb = fs;
                }
                else
                {
                    a = s;
                    fa = fs;
                }

                if (Math.Abs(fa) < Math.Abs(fb))
                {
                    double tmp = fa;
                    fa = fb;
                    fb = tmp;

                    tmp = a;
                    a = b;
                    b = tmp;
                }
                iter++;
            }
            return s;
        }
    }
}
