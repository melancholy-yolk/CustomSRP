using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName = "Rendering/Custom Post FX Settings")]
public class PostFXSettings : ScriptableObject
{
    [SerializeField]
    private Shader shader = default;

    [System.NonSerialized]
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

    [System.Serializable]
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
}
