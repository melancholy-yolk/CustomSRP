#ifndef CUSTOM_LIGHTING_INCLUDED
#define CUSTOM_LIGHTING_INCLUDED

float3 IncomingLight(Surface surface, Light light)
{
	float NdotL = max(0.0, dot(surface.normal, light.direction));
	return saturate(NdotL * light.attenuation) * light.color;
}

float3 GetSingleLighting(Surface surface, BRDF brdf, Light light)
{
	return IncomingLight(surface, light) * DirectBRDF(surface, brdf, light);
}

float3 GetLighting(Surface surfaceWS, BRDF brdf, GI gi)
{
	// get cascade index and tile index in shadow map by comparing distance between fragment world pos and cascade culling sphere
	// tile index = light index * 4 + cascade index  
	ShadowData shadowData = GetShadowData(surfaceWS);
	shadowData.shadowMask = gi.shadowMask;//烘焙阴影
	
	float3 indirect_diffuse = gi.diffuse;
	float3 indirect_specular = gi.specular;
	float3 color = IndirectBRDF(surfaceWS, brdf, indirect_diffuse, indirect_specular);//间接光

	// final color equals to the sum of all lighting result
	for(int i = 0; i < GetDirectionalLightCount(); i++)
	{
		Light light = GetDirectionalLight(i, surfaceWS, shadowData);
		color += GetSingleLighting(surfaceWS, brdf, light);
	}

	// 对于点光源和聚光灯这种局部光源 unity可以提前计算出每个物体受到哪些光源的影响，这个数据保存在unity_LightIndices中
	#if defined(_LIGHTS_PER_OBJECT)
		for(int j = 0; j < min(unity_LightData.y, 8); j++)
		{
			int lightIndex = unity_LightIndices[(uint)j / 4][(uint)j % 4];
			Light light = GetOtherLight(lightIndex, surfaceWS, shadowData);
			color += GetSingleLighting(surfaceWS, brdf, light);
		}
	#else
		for(int j = 0; j < GetOtherLightCount(); j++)
		{
			Light light = GetOtherLight(j, surfaceWS, shadowData);
			color += GetSingleLighting(surfaceWS, brdf, light);
		}
	#endif
	
	return color;
}

#endif