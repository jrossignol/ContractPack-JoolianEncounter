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
        private Rect windowPos = new Rect(320, 160f, 160f, 80f);

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
            if (GUILayout.Button("Boom goes the dynamite"))
            {
                JoolNova.DoNova();
            }
            GUI.DragWindow();
        }
    }
}
