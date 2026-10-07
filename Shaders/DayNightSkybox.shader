// Day/night skybox for URP.
// Sun = main directional light. Moon position comes from MoonController (C#).
Shader "Skybox/Day Night Skybox"
{
    Properties
    {
        [Header(Cloud Panoramas)]
        _Texture ("Day Clouds", 2D) = "white" {}
        _NightTexture ("Night Clouds", 2D) = "white" {}
        _SunsetTexture ("Sunset Clouds", 2D) = "white" {}
        _UV ("Panorama Tiling (XY) Offset (ZW)", Vector) = (1, 1, 0, 0)
        _Color ("Cloud Tint", Color) = (1, 1, 1, 1)
        _Sky_Rotation_Speed ("Rotation Speed (deg/s)", Float) = 1

        [Header(Sky Gradient)]
        _DayColor ("Day Top", Color) = (0, 0.2785, 1, 1)
        _DayOcean ("Day Bottom", Color) = (0, 0, 0, 1)
        _NightTop ("Night Top", Color) = (0, 0, 0, 1)
        _NightBottom ("Night Bottom", Color) = (0, 0, 0, 1)
        _SunsetColorTop ("Sunset Top", Color) = (0, 0, 0, 1)
        _SunsetColorBottom ("Sunset Bottom", Color) = (0, 0, 0, 1)
        _SkyStep ("Top Blend (XY) Bottom Blend (ZW)", Vector) = (0, 1, 0, 1)

        [Header(Day Night Timing)]
        _Night_Start ("Night Start (sun height)", Float) = -0.15
        _Night_End ("Night End (sun height)", Float) = 0.1
        _SunsetTime ("Sunset Range Day (XY)", Vector) = (0, 1, 0, 0)
        _Night_Sunset_Time ("Sunset Range Night (XY)", Vector) = (1.26, 1.39, 0, 0)

        [Header(Sun)]
        [HDR] _SunColor ("Sun Color", Color) = (1, 1, 1, 1)
        [HDR] _SunsetColor ("Sunset Sun Color", Color) = (0, 0, 0, 1)
        _SunStep ("Disc (XY) Cloud Glow (ZW)", Vector) = (0, 1, 0, 1)
        _Sunset_Sun_Size ("Sunset Sun Size", Float) = 1.8
        _SunClouds ("Cloud Glow Brightness", Float) = 1
        _SunsetCloudColor ("Sunset Cloud Glow", Color) = (0, 0, 0, 1)
        _SunClouds_Alpha ("Cloud Glow Amount", Float) = 0
        _Sun_Horizon ("Horizon Cutoff (XY)", Vector) = (-0.02, 0.02, 0, 0)

        [Header(Cloud Occlusion)]
        _Mask ("Cloud Mask (G)", 2D) = "white" {}
        _Mask_Step ("Mask Range (XY)", Vector) = (0, 0, 0, 0)
        _Mask_Alpha ("See Through Clouds", Float) = 0

        [Header(Horizon Glow)]
        [HDR] _Horizon_Glow_Color ("Color", Color) = (1, 0.45, 0.15, 1)
        _Horizon_Glow_Intensity ("Intensity", Float) = 1.5
        _Horizon_Glow_Height ("Thinness", Float) = 8
        _Horizon_Glow_Spread ("Focus Toward Sun", Float) = 3

        [Header(Moon)]
        _Moon_Texture ("Moon Texture", 2D) = "white" {}
        [HDR] _Moon_Color ("Moon Color", Color) = (1, 1, 1, 1)
        _Moon_Size ("Moon Size", Float) = 0.1
        _Moon_Step ("Edge (XY)", Vector) = (0.998, 0.999, 0, 0)
        _Moon_Day_Visibility ("Daytime Visibility", Float) = 0.25
        _Moon_Power ("Sky Light Falloff", Float) = 1
        _Moon_Light_Step ("Sky Light Range (XY)", Vector) = (0, 1, 0, 0)
        _Moon_Intensity ("Sky Light Intensity", Float) = 1
        [HDR] _Halo_Color ("Halo Color", Color) = (0.6, 0.7, 1, 1)
        _Halo_Power ("Halo Tightness", Float) = 500
        _Halo_Intensity ("Halo Intensity", Float) = 0.5
        [HideInInspector] _Moon_Direction ("Moon Direction", Vector) = (0, 1, 0, 0)
        [HideInInspector] _Moon_Right ("Moon Right", Vector) = (1, 0, 0, 0)
        [HideInInspector] _Moon_Up ("Moon Up", Vector) = (0, 0, 1, 0)

        [Header(Stars)]
        _Star_Texture ("Star Texture", 2D) = "black" {}
        _Star_UV ("Tiling (XY) Offset (ZW)", Vector) = (1, 1, 0, 0)
        _Star_Intensity ("Intensity", Float) = 1
        _Star_Fade ("Fade By Height (XY)", Vector) = (0.1, 0.5, 0, 0)
        _Twinkle_Mask ("Twinkle Noise", 2D) = "grey" {}
        _Twinkle_Mask_UV ("Noise Tiling (XY) Scroll (ZW)", Vector) = (0.5, 0.5, 0.02, 0.01)
        _Twinkle_Speed ("Twinkle Speed", Float) = 3
        _Twinkle_Amount ("Twinkle Amount", Float) = 0.7
    }

    SubShader
    {
        Tags { "Queue" = "Background" "RenderType" = "Background" "RenderPipeline" = "UniversalPipeline" "PreviewType" = "Skybox" }
        Cull Off
        ZWrite Off

        Pass
        {
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            TEXTURE2D(_Texture);        SAMPLER(sampler_Texture);
            TEXTURE2D(_NightTexture);   SAMPLER(sampler_NightTexture);
            TEXTURE2D(_SunsetTexture);  SAMPLER(sampler_SunsetTexture);
            TEXTURE2D(_Mask);           SAMPLER(sampler_Mask);
            TEXTURE2D(_Moon_Texture);   SAMPLER(sampler_Moon_Texture);
            TEXTURE2D(_Star_Texture);   SAMPLER(sampler_Star_Texture);
            TEXTURE2D(_Twinkle_Mask);   SAMPLER(sampler_Twinkle_Mask);

            CBUFFER_START(UnityPerMaterial)
                float4 _UV, _Color, _DayColor, _DayOcean, _NightTop, _NightBottom;
                float4 _SunsetColorTop, _SunsetColorBottom, _SkyStep;
                float4 _SunsetTime, _Night_Sunset_Time;
                float4 _SunColor, _SunsetColor, _SunStep, _SunsetCloudColor, _Sun_Horizon;
                float4 _Mask_Step, _Horizon_Glow_Color;
                float4 _Moon_Color, _Moon_Step, _Moon_Light_Step, _Halo_Color;
                float4 _Moon_Direction, _Moon_Right, _Moon_Up;
                float4 _Star_UV, _Star_Fade, _Twinkle_Mask_UV;
                float _Sky_Rotation_Speed, _Night_Start, _Night_End;
                float _Sunset_Sun_Size, _SunClouds, _SunClouds_Alpha, _Mask_Alpha;
                float _Horizon_Glow_Intensity, _Horizon_Glow_Height, _Horizon_Glow_Spread;
                float _Moon_Size, _Moon_Day_Visibility, _Moon_Power, _Moon_Intensity;
                float _Halo_Power, _Halo_Intensity;
                float _Star_Intensity, _Twinkle_Speed, _Twinkle_Amount;
            CBUFFER_END

            struct Attributes { float4 positionOS : POSITION; };
            struct Varyings   { float4 positionCS : SV_POSITION; float3 dir : TEXCOORD0; };

            Varyings Vert(Attributes v)
            {
                Varyings o;
                o.positionCS = TransformObjectToHClip(v.positionOS.xyz);
                o.dir = v.positionOS.xyz;
                return o;
            }

            float4 Frag(Varyings i) : SV_Target
            {
                float3 dir = normalize(i.dir);
                float3 sunDir = _MainLightPosition.xyz;    // points toward the sun

                // Rotate the cloud/star layers around Y over time
                float a = radians(_Time.y * _Sky_Rotation_Speed);
                float s = sin(a), c = cos(a);
                float3 rdir = float3(c * dir.x + s * dir.z, dir.y, -s * dir.x + c * dir.z);

                // Equirectangular panorama UV (height: 0 = straight down, 0.5 = horizon, 1 = straight up)
                float height = asin(clamp(rdir.y, -1, 1)) / PI + 0.5;
                float2 skyUV = float2(atan2(rdir.x, rdir.z) / (2 * PI) + 0.5, height) * _UV.xy + _UV.zw;

                // Day/night and sunset factors
                float day = smoothstep(_Night_Start, _Night_End, sunDir.y);
                float night = 1 - day;
                float sunset = smoothstep(lerp(_SunsetTime.x, _Night_Sunset_Time.x, night),
                                          lerp(_SunsetTime.y, _Night_Sunset_Time.y, night),
                                          distance(float3(0, 1, 0), abs(sunDir)));
                float horizon = smoothstep(_Sun_Horizon.x, _Sun_Horizon.y, dir.y);

                // Cloud panoramas
                float3 clouds = lerp(SAMPLE_TEXTURE2D(_Texture, sampler_Texture, skyUV).rgb,
                                     SAMPLE_TEXTURE2D(_NightTexture, sampler_NightTexture, skyUV).rgb, night);
                clouds = lerp(clouds, SAMPLE_TEXTURE2D(_SunsetTexture, sampler_SunsetTexture, skyUV).rgb, sunset);
                clouds *= _Color.rgb;

                // Cloud mask: clouds hidden under the top gradient never block the sun/moon
                float cloudMask = smoothstep(_Mask_Step.x, _Mask_Step.y, SAMPLE_TEXTURE2D(_Mask, sampler_Mask, skyUV).g);
                float topBlend = smoothstep(_SkyStep.x, _SkyStep.y, height);
                float clearSky = lerp(cloudMask, 1, topBlend);
                float clearSkyMoon = lerp(clearSky, 1, _Mask_Alpha);

                // Sun glow lighting up nearby clouds
                float sunDist = distance(dir, sunDir) / lerp(1, _Sunset_Sun_Size, sunset);
                float3 cloudGlow = lerp(clouds, lerp(_SunClouds.xxx, _SunsetCloudColor.rgb, sunset), _SunClouds_Alpha);
                float3 col = lerp(clouds, cloudGlow, smoothstep(_SunStep.z, _SunStep.w, sunDist) * (1 - cloudMask) * day);

                // Moon disc (UV axes from MoonController, so it never breaks overhead)
                float moonDot = dot(_Moon_Direction.xyz, dir);
                float2 moonUV = float2(dot(dir, _Moon_Right.xyz), dot(dir, _Moon_Up.xyz)) / _Moon_Size + 0.5;
                float4 moonTex = SAMPLE_TEXTURE2D(_Moon_Texture, sampler_Moon_Texture, moonUV);
                float3 moonCol = moonTex.rgb * _Moon_Color.rgb;
                float moonMask = smoothstep(_Moon_Step.x, _Moon_Step.y, moonDot);

                // Top gradient (night top is lit by the moon and shows the moon)
                float moonSkyLight = smoothstep(_Moon_Light_Step.x, _Moon_Light_Step.y, pow(saturate(moonDot), _Moon_Power)) * _Moon_Intensity;
                float3 nightTop = lerp(_NightTop.rgb + moonSkyLight, moonCol, moonMask * clearSkyMoon);
                float3 topCol = lerp(lerp(_DayColor.rgb, nightTop, night), _SunsetColorTop.rgb, sunset);
                col = lerp(col, topCol, topBlend);

                // Bottom gradient (ocean)
                float3 bottomCol = lerp(lerp(_DayOcean.rgb, _NightBottom.rgb, night), _SunsetColorBottom.rgb, sunset);
                col = lerp(col, bottomCol, smoothstep(_SkyStep.z, _SkyStep.w, height));

                // Moon halo
                col += pow(saturate(moonDot), _Halo_Power) * _Halo_Color.rgb * _Halo_Intensity * night * horizon * clearSkyMoon;

                // Stars: planar projection onto a plane above, twinkling by a scrolling noise
                float2 starUV = (rdir.xz / max(rdir.y, 0.001)) * _Star_UV.xy + _Star_UV.zw;
                float3 stars = SAMPLE_TEXTURE2D(_Star_Texture, sampler_Star_Texture, starUV).rgb * _Star_Intensity;
                float2 twinkleUV = starUV * _Twinkle_Mask_UV.xy + _Time.y * _Twinkle_Mask_UV.zw;
                float phase = SAMPLE_TEXTURE2D(_Twinkle_Mask, sampler_Twinkle_Mask, twinkleUV).r * 50 + _Time.y * _Twinkle_Speed;
                float twinkle = lerp(1, sin(phase) * 0.5 + 0.5, _Twinkle_Amount);
                col += stars * twinkle * smoothstep(_Star_Fade.x, _Star_Fade.y, dir.y) * night;

                // Sunset glow along the horizon, strongest toward the sun
                float band = pow(saturate(1 - abs(dir.y)), _Horizon_Glow_Height);
                float towardSun = pow(saturate(dot(dir, sunDir) * 0.5 + 0.5), _Horizon_Glow_Spread);
                col += band * towardSun * sunset * _Horizon_Glow_Color.rgb * _Horizon_Glow_Intensity;

                // Moon on top (faint in daytime, sinks into the ocean, hidden by clouds)
                float moonAlpha = moonMask * lerp(_Moon_Day_Visibility, 1, night) * moonTex.a * horizon * clearSkyMoon;
                col = lerp(col, moonCol, moonAlpha);

                // Sun on top
                float sunDisc = smoothstep(_SunStep.x, _SunStep.y, sunDist);
                float sunAlpha = lerp(sunDisc * clearSky, sunDisc, _Mask_Alpha) * horizon;
                col = lerp(col, lerp(_SunColor.rgb, _SunsetColor.rgb, sunset), sunAlpha);

                return float4(col, 1);
            }
            ENDHLSL
        }
    }
    Fallback Off
}
