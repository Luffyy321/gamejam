// Vignettage rose d'adrénaline, dessiné par-dessus tout (mais sous le fondu au noir). URP, compatible VR.
// _Intensity 0 = rien, 1 = bords très roses + traînées de vitesse vers les bords (impression de vision élargie).
Shader "SampleJojo/AdrenalineVignette"
{
    Properties
    {
        _Color ("Couleur", Color) = (1, 0.2, 0.62, 1)
        _Intensity ("Intensité", Range(0, 1)) = 0
        _Pulse ("Battement", Range(0, 1)) = 0
        _Streaks ("Traînées", Range(0, 1)) = 0
    }
    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Overlay+50" "RenderPipeline" = "UniversalPipeline" "IgnoreProjector" = "True" }
        Pass
        {
            Name "Vignette"
            Tags { "LightMode" = "UniversalForward" }
            ZTest Always
            ZWrite Off
            Cull Off
            Blend SrcAlpha OneMinusSrcAlpha

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _Color;
                half _Intensity;
                half _Pulse;
                half _Streaks;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float4 screenPos : TEXCOORD0;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings vert (Attributes input)
            {
                Varyings o;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                o.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                o.screenPos = ComputeScreenPos(o.positionCS);
                return o;
            }

            float Hash(float n) { return frac(sin(n * 127.1) * 43758.5453); }

            half4 frag (Varyings i) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);
                float2 uv = i.screenPos.xy / max(i.screenPos.w, 1e-5);
                float2 p = uv * 2.0 - 1.0;
                p.y *= 1.1;                                   // un peu plus fort en haut et en bas
                float r = length(p);                          // 0 au centre, ~1.45 dans les coins
                float k = saturate(_Intensity);
                if (k < 0.001) return half4(0, 0, 0, 0);

                // Vignettage doux : plus l'adrénaline monte, plus il rentre vers le centre.
                float inner = lerp(0.95, 0.2, k) - _Pulse * 0.06 * k;
                float v = smoothstep(inner, inner + 0.95, r);
                v = v * v * (3.0 - 2.0 * v);                  // dégradé encore plus doux

                // Rose vif vers l'intérieur, rose foncé / magenta tout au bord (comme le noir d'un vignettage).
                half3 deep = _Color.rgb * 0.35;
                half3 col = lerp(_Color.rgb, deep, saturate((v - 0.55) / 0.45));
                float alpha = saturate(v * lerp(0.6, 1.0, k) * (1.0 + _Pulse * 0.2 * k));
                return half4(col, alpha);
            }
            ENDHLSL
        }
    }
}
