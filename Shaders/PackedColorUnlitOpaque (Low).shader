//-------------------------------------------------------
// Copyright (c) Leopotam <leopotam@gmail.com>
// Copyright (c) Belfegnar <belfegnarinc@gmail.com>
// License: CC BY-NC-SA 4.0
//-------------------------------------------------------

Shader "LeopotamGroup/PackedColor/UnlitOpaque (Low Quality)" {
    Properties {
        _MainTex ("Color",  2D) = "white" {}
        _GSTex ("Grayscale",  2D) = "white" {}
        _GSMask ("Grayscale Mask",  Vector) = (0, 1, 0, 0)
    }

    SubShader {
        Tags { "RenderType" = "Opaque" "Queue" = "Geometry" "IgnoreProjector" = "True" "ForceNoShadowCasting" = "True" }
        LOD 100

        CGINCLUDE
        #include "UnityCG.cginc"

        sampler2D _MainTex;
        sampler2D _GSTex;
        float4 _MainTex_ST;
        fixed4 _GSMask;

        struct v2f {
            float4 pos : SV_POSITION;
            float2 uv : TEXCOORD0;
        };

        v2f vert (appdata_full v) {
            v2f o;
            o.pos = mul (UNITY_MATRIX_MVP, v.vertex);
            o.uv = TRANSFORM_TEX (v.texcoord, _MainTex);
            return o;
        }

        inline fixed3 YCoCgToRgb (fixed y, fixed3 cocg) {
            cocg -= 0.5;
            cocg.xz -= cocg.yy;
            return cocg + y;
        }

        fixed4 frag (v2f i) : SV_Target {
            fixed4 c = tex2D (_MainTex, i.uv);
            c.rgb = YCoCgToRgb (dot (tex2D (_GSTex, i.uv), _GSMask), c.rgb);
            return c;
        }

        ENDCG

        Pass {
            Tags { "LightMode" = "ForwardBase" }
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma fragmentoption ARB_precision_hint_fastest
            ENDCG
        }
    }
    Fallback Off
}