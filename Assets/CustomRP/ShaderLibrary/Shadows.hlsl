#ifndef CUSTOM_SHADOWS_INCLUDED
#define CUSTOM_SHADOWS_INCLUDED

#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Shadow/ShadowSamplingTent.hlsl"

#if defined(_DIRECTIONAL_PCF3)
	#define DIRECTIONAL_FILTER_SAMPLES 4
	#define DIRECTIONAL_FILTER_SETUP SampleShadow_ComputeSamples_Tent_3x3
#elif defined(_DIRECTIONAL_PCF5)
	#define DIRECTIONAL_FILTER_SAMPLES 9
	#define DIRECTIONAL_FILTER_SETUP SampleShadow_ComputeSamples_Tent_5x5
#elif defined(_DIRECTIONAL_PCF7)
	#define DIRECTIONAL_FILTER_SAMPLES 16
	#define DIRECTIONAL_FILTER_SETUP SampleShadow_ComputeSamples_Tent_7x7
#endif

#if defined(_OTHER_PCF3)
    #define OTHER_FILTER_SAMPLES 4
    #define OTHER_FILTER_SETUP SampleShadow_ComputeSamples_Tent_3x3
#elif defined(_OTHER_PCF5)
    #define OTHER_FILTER_SAMPLES 9
    #define OTHER_FILTER_SETUP SampleShadow_ComputeSamples_Tent_5x5
#elif defined(_OTHER_PCF7)
    #define OTHER_FILTER_SAMPLES 16
    #define OTHER_FILTER_SETUP SampleShadow_ComputeSamples_Tent_7x7
#endif

#define MAX_SHADOWED_DIRECTIONAL_LIGHT_COUNT 4
#define MAX_SHADOWED_OTHER_LIGHT_COUNT 16
#define MAX_CASCADE_COUNT 4

TEXTURE2D_SHADOW(_DirectionalShadowAtlas);
TEXTURE2D_SHADOW(_OtherShadowAtlas);
#define SHADOW_SAMPLER sampler_linear_clamp_compare
SAMPLER_CMP(SHADOW_SAMPLER);

CBUFFER_START(_CustomShadows)
	int _CascadeCount;
	float4 _CascadeCullingSpheres[MAX_CASCADE_COUNT];
	float4 _CascadeData[MAX_CASCADE_COUNT];
	float4x4 _DirectionalShadowMatrices[MAX_SHADOWED_DIRECTIONAL_LIGHT_COUNT * MAX_CASCADE_COUNT];
	float4x4 _OtherShadowMatrices[MAX_SHADOWED_OTHER_LIGHT_COUNT];
	float4 _OtherShadowTiles[MAX_SHADOWED_OTHER_LIGHT_COUNT];
	float4 _ShadowAtlasSize;
	float4 _ShadowDistanceFade;
CBUFFER_END

// 平行光阴影数据
struct DirectionalShadowData
{
	float strength;//directional light shadow strength
	int tileIndex;//阴影图集瓦片起始索引
    float normalBias;
	int shadowMaskChannel;//shadowmask的RGBA最大支持四个平行光的阴影
};

// 点光源/聚光灯阴影数据
struct OtherShadowData
{
	float strength;
	int tileIndex;
	int shadowMaskChannel;
	float3 lightPositionWS;
	float3 spotDirectionWS;
};

struct ShadowMask
{
	bool always;
	bool distance;
	float4 shadows;
};

// 片元阴影数据
struct ShadowData
{
    int cascadeIndex;
	float cascadeBlend;
    float strength;//if fragment in the last cascade level, fade out strength
	ShadowMask shadowMask;
};

float SampleDirectionalShadowAtlas(float3 positionSTS) 
{
	return SAMPLE_TEXTURE2D_SHADOW(_DirectionalShadowAtlas, SHADOW_SAMPLER, positionSTS);
}

float SampleOtherShadowAtlas(float3 positionSTS, float3 bounds) 
{
	//positionSTS.xy = clamp(positionSTS.xy, bounds.xy, bounds.xy + bounds.z);//将采样阴影贴图瓦片的uv限制在合法范围内（正确的tile内）
	return SAMPLE_TEXTURE2D_SHADOW(_OtherShadowAtlas, SHADOW_SAMPLER, positionSTS);
}

