// Point d'exclamation au-dessus du prof : orange, il se remplit de rouge depuis le bas (_Fill de 0 à 1).
// Dessiné entièrement par le shader (pas de texture), compatible VR (single-pass instancing).
Shader "SampleJojo/ProfExclamation"
{
    Properties
    {
        _Fill ("Remplissage", Range(0, 1)) = 0
        _Pulse ("Clignote (plein)", Range(0, 1)) = 0
        _BaseColor ("Couleur vide", Color) = (1, 0.55, 0.05, 1)
        _FullColor ("Couleur pleine", Color) = (0.95, 0.05, 0.03, 1)
        _OutlineColor ("Contour", Color) = (0.12, 0.03, 0.0, 1)
    }
    SubShader
    {
        Tags { "Queue" = "Transparent+50" "RenderType" = "Transparent" "IgnoreProjector" = "True" }
        ZWrite Off
        Cull Off
        Blend SrcAlpha OneMinusSrcAlpha

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #include "UnityCG.cginc"

            float _Fill, _Pulse;
            fixed4 _BaseColor, _FullColor, _OutlineColor;

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            v2f vert (appdata v)
            {
                v2f o;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);
                // Repère : x de -0.25 à 0.25 (le quad est 2x plus haut que large), y de 0 à 1.
                float2 p = float2((i.uv.x - 0.5) * 0.5, i.uv.y);

                // Barre (un peu plus large en haut) + point.
                float w = lerp(0.065, 0.11, saturate((p.y - 0.33) / 0.6));
                float2 q = float2(abs(p.x) - w, max(0.33 - p.y, p.y - 0.95));
                float dBar = length(max(q, 0.0)) + min(max(q.x, q.y), 0.0) - 0.02;
                float dDot = length(p - float2(0.0, 0.13)) - 0.085;
                float d = min(dBar, dDot);

                float aa = fwidth(d) * 1.2;
                float outline = 0.03;
                float alpha = 1.0 - smoothstep(outline - aa, outline + aa, d);
                float inside = 1.0 - smoothstep(-aa, aa, d);

                // Remplissage rouge depuis le bas.
                float level = lerp(0.03, 0.98, saturate(_Fill));
                float red = 1.0 - smoothstep(level - 0.01, level + 0.01, p.y);
                fixed4 col = lerp(_BaseColor, _FullColor, red);
                // Plein : il clignote un peu.
                col.rgb *= 1.0 + _Pulse * 0.35 * (0.5 + 0.5 * sin(_Time.y * 14.0));
                col = lerp(_OutlineColor, col, inside);
                col.a = alpha;
                return col;
            }
            ENDCG
        }
    }
}
