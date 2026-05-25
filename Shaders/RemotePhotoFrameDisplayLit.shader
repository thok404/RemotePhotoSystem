Shader "RemotePhotoSystem/Photo Frame Display Lit"
{
    Properties
    {
        _MainTex ("Albedo", 2D) = "white" {}
        [HideInInspector] _RemotePhotoImageTex ("Remote Photo Texture", 2D) = "white" {}
        [HideInInspector] _RemotePhotoPreloadTex ("Remote Photo Preload Texture", 2D) = "white" {}
        _RemotePhotoBackgroundColor ("Background Color", Color) = (0, 0, 0, 1)
        _RemotePhotoAlbedoInfluence ("Texture Mix", Range(0, 1)) = 0.5
        [HideInInspector] _RemotePhotoUseAlbedoBackground ("Use Albedo Background", Float) = 0
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
        _Color ("Color", Color) = (1, 1, 1, 1)
        _MetallicGlossMap ("Metallic/Smoothness Mask", 2D) = "white" {}
        _Glossiness ("Smoothness Strength", Range(0, 1)) = 0.2
        [Enum(Metallic Alpha,0,Albedo Alpha,1)] _SmoothnessTextureChannel ("Smoothness Source", Float) = 0
        _Metallic ("Metallic Strength", Range(0, 1)) = 0
        [Normal] _BumpMap ("Normal Map", 2D) = "bump" {}
        _BumpScale ("Normal Strength", Range(0, 2)) = 1
    }

    SubShader
    {
        Tags { "RenderType"="Opaque" "Queue"="Geometry" }
        LOD 200
        Cull [_RemotePhotoCullMode]

        CGPROGRAM
        #pragma surface surf Standard fullforwardshadows vertex:vert
        #pragma target 3.0

        sampler2D _MainTex;
        sampler2D _RemotePhotoImageTex;
        fixed4 _RemotePhotoBackgroundColor;
        half _RemotePhotoAlbedoInfluence;
        half _RemotePhotoUseAlbedoBackground;
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
        fixed4 _Color;
        sampler2D _MetallicGlossMap;
        half _Glossiness;
        half _SmoothnessTextureChannel;
        half _Metallic;
        sampler2D _BumpMap;
        half _BumpScale;

        struct Input
        {
            float2 uv_MainTex;
            float2 uv_RemotePhotoImageTex;
            float3 localPos;
            float3 localNormal;
        };

        void vert(inout appdata_full v, out Input o)
        {
            UNITY_INITIALIZE_OUTPUT(Input, o);
            o.localPos = v.vertex.xyz;
            o.localNormal = v.normal;
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

        void surf(Input IN, inout SurfaceOutputStandard o)
        {
            int shortestAxis = (int)round(_RemotePhotoShortestAxis);
            int dominantAxis = GetDominantAxis(IN.localNormal);
            bool usesBoxProjection = _RemotePhotoProjectionMode > 0.5;
            bool isPhotoFace = !usesBoxProjection || dominantAxis == shortestAxis;

            float2 baseUv = usesBoxProjection ? BuildBoxProjectionUv(IN.localPos) : IN.uv_RemotePhotoImageTex;
            if (usesBoxProjection && GetAxisValue(IN.localNormal, shortestAxis) < 0.0)
            {
                baseUv.x = 1.0 - baseUv.x;
            }
            if (usesBoxProjection && _RemotePhotoBoxHorizontalFlip > 0.5)
            {
                baseUv.x = 1.0 - baseUv.x;
            }

            float2 uv = RotatePhotoUv(baseUv);
            fixed4 albedo = tex2D(_MainTex, IN.uv_MainTex) * _Color;
            fixed4 photo = tex2D(_RemotePhotoImageTex, uv);
            half useAlbedoBackground = _RemotePhotoUseAlbedoBackground;
            half3 albedoDelta = abs(_Color.rgb - half3(1.0, 1.0, 1.0));
            if (albedoDelta.r + albedoDelta.g + albedoDelta.b > 0.001 || abs(_Color.a - 1.0) > 0.001)
            {
                useAlbedoBackground = 1.0;
            }

            fixed4 background = useAlbedoBackground > 0.5 ? albedo : _RemotePhotoBackgroundColor;
            fixed4 c = background;
            if (isPhotoFace)
            {
                fixed3 albedoMultiplier = lerp(fixed3(1.0, 1.0, 1.0), albedo.rgb, saturate(_RemotePhotoAlbedoInfluence));
                c = fixed4(photo.rgb * albedoMultiplier, photo.a);
            }

            if (_RemotePhotoFitMode > 0.5 && _RemotePhotoFitMode < 1.5)
            {
                if (uv.x < 0.0 || uv.x > 1.0 || uv.y < 0.0 || uv.y > 1.0)
                {
                    c = background;
                }
            }

            fixed4 metallicGloss = tex2D(_MetallicGlossMap, IN.uv_MainTex);
            half smoothnessMask = _SmoothnessTextureChannel > 0.5 ? albedo.a : metallicGloss.a;
            o.Albedo = c.rgb;
            o.Metallic = _Metallic * metallicGloss.r;
            o.Smoothness = _Glossiness * smoothnessMask;
            o.Normal = UnpackScaleNormal(tex2D(_BumpMap, IN.uv_MainTex), _BumpScale);
        }
        ENDCG
    }

    FallBack "Diffuse"
    CustomEditor "RemotePhotoSystem.Editor.RemotePhotoFrameDisplayLitShaderGUI"
}
