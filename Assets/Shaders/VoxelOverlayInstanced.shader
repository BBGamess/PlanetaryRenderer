Shader "Custom/VoxelWireOverlayInstanced"
{
    Properties
    {
        _Color ("Color", Color) = (1,0.5,0,0.9)
        _Scale ("Cube Scale", Float) = 0.98
        _EdgeWidth ("Edge Width", Range(0.001, 0.2)) = 0.03
        _WorldOffset ("World Offset", Vector) = (0,0,0,0)

        _ClipMode ("Clip Mode", Float) = 0
        _ClipPlane ("Clip Plane", Vector) = (1,0,0,0)
        _SliceThickness ("Slice Thickness", Float) = 10.0

        _SubsampleStride ("Subsample Stride", Float) = 1
    }

    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" }
        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off
        ZTest LEqual
        Cull Back

        Pass
        {
            HLSLPROGRAM
            #pragma target 4.5
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            struct Voxel
            {
                float3 center;
                float size;
            };

            StructuredBuffer<Voxel> _Voxels;

            float4 _Color;
            float _Scale;
            float _EdgeWidth;
            float3 _WorldOffset;

            float _ClipMode;          // 0=off, 1=halfspace, 2=slice
            float4 _ClipPlane;        // xyz = normal, w = plane offset
            float _SliceThickness;
            float _SubsampleStride;

            struct appdata
            {
                float3 vertex : POSITION;
                uint instanceID : SV_InstanceID;
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
                float3 localCube : TEXCOORD0;
                float3 worldCenter : TEXCOORD1;
                uint instanceID : TEXCOORD2;
            };

            v2f vert(appdata v)
            {
                Voxel voxel = _Voxels[v.instanceID];

                float3 scaledLocal = v.vertex * _Scale;
                float3 worldCenter = _WorldOffset + voxel.center;
                float3 worldPos = worldCenter + scaledLocal * voxel.size;

                v2f o;
                o.pos = UnityWorldToClipPos(float4(worldPos, 1.0));
                o.localCube = scaledLocal;
                o.worldCenter = worldCenter;
                o.instanceID = v.instanceID;
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                // Optional subsampling
                if (_SubsampleStride > 1.0)
                {
                    uint stride = (uint)_SubsampleStride;
                    if ((i.instanceID % stride) != 0)
                        discard;
                }

                // Optional clipping
                if (_ClipMode > 0.5)
                {
                    float planeDist = dot(i.worldCenter, _ClipPlane.xyz) + _ClipPlane.w;

                    // half-space cut
                    if (_ClipMode < 1.5)
                    {
                        if (planeDist > 0.0)
                            discard;
                    }
                    // slice
                    else
                    {
                        if (abs(planeDist) > _SliceThickness)
                            discard;
                    }
                }

                float3 a = abs(i.localCube);
                float3 d = abs(a - 0.5 * _Scale);

                bool xEdge = (d.y < _EdgeWidth && d.z < _EdgeWidth);
                bool yEdge = (d.x < _EdgeWidth && d.z < _EdgeWidth);
                bool zEdge = (d.x < _EdgeWidth && d.y < _EdgeWidth);

                if (!(xEdge || yEdge || zEdge))
                    discard;

                float dist = length(_WorldSpaceCameraPos - i.worldCenter);
                float fade = saturate(1.0 - dist * 0.002);
                return float4(_Color.rgb, _Color.a * fade);
            }
            ENDHLSL
        }
    }
}