#ifndef CUSTOM_BRDF_INCLUDED
#define CUSTOM_BRDF_INCLUDED

#define MIN_REFLECTIVITY 0.04

//为什么不放在 Surface 结构体中？与物体的BRDF材质参数相关的值
// 金属工作流：metallic smoothness
// 高光工作流：specular roughness
struct BRDF
{
	float3 diffuse;
	float3 specular;
	float roughness;
	float perceptualRoughness;
	float fresnel;
};

float OneMinusReflectivity(float metallic)
{
	float range = 1.0 - MIN_REFLECTIVITY;
	return range - metallic * range;
}

//金属影响镜面反射的颜色 非金属不 
//导体的镜面反射颜色是白色
BRDF GetBRDF (inout Surface surface, bool applyAlphaToDiffuse = false) 
{
	BRDF brdf;
	float oneMinusReflectivity = OneMinusReflectivity(surface.metallic);//金属度越高 漫反射部分越少 纯金属没有漫反射
	brdf.diffuse = surface.color * oneMinusReflectivity;//漫反射部分
	if(applyAlphaToDiffuse)
	{
		brdf.diffuse *= surface.alpha;
	}

	brdf.specular = lerp(MIN_REFLECTIVITY, surface.color, surface.metallic);//镜面反射部分

	brdf.perceptualRoughness = PerceptualSmoothnessToPerceptualRoughness(surface.smoothness);//感知粗糙度用于采样反射探针的mip level
	brdf.roughness = PerceptualRoughnessToRoughness(brdf.perceptualRoughness);
	brdf.fresnel = saturate(surface.smoothness + 1.0 - oneMinusReflectivity);
	
	return brdf;
}

//float SpecularStrength(Surface surface, BRDF brdf, Light light)
//{
//	float3 h = SafeNormalize(light.direction + surface.viewDirection);

//}

//CookTorrance
float SpecularStrength (Surface surface, BRDF brdf, Light light) 
{
	float3 h = SafeNormalize(light.direction + surface.viewDirection);
	float nh2 = Square(saturate(dot(surface.normal, h)));
	float lh2 = Square(saturate(dot(light.direction, h)));
	float r2 = Square(brdf.roughness);
	float d2 = Square(nh2 * (r2 - 1.0) + 1.00001);
	float normalization = brdf.roughness * 4.0 + 2.0;
	return r2 / (d2 * max(0.1, lh2) * normalization);
}

// 直接光漫反射、镜面反射
float3 DirectBRDF (Surface surface, BRDF brdf, Light light) 
{
	return SpecularStrength(surface, brdf, light) * brdf.specular + brdf.diffuse;
}

// 间接光漫反射、镜面反射
float3 IndirectBRDF(Surface surface, BRDF brdf, float3 diffuse, float3 specular)
{
	float fresnelStrength = surface.fresnelStrength * Pow4(1.0 - saturate(dot(surface.normal, surface.viewDirection)));
	float3 reflection  = specular * lerp(brdf.specular, brdf.fresnel, fresnelStrength);
	reflection /= brdf.roughness * brdf.roughness + 1.0; 
	return (diffuse * brdf.diffuse + reflection) * surface.occlusion; 
}

#endif