using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

public class Shadows 
{
    const string bufferName = "Shadows";
    CommandBuffer buffer = new CommandBuffer { name = bufferName };
    ScriptableRenderContext context;
    CullingResults cullingResults;
    ShadowSettings shadowSettings;

    const int 
        maxShadowedDirectionalLightCount = 4, 
        maxShadowedOtherLightCount = 16,
        maxCascades = 4;

    
    
    struct ShadowedDirectionalLight 
    {
        public int visibleLightIndex;
        public float slopeScaleBias;
        public float nearPlaneOffset;
    }

    struct ShadowedOtherLight
    {
        public int visibleLightIndex;
        public float slopeScaleBias;
        public float normalBias;
    }
    
    ShadowedDirectionalLight[] ShadowedDirectionalLights = new ShadowedDirectionalLight[maxShadowedDirectionalLightCount];
    ShadowedOtherLight[] shadowedOtherLights = new ShadowedOtherLight[maxShadowedOtherLightCount];

    int ShadowedDirectionalLightCount,//产生阴影的方向光数量
        shadowedOtherLightCount;//产生阴影的点/聚光的数量

    static int
        dirShadowAtlasId = Shader.PropertyToID("_DirectionalShadowAtlas"),
        dirShadowMatricesId = Shader.PropertyToID("_DirectionalShadowMatrices"),
        otherShadowAtlasId = Shader.PropertyToID("_OtherShadowAtlas"),
        otherShadowMatricesId = Shader.PropertyToID("_OtherShadowMatrices"),
        cascadeCountId = Shader.PropertyToID("_CascadeCount"),
        cascadeCullingSpheresId = Shader.PropertyToID("_CascadeCullingSpheres"),
        cascadeDataId = Shader.PropertyToID("_CascadeData"),
        shadowAtlasSizeId = Shader.PropertyToID("_ShadowAtlasSize"),
        shadowDistanceFadeId = Shader.PropertyToID("_ShadowDistanceFade"),
        shadowPancakingId = Shader.PropertyToID("_ShadowPancaking");

    static Matrix4x4[]
        dirShadowMatrices = new Matrix4x4[maxShadowedDirectionalLightCount * maxCascades],
        otherShadowMatrices = new Matrix4x4[maxShadowedOtherLightCount];

    static Vector4[]
        cascadeCullingSpheres = new Vector4[maxCascades],
        cascadeData = new Vector4[maxCascades];

    static string[] directionalFilterKeywords = {
        "_DIRECTIONAL_PCF3",
        "_DIRECTIONAL_PCF5",
        "_DIRECTIONAL_PCF7"
    };

    private static string[] otherFilterKeywords =
    {
        "_OTHER_PCF3",
        "_OTHER_PCF5",
        "_OTHER_PCF7",
    };
    
    static string[] cascadeBlendKeywords = {
        "_CASCADE_BLEND_SOFT",
		"_CASCADE_BLEND_DITHER"
    };

    private static string[] shadowMaskKeywords =
    {
        "_SHADOW_MASK_ALWAYS",
        "_SHADOW_MASK_DISTANCE"
    };

    private bool useShadowMask;
    private Vector4 atlasSizes;
    
    /// <summary>
    /// 初始化设置阴影
    /// </summary>
    /// <param name="context"></param>
    /// <param name="cullingResults"></param>
    /// <param name="shadowSettings"></param>
    public void Setup(ScriptableRenderContext context, CullingResults cullingResults, ShadowSettings shadowSettings)
    {
        this.context = context;
        this.cullingResults = cullingResults;
        this.shadowSettings = shadowSettings;

        ShadowedDirectionalLightCount = shadowedOtherLightCount = 0;//每次渲染阴影前清零
        useShadowMask = false;
    }

    void ExecuteBuffer()
    {
        context.ExecuteCommandBuffer(buffer);
        buffer.Clear();
    }

