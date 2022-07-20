#ifndef CUSTOM_UNITY_INPUT_INCLUDED
#define CUSTOM_UNITY_INPUT_INCLUDED

// 每次 Draw Call 提交 我自己的理解是：这是每个GameObject的Renderer组件负责提交的数据
CBUFFER_START(UnityPerDraw)
	float4x4 unity_ObjectToWorld;
	float4x4 unity_WorldToObject;
	float4 unity_LODFade;
	real4 unity_WorldTransformParams;//物体缩放的奇偶性
	float4 _ProjectionParams;//x分量表明纹理坐标的v方向

	// Lights Per Object
	real4 unity_LightData;// Y分量代表灯光数量
	real4 unity_LightIndices[2];//每个分量都是LightIndex 所以每个对象最大8个

	// GI
	float4 unity_ProbesOcclusion;//间接光阴影
	float4 unity_SpecCube0_HDR;//间接光镜面反射
	float4 unity_LightmapST;//间接光漫反射
	float4 unity_DynamicLightmapST;

	// 动态物体间接光漫反射球谐光照
	float4 unity_SHAr;
	float4 unity_SHAg;
	float4 unity_SHAb;
	float4 unity_SHBr;
	float4 unity_SHBg;
	float4 unity_SHBb;
	float4 unity_SHC;

	//体积较大的物体采样光照探针时需要一个体积代理
	float4 unity_ProbeVolumeParams;
	float4x4 unity_ProbeVolumeWorldToObject;
	float4 unity_ProbeVolumeSizeInv;
	float4 unity_ProbeVolumeMin;

	// 烘焙光照贴图时使用的数据
	// meta pass
	bool4 unity_MetaFragmentControl;
	float unity_OneOverOutputBoost;
	float unity_MaxOutputValue;
CBUFFER_END

float4x4 unity_MatrixVP;
float4x4 unity_MatrixV;
float4x4 glstate_matrix_projection;

float3 _WorldSpaceCameraPos;

#endif