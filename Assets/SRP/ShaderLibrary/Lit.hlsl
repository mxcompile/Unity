#ifndef MYRP_LIT_INCLUDED
#define MYRP_LIT_INCLUDED


#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Common.hlsl"
#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Shadow/ShadowSamplingTent.hlsl"


CBUFFER_START(UnityPerFrame)
float4x4 unity_MatrixVP;
CBUFFER_END

CBUFFER_START(UnityPerDraw)
	float4x4 unity_ObjectToWorld;
	//未找到使用方法
	float4 unity_PerObjectLightData;
	float4 unity_PerObjectLightIndices;
CBUFFER_END


#define MAX_VISIBLE_LIGHTS 16

//目前测试 数组方式仅支持vector4型 例如对于float[]型会自动扩展成float4[] size = (((sizeOf(array) - 1))* 4+ 1)*sizeOf(float)
//仅支持4个元素，非常奇怪
CBUFFER_START(_LightBufer)
	float4 _VisibleLightColors[MAX_VISIBLE_LIGHTS];
	float4 _VisibleLightDirectionsOrPositions[MAX_VISIBLE_LIGHTS];
	float4 _VisibleLightAttenuations[MAX_VISIBLE_LIGHTS];
	float4 _VisibleLightSpotDirections[MAX_VISIBLE_LIGHTS];
CBUFFER_END

float3 DiffuseLight (int index, float3 normal , float3 worldPos,float shadowAttenuation)
{
		float3 lightColor = _VisibleLightColors[index].rgb;
		float4 lightPositionOrDirection  = _VisibleLightDirectionsOrPositions[index];

		float3 lightVector = lightPositionOrDirection.xyz - lightPositionOrDirection.w*worldPos;
		float3 lightDirection = normalize(lightVector);
		float diffuse  = saturate(dot(normal,lightDirection)) ;


		float4 lightAttenuation = _VisibleLightAttenuations[index];
		float3 spotDirection = _VisibleLightSpotDirections[index].xyz;


		float rangeFade = dot(lightVector, lightVector) * lightAttenuation.x;
		rangeFade = saturate(1.0 - rangeFade * rangeFade);
		rangeFade *= rangeFade;

		float spotFade = dot(spotDirection, lightDirection);
		spotFade = saturate(spotFade * lightAttenuation.z + lightAttenuation.w);
		spotFade *= spotFade;


		

		float distanceSqr = max(dot(lightVector, lightVector), 0.00001);
		diffuse = diffuse * spotFade * rangeFade / distanceSqr + (1-lightPositionOrDirection.w)* diffuse;

		diffuse *= shadowAttenuation;

		return diffuse*lightColor;
}


//CBUFFER_START(UnityPerMaterial)
//float4 _Color;
//CBUFFER_END


#define UNITY_MATRIX_M unity_ObjectToWorld

#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/UnityInstancing.hlsl"

//自定义INSTANCING常量缓存取颜色数组
UNITY_INSTANCING_BUFFER_START(PerInstance)
	UNITY_DEFINE_INSTANCED_PROP(float4, _Color)
UNITY_INSTANCING_BUFFER_END(PerInstance)

//————————————————————阴影————————————————————————

CBUFFER_START(_ShadowBuffer)
	//float4x4 _WorldToShadowMatrix;
	//float _ShadowStrength;

	float4x4 _WorldToShadowMatrices[MAX_VISIBLE_LIGHTS];
	float4 _ShadowData[MAX_VISIBLE_LIGHTS];
	float4 _ShadowMapSize;

CBUFFER_END

TEXTURE2D_SHADOW(_ShadowMap);
SAMPLER_CMP(sampler_ShadowMap);


float HardShadowAttenuation(float4 shadowPos) {
	return SAMPLE_TEXTURE2D_SHADOW(_ShadowMap, sampler_ShadowMap, shadowPos.xyz);
}


float SoftShadowAttenuation(float4 shadowPos) {
	real tentWeights[9];
	real2 tentUVs[9];
	SampleShadow_ComputeSamples_Tent_5x5(
		_ShadowMapSize, shadowPos.xy, tentWeights, tentUVs
	);
	float attenuation = 0;
	for (int i = 0; i < 9; i++) {
		attenuation += tentWeights[i] * SAMPLE_TEXTURE2D_SHADOW(
			_ShadowMap, sampler_ShadowMap, float3(tentUVs[i].xy, shadowPos.z)
		);
	}
	return attenuation;
}


float ShadowAttenuation(int index ,float3 worldPos) {

	if (_ShadowData[index].x <= 0) {
		return 1.0;
	}

	float attenuation;

	float4 shadowPos = mul(_WorldToShadowMatrices[index], float4(worldPos, 1.0));
	shadowPos.xyz /= shadowPos.w;

#if defined(_SHADOWS_HARD)
	#if defined(_SHADOWS_SOFT)
	if (_ShadowData[index].y == 0) {

		attenuation = HardShadowAttenuation(shadowPos);
	}
	else
	{
		attenuation = SoftShadowAttenuation(shadowPos);
	}
	#else
		attenuation = HardShadowAttenuation(shadowPos);
	#endif
#else
	attenuation = SoftShadowAttenuation(shadowPos);
#endif
	return lerp(1, attenuation, _ShadowData[index].x);

}


//————————————————————阴影————————————————————————

struct VertexInput {
	float4 pos : POSITION;
	//给LitPassVertex的UNITY_SETUP_INSTANCE_ID使用,提取UNITY_MATRIX_M
	//实例ID也是这个批次中模型indexf
	float3 normal:Normal;
	UNITY_VERTEX_INPUT_INSTANCE_ID
};

struct VertexOutput {
	float4 clipPos : SV_POSITION;
	//给LitPassFragment的UNITY_ACCESS_INSTANCED_PROP使用,提取_Color
	float3 normal:TEXCOORD;
	float3 worldPos : TEXCOORD1;
	//float3 vertexLighting : TEXCOORD2;
	UNITY_VERTEX_INPUT_INSTANCE_ID
};



VertexOutput LitPassVertex(VertexInput input) {
	VertexOutput output;
	//重新计算UNITY_MATRIX_M等等矩阵，提取index
	UNITY_SETUP_INSTANCE_ID(input);
	//拷贝UNITY_VERTEX_INPUT_INSTANCE_ID 到  output中
	UNITY_TRANSFER_INSTANCE_ID(input, output);
	float4 worldPos = mul(UNITY_MATRIX_M, float4(input.pos.xyz, 1.0));
	output.clipPos =  mul(unity_MatrixVP, worldPos);
	output.worldPos = worldPos.xyz;
	output.normal = mul((float3x3)UNITY_MATRIX_M,input.normal);

	return output;
}

float4 LitPassFragment(VertexOutput input) : SV_TARGET{
	//重新计算UNITY_MATRIX_M等等矩阵提取index
	UNITY_SETUP_INSTANCE_ID(input);
	input.normal = normalize(input.normal);

	float3 albedo = UNITY_ACCESS_INSTANCED_PROP(PerInstance, _Color).rgb;
	
	//float3 deffuseLight = saturate(dot(input.normal ,float3(0, 1, 0)) );
	//float3 diffuseLight = input.vertexLighting;
	float3 diffuseLight = 0;
	for(int i=0;i<4;i++)
	{
		float shadowAttenuation = ShadowAttenuation(i,input.worldPos);
		diffuseLight += DiffuseLight(i,input.normal,input.worldPos, shadowAttenuation);
	}
	

	float3 color = diffuseLight * albedo;
	return float4(color,1);
}

#endif
