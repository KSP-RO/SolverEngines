using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;
using KSP;

// Parts of this file are taken from FerramAerospaceResearch, Copyright 2015, Michael Ferrara, aka Ferram4, used with permission

namespace SolverEngines.EnginesGUI
{
    public class EnginesFlightGUI : VesselModule
    {
        private Vessel vessel;
        private SolverFlightSys flightSys;
        private bool inAtmosphere;

        // Toolbar stuff

        private static IButton AJEFlightButtonBlizzy = null;
        private static ApplicationLauncherButton AJEFlightButtonStock = null;

        // GUI stuff

        public static bool ShowAllUIFlight = true;
        public static bool ShowFlightGUIWindow = false;

        public static Rect FlightWindowPos;

        private static int windowDisplayField = 0; // Bitfield describing what is visible in the flight window - used to determine when the height needs to be updated

        private void Awake()
        {
            FlightDataWrapper.Init();
        }

        private void Start()
        {
            vessel = gameObject.GetComponent<Vessel>();
            flightSys = gameObject.GetComponent<SolverFlightSys>();
            this.enabled = true;
            FlightGUISettings.ShowSettingsWindow = false;
            GUIUnitsSettings.ShowUnitsSettingsWindow = false;

            if (vessel.isActiveVessel)
            {
                LoadSettingsFromConfig();
            }

            if (ToolbarManager.ToolbarAvailable)
                CreateToolbarButtonBlizzy();
            else
                CreateToolbarButtonStock();

            GameEvents.onShowUI.Add(ShowUI);
            GameEvents.onHideUI.Add(HideUI);
        }

        private void OnDestroy()
        {
            GameEvents.onShowUI.Remove(ShowUI);
            GameEvents.onHideUI.Remove(HideUI);
            SaveSettingsToConfig();

            if (AJEFlightButtonBlizzy != null)
                AJEFlightButtonBlizzy.Destroy();
        }

        #region GUI

        public void OnGUI()
        {
            if (!vessel.isActiveVessel || !ShowFlightGUIWindow || !ShowAllUIFlight)
                return;

            if (!GUIUtil.StylesInitialized)
                GUIUtil.SetupStyles();

            FlightWindowPos = GUILayout.Window(GUIUtil.FlightWindowID, FlightWindowPos, FlightWindowGUI, "Advanced Jet Engine", GUILayout.MinWidth(150));

            FlightGUISettings.OnSettingsWindowGUI();
            GUIUnitsSettings.OnUnitsSettingsWindowGUI();
        }

