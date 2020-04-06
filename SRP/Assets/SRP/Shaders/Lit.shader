Shader "SRP/Lit"
{
    Properties
    {
        _Color ("Color", Color) = (1,1,1,1)
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" }
        LOD 100

        Pass
        {
            HLSLPROGRAM

            #pragma target 3.5

            #pragma multi_compile_instancing
            //在统一缩放下使用，非均匀缩放则剔除该宏
            #pragma instancing_options assumeuniformscaling
            #pragma multi_compile _ _CASCADED_SHADOWS_HARD _CASCADED_SHADOWS_SOFT
            #pragma multi_compile _ _SHADOWS_HARD
            #pragma multi_compile _ _SHADOWS_SOFT
           
            
            #pragma vertex LitPassVertex
            #pragma fragment LitPassFragment

            #include "../ShaderLibrary/Lit.hlsl"

            ENDHLSL
        }


        Pass{
            Tags {
                "LightMode" = "ShadowCaster"
            }

            HLSLPROGRAM

            #pragma target 3.5

            #pragma multi_compile_instancing
            #pragma instancing_options assumeuniformscaling

            #pragma vertex ShadowCasterPassVertex
            #pragma fragment ShadowCasterPassFragment

            #include "../ShaderLibrary/ShadowCaster.hlsl"
            ENDHLSL
            }
    }


}
