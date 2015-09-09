using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using UnityEngine;

namespace JoolianEncounter
{
    [KSPAddon(KSPAddon.Startup.MainMenu, true)]
    class TestWindow : MonoBehaviour
    {
        public static TestWindow Instance;

        private bool showGUI = false;
        private Rect windowPos = new Rect(480, 120f, 160f, 80f);

        void Start()
        {
            DontDestroyOnLoad(this);
            Instance = this;
        }

        void Update()
        {
            // Alt-F9 shows the contract configurator window
            if (GameSettings.MODIFIER_KEY.GetKey() && Input.GetKeyDown(KeyCode.F8))
            {
                showGUI = !showGUI;
            }

        }

        public void OnGUI()
        {
            if (showGUI)
            {
                var ainfoV = Attribute.GetCustomAttribute(typeof(TestWindow).Assembly, typeof(AssemblyInformationalVersionAttribute)) as AssemblyInformationalVersionAttribute;
                windowPos = GUILayout.Window(
                    typeof(TestWindow).FullName.GetHashCode(),
                    windowPos,
                    WindowGUI,
                    "Joolian Encounter " + ainfoV.InformationalVersion);
            }
        }

        private void WindowGUI(int windowID)
        {
            GUILayout.BeginVertical();
            GUILayout.Label("Jool");
            if (GUILayout.Button("Boom goes the dynamite"))
            {
                JoolNova.DoNova();
            }
            if (GUILayout.Button("Jool"))
            {
                JoolNova.SetTransformed(false);
            }
            if (GUILayout.Button("Sun"))
            {
                JoolNova.SetTransformed(true);
            }
            if (GUILayout.Button("Dump Component Debug"))
            {
                foreach (Component compo in PSystemManager.Instance.localBodies.Find(b => b.name == "Jool").
                    scaledBody.gameObject.GetComponentsInChildren<Component>(true))
                {
                    Debug.Log("    jool scaledBody compo: " + compo);
                }
            }
            GUILayout.Label("Kerbal");
            if (GUILayout.Button("Ghost") && FlightGlobals.ActiveVessel != null && FlightGlobals.ActiveVessel.vesselType == VesselType.EVA)
            {
                GhostKerbal.MakeGhost(FlightGlobals.ActiveVessel);
            }
            GUILayout.EndVertical();
            GUI.DragWindow();
        }
    }
}