    //返回结果用来填充GPU上DirectionalLightShadowData结构体
    // x：光源的阴影强度
    // y：光源在阴影图集中瓦片的开始索引
    // z：光源的法线偏移
    // w：the light's shadow mask channel index 光源的shadow mask的通道索引
    public Vector4 ReserveDirectionalShadows(Light light, int visibleLightIndex)
    {
        //首先决定是否光源使用shadow mask
        if (ShadowedDirectionalLightCount < maxShadowedDirectionalLightCount 
            && light.shadows != LightShadows.None 
            && light.shadowStrength > 0f 
            //&& cullingResults.GetShadowCasterBounds(visibleLightIndex, out Bounds b)
            )
        {
            float maskChannel = -1;//光源的shadow mask通道索引
            LightBakingOutput lightBaking = light.bakingOutput;
            if (lightBaking.lightmapBakeType == LightmapBakeType.Mixed && lightBaking.mixedLightingMode == MixedLightingMode.Shadowmask)
            {
                useShadowMask = true;
                maskChannel = lightBaking.occlusionMaskChannel;
            }

            // 如果这个光源没有实际照射到物体，那么这个光源不会有实时阴影shadow map，仅有shadow mask
            if (!cullingResults.GetShadowCasterBounds(visibleLightIndex, out Bounds b))
            {
                return new Vector4(-light.shadowStrength, 0f, 0f, maskChannel);
            }
            
            ShadowedDirectionalLights[ShadowedDirectionalLightCount] = 
                new ShadowedDirectionalLight { 
                visibleLightIndex = visibleLightIndex,
                slopeScaleBias = light.shadowBias,
                nearPlaneOffset = light.shadowNearPlane
            };
            return new Vector4(
                light.shadowStrength, 
                shadowSettings.directional.cascadeCount * ShadowedDirectionalLightCount++,//这个灯光在阴影图集中瓦片的开始索引
                light.shadowNormalBias,
                maskChannel
            );
        }
        return new Vector4(0f, 0f, 0f, -1f);
    }

    //填充OtherLightShadowData结构体
    public Vector4 ReserveOtherShadows(Light light, int visibleLightIndex)
    {
        if (light.shadows == LightShadows.None || light.shadowStrength <= 0f)
        {
            return new Vector4(0f, 0f, 0f, -1f);
        }

        float maskChannel = -1;// shadow mask channel
        LightBakingOutput lightBaking = light.bakingOutput;
        if (lightBaking.lightmapBakeType == LightmapBakeType.Mixed && lightBaking.mixedLightingMode == MixedLightingMode.Shadowmask)
        {
            useShadowMask = true;
            maskChannel = lightBaking.occlusionMaskChannel;
        }

        if (shadowedOtherLightCount >= maxShadowedOtherLightCount || !cullingResults.GetShadowCasterBounds(visibleLightIndex, out Bounds b))
        {
            return new Vector4(-light.shadowStrength, 0f, 0f, maskChannel);
        }

        shadowedOtherLights[shadowedOtherLightCount] = new ShadowedOtherLight
        {
            visibleLightIndex = visibleLightIndex,
            slopeScaleBias = light.shadowBias,
            normalBias = light.shadowNormalBias
        };
        
        return new Vector4(light.shadowStrength, shadowedOtherLightCount++, 0f, maskChannel);
    }
    
    public void Render()
    {
        if (ShadowedDirectionalLightCount > 0)
        {
            RenderDirectionalShadows();
        }
        else
        {
            //没有平行光阴影要渲染时 声明1x1傀儡纹理占位
            buffer.GetTemporaryRT(dirShadowAtlasId, 1, 1, 32, FilterMode.Bilinear, RenderTextureFormat.Shadowmap);
        }

        if (shadowedOtherLightCount > 0)
        {
            RenderOtherShadows();
        }
        else
        {
            buffer.SetGlobalTexture(otherShadowAtlasId, dirShadowAtlasId);
        }
        
        buffer.BeginSample(bufferName);
        SetKeywords(shadowMaskKeywords, useShadowMask ? (QualitySettings.shadowmaskMode == ShadowmaskMode.Shadowmask ? 0 : 1) : -1);
        
        buffer.SetGlobalInt(cascadeCountId, ShadowedDirectionalLightCount > 0 ? shadowSettings.directional.cascadeCount : 0);//没有投射阴影的平行光时 级联数量为0
        float f = 1f - shadowSettings.directional.cascadeFade;
        buffer.SetGlobalVector(shadowDistanceFadeId, new Vector4(
            1f / shadowSettings.maxDistance, 
            1f / shadowSettings.distanceFade, 
            1f / (1f - f * f)
            ));
        
        buffer.SetGlobalVector(shadowAtlasSizeId, atlasSizes);
        
        buffer.EndSample(bufferName);
        ExecuteBuffer();
    }

