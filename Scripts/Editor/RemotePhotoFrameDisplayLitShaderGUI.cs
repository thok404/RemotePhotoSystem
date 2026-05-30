using UnityEditor;
using UnityEngine;

namespace RemotePhotoSystem.Editor
{
    public class RemotePhotoFrameDisplayUnlitShaderGUI : ShaderGUI
    {
        private static readonly GUIContent AlbedoLabel = new GUIContent("Albedo");

        public override void OnGUI(MaterialEditor materialEditor, MaterialProperty[] properties)
        {
            MaterialProperty albedoTexture = FindProperty("_MainTex", properties);
            MaterialProperty albedoColor = FindProperty("_Color", properties);

            materialEditor.TexturePropertySingleLine(AlbedoLabel, albedoTexture, albedoColor);

            EditorGUILayout.Space();
            materialEditor.EnableInstancingField();
        }
    }

    public class RemotePhotoFrameDisplayLitShaderGUI : ShaderGUI
    {
        private static readonly GUIContent AlbedoLabel = new GUIContent("Albedo");
        private static readonly GUIContent MetallicLabel = new GUIContent("Metallic");
        private static readonly GUIContent NormalMapLabel = new GUIContent("Normal Map");
        private static readonly GUIContent SmoothnessSourceLabel = new GUIContent("Source");
        private static readonly GUIContent TextureMixLabel = new GUIContent("Texture Mix");
        private static readonly GUIContent SurfaceUvSetLabel = new GUIContent("Surface UV Set");
        private static readonly string[] SmoothnessSources = { "Metallic Alpha", "Albedo Alpha" };
        private static readonly string[] SurfaceUvSets = { "UV0", "UV1" };

        public override void OnGUI(MaterialEditor materialEditor, MaterialProperty[] properties)
        {
            MaterialProperty albedoTexture = FindProperty("_MainTex", properties);
            MaterialProperty albedoColor = FindProperty("_Color", properties);
            MaterialProperty metallicMap = FindProperty("_MetallicGlossMap", properties);
            MaterialProperty metallic = FindProperty("_Metallic", properties);
            MaterialProperty smoothness = FindProperty("_Glossiness", properties);
            MaterialProperty smoothnessSource = FindProperty("_SmoothnessTextureChannel", properties);
            MaterialProperty normalMap = FindProperty("_BumpMap", properties);
            MaterialProperty normalScale = FindProperty("_BumpScale", properties);
            MaterialProperty photoAlbedoInfluence = FindProperty("_RemotePhotoAlbedoInfluence", properties);
            MaterialProperty surfaceUvSet = FindProperty("_RemotePhotoSurfaceUvSet", properties);
            MaterialProperty useAlbedoBackground = FindProperty("_RemotePhotoUseAlbedoBackground", properties, false);

            int surfaceUvSetIndex = Mathf.Clamp(Mathf.RoundToInt(surfaceUvSet.floatValue), 0, SurfaceUvSets.Length - 1);
            surfaceUvSet.floatValue = EditorGUILayout.Popup(SurfaceUvSetLabel, surfaceUvSetIndex, SurfaceUvSets);
            materialEditor.RangeProperty(photoAlbedoInfluence, TextureMixLabel.text);
            materialEditor.TexturePropertySingleLine(AlbedoLabel, albedoTexture, albedoColor);
            if (useAlbedoBackground != null)
            {
                Color color = albedoColor.colorValue;
                bool nonDefaultAlbedo = albedoTexture.textureValue != null ||
                    Mathf.Abs(color.r - 1f) > 0.001f ||
                    Mathf.Abs(color.g - 1f) > 0.001f ||
                    Mathf.Abs(color.b - 1f) > 0.001f ||
                    Mathf.Abs(color.a - 1f) > 0.001f;
                useAlbedoBackground.floatValue = nonDefaultAlbedo ? 1f : 0f;
            }

            materialEditor.TexturePropertySingleLine(MetallicLabel, metallicMap, metallic);

            EditorGUI.indentLevel++;
            materialEditor.RangeProperty(smoothness, "Smoothness");
            int smoothnessSourceIndex = Mathf.Clamp(Mathf.RoundToInt(smoothnessSource.floatValue), 0, SmoothnessSources.Length - 1);
            smoothnessSource.floatValue = EditorGUILayout.Popup(SmoothnessSourceLabel, smoothnessSourceIndex, SmoothnessSources);
            EditorGUI.indentLevel--;

            materialEditor.TexturePropertySingleLine(NormalMapLabel, normalMap, normalScale);

            EditorGUILayout.Space();
            materialEditor.EnableInstancingField();
        }
    }
}
