using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
#if DAYNIGHT_INPUT_SYSTEM && ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem.UI;
#endif

namespace DayNightSkybox
{
    // Runtime panel for the sun: X/Y/Z rotation sliders, moon position slider,
    // time-of-day preset buttons and automatic time flow.
    // Also fades the sun light, ambient light and SRP lens flare at night.
    public class SunAngleUI : MonoBehaviour
    {
        [Header("Sun (Directional Light). Found automatically if empty")]
        public Light sun;

        [Header("Panel Layout")]
        public Vector2 panelPosition = new Vector2(20, -20); // from the top-left corner
        public float sliderWidth = 300f;
        public int fontSize = 18;

        [Header("Night Lighting (match the skybox Night Start / Night End)")]
        public bool fadeLightAtNight = true;
        public float nightStart = -0.15f; // sun height below this: full night
        public float nightEnd = 0.1f;     // sun height above this: full day
        [Range(0f, 1f)] public float nightLightScale = 0.05f;   // sun intensity left at night
        [Range(0f, 1f)] public float nightAmbientScale = 0.3f;  // ambient intensity left at night

        [System.Serializable]
        public class TimePreset
        {
            public string name;
            [Tooltip("Sun X angle: 0-180 is day (5 = sunrise, 175 = sunset), 180-360 is night (270 = sun straight below)")]
            public float sunX;
        }

        [Header("Time Of Day Buttons")]
        public TimePreset[] presets =
        {
            new TimePreset { name = "Dawn", sunX = 8f },
            new TimePreset { name = "Day", sunX = 50f },
            new TimePreset { name = "Dusk", sunX = 175f },
            new TimePreset { name = "Night", sunX = 200f },
            new TimePreset { name = "Midnight", sunX = 270f },
        };
        [Tooltip("Seconds to blend to a preset, 0 = instant")]
        public float transitionDuration = 1.5f;

        [Header("Moon Slider (shown when a MoonController exists, found automatically if empty)")]
        public MoonController moonController;

        [Header("Time Flow")]
        public bool timeFlowOnStart = false;
        [Tooltip("Seconds for one full day (one sun revolution)")]
        public float dayLengthSeconds = 120f;
        public float minDayLength = 10f;
        public float maxDayLength = 600f;

        [Header("Lens Flare (SRP): fades out below the horizon")]
        public float flareFadeStart = -0.02f; // sun height below this: flare off
        public float flareFadeEnd = 0.05f;    // sun height above this: full flare

        private readonly Slider[] sliders = new Slider[3];
        private readonly Text[] valueLabels = new Text[3];
        private readonly string[] axisNames = { "X", "Y", "Z" };
        private Slider moonSlider;
        private Text moonValueLabel;
        private Text timeFlowButtonLabel;
        private Text dayLengthLabel;
        private bool timeFlowing;
        private Coroutine transitionRoutine;

        private UnityEngine.Rendering.LensFlareComponentSRP sunFlare;
        private float baseFlareIntensity;
        private float baseLightIntensity;
        private float baseAmbientIntensity;

