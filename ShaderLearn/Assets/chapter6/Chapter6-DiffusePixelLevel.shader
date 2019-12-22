Shader "Unity Shaders Book/Chapter6/Diffuse Pixel-Level"
{
    Properties
    {   
        _Diffuse ("Diffuse", Color) = (1,1,1,1)
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" "LightMode" = "ForwardBase"}
        LOD 100

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            
            
            #include "Lighting.cginc"

            fixed4 _Diffuse;

            struct a2f
            {
                float3 pos : POSITION;
                float3 normal : NORMAL;
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
                float3 worldNormal:TEXCOORD0;
            };

            v2f vert (a2f v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.pos);
                o.worldNormal = mul(v.normal,(float3x3)unity_WorldToObject);
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                fixed3 ambient = UNITY_LIGHTMODEL_AMBIENT.xyz;
                fixed3 worldnormal = normalize(i.worldNormal);
                fixed3 lightdir = normalize(_WorldSpaceLightPos0.xyz);
                //phong光照模型
                //fixed3 diffuse = _LightColor0.rgb*_Diffuse.rgb*saturate(dot(worldnormal,lightdir));
                // Blinn
                //fixed3 v= reflect(-1*lightdir,worldnormal);
                //fixed3 h = normalize((v + lightdir)/dot(v + lightdir,v + lightdir)) ;
                //fixed3 diffuse = _LightColor0.rgb*_Diffuse.rgb*saturate(dot(worldnormal,h));
                // half lambert

                fixed3 halflambert = dot(worldnormal,lightdir)*0.5 +0.5;
                fixed3 diffuse = _LightColor0.rgb*_Diffuse.rgb* halflambert;
                fixed3 color = ambient + diffuse;
                return fixed4(color,1);
                //return fixed4(lightdir.xyz,1);
            }
            ENDCG
        }
    }
}
