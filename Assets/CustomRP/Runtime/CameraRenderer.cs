using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

public partial class CameraRenderer
{
    ScriptableRenderContext context;
    Camera camera;
    const string bufferName = "Render Camera";//frame debugger 采样标签
    CommandBuffer buffer = new CommandBuffer() { name = bufferName};

    CullingResults cullingResults;

    static ShaderTagId unlitShaderTagId = new ShaderTagId("SRPDefaultUnlit");
    static ShaderTagId litShaderTagId = new ShaderTagId("CustomLit");

    Lighting lighting = new Lighting();

    PostFXStack postFXStack = new PostFXStack();
    private static int frame_buffer_id = Shader.PropertyToID("_CameraFrameBuffer");

    public void Render(ScriptableRenderContext context, Camera camera, bool useDynamicBatching, bool useGPUInstancing, bool useLightsPerObject, ShadowSettings shadowSettings, PostFXSettings postFXSettings)
    {
        this.context = context;
        this.camera = camera;

        PrepareBuffer();
        PrepareForSceneWindow();

        if (!Cull(shadowSettings.maxDistance))
        {
            return;
        }

        buffer.BeginSample(SampleName);
        ExecuteBuffer();
        lighting.Setup(context, cullingResults, shadowSettings, useLightsPerObject);//CPU将光源数据发送到GPU
        postFXStack.Setup(context, camera, postFXSettings);
        buffer.EndSample(SampleName);
        
        SetUp();//将相机数据发送到GPU clear
        DrawVisibleGeometry(useDynamicBatching, useGPUInstancing, useLightsPerObject);//绘制可见几何体
        DrawUnsupportedShaders();
        DrawGizmosBeforeFX();
        if (postFXStack.IsActive)
        {
            postFXStack.Render(frame_buffer_id);
        }
        DrawGizmosAfterFX();
        Cleanup();
        Submit();
    }

    // cull sort filter
    void DrawVisibleGeometry(bool useDynamicBatching, bool useGPUInstancing, bool useLightsPerObject)
    {
        PerObjectData lightsPerObjectFlags =
            useLightsPerObject ? PerObjectData.LightData | PerObjectData.LightIndices : PerObjectData.None;
        
        //render opaque
        var sortingSettings = new SortingSettings(camera) 
        {
            criteria = SortingCriteria.CommonOpaque
        };

        var drawSettings = new DrawingSettings(unlitShaderTagId, sortingSettings)
        {
            enableDynamicBatching = useDynamicBatching,
            enableInstancing = useGPUInstancing,
            perObjectData = 
                PerObjectData.ReflectionProbes |
                PerObjectData.Lightmaps | PerObjectData.ShadowMask | 
                PerObjectData.LightProbe | PerObjectData.OcclusionProbe | 
                PerObjectData.LightProbeProxyVolume | PerObjectData.OcclusionProbeProxyVolume |
                lightsPerObjectFlags
        };
        drawSettings.SetShaderPassName(1, litShaderTagId);

        var filteringSettings = new FilteringSettings(RenderQueueRange.opaque);
        context.DrawRenderers(cullingResults, ref drawSettings, ref filteringSettings);

        //render skybox
        context.DrawSkybox(camera);

        //render transparent
        sortingSettings.criteria = SortingCriteria.CommonTransparent;
        drawSettings.sortingSettings = sortingSettings;
        filteringSettings.renderQueueRange = RenderQueueRange.transparent;
        context.DrawRenderers(cullingResults, ref drawSettings, ref filteringSettings);
    }

    

    void SetUp()
    {
        context.SetupCameraProperties(camera);//设置VP矩阵 lookat perspective_project
        CameraClearFlags clearFlags = camera.clearFlags;

        if (postFXStack.IsActive)
        {
            if (clearFlags > CameraClearFlags.Color)
            {
                clearFlags = CameraClearFlags.Color;
            }
            buffer.GetTemporaryRT(frame_buffer_id, camera.pixelWidth, camera.pixelHeight, 32, FilterMode.Bilinear, RenderTextureFormat.Default);
            buffer.SetRenderTarget(frame_buffer_id, RenderBufferLoadAction.DontCare, RenderBufferStoreAction.Store);
        }
        
        
        buffer.ClearRenderTarget(true, true, Color.clear);
        buffer.BeginSample(SampleName);
        ExecuteBuffer();
    }

    void Submit()
    {
        buffer.EndSample(SampleName);
        ExecuteBuffer();
        context.Submit();
    }

    void ExecuteBuffer()
    {
        context.ExecuteCommandBuffer(buffer);
        buffer.Clear();
    }

    bool Cull(float maxShadowDistance)
    {
        //相机视锥体剔除
        if (camera.TryGetCullingParameters(out ScriptableCullingParameters p))
        {
            p.shadowDistance = Mathf.Min(maxShadowDistance, camera.farClipPlane);
            cullingResults = context.Cull(ref p);
            return true;
        }
        return false;
    }

    void Cleanup()
    {
        lighting.Cleanup();
        if (postFXStack.IsActive)
        {
            buffer.ReleaseTemporaryRT(frame_buffer_id);
        }
    }
    
}
