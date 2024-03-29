﻿using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

public class CustomShaderGUI : ShaderGUI
{
    MaterialEditor editor;
    Object[] materials;
    MaterialProperty[] properties;

    bool Clipping { set => SetProperty("_Clipping", "_CLIPPING", value); }
    bool PremultiplyAlpha { set => SetProperty("_PremultiplyAlpha", "_PREMULTIPLY_ALPHA", value); }
    BlendMode SrcBlend { set => SetProperty("_SrcBlend", (float)value); }
    BlendMode DstBlend { set => SetProperty("_DstBlend", (float)value); }
    bool ZWrite { set => SetProperty("_ZWrite", value ? 1f : 0f); }
    RenderQueue RenderQueue { 
        set {
            foreach (Material mat in materials)
            {
                mat.renderQueue = (int)value;
            }
        } 
    }

    bool HasProperty(string name) => FindProperty(name, properties, false) != null;
    bool HasPremultiplyAlpha => HasProperty("_PremultiplyAlpha");

    bool showPresets;

    enum ShadowMode
    {
        On, 
        Clip, 
        Dither, 
        Off
    }

    ShadowMode Shadows
    {
        set
        {
            if (SetProperty("_Shadows", (float)value))
            {
                SetKeyword("_SHADOWS_CLIP", value == ShadowMode.Clip);
                SetKeyword("_SHADOWS_DITHER", value == ShadowMode.Dither);
            }
        }
    }

    public override void OnGUI(MaterialEditor materialEditor, MaterialProperty[] properties)
    {
        EditorGUI.BeginChangeCheck();

        base.OnGUI(materialEditor, properties);

        editor = materialEditor;
        materials = materialEditor.targets;
        this.properties = properties;

        BakedEmission();

        EditorGUILayout.Space();
        showPresets = EditorGUILayout.Foldout(showPresets, "Presets", true);
        if (showPresets)
        {
            OpaquePreset();
            ClipPreset();
            FadePreset();
            TransparentPreset();
        }
        
        if (EditorGUI.EndChangeCheck())
        {
            SetShadowCasterPass();
            CopyLightMappingProperties();
        }
    }

    private void CopyLightMappingProperties()
    {
        MaterialProperty mainTex = FindProperty("_MainTex", properties, false);
		MaterialProperty baseMap = FindProperty("_BaseMap", properties, false);
		if (mainTex != null && baseMap != null) {
			mainTex.textureValue = baseMap.textureValue;
			mainTex.textureScaleAndOffset = baseMap.textureScaleAndOffset;
		}
		MaterialProperty color = FindProperty("_Color", properties, false);
		MaterialProperty baseColor = FindProperty("_BaseColor", properties, false);
		if (color != null && baseColor != null) {
			color.colorValue = baseColor.colorValue;
		}
    }

    private void BakedEmission()
    {
        EditorGUI.BeginChangeCheck();
        editor.LightmapEmissionProperty();
        if (EditorGUI.EndChangeCheck())
        {
            foreach (Material mat in editor.targets)
            {
                mat.globalIlluminationFlags &= ~MaterialGlobalIlluminationFlags.EmissiveIsBlack;
            }
        }
    }

    bool PresetButton(string name)
    {
        if (GUILayout.Button(name))
        {
            editor.RegisterPropertyChangeUndo(name);
            return true;
        }
        return false;
    }

    bool SetProperty(string name, float value)
    {
        MaterialProperty property = FindProperty(name, properties, false);
        if (property != null)
        {
            property.floatValue = value;
            return true;
        }
        return false;
    }

    void SetProperty(string name, string keyword, bool value)
    {
        if (SetProperty(name, value ? 1f : 0f))
        {
            SetKeyword(keyword, value);
        }
    }

    void SetKeyword(string keyword, bool enabled)
    {
        if (enabled)
        {
            foreach (Material mat in materials)
            {
                mat.EnableKeyword(keyword);
            }
        }
        else
        {
            foreach (Material mat in materials)
            {
                mat.DisableKeyword(keyword);
            }
        }    
    }

    void OpaquePreset()
    {
        if (HasPremultiplyAlpha && PresetButton("Opaque"))
        {
            Clipping = false;
            Shadows = ShadowMode.On;
            PremultiplyAlpha = false;
            SrcBlend = BlendMode.One;
            DstBlend = BlendMode.Zero;
            ZWrite = true;
            RenderQueue = RenderQueue.Geometry;
        }
    }

    void ClipPreset()
    {
        if (PresetButton("Clip"))
        {
            Clipping = true;
            Shadows = ShadowMode.Clip;
            PremultiplyAlpha = false;
            SrcBlend = BlendMode.One;
            DstBlend = BlendMode.Zero;
            ZWrite = true;
            RenderQueue = RenderQueue.AlphaTest;
        }
    }

    void FadePreset()
    {
        if (PresetButton("Fade"))
        {
            Clipping = false;
            Shadows = ShadowMode.Dither;
            PremultiplyAlpha = false;
            SrcBlend = BlendMode.SrcAlpha;
            DstBlend = BlendMode.OneMinusSrcAlpha;
            ZWrite = false;
            RenderQueue = RenderQueue.Transparent;
        }
    }

    void TransparentPreset()
    {
        if (PresetButton("Transparent"))
        {
            Clipping = false;
            Shadows = ShadowMode.Dither;
            PremultiplyAlpha = true;
            SrcBlend = BlendMode.One;
            DstBlend = BlendMode.OneMinusSrcAlpha;
            ZWrite = false;
            RenderQueue = RenderQueue.Transparent;
        }
    }

    void SetShadowCasterPass()
    {
        MaterialProperty shadows = FindProperty("_Shadows", properties, false);
        if (shadows == null || shadows.hasMixedValue)
        {
            return;
        }
        bool enabled = shadows.floatValue < (float)ShadowMode.Off;
        foreach (Material mat in materials)
        {
            mat.SetShaderPassEnabled("ShadowCaster", enabled);
        }
    }

}