    /// <summary>
    /// 渲染全部产生阴影的平行光shadowmap
    /// </summary>
    void RenderDirectionalShadows()
    {
        //在GPU上申请一张RT来存放Depth Map
        int atlasSize = (int)shadowSettings.directional.atlasSize;
        atlasSizes.x = atlasSize;
        atlasSizes.y = 1f / atlasSize;
        
        buffer.GetTemporaryRT(dirShadowAtlasId, atlasSize, atlasSize, 32, FilterMode.Bilinear, RenderTextureFormat.Shadowmap);
        buffer.SetRenderTarget(dirShadowAtlasId, RenderBufferLoadAction.DontCare, RenderBufferStoreAction.Store);
        buffer.ClearRenderTarget(true, false, Color.clear);
        buffer.SetGlobalFloat(shadowPancakingId, 1f);
        buffer.BeginSample(bufferName);
        ExecuteBuffer();

        // 瓦片个数 = 产生阴影方向光数量 x 级联数量
        int tiles = ShadowedDirectionalLightCount * shadowSettings.directional.cascadeCount;
        //int split = ShadowedDirectionalLightCount <= 1 ? 1 : 2;
        int split = tiles <= 1 ? 1 : (tiles <= 4 ? 2 : 4);
        int tileSize = atlasSize / split;

        for (int i = 0; i < ShadowedDirectionalLightCount; i++)
        {
            RenderSingleDirectionalShadow(i, split, tileSize);//渲染单个平行光阴影
        }

        //buffer.SetGlobalInt(cascadeCountId, shadowSettings.directional.cascadeCount);
        buffer.SetGlobalVectorArray(cascadeCullingSpheresId, cascadeCullingSpheres);
        buffer.SetGlobalVectorArray(cascadeDataId, cascadeData);
        buffer.SetGlobalMatrixArray(dirShadowMatricesId, dirShadowMatrices);//发送阴影矩阵数组到GPU
        //float f = 1f - shadowSettings.directional.cascadeFade;
        //buffer.SetGlobalVector(shadowDistanceFadeId, new Vector4(1f / shadowSettings.maxDistance, 1f / shadowSettings.distanceFade, 1f / (1f - f * f)));

        SetKeywords(directionalFilterKeywords, (int)shadowSettings.directional.filter - 1);
        SetKeywords(cascadeBlendKeywords, (int)shadowSettings.directional.cascadeBlend - 1);
        //buffer.SetGlobalVector(shadowAtlasSizeId, new Vector4(atlasSize, 1f / atlasSize));
        buffer.EndSample(bufferName);
        ExecuteBuffer();
    }

