using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

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
        BloomCombine,
        BloomPrefilter,
        Copy,
    }

    int bloom_bicubic_upsampling_id = Shader.PropertyToID("_BloomBicubicUpsampling");
    int bloom_intensity_id = Shader.PropertyToID("_BloomIntensity");
    int bloom_prefilter_id = Shader.PropertyToID("_BloomPrefilter");
    int bloom_threshold_id = Shader.PropertyToID("_BloomThreshold");
    int fx_source_id = Shader.PropertyToID("_PostFXSource");
    int fx_source2_id = Shader.PropertyToID("_PostFXSource2");

    private const int maxBloomPyramidLevels = 16;
    private int bloomPyramidId;

    public PostFXStack()
    {
        bloomPyramidId = Shader.PropertyToID("_BloomPyramid0");
        for (int i = 0; i < maxBloomPyramidLevels * 2; i++)
        {
            Shader.PropertyToID("_BloomPyramid" + i);
        }
    }
    
    public void Setup(ScriptableRenderContext context, Camera camera, PostFXSettings settings)
    {
        this.context = context;
        this.camera = camera;
        this.settings = camera.cameraType <= CameraType.SceneView ? settings : null;
        
        ApplySceneViewState();
    }

    public void Render(int sourceId)
    {
        //buffer.Blit(sourceId, BuiltinRenderTextureType.CameraTarget);
        //Draw(sourceId, BuiltinRenderTexture   Type.CameraTarget, Pass.Copy);
        DoBloom(sourceId);
        context.ExecuteCommandBuffer(buffer);
        buffer.Clear();
    }

    void Draw(RenderTargetIdentifier from, RenderTargetIdentifier to, Pass pass)
    {
        buffer.SetGlobalTexture(fx_source_id, from);
        buffer.SetRenderTarget(to, RenderBufferLoadAction.DontCare, RenderBufferStoreAction.Store);
        buffer.DrawProcedural(Matrix4x4.identity, settings.Material, (int)pass, MeshTopology.Triangles, 3);
    }

    void DoBloom(int sourceId)
    {
        buffer.BeginSample("Bloom");

        PostFXSettings.BloomSettings bloom = settings.Bloom;
        int width = camera.pixelWidth / 2;
        int height = camera.pixelHeight / 2;

        if (bloom.maxIterations == 0 || bloom.intensity <= 0f || height < bloom.downscaleLimit * 2 || width < bloom.downscaleLimit * 2)
        {
            Draw(sourceId, BuiltinRenderTextureType.CameraTarget, Pass.Copy);
            buffer.EndSample("Bloom");
            return;
        }

        // 亮度阈值曲线计算参数
        Vector4 threshold;
        threshold.x = Mathf.GammaToLinearSpace(bloom.threshold);
        threshold.y = threshold.x * bloom.thresholdKnee;
        threshold.z = 2f * threshold.y;
        threshold.w = 0.25f / (threshold.y + 0.00001f);
        threshold.y -= threshold.x;
        buffer.SetGlobalVector(bloom_threshold_id, threshold);
        
        RenderTextureFormat format = RenderTextureFormat.Default;
        
        // 预过滤 以一半分辨率作为起点进行bloom的金字塔
        buffer.GetTemporaryRT(bloom_prefilter_id, width, height, 0, FilterMode.Bilinear, format);
        Draw(sourceId, bloom_prefilter_id, Pass.BloomPrefilter);//预先进行一次降采样 降低bloom的消耗
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
        buffer.SetGlobalFloat(bloom_intensity_id, 1f);
        if (i > 1)
        {
            buffer.ReleaseTemporaryRT(fromId - 1);
            toId -= 5;
        
            for (i -= 1; i > 0; i--)
            {
                buffer.SetGlobalTexture(fx_source2_id, toId + 1);
                Draw(fromId, toId, Pass.BloomCombine);
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
        
        buffer.SetGlobalFloat(bloom_intensity_id, bloom.intensity);
        buffer.SetGlobalTexture(fx_source2_id, sourceId);
        Draw(fromId, BuiltinRenderTextureType.CameraTarget, Pass.BloomCombine);
        buffer.ReleaseTemporaryRT(fromId);
        
        buffer.EndSample("Bloom");
    }
}
