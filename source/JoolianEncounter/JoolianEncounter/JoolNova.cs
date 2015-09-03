﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using UnityEngine;

namespace JoolianEncounter
{
    [KSPScenario(ScenarioCreationOptions.AddToExistingCareerGames | ScenarioCreationOptions.AddToNewCareerGames,
        GameScenes.FLIGHT, GameScenes.TRACKSTATION, GameScenes.SPACECENTER)]
    class JoolNova : ScenarioModule
    {
        private enum State
        {
            IDLE,
            JOOL_SHRINK,
            SUN_GROW,
            EXPLOSION_GROW,
        }

        public static JoolNova Instance;

        private const float SHRINK_RATE = 0.08f;
        private const float GROW_RATE = 1.0f;
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

        private bool burninating = false;
        private bool explosionsDone = false;

        private static bool transformed = false;

        private float startupTime = 0;

        void Start()
        {
            Instance = this;
            GameEvents.onGameSceneSwitchRequested.Add(new EventData<GameEvents.FromToAction<GameScenes, GameScenes>>.OnEvent(GameSceneSwitch));
        }

        void Destroy()
        {
            Instance = null;
            GameEvents.onGameSceneSwitchRequested.Remove(new EventData<GameEvents.FromToAction<GameScenes, GameScenes>>.OnEvent(GameSceneSwitch));
        }

        void GameSceneSwitch(GameEvents.FromToAction<GameScenes, GameScenes> evt)
        {
            SetTransformed(false);
        }

