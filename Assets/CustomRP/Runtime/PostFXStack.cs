using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using static PostFXSettings;

public partial class PostFXStack
{
    private const string bufferName = "Post FX";

    private CommandBuffer buffer = new CommandBuffer() { name = bufferName};

    private ScriptableRenderContext context;

    private Camera camera;

    private PostFXSettings settings;

    public bool IsActive => settings != null;

    enum Pass
    {
        BloomHorizontal,
        BloomVertical,
        BloomAdd,
        BloomScatter,
        BloomScatterFinal,
        BloomPrefilter,
        Copy,
        BloomPrefilterFireflies,
        ToneMappingNone,
        ToneMappingACES,
        ToneMappingNeutral,
        ToneMappingReinhard,
    }

    int bloom_bicubic_upsampling_id = Shader.PropertyToID("_BloomBicubicUpsampling");
    int bloom_intensity_id = Shader.PropertyToID("_BloomIntensity");
    int bloom_threshold_id = Shader.PropertyToID("_BloomThreshold");
    
    int bloom_prefilter_id = Shader.PropertyToID("_BloomPrefilter");
    int bloom_result_id = Shader.PropertyToID("_BloomResult");
    
    int fx_source_id = Shader.PropertyToID("_PostFXSource");
    int fx_source2_id = Shader.PropertyToID("_PostFXSource2");
    
    int color_adjustments_id = Shader.PropertyToID("_ColorAdjustments");
    int color_filter_id = Shader.PropertyToID("_ColorFilter");
    int white_balance_id = Shader.PropertyToID("_WhiteBalance");
    int split_toning_shadows_id = Shader.PropertyToID("_SplitToningShadows");
    int split_toning_highlights_id = Shader.PropertyToID("_SplitToningHighlights");
    int channel_mixer_red_id = Shader.PropertyToID("_ChannelMixerRed");
    int channel_mixer_green_id = Shader.PropertyToID("_ChannelMixerGreen");
    int channel_mixer_blue_id = Shader.PropertyToID("_ChannelMixerBlue");

    private const int maxBloomPyramidLevels = 16;
    private int bloomPyramidId;

    private bool useHDR;
    
    public PostFXStack()
    {
        bloomPyramidId = Shader.PropertyToID("_BloomPyramid0");
        for (int i = 0; i < maxBloomPyramidLevels * 2; i++)
        {
            Shader.PropertyToID("_BloomPyramid" + i);
        }
    }
    
    public void Setup(ScriptableRenderContext context, Camera camera, PostFXSettings settings, bool useHDR)
    {
        this.context = context;
        this.camera = camera;
        this.settings = camera.cameraType <= CameraType.SceneView ? settings : null;
        this.useHDR = useHDR;
        
        ApplySceneViewState();
    }

    public void Render(int sourceId)
    {
        //buffer.Blit(sourceId, BuiltinRenderTextureType.CameraTarget);
        //Draw(sourceId, BuiltinRenderTexture   Type.CameraTarget, Pass.Copy);
        if (DoBloom(sourceId))
        {
            DoColorGradingAndToneMapping(bloom_result_id);
            buffer.ReleaseTemporaryRT(bloom_result_id);
        }
        else
        {
            DoColorGradingAndToneMapping(sourceId);
        }
        context.ExecuteCommandBuffer(buffer);
        buffer.Clear();
    }

    void Draw(RenderTargetIdentifier from, RenderTargetIdentifier to, Pass pass)
    {
        buffer.SetGlobalTexture(fx_source_id, from);
        buffer.SetRenderTarget(to, RenderBufferLoadAction.DontCare, RenderBufferStoreAction.Store);
        buffer.DrawProcedural(Matrix4x4.identity, settings.Material, (int)pass, MeshTopology.Triangles, 3);
    }

