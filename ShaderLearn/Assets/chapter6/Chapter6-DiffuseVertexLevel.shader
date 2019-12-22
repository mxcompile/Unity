// Upgrade NOTE: replaced 'mul(UNITY_MATRIX_MVP,*)' with 'UnityObjectToClipPos(*)'

// Upgrade NOTE: replaced 'mul(UNITY_MATRIX_MVP,*)' with 'UnityObjectToClipPos(*)'

// Upgrade NOTE: replaced 'mul(UNITY_MATRIX_MVP,*)' with 'UnityObjectToClipPos(*)'

// Upgrade NOTE: replaced '_World2Object' with 'unity_WorldToObject'

// Upgrade NOTE: replaced '_World2Object' with 'unity_WorldToObject'

Shader "Unity Shaders Book/Chapter6/Diffuse Vertex-Level"
{
    Properties
    {
        _Diffuse ("Diffuse", Color) = (1,1,1,1)
    }
    SubShader
    {
        Tags { "LightMode" = "ForwardBase" "RenderType"="Opaque" }
        LOD 100

        Pass
        {
            CGPROGRAM
            #pragma vertex  vert
            #pragma fragment frag

            
            #include "Lighting.cginc"
            
            fixed4 _Diffuse;
            
            struct a2v
            {
                float3 pos :POSITION;
                float3 normal:NORMAL;
            };

            struct v2f
            {
                float4 pos :SV_POSITION;
                fixed4 color:COLOR;
            };

            v2f vert(a2v v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.pos);
                fixed3 ambient = UNITY_LIGHTMODEL_AMBIENT.xyz;
                fixed3 normalworld = normalize(mul(v.normal,(float3x3)unity_WorldToObject)) ;
                fixed3 lightdir = normalize(_WorldSpaceLightPos0.xyz);

                fixed3 diffuse = _LightColor0.rgb*_Diffuse.rgb*saturate(dot(normalworld,lightdir));

                o.color.rgb= ambient + diffuse;
                return o;
            }

            fixed4 frag(v2f f):SV_TARGET
            {
                return fixed4(f.color.rgb,1);
            }

            ENDCG
        }
    }

    Fallback "Diffuse"
}
