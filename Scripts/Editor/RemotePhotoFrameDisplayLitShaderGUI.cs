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
        private static readonly string[] SmoothnessSources = { "Metallic Alpha", "Albedo Alpha" };

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

            materialEditor.TexturePropertySingleLine(AlbedoLabel, albedoTexture, albedoColor);
            materialEditor.TexturePropertySingleLine(MetallicLabel, metallicMap, metallic);

            EditorGUI.indentLevel++;
            materialEditor.RangeProperty(smoothness, "Smoothness");
            int smoothnessSourceIndex = Mathf.Clamp(Mathf.RoundToInt(smoothnessSource.floatValue), 0, SmoothnessSources.Length - 1);
            smoothnessSource.floatValue = EditorGUILayout.Popup(SmoothnessSourceLabel, smoothnessSourceIndex, SmoothnessSources);
            EditorGUI.indentLevel--;

            materialEditor.TexturePropertySingleLine(NormalMapLabel, normalMap, normalScale);
            if (normalMap.textureValue != null)
            {
                EditorGUI.indentLevel++;
                materialEditor.TextureScaleOffsetProperty(normalMap);
                EditorGUI.indentLevel--;
            }

            EditorGUILayout.Space();
            materialEditor.EnableInstancingField();
        }
    }
}