        void Start()
        {
            if (sun == null) sun = FindSun();
            if (sun == null)
            {
                Debug.LogWarning("SunAngleUI: no Directional Light found.");
                return;
            }

            baseLightIntensity = sun.intensity;
            baseAmbientIntensity = RenderSettings.ambientIntensity;
            sunFlare = sun.GetComponent<UnityEngine.Rendering.LensFlareComponentSRP>();
            if (sunFlare != null) baseFlareIntensity = sunFlare.intensity;

            if (moonController == null) moonController = FindAnyObjectByType<MoonController>();

            EnsureEventSystem();
            BuildUI();
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

        void EnsureEventSystem()
        {
            if (FindAnyObjectByType<EventSystem>() != null) return;

            var es = new GameObject("EventSystem", typeof(EventSystem));
#if DAYNIGHT_INPUT_SYSTEM && ENABLE_INPUT_SYSTEM
            es.AddComponent<InputSystemUIInputModule>();
#else
            es.AddComponent<StandaloneInputModule>();
#endif
        }

        void BuildUI()
        {
            var canvasGO = new GameObject("SunAngleCanvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            var canvas = canvasGO.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 100;
            var scaler = canvasGO.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);

            // Background panel
            float rowHeight = 40f;
            float labelWidth = 60f;
            float valueWidth = 70f;
            float padding = 12f;
            var panel = new GameObject("Panel", typeof(RectTransform), typeof(Image));
            panel.transform.SetParent(canvasGO.transform, false);
            panel.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.5f);
            var panelRT = panel.GetComponent<RectTransform>();
            panelRT.anchorMin = panelRT.anchorMax = panelRT.pivot = new Vector2(0, 1);
            panelRT.anchoredPosition = panelPosition;
            float buttonRowHeight = (presets != null && presets.Length > 0) ? 44f : 0f;
            float panelWidth = padding * 2 + labelWidth + sliderWidth + valueWidth;
            int timeRow = moonController != null ? 4 : 3;
            int sliderRows = timeRow + 1;
            panelRT.sizeDelta = new Vector2(panelWidth, padding * 2 + rowHeight * sliderRows + buttonRowHeight);

            Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            Vector3 euler = sun.transform.eulerAngles;

            // Sun rotation
            for (int i = 0; i < 3; i++)
            {
                float y = -padding - rowHeight * i - rowHeight / 2f;
                sliders[i] = CreateSliderRow(panel.transform, axisNames[i], font, y, 0f, 360f, euler[i],
                    padding, labelWidth, valueWidth, rowHeight, out valueLabels[i]);
                sliders[i].onValueChanged.AddListener(_ => ApplyRotation());
            }

            // Moon position
            if (moonController != null)
            {
                float y = -padding - rowHeight * 3 - rowHeight / 2f;
                moonSlider = CreateSliderRow(panel.transform, "Moon", font, y, 0f, 1f, moonController.moonPhase,
                    padding, labelWidth, valueWidth, rowHeight, out moonValueLabel);
                moonSlider.onValueChanged.AddListener(v =>
                {
                    moonController.moonPhase = v;
                    moonValueLabel.text = MoonOffsetText(v);
                });
                moonValueLabel.text = MoonOffsetText(moonController.moonPhase);
            }

            // Time flow: play/pause button + day length slider.
            // The slider row goes first so its empty label does not cover the button.
            {
                float y = -padding - rowHeight * timeRow - rowHeight / 2f;
                var dayLengthSlider = CreateSliderRow(panel.transform, "", font, y, minDayLength, maxDayLength, dayLengthSeconds,
                    padding, labelWidth, valueWidth, rowHeight, out dayLengthLabel);
                dayLengthSlider.onValueChanged.AddListener(v =>
                {
                    dayLengthSeconds = v;
                    dayLengthLabel.text = v.ToString("0") + "s";
                });
                dayLengthLabel.text = dayLengthSeconds.ToString("0") + "s";

                var playButton = CreateButton(panel.transform, "Button_TimeFlow", font, fontSize - 2,
                    new Vector2(padding - 4f, y), new Vector2(labelWidth, rowHeight - 8f), out timeFlowButtonLabel);
                playButton.onClick.AddListener(() => SetTimeFlow(!timeFlowing));
                SetTimeFlow(timeFlowOnStart);
            }

            // Time of day presets
            if (buttonRowHeight > 0f)
            {
                float spacing = 6f;
                int count = presets.Length;
                float buttonWidth = (panelWidth - padding * 2 - spacing * (count - 1)) / count;
                float y = -padding - rowHeight * sliderRows - buttonRowHeight / 2f;
                for (int i = 0; i < count; i++)
                {
                    var preset = presets[i];
                    var button = CreateButton(panel.transform, "Button_" + preset.name, font, fontSize,
                        new Vector2(padding + i * (buttonWidth + spacing), y), new Vector2(buttonWidth, buttonRowHeight - 10f), out var label);
                    label.text = preset.name;
                    button.onClick.AddListener(() => GoToPreset(preset.sunX));
                }
            }

            ApplyRotation();
        }

        Slider CreateSliderRow(Transform parent, string label, Font font, float y, float min, float max, float value,
            float padding, float labelWidth, float valueWidth, float rowHeight, out Text valueLabel)
        {
            CreateText(parent, label, font, new Vector2(padding, y), new Vector2(labelWidth, rowHeight), TextAnchor.MiddleLeft);

            var sliderGO = DefaultControls.CreateSlider(new DefaultControls.Resources());
            sliderGO.name = "Slider_" + label;
            sliderGO.transform.SetParent(parent, false);
            var srt = sliderGO.GetComponent<RectTransform>();
            srt.anchorMin = srt.anchorMax = new Vector2(0, 1);
            srt.pivot = new Vector2(0, 0.5f);
            srt.anchoredPosition = new Vector2(padding + labelWidth, y);
            srt.sizeDelta = new Vector2(sliderWidth - 10f, 20f);

            var slider = sliderGO.GetComponent<Slider>();
            slider.minValue = min;
            slider.maxValue = max;
            slider.value = value;

            valueLabel = CreateText(parent, "", font,
                new Vector2(padding + labelWidth + sliderWidth, y), new Vector2(valueWidth, rowHeight), TextAnchor.MiddleRight);
            return slider;
        }

