using System;
using UnityEngine;
// Parts of this class are taken from FerramAerospaceResearch, Copyright 2014, Michael Ferrara, aka Ferram4, used with permission

namespace SolverEngines.EnginesGUI
{
    public static class GUIUtil
    {
        public const int FlightWindowID = 1024;
        public const int SettingsWindowID = 1025;
        public const int UnitsSettingsWindowID = 1026;

        public static GUIStyle LeftLabel { get; private set; }
        public static GUIStyle ButtonToggle { get; private set; }
        public static GUIStyle NormalToggle { get; private set; }

        private static bool stylesInitialized = false;

        public static GUILayoutOption smallWidth = GUILayout.Width(90);
        public static GUILayoutOption normalWidth = GUILayout.Width(150);
        public static GUILayoutOption wideWidth = GUILayout.Width(200);

        public static void SetupStyles()
        {
            LeftLabel = new GUIStyle(GUI.skin.label);
            LeftLabel.normal.textColor = LeftLabel.focused.textColor = LeftLabel.hover.textColor = LeftLabel.onNormal.textColor = LeftLabel.onFocused.textColor = LeftLabel.onHover.textColor = LeftLabel.onActive.textColor = Color.white;
            LeftLabel.padding = new RectOffset(4, 4, 4, 4);
            LeftLabel.alignment = TextAnchor.UpperLeft;

            ButtonToggle = new GUIStyle(GUI.skin.button);
            ButtonToggle.normal.textColor = ButtonToggle.focused.textColor = ButtonToggle.hover.textColor = ButtonToggle.onNormal.textColor = ButtonToggle.onFocused.textColor = ButtonToggle.onHover.textColor = ButtonToggle.onActive.textColor = Color.white;
            ButtonToggle.onNormal.background = ButtonToggle.onHover.background = ButtonToggle.onActive.background = ButtonToggle.active.background = HighLogic.Skin.button.onNormal.background;
            ButtonToggle.hover.background = ButtonToggle.normal.background;
            ButtonToggle.padding = new RectOffset(4, 4, 2, 2);
            ButtonToggle.alignment = TextAnchor.UpperLeft;
            ButtonToggle.stretchWidth = true;

            NormalToggle = new GUIStyle(GUI.skin.toggle);
            NormalToggle.normal.textColor = NormalToggle.focused.textColor = NormalToggle.hover.textColor = NormalToggle.onNormal.textColor = NormalToggle.onFocused.textColor = NormalToggle.onHover.textColor = NormalToggle.onActive.textColor = Color.white;
            NormalToggle.padding = new RectOffset(16, 4, 2, 2);
            NormalToggle.alignment = TextAnchor.MiddleLeft;

            stylesInitialized = true;
        }

        public static bool StylesInitialized
        {
            get
            {
                return stylesInitialized;
            }
        }

        public static void FlightWindowField(string title, string value)
        {
            GUILayout.BeginHorizontal();
            GUILayout.Box(title, LeftLabel, normalWidth);
            GUILayout.Box(value, LeftLabel, smallWidth);
            GUILayout.EndHorizontal();
        }

        public static void SettingsWindowLabel(string title)
        {
            GUILayout.Label(title, LeftLabel, wideWidth);
        }

        public static void SettingsWindowToggle(string title, ref bool value, bool invert=false)
        {
            if (invert)
                value = !value;
            value = GUILayout.Toggle(value, title, NormalToggle, wideWidth);
            if (invert)
                value = !value;
        }

        public static void UnitSelectionGrid<T>(ref GUIUnits.Units<T> units)
        {
            GUILayout.BeginVertical();
            GUILayout.Box(units.UnitTypeName + " Units:", LeftLabel, normalWidth);
            units = GUIUnits.UnitsAtIndexGUI<T>(GUILayout.SelectionGrid(units.IndexGUI, GUIUnits.UnitNamesGUI<T>(), GUIUnits.UnitsCountGUI<T>(), ButtonToggle, wideWidth));
            GUILayout.EndHorizontal();
        }

        // From FAR
        public static Rect ClampToScreen(Rect r)
        {
            r.x = Mathf.Clamp(r.x, -r.width + 20, Screen.width - 20);
            r.y = Mathf.Clamp(r.y, -r.height + 20, Screen.height - 20);
            return r;
        }
    }
}
