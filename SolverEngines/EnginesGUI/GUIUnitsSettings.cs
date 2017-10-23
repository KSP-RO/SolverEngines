using System;
using UnityEngine;

// Parts of this file are taken from FerramAerospaceResearch, Copyright 2015, Michael Ferrara, aka Ferram4, used with permission

namespace SolverEngines.EnginesGUI
{
    public static class GUIUnitsSettings
    {
        private static Rect UnitsSettingsWindowPos;

        public static GUIUnits.Units<GUIUnits.Temperature> TemperatureUnits = GUIUnits.Temperature.kelvin;
        public static GUIUnits.Units<GUIUnits.Pressure> PressureUnits = GUIUnits.Pressure.kPa;
        public static GUIUnits.Units<GUIUnits.Force> ForceUnits = GUIUnits.Force.kN;
        public static GUIUnits.Units<GUIUnits.Isp> IspUnits = GUIUnits.Isp.s;
        public static GUIUnits.Units<GUIUnits.TSFC> TSFCUnits = GUIUnits.TSFC.kg__kgf_h;

        public static bool ShowUnitsSettingsWindow = false;

        #region GUI

        public static void OnUnitsSettingsWindowGUI()
        {
            if (ShowUnitsSettingsWindow)
            {
                UnitsSettingsWindowPos = GUILayout.Window(GUIUtil.UnitsSettingsWindowID, UnitsSettingsWindowPos, UnitsSettingsWindowGUI, "Engines GUI Units Settings", GUILayout.MinWidth(150));
            }
        }

        public static void UnitsSettingsWindowGUI(int windowID)
        {
            GUILayout.BeginVertical();

            GUIUtil.UnitSelectionGrid<GUIUnits.Temperature>(ref TemperatureUnits);
            GUIUtil.UnitSelectionGrid<GUIUnits.Pressure>(ref PressureUnits);
            GUIUtil.UnitSelectionGrid<GUIUnits.Force>(ref ForceUnits);
            GUIUtil.UnitSelectionGrid<GUIUnits.Isp>(ref IspUnits);
            GUIUtil.UnitSelectionGrid<GUIUnits.TSFC>(ref TSFCUnits);

            GUILayout.EndVertical();

            GUI.DragWindow();

            UnitsSettingsWindowPos = GUIUtil.ClampToScreen(UnitsSettingsWindowPos);
        }

        #endregion

        #region Configs

        public static void LoadSettings(ref KSP.IO.PluginConfiguration config)
        {
            UnitsSettingsWindowPos = config.GetValue("unitsSettingsWindowPos", new Rect());

            PressureUnits = GUIUnits.UnitsFromConfig<GUIUnits.Pressure>(ref config, GUIUnits.Pressure.kPa);
            TemperatureUnits = GUIUnits.UnitsFromConfig<GUIUnits.Temperature>(ref config, GUIUnits.Temperature.kelvin);
            ForceUnits = GUIUnits.UnitsFromConfig<GUIUnits.Force>(ref config, GUIUnits.Force.kN);
            IspUnits = GUIUnits.UnitsFromConfig<GUIUnits.Isp>(ref config, GUIUnits.Isp.s);
            TSFCUnits = GUIUnits.UnitsFromConfig<GUIUnits.TSFC>(ref config, GUIUnits.TSFC.kg__kgf_h);
        }

        public static void SaveSettings(ref KSP.IO.PluginConfiguration config)
        {
            config.SetValue("unitsSettingsWindowPos", UnitsSettingsWindowPos);

            PressureUnits.SaveToConfig(ref config);
            TemperatureUnits.SaveToConfig(ref config);
            ForceUnits.SaveToConfig(ref config);
            IspUnits.SaveToConfig(ref config);
            TSFCUnits.SaveToConfig(ref config);
        }

        #endregion
    }
}