        public void FlightWindowGUI(int windowID)
        {
            inAtmosphere = vessel.altitude < vessel.mainBody.atmosphereDepth;
            int tmpDisplayField = windowDisplayField;
            windowDisplayField = 0;
            int counter = 1;

            GUILayout.BeginVertical();

            if (FlightGUISettings.ShowAmbientTemp)
            {
                GUIUtil.FlightWindowField("Ambient Temperature", GUIUnitsSettings.TemperatureUnits.Format(vessel.atmosphericTemperature, GUIUnits.Temperature.kelvin));
                windowDisplayField += counter;
            }
            counter *= 2;
            if (FlightGUISettings.ShowAmbientPressure)
            {
                GUIUtil.FlightWindowField("Ambient Pressure", GUIUnitsSettings.PressureUnits.Format(vessel.staticPressurekPa, GUIUnits.Pressure.kPa));
                windowDisplayField += counter;
            }
            counter *= 2;
            if (FlightGUISettings.ShowRecoveryTemp && inAtmosphere && flightSys.InletArea > 0f && flightSys.EngineArea > 0f)
            {
                GUIUtil.FlightWindowField("Recovery Temperature", GUIUnitsSettings.TemperatureUnits.Format(flightSys.InletTherm.T, GUIUnits.Temperature.kelvin));
                windowDisplayField += counter;
            }
            counter *= 2;
            if (FlightGUISettings.ShowRecoveryPressure && inAtmosphere && flightSys.InletArea > 0f && flightSys.EngineArea > 0f)
            {
                GUIUtil.FlightWindowField("Recovery Pressure", GUIUnitsSettings.PressureUnits.Format(flightSys.InletTherm.P, GUIUnits.Pressure.Pa));
                windowDisplayField += counter;
            }
            counter *= 2;
            if (FlightGUISettings.ShowInletPercent && inAtmosphere && flightSys.EngineArea > 0f)
            {
                GUIStyle inletPercentStyle = new GUIStyle(GUIUtil.LeftLabel);
                float.IsInfinity(flightSys.AreaRatio);

                string areaPercentString = "";
                if (float.IsInfinity(flightSys.AreaRatio) || float.IsNaN(flightSys.AreaRatio))
                    areaPercentString = "n/a";
                else
                {
                    areaPercentString = flightSys.AreaRatio.ToString("P2");
                    Color inletPercentColor = Color.white;
                    if (flightSys.AreaRatio >= 1)
                        inletPercentColor = Color.green;
                    else if (flightSys.AreaRatio < 1)
                        inletPercentColor = Color.red;
                    inletPercentStyle.normal.textColor = inletPercentStyle.focused.textColor = inletPercentStyle.hover.textColor = inletPercentStyle.onNormal.textColor = inletPercentStyle.onFocused.textColor = inletPercentStyle.onHover.textColor = inletPercentStyle.onActive.textColor = inletPercentColor;
                }

                GUIUtil.FlightWindowField("Inlet", areaPercentString);
                windowDisplayField += counter;
            }
            counter *= 2;

            if (FlightGUISettings.ShowTPR && inAtmosphere && flightSys.InletArea > 0f && flightSys.EngineArea > 0f)
            {
                GUIUtil.FlightWindowField("TPR", flightSys.OverallTPR.ToString("P2"));
                windowDisplayField += counter;
            }
            counter *= 2;

            if (FlightGUISettings.ShowInletPressureRatio && inAtmosphere && flightSys.InletArea > 0f && flightSys.EngineArea > 0f)
            {
                GUIUtil.FlightWindowField("Inlet Pressure Ratio", (flightSys.InletTherm.P / flightSys.AmbientTherm.P).ToString("F2"));
                windowDisplayField += counter;
            }
            counter *= 2;

            double totalThrust = 0;
            double totalMDot = 0;

            if (FlightGUISettings.ShowThrust || FlightGUISettings.ShowTWR || FlightGUISettings.ShowTDR || FlightGUISettings.ShowIsp || FlightGUISettings.ShowTSFC)
            {
                List<ModuleEngines> allEngines = flightSys.EngineList;
                for (int i = 0; i < allEngines.Count; i++)
                {
                    if (allEngines[i].EngineIgnited)
                    {
                        totalThrust += allEngines[i].finalThrust; // kN
                        totalMDot += allEngines[i].finalThrust / allEngines[i].realIsp;
                    }
                }
                totalMDot /= 9.81d;
                totalMDot *= 1000d; // kg/s
            }

            if (FlightGUISettings.ShowThrust)
            {
                GUIUtil.FlightWindowField("Thrust", GUIUnitsSettings.ForceUnits.Format(totalThrust, GUIUnits.Force.kN));
                windowDisplayField += counter;
            }
            counter *= 2;
            if (FlightGUISettings.ShowTWR)
            {
                double TWR = totalThrust / (FlightGlobals.getGeeForceAtPosition(vessel.CoM).magnitude * vessel.GetTotalMass());

                GUIUtil.FlightWindowField("TWR", TWR.ToString("F2"));
                windowDisplayField += counter;
            }
            counter *= 2;
            
            if (FlightGUISettings.ShowTDR && inAtmosphere)
            {
                double totalDrag = FlightDataWrapper.VesselTotalDragkN(vessel);
                double tdr = 0;
                if (FlightDataWrapper.VesselDynPreskPa(vessel) > 0.01d)
                    tdr = totalThrust / totalDrag;

                GUIUtil.FlightWindowField("Thrust / Drag", tdr.ToString("G3"));
                windowDisplayField += counter;
            }
            counter *= 2;

            if (FlightGUISettings.ShowIsp)
            {
                double Isp = 0d;
                if (totalThrust > 0d && totalMDot > 0d)
                    Isp = totalThrust / totalMDot; // kN/(kg/s) = km/s

                GUIUtil.FlightWindowField("Specific Impulse", GUIUnitsSettings.IspUnits.Format(Isp, GUIUnits.Isp.km__s));
                windowDisplayField += counter;
            }
            counter *= 2;
            if (FlightGUISettings.ShowTSFC)
            {
                double SFC = 0d;
                if (totalThrust > 0d && totalMDot > 0d)
                    SFC = totalMDot / totalThrust; // (kg/s)/kN

                GUIUtil.FlightWindowField("TSFC", GUIUnitsSettings.TSFCUnits.Format(SFC, GUIUnits.TSFC.kg__kN_s));
                windowDisplayField += counter;
            }

            if (windowDisplayField != tmpDisplayField)
                FlightWindowPos.height = 0;

            GUILayout.BeginHorizontal();
            FlightGUISettings.ShowSettingsWindow = GUILayout.Toggle(FlightGUISettings.ShowSettingsWindow, "Settings", GUIUtil.ButtonToggle, GUIUtil.normalWidth);
            GUIUnitsSettings.ShowUnitsSettingsWindow = GUILayout.Toggle(GUIUnitsSettings.ShowUnitsSettingsWindow, "Units", GUIUtil.ButtonToggle, GUIUtil.smallWidth);
            GUILayout.EndHorizontal();

            GUILayout.EndVertical();

            GUI.DragWindow();

            FlightWindowPos = GUIUtil.ClampToScreen(FlightWindowPos);
        }