float FilterDirectionalShadow(float3 positionSTS)
{
	#if defined(DIRECTIONAL_FILTER_SETUP)
		float weights[DIRECTIONAL_FILTER_SAMPLES];
		float2 positions[DIRECTIONAL_FILTER_SAMPLES];
		float4 size = _ShadowAtlasSize.yyxx;
		DIRECTIONAL_FILTER_SETUP(size, positionSTS.xy, weights, positions);
		float shadow = 0;
		for(int i = 0; i < DIRECTIONAL_FILTER_SAMPLES; i++)
		{
			shadow += weights[i] * SampleDirectionalShadowAtlas(float3(positions[i].xy, positionSTS.z));//bilinear filter
		}
		return shadow;
	#else
		return SampleDirectionalShadowAtlas(positionSTS);
	#endif
}

float FilterOtherShadow(float3 positionSTS, float3 bounds)
{
	#if defined(OTHER_FILTER_SETUP)
		real weights[OTHER_FILTER_SAMPLES];
		real2 positions[OTHER_FILTER_SAMPLES];
		float4 size = _ShadowAtlasSize.wwzz;
		OTHER_FILTER_SETUP(size, positionSTS.xy, weights, positions);
		float shadow = 0;
		for(int i = 0; i < OTHER_FILTER_SAMPLES; i++)
		{
			shadow += weights[i] * SampleOtherShadowAtlas(float3(positions[i].xy, positionSTS.z), bounds);//bilinear filter
		}
		return shadow;
	#else
		return SampleOtherShadowAtlas(positionSTS, bounds);
	#endif
}

float GetCascadedShadow(DirectionalShadowData directional, ShadowData global, Surface surfaceWS)
{
	// 为了避免阴影瑕疵 shadow acne 彼得潘宁
	//when sample shadow map per fragment, inflate vertex pos along normal direction 
	float3 normalBias = surfaceWS.interpolatedNormal * (directional.normalBias * _CascadeData[global.cascadeIndex].y);
	//WorldSpace ---> ShadowTextureSpace
	float3 positionSTS = mul(_DirectionalShadowMatrices[directional.tileIndex], float4(surfaceWS.position + normalBias, 1.0)).xyz;
	float shadow = FilterDirectionalShadow(positionSTS);

	//avoid to sudden transit between cascades, blend shadow value 
	if(global.cascadeBlend < 1.0)
	{
		//采样下一级级联阴影值
		normalBias = surfaceWS.interpolatedNormal * (directional.normalBias * _CascadeData[global.cascadeIndex + 1].y);
		positionSTS = mul(_DirectionalShadowMatrices[directional.tileIndex + 1], float4(surfaceWS.position + normalBias, 1.0)).xyz;
		shadow = lerp(FilterDirectionalShadow(positionSTS), shadow, global.cascadeBlend);
	}
	return shadow;
}

float GetBakedShadow(ShadowMask mask, int channel)
{
	float shadow = 1.0;
	if (mask.always || mask.distance)
	{
		if (channel >= 0)
		{
			shadow = mask.shadows[channel];
		}
	}
	return shadow;
}

float GetBakedShadow(ShadowMask mask, int channel, float strength)
{
	if (mask.always || mask.distance)
	{
		return lerp(1.0, GetBakedShadow(mask, channel), strength);
	}
	return 1.0;
}

// float MixBakedAndRealtimeShadows(ShadowData global, float realShadow, float strength)
// {
// 	float finalShadow;
// 	float bakedShadow = GetBakedShadow(global.shadowMask);
// 	if (global.shadowMask.distance)
// 	{
// 		finalShadow = lerp(bakedShadow, realShadow, global.strength);
// 	}
// 	else
// 	{
// 		finalShadow = realShadow;
// 	}
// 	finalShadow = lerp(1.0, finalShadow, strength);
// 	return finalShadow;
// }

// 基于片段的深度 进行实时级联阴影和烘焙阴影的混合
// global：片元阴影参数
// shadow：片元级联阴影
// strength：平行光阴影强度
// shadowMaskChannel：shadow mask通道索引
float MixBakedAndRealtimeShadows(ShadowData global, float shadow, int shadowMaskChannel, float strength)
{
	// 首先，为了基于深度渐暗，实时阴影必须被片元阴影强度调整
	// 然后，烘焙和实时阴影被合并，通过采用他们的最小值，
	// 再然后，光源的阴影强度被应用到合并后的阴影
	float baked = GetBakedShadow(global.shadowMask, shadowMaskChannel);
	if (global.shadowMask.always)
	{
		shadow = lerp(1.0, shadow, global.strength);
		shadow = min(baked, shadow);
		return lerp(1.0, shadow, strength);
	}
	if (global.shadowMask.distance)
	{
		shadow = lerp(baked, shadow, global.strength);
		return lerp(1.0, shadow, strength);
	}
	return lerp(1.0, shadow, strength * global.strength);
}

