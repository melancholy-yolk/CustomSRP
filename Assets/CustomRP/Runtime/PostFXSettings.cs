using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName = "Rendering/Custom Post FX Settings")]
public class PostFXSettings : ScriptableObject
{
    [SerializeField]
    private Shader shader = default;

    [NonSerialized]
    private Material material;

    public Material Material
    {
        get
        {
            if (material == null && shader != null)
            {
                material = new Material(shader);
                material.hideFlags = HideFlags.HideAndDontSave;
            }
            return material;
        }
    }

    #region Bloom
    [Serializable]
    public struct BloomSettings
    {
        /// <summary>
        /// 下采样迭代数
        /// </summary>
        [Range(0f, 16f)]
        public int maxIterations;
        
        /// <summary>
        /// 下采样RT尺寸限制
        /// </summary>
        [Min(1f)]
        public int downscaleLimit;

        /// <summary>
        /// 三线性上采样
        /// </summary>
        public bool bicubicUpsampling;

        [Min(0f)]
        public float threshold;

        [Range(0f, 1f)]
        public float thresholdKnee;

        [Min(0f)] 
        public float intensity;

        public bool fadeFireFlies;

        public enum Mode
        {
            Additive,
            Scattering,
        }

        public Mode mode;

        [Range(0.05f, 0.95f)]
        public float scatter;
    }

    [SerializeField]
    private BloomSettings bloom = new BloomSettings
    {
        scatter = 0.7f
    };
    
    public BloomSettings Bloom => bloom;
    #endregion

    #region Color Adjustments
    
    [Serializable]
    public struct ColorAdjustmentsSettings
    {
        public float postExposure;

        [Range(-100f, 100f)]
        public float contrast;

        [ColorUsage(false, true)]
        public Color colorFilter;

        [Range(-180f, 180f)]
        public float hueShift;
        
        [Range(-100f, 100f)]
        public float saturation;
    }

    [SerializeField]
    private ColorAdjustmentsSettings colorAdjustments = new ColorAdjustmentsSettings
    {
        colorFilter = Color.white
    };

    public ColorAdjustmentsSettings ColorAdjustments => colorAdjustments;

    #endregion
    
    #region Tone Mapping
    [Serializable]
    public struct ToneMappingSettings
    {
        public enum Mode
        {
            None,
            ACES,
            Neutral,
            Reinhard,
        }

        public Mode mode;
    }

    [SerializeField]
    private ToneMappingSettings toneMapping = default;

    public ToneMappingSettings ToneMapping => toneMapping;
    #endregion

    #region White Balance

    [Serializable]
    public struct WhiteBalanceSettings
    {
        [Range(-100f, 100f)]
        public float temperature, tint;
    }

    [SerializeField]
    private WhiteBalanceSettings whiteBalance = default;

    public WhiteBalanceSettings WhiteBalance => whiteBalance;

    #endregion

    #region Split Toning

    [Serializable]
    public struct SplitToningSettings
    {
        [ColorUsage(false)]
        public Color shadows, highlights;

        [Range(-100f, 100f)]
        public float balance;
    }

    [SerializeField]
    private SplitToningSettings splitToning = new SplitToningSettings
    {
        shadows = Color.gray,
        highlights = Color.gray
    };

    public SplitToningSettings SplitToning => splitToning;

    #endregion

    #region Channel Mixer

    [Serializable]
    public struct ChannelMixerSettings
    {
        public Vector3 red, green, blue;
    }

    [SerializeField]
    private ChannelMixerSettings channelMixer = new ChannelMixerSettings
    {
        red = Vector3.right,
        green = Vector3.up,
        blue = Vector3.forward
    };

    public ChannelMixerSettings ChannelMixer => channelMixer;

    #endregion

}
