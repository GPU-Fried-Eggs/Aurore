Shader "BoneRenderer"
{
    SubShader
    {
        Tags
        {
            "RenderPipeline" = "UniversalPipeline"
            "RenderType" = "Opaque"
        }

        Pass
        {
            Tags
            {
                "LightMode" = "UniversalForward"
            }

            Blend SrcAlpha OneMinusSrcAlpha
            ZTest off

            HLSLPROGRAM
            #pragma target 3.0

            #pragma vertex ForwardPassVertex
            #pragma fragment ForwardPassFragment

            #include "./BoneRendererForwardPass.hlsl"
            ENDHLSL
        }
    }
}