    bool DoBloom(int sourceId)
    {
        //buffer.BeginSample("Bloom");

        PostFXSettings.BloomSettings bloom = settings.Bloom;
        int width = camera.pixelWidth / 2;
        int height = camera.pixelHeight / 2;

        if (bloom.maxIterations == 0 || bloom.intensity <= 0f || height < bloom.downscaleLimit * 2 || width < bloom.downscaleLimit * 2)
        {
            //Draw(sourceId, BuiltinRenderTextureType.CameraTarget, Pass.Copy);
            //buffer.EndSample("Bloom");
            return false;
        }

        buffer.BeginSample("Bloom");
        {
            // 亮度阈值曲线计算参数
            Vector4 threshold;
            threshold.x = Mathf.GammaToLinearSpace(bloom.threshold);
            threshold.y = threshold.x * bloom.thresholdKnee;
            threshold.z = 2f * threshold.y;
            threshold.w = 0.25f / (threshold.y + 0.00001f);
            threshold.y -= threshold.x;
            buffer.SetGlobalVector(bloom_threshold_id, threshold);
        }
        
        RenderTextureFormat format = useHDR ? RenderTextureFormat.DefaultHDR : RenderTextureFormat.Default;
        
        // 预过滤 以一半分辨率作为起点进行bloom的金字塔
        buffer.GetTemporaryRT(bloom_prefilter_id, width, height, 0, FilterMode.Bilinear, format);
        Draw(
            sourceId, bloom_prefilter_id, 
            bloom.fadeFireFlies ? Pass.BloomPrefilterFireflies : Pass.BloomPrefilter);//预先进行一次降采样 降低bloom的消耗
        width /= 2;
        height /= 2;
        
        int fromId = bloom_prefilter_id;
        int toId = bloomPyramidId + 1;

        // 每次迭代进行两个步骤
        // 1.下采样 + 水平高斯
        // 2.垂直高斯
        int i;
        for (i = 0; i < bloom.maxIterations; i++)
        {
            if (height < bloom.downscaleLimit || width < bloom.downscaleLimit)
            {
                break;
            }

            int midId = toId - 1;
            buffer.GetTemporaryRT(midId, width, height, 0, FilterMode.Bilinear, format);
            buffer.GetTemporaryRT(toId, width, height, 0, FilterMode.Bilinear, format);
            Draw(fromId, midId, Pass.BloomHorizontal);
            Draw(midId, toId, Pass.BloomVertical);
            fromId = toId;
            toId += 2;
            width /= 2;
            height /= 2;
        }
        
        buffer.ReleaseTemporaryRT(bloom_prefilter_id);

        // 上采样过程是否采用三线性过滤
        buffer.SetGlobalFloat(bloom_bicubic_upsampling_id, bloom.bicubicUpsampling ? 1f : 0f);
        //buffer.SetGlobalFloat(bloom_intensity_id, 1f);

        Pass combinePass, finalPass;
        float finalIntensity;
        if (bloom.mode == PostFXSettings.BloomSettings.Mode.Additive)
        {
            combinePass = finalPass = Pass.BloomAdd;
            buffer.SetGlobalFloat(bloom_intensity_id, 1f);
            finalIntensity = bloom.intensity;
        }
        else
        {
            combinePass = Pass.BloomScatter;
            finalPass = Pass.BloomScatterFinal;
            buffer.SetGlobalFloat(bloom_intensity_id, bloom.scatter);
            finalIntensity = Mathf.Min(bloom.intensity, 0.95f);
        }
        
        if (i > 1)//至少两次下采样过程 pyramid 0-1 2-3
        {
            buffer.ReleaseTemporaryRT(fromId - 1);
            toId -= 5;
        
            for (i -= 1; i > 0; i--)
            {
                buffer.SetGlobalTexture(fx_source2_id, toId + 1);
                Draw(fromId, toId, combinePass);
                buffer.ReleaseTemporaryRT(fromId);
                buffer.ReleaseTemporaryRT(toId + 1);
                fromId = toId;
                toId -= 2;
            }
        }
        else
        {
            buffer.ReleaseTemporaryRT(bloomPyramidId);
        }
        
        buffer.SetGlobalFloat(bloom_intensity_id, finalIntensity);
        buffer.SetGlobalTexture(fx_source2_id, sourceId);
        buffer.GetTemporaryRT(bloom_result_id, camera.pixelWidth, camera.pixelHeight, 0, FilterMode.Bilinear, format);
        Draw(fromId, bloom_result_id, finalPass);
        buffer.ReleaseTemporaryRT(fromId);
        buffer.EndSample("Bloom");
        return true;
    }

    void ConfigureColorAdjustments()
    {
        ColorAdjustmentsSettings colorAdjustments = settings.ColorAdjustments;
        buffer.SetGlobalVector(color_adjustments_id, new Vector4(
            Mathf.Pow(2f, colorAdjustments.postExposure),
            colorAdjustments.contrast * 0.01f + 1f,
            colorAdjustments.hueShift * (1f / 360f),
            colorAdjustments.saturation * 0.01f + 1f
            ));
        buffer.SetGlobalColor(color_filter_id, colorAdjustments.colorFilter.linear);
    }

    void ConfigureWhiteBalance()
    {
        WhiteBalanceSettings whiteBalance = settings.WhiteBalance;
        buffer.SetGlobalVector(white_balance_id, ColorUtils.ColorBalanceToLMSCoeffs(
            whiteBalance.temperature, whiteBalance.tint
            ));
    }

    void ConfigureSplitToning()
    {
        SplitToningSettings splitToning = settings.SplitToning;
        Color splitColor = splitToning.shadows;
        splitColor.a = splitToning.balance * 0.01f;
        buffer.SetGlobalColor(split_toning_shadows_id, splitColor);
        buffer.SetGlobalColor(split_toning_highlights_id, splitToning.highlights);
    }
    
    void ConfigureChannelMixer()
    {
        ChannelMixerSettings channelMixer = settings.ChannelMixer;
        buffer.SetGlobalVector(channel_mixer_red_id, channelMixer.red);
        buffer.SetGlobalVector(channel_mixer_green_id, channelMixer.green);
        buffer.SetGlobalVector(channel_mixer_blue_id, channelMixer.blue);
    }
    
    void DoColorGradingAndToneMapping(int sourceId)
    {
        ConfigureColorAdjustments();
        ConfigureWhiteBalance();
        ConfigureSplitToning();
        ConfigureChannelMixer();
        
        ToneMappingSettings.Mode mode = settings.ToneMapping.mode;
        Pass pass = Pass.ToneMappingNone + (int)mode;
        Draw(sourceId, BuiltinRenderTextureType.CameraTarget, pass);
    }
}