        Button CreateButton(Transform parent, string name, Font font, int size, Vector2 pos, Vector2 rectSize, out Text label)
        {
            var go = DefaultControls.CreateButton(new DefaultControls.Resources());
            go.name = name;
            go.transform.SetParent(parent, false);
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = new Vector2(0, 1);
            rt.pivot = new Vector2(0, 0.5f);
            rt.anchoredPosition = pos;
            rt.sizeDelta = rectSize;
            label = go.GetComponentInChildren<Text>();
            label.font = font;
            label.fontSize = size;
            return go.GetComponent<Button>();
        }

        Text CreateText(Transform parent, string content, Font font, Vector2 pos, Vector2 size, TextAnchor align)
        {
            var go = new GameObject("Text", typeof(RectTransform), typeof(Text));
            go.transform.SetParent(parent, false);
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = new Vector2(0, 1);
            rt.pivot = new Vector2(0, 0.5f);
            rt.anchoredPosition = pos;
            rt.sizeDelta = size;
            var t = go.GetComponent<Text>();
            t.font = font;
            t.fontSize = fontSize;
            t.color = Color.white;
            t.alignment = align;
            t.text = content;
            return t;
        }

        // How far the moon trails the sun, in degrees
        string MoonOffsetText(float phase)
        {
            return (Mathf.Repeat(phase, 1f) * 360f).ToString("0") + "°";
        }

        void SetTimeFlow(bool on)
        {
            timeFlowing = on;
            if (timeFlowButtonLabel != null) timeFlowButtonLabel.text = on ? "Pause" : "Play";
        }

        void Update()
        {
            // Time flow (paused while a preset transition is running)
            if (timeFlowing && transitionRoutine == null && sliders[0] != null && dayLengthSeconds > 0f)
            {
                sliders[0].value = Mathf.Repeat(sliders[0].value + 360f / dayLengthSeconds * Time.deltaTime, 360f);
            }

            // Keep the moon slider in sync when MoonController advances it automatically
            if (moonSlider != null && moonController != null && !Mathf.Approximately(moonSlider.value, moonController.moonPhase))
            {
                moonSlider.SetValueWithoutNotify(moonController.moonPhase);
                moonValueLabel.text = MoonOffsetText(moonController.moonPhase);
            }
        }

        void GoToPreset(float targetX)
        {
            if (transitionRoutine != null) StopCoroutine(transitionRoutine);
            if (transitionDuration <= 0f)
            {
                sliders[0].value = Mathf.Repeat(targetX, 360f);
                return;
            }
            transitionRoutine = StartCoroutine(TransitionRoutine(targetX));
        }

        System.Collections.IEnumerator TransitionRoutine(float targetX)
        {
            float startX = sliders[0].value;
            float elapsed = 0f;
            while (elapsed < transitionDuration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.SmoothStep(0f, 1f, elapsed / transitionDuration);
                // shortest angular path
                sliders[0].value = Mathf.Repeat(Mathf.LerpAngle(startX, targetX, t), 360f);
                yield return null;
            }
            sliders[0].value = Mathf.Repeat(targetX, 360f);
            transitionRoutine = null;
        }

        void ApplyRotation()
        {
            var euler = new Vector3(sliders[0].value, sliders[1].value, sliders[2].value);
            sun.transform.rotation = Quaternion.Euler(euler);
            for (int i = 0; i < 3; i++)
            {
                valueLabels[i].text = euler[i].ToString("0") + "°";
            }

            float sunHeight = Vector3.Dot(-sun.transform.forward, Vector3.up); // the sun is at -forward
            if (fadeLightAtNight)
            {
                float day = SmoothStep01(nightStart, nightEnd, sunHeight); // same curve as the shader
                sun.intensity = baseLightIntensity * Mathf.Lerp(nightLightScale, 1f, day);
                RenderSettings.ambientIntensity = baseAmbientIntensity * Mathf.Lerp(nightAmbientScale, 1f, day);
            }
            if (sunFlare != null)
            {
                float visible = SmoothStep01(flareFadeStart, flareFadeEnd, sunHeight);
                sunFlare.intensity = baseFlareIntensity * visible;
                sunFlare.enabled = visible > 0.001f;
            }
        }

        static float SmoothStep01(float edge0, float edge1, float x)
        {
            float t = Mathf.Clamp01(Mathf.InverseLerp(edge0, edge1, x));
            return t * t * (3f - 2f * t);
        }

        void OnDestroy()
        {
            // Restore scene lighting when leaving Play mode
            if (sunFlare != null)
            {
                sunFlare.intensity = baseFlareIntensity;
                sunFlare.enabled = true;
            }
            if (sun != null && fadeLightAtNight)
            {
                sun.intensity = baseLightIntensity;
                RenderSettings.ambientIntensity = baseAmbientIntensity;
            }
        }
    }
}
