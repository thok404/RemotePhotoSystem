using UnityEditor;
using UnityEngine;

namespace RemotePhotoSystem.Editor
{
    [CustomEditor(typeof(RemotePhotoButton))]
    [CanEditMultipleObjects]
    public class RemotePhotoButtonEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            RemotePhotoInspectorLanguage language = ResolveLanguage(out bool missingGroup, out bool mixedLanguages);

            EditorGUILayout.PropertyField(
                serializedObject.FindProperty("group"),
                G(language,
                    "Remote Photo Group", "Remote Photo Group", "相框组", "Remote Photo Group",
                    "Send this Button's action to this Group.",
                    "この Button の動作をこの Group に送ります。",
                    "把此 Button 的动作发送给这个 Group。",
                    "이 Button의 동작을 이 Group으로 보냅니다."));

            EditorGUILayout.PropertyField(
                serializedObject.FindProperty("buttonAction"),
                G(language,
                    "Button Action", "ボタン動作", "按钮动作", "버튼 동작",
                    "Sets what this Button does.",
                    "この Button の動作を設定します。",
                    "设置此 Button 的动作。",
                    "이 Button의 동작을 설정합니다."));

            if (missingGroup)
            {
                EditorGUILayout.HelpBox(
                    L(language,
                        "Assign a Remote Photo Group before using this button.",
                        "このボタンを使う前に Remote Photo Group を割り当ててください。",
                        "使用此按钮前，请先指定一个相框组。",
                        "이 버튼을 사용하기 전에 Remote Photo Group을 지정하세요."),
                    MessageType.Warning);
            }

            if (mixedLanguages)
            {
                EditorGUILayout.HelpBox(
                    L(language,
                        "Multiple selected buttons resolve to managers with different Inspector languages, so this Inspector is shown in English.",
                        "選択中の複数ボタンは異なる Inspector 言語の Manager に解決されるため、この Inspector は英語で表示されます。",
                        "当前多选按钮归属到不同 Inspector 语言的 Manager，因此此 Inspector 会使用英文显示。",
                        "여러 선택된 버튼이 서로 다른 Inspector 언어의 Manager에 연결되어 있어 이 Inspector는 영어로 표시됩니다."),
                    MessageType.Warning);
            }

            serializedObject.ApplyModifiedProperties();
        }

        private RemotePhotoInspectorLanguage ResolveLanguage(out bool missingGroup, out bool mixedLanguages)
        {
            missingGroup = false;
            mixedLanguages = false;
            bool hasProjectLanguage = RemotePhotoEditorLocalization.TryResolveProjectLanguage(
                out RemotePhotoInspectorLanguage projectLanguage,
                out bool projectLanguageConflict);

            if (projectLanguageConflict)
            {
                mixedLanguages = true;
                return RemotePhotoInspectorLanguage.English;
            }

            RemotePhotoInspectorLanguage resolved = RemotePhotoInspectorLanguage.English;
            bool resolvedAny = false;

            int index = 0;
            while (index < targets.Length)
            {
                RemotePhotoButton button = targets[index] as RemotePhotoButton;
                RemotePhotoInspectorLanguage candidate = hasProjectLanguage ? projectLanguage : RemotePhotoInspectorLanguage.English;
                if (button == null || button.group == null)
                {
                    missingGroup = true;
                }
                else if (!hasProjectLanguage && !RemotePhotoEditorLocalization.TryResolve(button.group, out candidate))
                {
                    candidate = RemotePhotoInspectorLanguage.English;
                }

                if (!resolvedAny)
                {
                    resolved = candidate;
                    resolvedAny = true;
                }
                else if (resolved != candidate)
                {
                    mixedLanguages = true;
                    return RemotePhotoInspectorLanguage.English;
                }

                index++;
            }

            return resolvedAny ? resolved : RemotePhotoInspectorLanguage.English;
        }

        private string L(RemotePhotoInspectorLanguage language, string english, string japanese, string chinese, string korean)
        {
            return RemotePhotoEditorLocalization.T(language, english, japanese, chinese, korean);
        }

        private GUIContent G(RemotePhotoInspectorLanguage language, string englishLabel, string japaneseLabel, string chineseLabel, string koreanLabel, string englishTooltip, string japaneseTooltip, string chineseTooltip, string koreanTooltip)
        {
            return RemotePhotoEditorLocalization.G(language, englishLabel, japaneseLabel, chineseLabel, koreanLabel, englishTooltip, japaneseTooltip, chineseTooltip, koreanTooltip);
        }
    }
}
