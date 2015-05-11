using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using KSP;



public class EngineSolver
{
    //freestream flight conditions; static pressure, static temperature, static density, and mach number
    protected double alt, p0, t0, eair0, vel, M0 = 0, rho, mach;
    protected bool oxygen = false;

    // engine state
    protected bool running = false;
    protected double ffFraction = 1d;

    //inlet total pressure recovery
    protected double TPR;

    //gas properties at start
    protected double gamma_c, inv_gamma_c, inv_gamma_cm1;
    protected double R_c;
    protected double Cp_c;
    protected double Cv_c;

    //Throttles for burner and afterburner
    protected double mainThrottle, abThrottle;

    //thrust and Isp and fuel flow of the engine
    protected double thrust, Isp, fuelFlow;

    // the string to report when can't thrust
    protected string statusString = "";

    public string debugstring;
    //---------------------------------------------------------
    //Initialization Functions

    /// <summary>
    /// Sets the freestream properties
    /// </summary>
    /// <param name="altitude">altitude in m</param>
    /// <param name="pressure">pressure in kPa</param>
    /// <param name="temperature">temperature in K</param>
    /// <param name="velocity">velocity in m/s</param>
    /// <param name="hasOxygen">does the atmosphere contain oxygen</param>
    virtual public void SetFreestream(double altitude, double pressure, double temperature, double inRho, double inMach, double velocity, bool hasOxygen)
    {
        alt = altitude;
        p0 = pressure * 1000d;
        t0 = temperature;
        oxygen = hasOxygen;
        vel = velocity;
        rho = inRho;
        mach = inMach;

        gamma_c = CalculateGamma(t0, 0);
        inv_gamma_c = 1d / gamma_c;
        inv_gamma_cm1 = 1d / (gamma_c - 1d);
        Cp_c = CalculateCp(t0, 0);
        Cv_c = Cp_c * inv_gamma_c;
        R_c = Cv_c * (gamma_c - 1);


        M0 = vel / Math.Sqrt(gamma_c * R_c * t0);

        eair0 = Math.Sqrt(gamma_c / R_c / t0);
    }

    /// <summary>
    /// Sets the engine state based on module setting and resource availability
    /// </summary>
    /// <param name="isRunning">is the engine running (i.e. active/enabled)</param>
    /// <param name="ffFrac">fraction of desired fuel flow passed to the engine last tick</param>
    virtual public void SetEngineState(bool isRunning, double ffFrac)
    {
        running = isRunning;
        ffFraction = ffFrac;
    }

    /// <summary>
    /// Calculates enigne state based on existing and passed info
    /// </summary>
    /// <param name="airRatio">ratio of air requirement met</param>
    /// <param name="commandedThrottle">current throttle state</param>
    /// <param name="flowMult">a multiplier to fuel flow (and thus thrust)--Isp unchanged</param>
    /// <param name="ispMult">a multiplier to Isp (and thus thrust)--fuel flow unchanged</param>
    virtual public void CalculatePerformance(double airRatio, double commandedThrottle, double flowMult, double ispMult)
    {
        
        fuelFlow = 0d;
        Isp = 0d;
        thrust = 0d;
    }
        
    // getters for base fields
    public void SetTPR(double t) { TPR = t; }
    public double GetThrust() { return thrust; }
    public double GetIsp() { return Isp; }
    public double GetFuelFlow() { return fuelFlow; }
    public double GetM0() { return M0; }

    // virtual getters
    virtual public double GetEngineTemp() { return 288.15d; }
    virtual public double GetArea() { return 0d; }
    virtual public double GetEmissive() { return 0d; }
    virtual public string GetStatus() { return statusString; }


    protected double CalculateGamma(double temperature, double fuel_fraction)
    {
        double gamma = 1.4 - 0.1 * Math.Max((temperature - 300) * 0.0005, 0) * (1 + fuel_fraction);
        gamma = Math.Min(1.4, gamma);
        gamma = Math.Max(1.1, gamma);
        return gamma;
    }

    protected double CalculateCp(double temperature, double fuel_fraction)
    {
        double Cp = 1004.5 + 250 * Math.Max((temperature - 300) * 0.0005, 0) * (1 + 10 * fuel_fraction);
        Cp = Math.Min(1404.5, Cp);
        Cp = Math.Max(1004.5, Cp);
        return Cp;
    }

}
