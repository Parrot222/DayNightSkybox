using UnityEngine;

namespace DayNightSkybox
{
    // Places the moon on the sun's daily path and sends its direction to the skybox shader.
    // moonPhase is how far (in revolutions) the moon trails the sun: 0.5 = opposite the sun.
    [ExecuteAlways]
    public class MoonController : MonoBehaviour
    {
        [Header("Sun (Directional Light). Found automatically if empty")]
        public Light sun;

        [Header("Skybox material. Uses the Lighting skybox if empty")]
        public Material skyboxMaterial;

        [Header("Moon position: revolutions behind the sun (0.25 = 90 deg, 0.5 = opposite)")]
        [Range(0f, 1f)] public float moonPhase = 0.5f;

        [Header("Advance the moon a little every day (Play mode only)")]
        public bool autoAdvancePhase = false;
        public float daysPerLunarCycle = 29.5f;

        private static readonly int MoonDirectionId = Shader.PropertyToID("_Moon_Direction");
        private static readonly int MoonRightId = Shader.PropertyToID("_Moon_Right");
        private static readonly int MoonUpId = Shader.PropertyToID("_Moon_Up");

        private Vector3 lastSunDir;
        private bool hasLastSunDir;

        void Update()
        {
            if (sun == null) sun = FindSun();
            Material mat = skyboxMaterial != null ? skyboxMaterial : RenderSettings.skybox;
            if (sun == null || mat == null) return;

            Transform s = sun.transform;
            Vector3 sunDir = -s.forward; // toward the sun
            Vector3 dailyAxis = s.right;  // axis the sun turns around when its X angle changes

            if (autoAdvancePhase && Application.isPlaying)
            {
                if (hasLastSunDir)
                {
                    float delta = Vector3.SignedAngle(lastSunDir, sunDir, dailyAxis);
                    moonPhase = Mathf.Repeat(moonPhase + delta / 360f / daysPerLunarCycle, 1f);
                }
                lastSunDir = sunDir;
                hasLastSunDir = true;
            }

            Quaternion moonRot = Quaternion.AngleAxis(-moonPhase * 360f, dailyAxis) * s.rotation;

            // Axes come straight from the rotation, so the moon never breaks when overhead
            mat.SetVector(MoonDirectionId, -(moonRot * Vector3.forward));
            mat.SetVector(MoonRightId, -(moonRot * Vector3.right));
            mat.SetVector(MoonUpId, moonRot * Vector3.up);
        }

        Light FindSun()
        {
            if (RenderSettings.sun != null) return RenderSettings.sun;
            foreach (var l in FindObjectsByType<Light>(FindObjectsSortMode.None))
            {
                if (l.type == LightType.Directional) return l;
            }
            return null;
        }
    }
}
