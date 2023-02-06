using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

public partial class CustomRenderPipeline : RenderPipeline
{
    bool allowHDR;
    bool useDynamicBatching;
    bool useGPUInstancing;
    private bool useLightsPerObject;
    CameraRenderer renderer = new CameraRenderer();
    ShadowSettings shadowSettings;
    PostFXSettings postFXSettings;
    
    public CustomRenderPipeline(bool allowHDR, bool useDynamicBatching, bool useGPUInstancing, bool useSRPBatcher, bool useLightsPerObject, ShadowSettings shadowSettings, PostFXSettings postFXSettings)
    {
        this.allowHDR = allowHDR;
        this.shadowSettings = shadowSettings;
        this.useDynamicBatching = useDynamicBatching;
        this.useGPUInstancing = useGPUInstancing;
        this.useLightsPerObject = useLightsPerObject;
        this.postFXSettings = postFXSettings;
        GraphicsSettings.useScriptableRenderPipelineBatching = useSRPBatcher;
        GraphicsSettings.lightsUseLinearIntensity = true;

        InitializeForEditor();
    }

    protected override void Render(ScriptableRenderContext context, Camera[] cameras)
    {
        foreach (Camera camera in cameras)
        {
            renderer.Render(
                context, camera, allowHDR,
                useDynamicBatching, useGPUInstancing, useLightsPerObject, 
                shadowSettings, postFXSettings
                );
        }
    }
}
