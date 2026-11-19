Shader "Hidden/ShaderNPR/GeometryMask"
{
    SubShader
    {
        Pass
        {
            Cull Off
            ZWrite Off
            ZTest Always
            Blend One Zero

            HLSLPROGRAM
            #pragma vertex Vertex
            #pragma fragment Fragment

            float4x4 _GeometryMvp;

            float4 Vertex(float4 position : POSITION) : SV_POSITION
            {
                return mul(_GeometryMvp, position);
            }

            float4 Fragment() : SV_Target
            {
                return float4(1, 1, 1, 1);
            }
            ENDHLSL
        }
    }
}