        #endregion

        #region Configs

        public void LoadSettingsFromConfig()
        {
            KSP.IO.PluginConfiguration config = KSP.IO.PluginConfiguration.CreateForType<EnginesFlightGUI>();
            config.load();

            FlightWindowPos = config.GetValue("flightWindowPos", new Rect());
            //FlightGUI.ShowFlightGUIWindow = config.GetValue("showFlightWindow", false);

            FlightGUISettings.LoadSettings(ref config);
            GUIUnitsSettings.LoadSettings(ref config);
        }

        public void SaveSettingsToConfig()
        {
            KSP.IO.PluginConfiguration config = KSP.IO.PluginConfiguration.CreateForType<EnginesFlightGUI>();
            //config.SetValue("showFlightWindow", FlightGUI.ShowFlightGUIWindow);

            config.SetValue("flightWindowPos", FlightWindowPos);

            FlightGUISettings.SaveSettings(ref config);
            GUIUnitsSettings.SaveSettings(ref config);

            config.save();
        }

        #endregion

        #region Toolbar Methods

        private static void CreateToolbarButtonBlizzy()
        {
            if (AJEFlightButtonBlizzy == null)
            {
                AJEFlightButtonBlizzy = ToolbarManager.Instance.add("AJE", "AJEFlightButton");
                AJEFlightButtonBlizzy.TexturePath = "SolverEngines/Icons/AJEIconBlizzy";
                AJEFlightButtonBlizzy.ToolTip = "Advanced Jet Engine";
                AJEFlightButtonBlizzy.OnClick += (e) => ShowFlightGUIWindow = !ShowFlightGUIWindow;
            }
        }

        private static void CreateToolbarButtonStock()
        {
            if (ApplicationLauncher.Ready && AJEFlightButtonStock == null)
            {
                AJEFlightButtonStock = ApplicationLauncher.Instance.AddModApplication(
                    onAppLaunchToggleOn,
                    onAppLaunchToggleOff,
                    DummyVoid,
                    DummyVoid,
                    DummyVoid,
                    DummyVoid,
                    ApplicationLauncher.AppScenes.FLIGHT,
                    (Texture)GameDatabase.Instance.GetTexture("SolverEngines/Icons/AJEIconStock", false));
            }
        }

        private void HideUI()
        {
            ShowAllUIFlight = false;
        }

        private void ShowUI()
        {
            ShowAllUIFlight = true;
        }

        private static void DummyVoid() { }

        private static void onAppLaunchToggleOn()
        {
            ShowFlightGUIWindow = true;
        }

        private static void onAppLaunchToggleOff()
        {
            ShowFlightGUIWindow = false;
        }

        #endregion
    }
}
