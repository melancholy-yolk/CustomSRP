Shader "Unlit/TestGeometry"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" }
        LOD 100

        HLSLINCLUDE
        
        ENDHLSL
        
        Pass
        {
            HLSLPROGRAM
            
            ENDHLSL
        }
    }
}
