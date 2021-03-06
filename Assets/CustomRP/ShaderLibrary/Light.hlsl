#ifndef CUSTOM_LIGHT_INCLUDED
#define CUSTOM_LIGHT_INCLUDED

#define MAX_DIRECTIONAL_LIGHT_COUNT 4
#define MAX_OTHER_LIGHT_COUNT 64

CBUFFER_START(_CustomLight)
	int _DirectionalLightCount;
	float4 _DirectionalLightColors[MAX_DIRECTIONAL_LIGHT_COUNT];
	float4 _DirectionalLightDirections[MAX_DIRECTIONAL_LIGHT_COUNT];
	float4 _DirectionalLightShadowData[MAX_DIRECTIONAL_LIGHT_COUNT];

	int _OtherLightCount;
	float4 _OtherLightColors[MAX_OTHER_LIGHT_COUNT];
	float4 _OtherLightPositions[MAX_OTHER_LIGHT_COUNT];
	float4 _OtherLightDirections[MAX_OTHER_LIGHT_COUNT];
	float4 _OtherLightSpotAngles[MAX_OTHER_LIGHT_COUNT];
	float4 _OtherLightShadowData[MAX_OTHER_LIGHT_COUNT];
CBUFFER_END

struct Light
{
	float3 color;
	float3 direction;
	float attenuation;
};

int GetDirectionalLightCount()
{
	return min(_DirectionalLightCount, MAX_DIRECTIONAL_LIGHT_COUNT);
}

int GetOtherLightCount()
{
	return _OtherLightCount;
}

DirectionalShadowData GetDirectionalShadowData(int lightIndex, ShadowData shadowData)
{
	DirectionalShadowData data;
    data.strength = _DirectionalLightShadowData[lightIndex].x;// * shadowData.strength;
	data.tileIndex = _DirectionalLightShadowData[lightIndex].y + shadowData.cascadeIndex;//ShadowAtlas被分为16个tile
    data.normalBias = _DirectionalLightShadowData[lightIndex].z;
	data.shadowMaskChannel = _DirectionalLightShadowData[lightIndex].w;//平行光shadow mask的通道索引
	return data;
}

OtherShadowData GetOtherShadowData(int lightIndex)
{
	OtherShadowData data;
	data.strength = _OtherLightShadowData[lightIndex].x;//光源阴影强度
	data.tileIndex = _OtherLightShadowData[lightIndex].y;//瓦片在阴影图集中的起始位置索引
	data.shadowMaskChannel = _OtherLightShadowData[lightIndex].w;
	data.isPoint = _OtherLightShadowData[lightIndex].z;
	data.lightPositionWS = 0.0;
	data.lightDirectionWS = 0.0;
	data.spotDirectionWS = 0.0;
	return data;
}

Light GetDirectionalLight(int index, Surface surfaceWS, ShadowData shadowData) 
{
	Light light;
	light.color = _DirectionalLightColors[index].rgb;
	light.direction = _DirectionalLightDirections[index].xyz;

	//DirectionalShadowData shadowData = GetDirectionalShadowData(index);
	//light.attenuation = GetDirectionalShadowAttenuation(shadowData, surfaceWS);

	//平行光阴影数据：阴影强度 阴影贴图瓦片索引
	DirectionalShadowData dirShadowData = GetDirectionalShadowData(index, shadowData);
	light.attenuation = GetDirectionalShadowAttenuation(dirShadowData, shadowData, surfaceWS);//采样ShadowMap
	//light.attenuation = shadowData.cascadeIndex * 0.25;
	return light;
}

Light GetOtherLight(int index, Surface surfaceWS, ShadowData shadowData)
{
	Light light;
	light.color = _OtherLightColors[index].rgb;
	
	float3 position = _OtherLightPositions[index].xyz;
	float3 ray = position - surfaceWS.position;
	light.direction = normalize(ray);
	
	float distanceSqr = max(dot(ray, ray), 0.00001);
	float rangeAttenuation = Square(saturate(1.0 - Square(distanceSqr * _OtherLightPositions[index].w)));
	float4 spotAngles = _OtherLightSpotAngles[index];
	float3 spotDirection = _OtherLightDirections[index].xyz;
	float spotAttenuation = Square(
		saturate(dot(spotDirection, light.direction) *
		spotAngles.x + spotAngles.y)
	);//片元到光源朝向 与 聚光灯朝向（反向）的点乘
	
	OtherShadowData other_shadow_data = GetOtherShadowData(index);
	other_shadow_data.lightPositionWS = position;
	other_shadow_data.lightDirectionWS = light.direction;
	other_shadow_data.spotDirectionWS = spotDirection;
	
	light.attenuation = GetOtherShadowAttenuation(other_shadow_data, shadowData, surfaceWS) * spotAttenuation * rangeAttenuation / distanceSqr;
	
	return light;
}

#endif