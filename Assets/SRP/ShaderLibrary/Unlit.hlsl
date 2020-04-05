#ifndef MYRP_UNLIT_INCLUDED
#define MYRP_UNLIT_INCLUDED


#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Common.hlsl"


CBUFFER_START(UnityPerFrame)
float4x4 unity_MatrixVP;
CBUFFER_END

CBUFFER_START(UnityPerDraw)
float4x4 unity_ObjectToWorld;
CBUFFER_END

//CBUFFER_START(UnityPerMaterial)
//float4 _Color;
//CBUFFER_END


#define UNITY_MATRIX_M unity_ObjectToWorld

#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/UnityInstancing.hlsl"

//自定义常量缓存取颜色数组
UNITY_INSTANCING_BUFFER_START(PerInstance)
UNITY_DEFINE_INSTANCED_PROP(float4, _Color)
UNITY_INSTANCING_BUFFER_END(PerInstance)





struct VertexInput {
	float4 pos : POSITION;
	//给UnlitPassVertex的UNITY_SETUP_INSTANCE_ID使用,提取UNITY_MATRIX_M
	//实例ID也是这个批次中模型index
	UNITY_VERTEX_INPUT_INSTANCE_ID
};

struct VertexOutput {
	float4 clipPos : SV_POSITION;
	//给UnlitPassFragment的UNITY_ACCESS_INSTANCED_PROP使用,提取_Color
	UNITY_VERTEX_INPUT_INSTANCE_ID
};



VertexOutput UnlitPassVertex(VertexInput input) {
	VertexOutput output;
	//重新计算UNITY_MATRIX_M等等矩阵，提取index
	UNITY_SETUP_INSTANCE_ID(input);
	//拷贝UNITY_VERTEX_INPUT_INSTANCE_ID 到  output中
	UNITY_TRANSFER_INSTANCE_ID(input, output);
	float4 worldPos = mul(UNITY_MATRIX_M, float4(input.pos.xyz, 1.0));
	output.clipPos =  mul(unity_MatrixVP, worldPos);
	return output;
}

float4 UnlitPassFragment(VertexOutput input) : SV_TARGET{
	//重新计算UNITY_MATRIX_M等等矩阵提取index
	UNITY_SETUP_INSTANCE_ID(input);
	return  UNITY_ACCESS_INSTANCED_PROP(PerInstance, _Color);
}

#endif