// 得到平行光阴影衰减
// 平行光阴影参数
// 片元阴影参数
// 片元数据
float GetDirectionalShadowAttenuation(DirectionalShadowData directional, ShadowData global, Surface surfaceWS) 
{
	#if !defined(_RECEIVE_SHADOWS)
		return 1.0;
	#endif

	float shadow;
    if (directional.strength * global.strength <= 0.0) //无实时阴影仅烘焙阴影时
	{
		shadow = GetBakedShadow(global.shadowMask, directional.shadowMaskChannel, abs(directional.strength));
	}
    else
    {
    	shadow = GetCascadedShadow(directional, global, surfaceWS);
    	shadow = MixBakedAndRealtimeShadows(global, shadow, directional.shadowMaskChannel, directional.strength);
    }

    return shadow;
}

float GetOtherShadow(OtherShadowData other, ShadowData global, Surface surfaceWS)
{
	float4 tileData = _OtherShadowTiles[other.tileIndex];

	//将片段到光源向量与聚光灯朝向进行点乘 得到片段到光源平面的距离
	float3 surfaceToLight = other.lightPositionWS - surfaceWS.position;
	float3 distanceToLightPlane = dot(surfaceToLight, other.spotDirectionWS);
	
	float3 normalBias = surfaceWS.interpolatedNormal * (distanceToLightPlane * tileData.w);
	float4 positionSTS = mul(_OtherShadowMatrices[other.tileIndex], float4(surfaceWS.position + normalBias, 1.0));
	return FilterOtherShadow(positionSTS.xyz / positionSTS.w, tileData.xyz);
}

float GetOtherShadowAttenuation(OtherShadowData other_shadow_data, ShadowData global, Surface surfaceWS)
{
	#if !defined(_RECEIVE_SHADOWS)
		return 1.0;
	#endif

	float shadow;
	// 片段阴影强度<=0 超过了最大阴影距离
	//if (other_shadow_data.strength > 0.0)// other_shadow_data.strength * global.strength <= 0
	if (other_shadow_data.strength * global.strength <= 0)
	{
		shadow = GetBakedShadow(global.shadowMask, other_shadow_data.shadowMaskChannel, other_shadow_data.strength);
	}
	else
	{
		shadow = GetOtherShadow(other_shadow_data, global, surfaceWS);
		shadow = MixBakedAndRealtimeShadows(global, shadow, other_shadow_data.shadowMaskChannel, other_shadow_data.strength);
	}
	return shadow;
}

float FadedShadowStrength(float distance, float scale, float fade)
{
    return saturate((1.0 - distance * scale) * fade);
}

//calculate cascade level of a fragment
//计算片元阴影衰减：每个片段的阴影衰减值有两种衰减因子
//一种是最大阴影距离衰减（避免阴影在最大阴影距离出突然截断） ShadowDistanceFade
//另一种是不同级联等级之间的渐变衰减（避免阴影在不同级联等级间突变） CascadeFade
ShadowData GetShadowData(Surface surfaceWS)
{
	ShadowData data;
	data.shadowMask.always = false;//总是使用shadow mask
	data.shadowMask.distance = false;//超过最大阴影距离时使用shadow mask
	data.shadowMask.shadows = 1.0;//该片段处采样shadow mask的值
	data.cascadeBlend = 1.0;//片段位于两个级联相邻区域时 混合插值

	// 最大阴影距离衰减
    data.strength = FadedShadowStrength(surfaceWS.depth, _ShadowDistanceFade.x, _ShadowDistanceFade.y);//最大一级级联阴影逐渐消失

	// 级联阴影衰减
	int i;
	for(i = 0; i < _CascadeCount; i++)
	{
		float4 sphere = _CascadeCullingSpheres[i];
		float distanceSqr = DistanceSquared(surfaceWS.position, sphere.xyz);
		if(distanceSqr < sphere.w)
		{
			float fade = FadedShadowStrength(distanceSqr, _CascadeData[i].x, _ShadowDistanceFade.z);
			if(i == _CascadeCount - 1)
            {
                data.strength *= fade;
            }
			else
			{
				data.cascadeBlend = fade;
			}
			break;
		}
	}

	if(i == _CascadeCount && _CascadeCount > 0)
	{
		data.strength = 0.0;
	}
#if defined(_CASCADE_BLEND_DITHER)
	else if(data.cascadeBlend < surfaceWS.dither)
	{
		i += 1;
	}
#endif
	
#if !defined(_CASCADE_BLEND_SOFT)
	data.cascadeBlend = 1.0;
#endif
	
	data.cascadeIndex = i;
	return data;
}



#endif