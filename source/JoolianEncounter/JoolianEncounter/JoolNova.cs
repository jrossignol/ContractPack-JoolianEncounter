using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using UnityEngine;

namespace JoolianEncounter
{
    [KSPAddon(KSPAddon.Startup.Flight, false)]
    class JoolNova : MonoBehaviour
    {
        private enum State
        {
            IDLE,
            JOOL_SHRINK,
            SUN_GROW,
        }

        public static JoolNova Instance;

        private const float SHRINK_RATE = 0.08f;
        private const float GROW_RATE = 1.8f;
        private const float SHRUNK_SCALE = 0.005f;
        private const float FINAL_SCALE = 1.0f;

        private static Color newSunColor = new Color(0.82f, 0.26f, 0.03f, 0.5f);
        private static Color newSunColor2 = new Color(0.72f, 0.16f, 0.0f, 1.0f);
        private static Color newSunColor3 = new Color(1.0f, 0.35f, 0.1f, 1.0f);

        private static CelestialBody jool = null;
        private static CelestialBody sun = null;
        private static Material joolMaterial;
        private static Material sunMaterial;
        private static Sun secondSun = null;

        private State state = State.IDLE;
        private float currentScale;
        private Vector3 origScale;
        private Vector3 origAtmoScale;
        private float origInnerRadius;
        private float origOuterRadius;

        private static bool transformed = false;

        void Start()
        {
            Instance = this;
        }

        void Destroy()
        {
            Instance = null;
        }

        public void SetTransformed(bool transformed)
        {
            // Initialize static variables
            if (jool == null)
            {
                jool = PSystemManager.Instance.localBodies.Find(b => b.name == "Jool");
                sun = PSystemManager.Instance.localBodies.Find(b => b.name == "Sun");

                // Setup materials
                MeshRenderer sunRenderer = sun.scaledBody.GetComponent<MeshRenderer>();
                MeshRenderer joolRenderer = jool.scaledBody.GetComponent<MeshRenderer>();
                joolMaterial = joolRenderer.material;

                sunMaterial = Instantiate(sunRenderer.material) as Material;
                sunMaterial.SetColor(Shader.PropertyToID("_EmitColor0"), newSunColor);
                sunMaterial.SetColor(Shader.PropertyToID("_EmitColor1"), newSunColor2);
                sunMaterial.SetColor(Shader.PropertyToID("_RimColor"), newSunColor3);

                // Setup sun
                Sun old = Sun.Instance;
                secondSun = Instantiate(Sun.Instance) as Sun;
                Sun.Instance = old;
                foreach (FieldInfo field in typeof(Sun).GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic))
                {
                    if (field.GetValue(secondSun) == null)
                    {
                    field.SetValue(secondSun, field.GetValue(old));
                    }
                }
                secondSun.light.color = newSunColor;
                secondSun.enabled = false;
                secondSun.sun = jool;
                secondSun.fadeEnd = 0.01f;
                secondSun.SunlightEnabled(false);
                secondSun.sunFlare.color = newSunColor;
                secondSun.sunFlare.brightness *= 0.25f;
            }

            // Add missing components
            if (jool.scaledBody.GetComponent<SunShaderController>() == null)
            {
                // Add in sun shader 
                SunShaderController sunShader = sun.scaledBody.gameObject.GetComponent<SunShaderController>();
                SunShaderController joolSunShader = jool.scaledBody.gameObject.AddComponent<SunShaderController>();
                foreach (FieldInfo field in typeof(SunShaderController).GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic))
                {
                    field.SetValue(joolSunShader, field.GetValue(sunShader));
                }
                joolSunShader.enabled = false;

                // Add in the coronas
                foreach (Transform t in sun.scaledBody.transform)
                {
                    if (t != null && t.gameObject != null)
                    {
                        GameObject corona = (GameObject)Instantiate(t.gameObject);
                        corona.name = "Corona";
                        corona.transform.parent = jool.scaledBody.transform;
                        corona.SetActive(false);

                        corona.transform.localPosition = Vector3.zero;

                        MeshRenderer coronaRenderer = corona.GetComponent<MeshRenderer>();

                        Texture2D coronaTex = GameDatabase.Instance.GetTexture("ContractPacks/JoolianEncounter/Images/redsuncorona", false);
                        coronaRenderer.material = Instantiate(coronaRenderer.material) as Material;
                        coronaRenderer.material.SetColor(Shader.PropertyToID("_TintColor"), newSunColor);
                        coronaRenderer.material.SetTexture(Shader.PropertyToID("_MainTex"), coronaTex);
                    }
                }
            }

