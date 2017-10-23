using System;
using UnityEngine;

// Parts of this file are taken from FerramAerospaceResearch, Copyright 2015, Michael Ferrara, aka Ferram4, used with permission

namespace SolverEngines.EnginesGUI
{
    public class FlightGUISettings
    {
        private static Rect SettingsWindowPos;

        public static bool ShowSettingsWindow = false;

        // Display settings

        public static bool ShowAmbientTemp = true;
        public static bool ShowAmbientPressure = true;
        public static bool ShowRecoveryTemp = true;
        public static bool ShowRecoveryPressure = true;
        public static bool ShowInletPercent = true;
        public static bool ShowTPR = true;
        public static bool ShowInletPressureRatio = true;
        public static bool ShowThrust = true;
        public static bool ShowTWR = true;
        public static bool ShowTDR = true;
        public static bool ShowIsp = true;
        public static bool ShowTSFC = true;

        #region GUI

        public static void OnSettingsWindowGUI()
        {
            if (ShowSettingsWindow)
            {
                SettingsWindowPos = GUILayout.Window(GUIUtil.SettingsWindowID, SettingsWindowPos, SettingsWindowGUI, "Engines Flight GUI Settings", GUILayout.MinWidth(150));
            }
        }

        public static void SettingsWindowGUI(int windowID)
        {
            GUILayout.BeginVertical();

            GUIUtil.SettingsWindowToggle("Show Ambient Temperature", ref ShowAmbientTemp);
            GUIUtil.SettingsWindowToggle("Show Ambient Pressure", ref ShowAmbientPressure);
            GUIUtil.SettingsWindowToggle("Show Recovery Temperature", ref ShowRecoveryTemp);
            GUIUtil.SettingsWindowToggle("Show Recovery Pressure", ref ShowRecoveryPressure);
            GUIUtil.SettingsWindowToggle("Show Inlet Percentage", ref ShowInletPercent);
            GUIUtil.SettingsWindowToggle("Show Total Pressure Recovery", ref ShowTPR);
            GUIUtil.SettingsWindowToggle("Show Thrust", ref ShowThrust);
            GUIUtil.SettingsWindowToggle("Show Thrust to Weight Ratio", ref ShowTWR);
            GUIUtil.SettingsWindowToggle("Show Thrust / Drag", ref ShowTDR);
            GUIUtil.SettingsWindowToggle("Show Isp", ref ShowIsp);
            GUIUtil.SettingsWindowToggle("Show TSFC", ref ShowTSFC);

            GUILayout.EndVertical();

            GUI.DragWindow();

            SettingsWindowPos = GUIUtil.ClampToScreen(SettingsWindowPos);
        }

        #endregion

        #region Configs

        public static void LoadSettings(ref KSP.IO.PluginConfiguration config)
        {
            ShowAmbientTemp = config.GetValue("showAmbientTemp", true);
            ShowAmbientPressure = config.GetValue("showAmbientPressure", true);
            ShowRecoveryTemp = config.GetValue("showRecoveryTemp", true);
            ShowRecoveryPressure = config.GetValue("showRecoveryPressure", true);
            ShowInletPercent = config.GetValue("showInletPercent", true);
            ShowTPR = config.GetValue("showTPR", true);
            ShowInletPressureRatio = config.GetValue("showInletPressureRatio", true);
            ShowThrust = config.GetValue("showThrust", true);
            ShowTWR = config.GetValue("showTWR", true);
            ShowTDR = config.GetValue("showTDR", true);
            ShowIsp = config.GetValue("showIsp", true);
            ShowTSFC = config.GetValue("showTSFC", true);
        }

        public static void SaveSettings(ref KSP.IO.PluginConfiguration config)
        {
            config.SetValue("settingsWindowPos", SettingsWindowPos);

            config.SetValue("showAmbientTemp", ShowAmbientTemp);
            config.SetValue("showAmbientPressure", ShowAmbientPressure);
            config.SetValue("showRecoveryTemp", ShowRecoveryTemp);
            config.SetValue("showRecoveryPressure", ShowRecoveryPressure);
            config.SetValue("showInletPercent", ShowInletPercent);
            config.SetValue("showTPR", ShowTPR);
            config.SetValue("showInletPressureRatio", ShowInletPressureRatio);
            config.SetValue("showThrust", ShowThrust);
            config.SetValue("showTWR", ShowTWR);
            config.SetValue("showTDR", ShowTDR);
            config.SetValue("showIsp", ShowIsp);
            config.SetValue("showTSFC", ShowTSFC);
        }

        #endregion
    }
}