    /// <summary>
    /// 渲染单个方向光产生的阴影
    /// </summary>
    /// <param name="index">光源索引</param>
    /// <param name="split">图集拆分数</param>
    /// <param name="tileSize">瓦片尺寸</param>
    void RenderSingleDirectionalShadow(int lightIndex, int split, int tileSize)
    {
        ShadowedDirectionalLight light = ShadowedDirectionalLights[lightIndex];
        var shadowDrawSettings = new ShadowDrawingSettings(cullingResults, light.visibleLightIndex);

        int cascadeCount = shadowSettings.directional.cascadeCount;
        int tileOffset = lightIndex * cascadeCount;
        Vector3 ratios = shadowSettings.directional.CascadeRatios;

        float cullingFactor = Mathf.Max(0f, 0.8f - shadowSettings.directional.cascadeFade);
        for (int i = 0; i < cascadeCount; i++)
        {
            //在每个光源位置计算VP矩阵
            cullingResults.ComputeDirectionalShadowMatricesAndCullingPrimitives(
                light.visibleLightIndex, 
                i, cascadeCount, ratios, 
                tileSize, light.nearPlaneOffset,
                out Matrix4x4 viewMatrix, out Matrix4x4 projectionMatrix, out ShadowSplitData splitData);

            splitData.shadowCascadeBlendCullingFactor = cullingFactor;
            shadowDrawSettings.splitData = splitData;//投射阴影的物体怎么被剔除

            if (lightIndex == 0)
            {
                SetCascadeData(i, splitData.cullingSphere, tileSize);
            }
            //每个光源的每个级联等级都有一个tile
            int tileIndex = tileOffset + i;
            dirShadowMatrices[tileIndex] = ConvertToAtlasMatrix(projectionMatrix * viewMatrix, SetTileViewport(tileIndex, split, tileSize), split);
            buffer.SetViewProjectionMatrices(viewMatrix, projectionMatrix);
            buffer.SetGlobalDepthBias(0f, light.slopeScaleBias);
            ExecuteBuffer();
            context.DrawShadows(ref shadowDrawSettings);
            buffer.SetGlobalDepthBias(0f, 0f);
        }

    }

    private void SetCascadeData(int cascadeIndex, Vector4 cullingSphere, float tileSize)
    {
        //world-space texel size
        float texelSize = 2f * cullingSphere.w / tileSize;//级联剔除球的直径 / 瓦片边长
        float filterSize = texelSize * ((float)shadowSettings.directional.filter + 1f);//when increasing filter size, increase normal bias to avoid shadow acne
        cullingSphere.w -= filterSize;//to avoid sample outside of the culling sphere, reduce cascade culliung sphere's radius
        cullingSphere.w *= cullingSphere.w;//级联剔除球半径的平方
        cascadeCullingSpheres[cascadeIndex] = cullingSphere;
        cascadeData[cascadeIndex] = new Vector4(1f / cullingSphere.w, filterSize * 1.4142136f);//x:剔除球半径的倒数 y:normal bias
    }

    public void Cleanup()
    {
        buffer.ReleaseTemporaryRT(dirShadowAtlasId);
        if (shadowedOtherLightCount > 0)
        {
            buffer.ReleaseTemporaryRT(otherShadowAtlasId);
        }
        ExecuteBuffer();
    }

    //指定在当前绑定的FrameBuffer上的某个矩形区域内渲染
    Vector2 SetTileViewport(int tileIndex, int split, float tileSize)
    {
        Vector2 offset = new Vector2(tileIndex % split, tileIndex / split);
        buffer.SetViewport(new Rect(offset.x * tileSize, offset.y * tileSize, tileSize, tileSize));
        return offset;
    }