        public static void SetTransformed(bool transformed)
        {
            Debug.Log("JoolNova - SetTransformed = " + transformed);
            // Initialize static variables
            if (jool == null)
            {
                Debug.Log("JoolNova - Doing one-time setup");
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

            // Remove the sun object
            if (secondSun != null && !transformed)
            {
                Destroy(secondSun.gameObject);
                secondSun = null;
            }

            // Do the transform
            if (JoolNova.transformed != transformed)
            {
                JoolNova.transformed = transformed;

                MeshRenderer joolRenderer = jool.scaledBody.GetComponent<MeshRenderer>();

                // Set the material
                joolRenderer.material = transformed ? sunMaterial : joolMaterial;

                // Turn components on/off
                joolRenderer.GetComponent<SunShaderController>().enabled = transformed;
                joolRenderer.GetComponent<ScaledSpaceFader>().enabled = !transformed;
                joolRenderer.GetComponent<MaterialSetDirection>().enabled = !transformed;
                joolRenderer.GetComponent<MeshRenderer>().enabled = true;

                // Turn gameObjects on/off
                foreach (Transform t in joolRenderer.transform)
                {
                    if (t != null && t.gameObject != null)
                    {
                        t.gameObject.SetActive((t.gameObject.name == "Atmosphere") ^ transformed);
                    }
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
            else if (state == State.EXPLOSION_GROW)
            {
                currentScale += GROW_RATE * Time.deltaTime;
            }

            // Not sure why, but if this gets done too early the light isn't initialized properly
            startupTime += Time.deltaTime;
            if (startupTime > 0.1f && transformed && secondSun == null)
            {
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
                secondSun.gameObject.SetActive(true);
                secondSun.sun = jool;
                secondSun.fadeEnd = 0.01f;
                secondSun.sunFlare.color = newSunColor;
                secondSun.sunFlare.brightness *= 0.25f;
                secondSun.enabled = true;
                secondSun.useLocalSpaceSunLight = true;
                secondSun.SunlightEnabled(transformed);
            }
        }

        private IEnumerator<YieldInstruction> NovaCoroutine()
        {
            Debug.Log("JoolNova - Jool is going nova");
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

            // Do the transformation
            SetTransformed(true);

            // Add our extra stuff
            GameObject explosion = new GameObject("Explosion");
            explosion.transform.parent = jool.scaledBody.transform;
            explosion.layer = jool.scaledBody.layer;
            explosion.transform.localScale = Vector3.one * 1.01f;
            explosion.transform.localPosition = Vector3.zero;
            explosion.transform.localRotation = Quaternion.identity;
            MeshRenderer explosionRenderer = explosion.AddComponent<MeshRenderer>();
            foreach (FieldInfo field in typeof(MeshRenderer).GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic))
            {
                field.SetValue(explosionRenderer, field.GetValue(jool.scaledBody.GetComponent<MeshRenderer>()));
            }
            Texture2D explosionTex = GameDatabase.Instance.GetTexture("ContractPacks/JoolianEncounter/Images/explosion", false);
            explosionRenderer.material.shader = Shader.Find("KSP/Alpha/Translucent");
            explosionRenderer.material.SetTexture(Shader.PropertyToID("_MainTex"), explosionTex);
            MeshFilter explosionMesh = explosion.AddComponent<MeshFilter>();
            explosionMesh.sharedMesh = jool.scaledBody.GetComponent<MeshFilter>().sharedMesh;
            explosion.SetActive(true);

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

            // Continue with the outer part of the explosion
            currentScale = 1.01f;
            state = State.EXPLOSION_GROW;
            explosionsDone = false;
            while (true)
            {
                // Rescale the explosion
                explosion.transform.localScale = Vector3.one * currentScale;

                // Stop burninating 1000 km after it has passed
                if (currentScale * jool.Radius >= FlightGlobals.ActiveVessel.altitude + jool.Radius + 1000000)
                {
                    burninating = false;
                }
                // There's a big delay before the atmospheric effects kick in, so start burninating pretty early (2500km)
                else if (!burninating && currentScale * jool.Radius >= FlightGlobals.ActiveVessel.altitude + jool.Radius - 2500000)
                {
                    burninating = true;
                    StartCoroutine(Burninator());
                }

                // Blow up the ship! (part of it)
                if (!explosionsDone && currentScale * jool.Radius >= FlightGlobals.ActiveVessel.altitude + jool.Radius)
                {
                    ExplodeParts();
                }

                if (currentScale >= 10.0f)
                {
                    break;
                }
                yield return null;
            }

            // Remove explodey part
            burninating = false;
            explosion.transform.parent = null;
            Destroy(explosion);
        }

        public IEnumerator<YieldInstruction> Burninator()
        {
            while (burninating && FlightGlobals.ActiveVessel != null && FlightGlobals.ActiveVessel.state  != Vessel.State.DEAD)
            {
                Vessel v = FlightGlobals.ActiveVessel;
                v.srf_velocity = -jool.GetSurfaceNVector(v.latitude, v.longitude);
                v.externalTemperature = 1000f;
                v.atmDensity = 1.0f;
                v.speed = 3000f;
                v.mach = 4f;

                yield return new WaitForFixedUpdate();
            }
        }

        // TODO - this only works for loaded vessels right now
        private void ExplodeParts()
        {
            // Find suitable vessels
            string[] validBodyNames = new string[] { "Jool", "Laythe", "Vall", "Tylo" };
            foreach (Vessel v in FlightGlobals.Vessels.Where(v =>
            {
                if (v == null || v.mainBody == null || !validBodyNames.Contains(v.mainBody.name))
                {
                    return false;
                }

                if (v.mainBody == jool)
                {
                    return v.altitude <= 85000000;
                }

                if (v.mainBody.name == "Laythe" && v.situation == Vessel.Situations.LANDED)
                {
                    return false;
                }

                // Are we hidden by the planet?
                Vector3 vdiff = jool.transform.position - v.mainBody.transform.position;
                Vector3 pdiff = jool.transform.position - v.mainBody.transform.position;
                double t = Vector3.Dot(vdiff, pdiff) / pdiff.sqrMagnitude;
                if (t > 0)
                {
                    return false;
                }
                double d2 = Vector3.Cross(pdiff, vdiff).sqrMagnitude / pdiff.sqrMagnitude;
                return d2 > v.mainBody.Radius * v.mainBody.Radius;
            }))
            {
                // Find suitable parts
                List<Part> parts = new List<Part>();
                foreach (Part part in v.parts.Where(p => 
                {
                    if (p.maxTemp < 2000f)
                    {
                        return true;
                    }
                    
                    foreach (PartModule pm in p.Modules)
                    {
                        if (pm.name == "ModuleDeployableSolarPanel")
                        {
                            ModuleDeployableSolarPanel solarPanel = pm as ModuleDeployableSolarPanel;
                            if (solarPanel != null)
                            {
                                return solarPanel.panelState != ModuleDeployableSolarPanel.panelStates.RETRACTED;
                            }
                        }
                    }
                    return false;
                }))
                {
                    parts.Add(part);
                }

                // Blow them up
                foreach (Part p in parts)
                {
                    p.explode();
                }
            }
            explosionsDone = true;
        }

        public override void OnLoad(ConfigNode node)
        {
            bool transformed = node.HasValue("joolNova") && Boolean.Parse(node.GetValue("joolNova"));
            SetTransformed(transformed);
        }

        public override void OnSave(ConfigNode node)
        {
            node.AddValue("joolNova", transformed);
        }
    }
}