            // Do the transform
            if (JoolNova.transformed != transformed)
            {
                JoolNova.transformed = transformed;

                MeshRenderer joolRenderer = jool.scaledBody.GetComponent<MeshRenderer>();

                // Turn components on/off
                joolRenderer.GetComponent<SunShaderController>().enabled = transformed;
                joolRenderer.GetComponent<ScaledSpaceFader>().enabled = !transformed;
                joolRenderer.GetComponent<MaterialSetDirection>().enabled = !transformed;
                secondSun.enabled = transformed;
                secondSun.SunlightEnabled(transformed);

                // Turn gameObjects on/off
                foreach (Transform t in joolRenderer.transform)
                {
                    if (t != null && t.gameObject != null)
                    {
                        t.gameObject.SetActive((t.gameObject.name == "Atmosphere") ^ transformed);
                    }
                }

                // Do the transform
                if (transformed)
                {
                    joolRenderer.material = sunMaterial;
                }
                // Undo the transform
                else
                {
                    joolRenderer.material = joolMaterial;
                }
            }
        }

        public static void DoNova()
        {
            if (Instance != null)
            {
                Instance.StartCoroutine(Instance.NovaCoroutine());
            }
        }

        private void Update()
        {
            if (state == State.JOOL_SHRINK)
            {
                currentScale -= SHRINK_RATE * Time.deltaTime;
            }
            else if (state == State.SUN_GROW)
            {
                currentScale += GROW_RATE * Time.deltaTime;
            }
        }

        private IEnumerator<YieldInstruction> NovaCoroutine()
        {
            Debug.Log("Jool is going nova");
            state = State.JOOL_SHRINK;
            currentScale = 1.0f;

            SetTransformed(false);

            // Get the AtmosphereFromGround object
            AtmosphereFromGround afg = Resources.FindObjectsOfTypeAll<AtmosphereFromGround>().
                Where(a => a != null && a.planet != null && a.planet.name == "Jool").First();

            // Capture original scaling info
            origScale = jool.scaledBody.transform.localScale;
            origInnerRadius = afg.innerRadius;
            origOuterRadius = afg.outerRadius;
            origAtmoScale = afg.transform.localScale;

            // Rescale over every iteration
            while (true)
            {
                // Check if this will be the last scaling operation
                bool lastIteration = currentScale <= SHRUNK_SCALE;
                if (lastIteration)
                {
                    currentScale = SHRUNK_SCALE;
                }

                // Rescale Jool's ScaledVersion Transform
                jool.scaledBody.transform.localScale = origScale * currentScale;

                // Rescale atmosphere
                afg.innerRadius = origInnerRadius * currentScale;
                afg.outerRadius = origOuterRadius * currentScale;
                afg.transform.localScale = origAtmoScale * currentScale;

                afg.KrESun = afg.Kr * afg.ESun;
                afg.KmESun = afg.Km * afg.ESun;
                afg.Kr4PI = afg.Kr * 4f * (float)Math.PI;
                afg.Km4PI = afg.Km * 4f * (float)Math.PI;
                afg.g2 = afg.g * afg.g;
                afg.outerRadius2 = afg.outerRadius * afg.outerRadius;
                afg.innerRadius2 = afg.innerRadius * afg.innerRadius;
                afg.scale = 1f / (afg.outerRadius - afg.innerRadius);
                afg.scaleDepth = -0.25f;
                afg.scaleOverScaleDepth = afg.scale / afg.scaleDepth;

                MethodInfo setMaterial = typeof(AtmosphereFromGround).GetMethod("SetMaterial", BindingFlags.NonPublic | BindingFlags.Instance);
                setMaterial.Invoke(afg, new object[] { true });

                yield return null;

                if (lastIteration)
                {
                    break;
                }
            }

            foreach (Component compo in jool.scaledBody.gameObject.GetComponentsInChildren<Component>(true))
            {
                Debug.Log("    jool scaledBody compo: " + compo);
            }
            foreach (Component compo in sun.scaledBody.gameObject.GetComponentsInChildren<Component>(true))
            {
                Debug.Log("    sun scaledBody compo: " + compo);
            }

            // Do the transformation
            SetTransformed(true);

            // Rescale over every iteration
            currentScale = SHRUNK_SCALE;
            state = State.SUN_GROW;
            while (true)
            {
                // Check if this will be the last scaling operation
                bool lastIteration = currentScale >= FINAL_SCALE;
                if (lastIteration)
                {
                    currentScale = FINAL_SCALE;
                }

                // Rescale Jool's ScaledVersion Transform
                jool.scaledBody.transform.localScale = origScale * currentScale;

                if (lastIteration)
                {
                    break;
                }
                yield return null;
            }
        }
    }
}