    /// <summary>
    /// WorldSpace->LightSpace
    /// </summary>
    /// <param name="m"></param>
    /// <param name="offset"></param>
    /// <param name="split"></param>
    /// <returns></returns>
    Matrix4x4 ConvertToAtlasMatrix(Matrix4x4 m, Vector2 offset, int split)
    {
        if (SystemInfo.usesReversedZBuffer)
        {
            m.m20 = -m.m20;
            m.m21 = -m.m21;
            m.m22 = -m.m22;
            m.m23 = -m.m23;
        }

        //m.m00 = 0.5f * (m.m00 + m.m30);
        //m.m01 = 0.5f * (m.m01 + m.m31);
        //m.m02 = 0.5f * (m.m02 + m.m32);
        //m.m03 = 0.5f * (m.m03 + m.m33);
        //m.m10 = 0.5f * (m.m10 + m.m30);
        //m.m11 = 0.5f * (m.m11 + m.m31);
        //m.m12 = 0.5f * (m.m12 + m.m32);
        //m.m13 = 0.5f * (m.m13 + m.m33);

        float scale = 1f / split;
        m.m00 = (0.5f * (m.m00 + m.m30) + offset.x * m.m30) * scale;
        m.m01 = (0.5f * (m.m01 + m.m31) + offset.x * m.m31) * scale;
        m.m02 = (0.5f * (m.m02 + m.m32) + offset.x * m.m32) * scale;
        m.m03 = (0.5f * (m.m03 + m.m33) + offset.x * m.m33) * scale;
        m.m10 = (0.5f * (m.m10 + m.m30) + offset.y * m.m30) * scale;
        m.m11 = (0.5f * (m.m11 + m.m31) + offset.y * m.m31) * scale;
        m.m12 = (0.5f * (m.m12 + m.m32) + offset.y * m.m32) * scale;
        m.m13 = (0.5f * (m.m13 + m.m33) + offset.y * m.m33) * scale;

        m.m20 = 0.5f * (m.m20 + m.m30);
        m.m21 = 0.5f * (m.m21 + m.m31);
        m.m22 = 0.5f * (m.m22 + m.m32);
        m.m23 = 0.5f * (m.m23 + m.m33);

        return m;
    }

    void SetKeywords(string[] keywords, int enabledIndex)
    {
        for (int i = 0; i < keywords.Length; i++)
        {
            if (i == enabledIndex)
            {
                buffer.EnableShaderKeyword(keywords[i]);
            }
            else
            {
                buffer.DisableShaderKeyword(keywords[i]);
            }
        }
    }

    //渲染其他光源的阴影贴图
    void RenderOtherShadows()
    {
        int atlasSize = (int)shadowSettings.other.atlasSize;
        atlasSizes.z = atlasSize;
        atlasSizes.w = 1f / atlasSize;
        
        buffer.GetTemporaryRT(otherShadowAtlasId, atlasSize, atlasSize, 32, FilterMode.Bilinear, RenderTextureFormat.Shadowmap);
        buffer.SetRenderTarget(otherShadowAtlasId, RenderBufferLoadAction.DontCare, RenderBufferStoreAction.Store);
        buffer.ClearRenderTarget(true, false, Color.clear);
        buffer.SetGlobalFloat(shadowPancakingId, 0f);
        buffer.BeginSample(bufferName);
        ExecuteBuffer();

        int tiles = shadowedOtherLightCount;
        int split = tiles <= 1 ? 1 : (tiles <= 4 ? 2 : 4);
        int tileSize = atlasSize / split;

        for (int i = 0; i < shadowedOtherLightCount; i++)
        {
            RenderSpotShadows(i, split, tileSize);
        }
        
        buffer.SetGlobalMatrixArray(otherShadowMatricesId, otherShadowMatrices);
        SetKeywords(otherFilterKeywords, (int)shadowSettings.other.filter - 1);//采样shadowmap时进行PCF过滤
        
        buffer.EndSample(bufferName);
        ExecuteBuffer();
    }

    void RenderSpotShadows(int index, int split, int tileSize)
    {
        ShadowedOtherLight light = shadowedOtherLights[index];
        var shadow_drawing_settings = new ShadowDrawingSettings(cullingResults, light.visibleLightIndex);
        cullingResults.ComputeSpotShadowMatricesAndCullingPrimitives(light.visibleLightIndex, out Matrix4x4 viewMatrix, out Matrix4x4 projMatrix, out ShadowSplitData splitData);
        shadow_drawing_settings.splitData = splitData;
        otherShadowMatrices[index] = ConvertToAtlasMatrix(projMatrix * viewMatrix, SetTileViewport(index, split, tileSize), split);
        buffer.SetViewProjectionMatrices(viewMatrix, projMatrix);
        buffer.SetGlobalDepthBias(0f, light.slopeScaleBias);
        ExecuteBuffer();
        context.DrawShadows(ref shadow_drawing_settings);
        buffer.SetGlobalDepthBias(0f, 0f);
    }

    void RenderPointShadows()
    {
        
    }
}
