using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;
using KSP;

namespace JoolianEncounter
{
    public class GhostKerbal
    {
        public static void MakeGhost(Vessel v)
        {
            foreach (Renderer renderer in v.gameObject.GetComponentsInChildren<Renderer>())
            {
                if (renderer.name == "kbEVA_flagDecals" || renderer.name.StartsWith("jetpack_base") ||
                    renderer.name.Contains("handle") || renderer.name.Contains("thruster") || renderer.name.Contains("tank") ||
                    renderer.name.Contains("pivot") || renderer.name.EndsWith("_a01") || renderer.name.EndsWith("_b01") ||
                    renderer.name == "helmet" || renderer.name == "visor")
                {
                    renderer.enabled = false;
                }
                else
                {
                    renderer.material.shader = Shader.Find(renderer.name.Contains("head") ? "KSP/Unlit" : "KSP/Alpha/Unlit Transparent");
                    renderer.material.renderQueue = renderer.name.Contains("pupil") ? 3003 :
                        renderer.name.Contains("eyeball") ? 3002 : 
                        renderer.name.Contains("head") ? 3001 : 3000;
                    if (!renderer.name.Contains("pupil"))
                    {
                        renderer.material.color = new Color(0.5f, 0.8f, 1.0f, 0.5f);
                    }
                }
            }
        }
    }
}
