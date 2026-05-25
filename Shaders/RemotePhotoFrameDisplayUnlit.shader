Shader "RemotePhotoSystem/Photo Frame Display Unlit"
{
    Properties
    {
        _MainTex ("Photo Texture", 2D) = "white" {}
        [HideInInspector] _RemotePhotoPreloadTex ("Remote Photo Preload Texture", 2D) = "white" {}
        _Color ("Color", Color) = (1, 1, 1, 1)
        _RemotePhotoBackgroundColor ("Background Color", Color) = (0, 0, 0, 1)
        [HideInInspector] _RemotePhotoFitMode ("Remote Photo Fit Mode", Float) = 0
        [HideInInspector] _PhotoRotationDegrees ("Photo Rotation Degrees", Range(0, 360)) = 0
        [HideInInspector] _RemotePhotoProjectionMode ("Remote Photo Projection Mode", Float) = 0
        [HideInInspector] _RemotePhotoCullMode ("Remote Photo Cull Mode", Float) = 2
        [HideInInspector] _RemotePhotoShortestAxis ("Remote Photo Shortest Axis", Float) = 2
        [HideInInspector] _RemotePhotoBoundsCenterX ("Remote Photo Bounds Center X", Float) = 0
        [HideInInspector] _RemotePhotoBoundsCenterY ("Remote Photo Bounds Center Y", Float) = 0
        [HideInInspector] _RemotePhotoBoundsCenterZ ("Remote Photo Bounds Center Z", Float) = 0
        [HideInInspector] _RemotePhotoBoundsSizeX ("Remote Photo Bounds Size X", Float) = 1
        [HideInInspector] _RemotePhotoBoundsSizeY ("Remote Photo Bounds Size Y", Float) = 1
        [HideInInspector] _RemotePhotoBoundsSizeZ ("Remote Photo Bounds Size Z", Float) = 1
        [HideInInspector] _RemotePhotoBoxHorizontalFlip ("Remote Photo Box Horizontal Flip", Float) = 0
        [HideInInspector] _RemotePhotoUvScaleX ("Remote Photo UV Scale X", Float) = 1
        [HideInInspector] _RemotePhotoUvScaleY ("Remote Photo UV Scale Y", Float) = 1
        [HideInInspector] _RemotePhotoUvOffsetX ("Remote Photo UV Offset X", Float) = 0
        [HideInInspector] _RemotePhotoUvOffsetY ("Remote Photo UV Offset Y", Float) = 0
    }

    SubShader
    {
        Tags { "RenderType"="Opaque" "Queue"="Geometry" }
        LOD 100

        Pass
        {
            Cull [_RemotePhotoCullMode]

            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
                float3 normal : NORMAL;
                float2 uv : TEXCOORD0;
            };

            struct v2f
            {
                float4 vertex : SV_POSITION;
                float2 uv : TEXCOORD0;
                float3 localPos : TEXCOORD1;
                float3 localNormal : TEXCOORD2;
            };

            sampler2D _MainTex;
            float4 _MainTex_ST;
            fixed4 _Color;
            fixed4 _RemotePhotoBackgroundColor;
            float _RemotePhotoFitMode;
            float _PhotoRotationDegrees;
            float _RemotePhotoProjectionMode;
            float _RemotePhotoShortestAxis;
            float _RemotePhotoBoundsCenterX;
            float _RemotePhotoBoundsCenterY;
            float _RemotePhotoBoundsCenterZ;
            float _RemotePhotoBoundsSizeX;
            float _RemotePhotoBoundsSizeY;
            float _RemotePhotoBoundsSizeZ;
            float _RemotePhotoBoxHorizontalFlip;
            float _RemotePhotoUvScaleX;
            float _RemotePhotoUvScaleY;
            float _RemotePhotoUvOffsetX;
            float _RemotePhotoUvOffsetY;

            v2f vert(appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = TRANSFORM_TEX(v.uv, _MainTex);
                o.localPos = v.vertex.xyz;
                o.localNormal = v.normal;
                return o;
            }

            float GetAxisValue(float3 value, int axis)
            {
                if (axis == 0)
                {
                    return value.x;
                }

                if (axis == 1)
                {
                    return value.y;
                }

                return value.z;
            }

            int GetDominantAxis(float3 value)
            {
                float3 absoluteValue = abs(value);
                if (absoluteValue.x >= absoluteValue.y && absoluteValue.x >= absoluteValue.z)
                {
                    return 0;
                }

                if (absoluteValue.y >= absoluteValue.x && absoluteValue.y >= absoluteValue.z)
                {
                    return 1;
                }

                return 2;
            }

            float2 BuildBoxProjectionUv(float3 localPos)
            {
                int shortestAxis = (int)round(_RemotePhotoShortestAxis);
                float3 center = float3(_RemotePhotoBoundsCenterX, _RemotePhotoBoundsCenterY, _RemotePhotoBoundsCenterZ);
                float3 size = max(float3(_RemotePhotoBoundsSizeX, _RemotePhotoBoundsSizeY, _RemotePhotoBoundsSizeZ), 0.0001);
                float3 minValue = center - (size * 0.5);

                int axisA = shortestAxis == 0 ? 1 : 0;
                int axisB = shortestAxis == 2 ? 1 : 2;
                if (shortestAxis == 1)
                {
                    axisA = 0;
                    axisB = 2;
                }

                float sizeA = GetAxisValue(size, axisA);
                float sizeB = GetAxisValue(size, axisB);
                if (sizeB > sizeA)
                {
                    int swapAxis = axisA;
                    axisA = axisB;
                    axisB = swapAxis;
                }

                float u = (GetAxisValue(localPos, axisA) - GetAxisValue(minValue, axisA)) / GetAxisValue(size, axisA);
                float v = (GetAxisValue(localPos, axisB) - GetAxisValue(minValue, axisB)) / GetAxisValue(size, axisB);

                return float2(u, v) * float2(_RemotePhotoUvScaleX, _RemotePhotoUvScaleY) +
                    float2(_RemotePhotoUvOffsetX, _RemotePhotoUvOffsetY);
            }

            float2 RotatePhotoUv(float2 baseUv)
            {
                float radians = _PhotoRotationDegrees * 0.01745329252;
                float sineValue = sin(radians);
                float cosineValue = cos(radians);
                float2 centeredUv = baseUv - 0.5;
                return float2(
                    (centeredUv.x * cosineValue) - (centeredUv.y * sineValue),
                    (centeredUv.x * sineValue) + (centeredUv.y * cosineValue)) + 0.5;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                int shortestAxis = (int)round(_RemotePhotoShortestAxis);
                int dominantAxis = GetDominantAxis(i.localNormal);
                bool usesBoxProjection = _RemotePhotoProjectionMode > 0.5;
                bool isPhotoFace = !usesBoxProjection || dominantAxis == shortestAxis;
                if (!isPhotoFace)
                {
                    return fixed4(_RemotePhotoBackgroundColor.rgb, 1.0);
                }

                float2 baseUv = usesBoxProjection ? BuildBoxProjectionUv(i.localPos) : i.uv;
                if (usesBoxProjection && GetAxisValue(i.localNormal, shortestAxis) < 0.0)
                {
                    baseUv.x = 1.0 - baseUv.x;
                }
                if (usesBoxProjection && _RemotePhotoBoxHorizontalFlip > 0.5)
                {
                    baseUv.x = 1.0 - baseUv.x;
                }

                float2 uv = RotatePhotoUv(baseUv);
                fixed4 color = tex2D(_MainTex, uv) * _Color;

                if (_RemotePhotoFitMode > 0.5 && _RemotePhotoFitMode < 1.5)
                {
                    if (uv.x < 0.0 || uv.x > 1.0 || uv.y < 0.0 || uv.y > 1.0)
                    {
                        color = _RemotePhotoBackgroundColor;
                    }
                }

                color.a = 1.0;
                return color;
            }
            ENDCG
        }
    }

    CustomEditor "RemotePhotoSystem.Editor.RemotePhotoFrameDisplayUnlitShaderGUI"
}
