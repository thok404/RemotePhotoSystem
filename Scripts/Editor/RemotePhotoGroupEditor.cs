using UnityEditor;
using UnityEngine;

namespace RemotePhotoSystem.Editor
{
    [CustomEditor(typeof(RemotePhotoGroup))]
    [CanEditMultipleObjects]
    public class RemotePhotoGroupEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            bool multiObjectEditing = targets.Length > 1;
            RemotePhotoInspectorLanguage language = ResolveLanguage(out bool missingServiceLink, out bool mixedServiceLanguages);

            if (missingServiceLink)
            {
                EditorGUILayout.HelpBox(
                    L(language,
                        "This group is not linked to a Remote Photo Manager. Inspector language falls back to English until the link is restored.",
                        "このグループは Remote Photo Manager にリンクされていません。リンクされるまで Inspector の言語は英語にフォールバックします。",
                        "此组尚未连接到 Remote Photo Manager。在恢复连接前，Inspector 语言会回退为英文。",
                        "이 그룹은 Remote Photo Manager에 연결되어 있지 않습니다. 연결이 복구될 때까지 Inspector 언어는 영어로 대체됩니다."),
                    MessageType.Warning);
            }

            if (mixedServiceLanguages)
            {
                EditorGUILayout.HelpBox(
                    L(language,
                        "Multiple selected groups are managed by Remote Photo Managers using different languages. This inspector is temporarily shown in English.",
                        "選択中の複数グループは異なる言語設定の Remote Photo Manager に管理されています。この Inspector は一時的に英語で表示されます。",
                        "当前多选的组由使用不同语言设置的 Remote Photo Manager 管理，因此此 Inspector 会暂时使用英文显示。",
                        "여러 선택된 그룹이 서로 다른 언어 설정의 Remote Photo Manager에 의해 관리되고 있습니다. 이 Inspector는 임시로 영어로 표시됩니다."),
                    MessageType.Warning);
            }

            EditorGUILayout.PropertyField(
                serializedObject.FindProperty("triggerCooldownSeconds"),
                G(language,
                    "Trigger Cooldown Seconds", "トリガークールダウン秒数", "触发冷却秒数", "트리거 쿨다운 초",
                    "Blocks repeated triggers for this many seconds.",
                    "この秒数だけ連続トリガーを防ぎます。",
                    "在这段秒数内阻止重复触发。",
                    "이 초 동안 반복 트리거를 막습니다."));

            EditorGUILayout.PropertyField(
                serializedObject.FindProperty("targets"),
                G(language,
                    "Frames", "フレーム", "相框", "프레임",
                    "Photos are assigned and loaded in this synced order.",
                    "写真はこの同期順で Frames に割り当て、読み込みます。",
                    "图片会按这个同步顺序分配并加载。",
                    "사진은 이 동기화 순서로 Frames에 할당되고 로드됩니다."),
                true);

            if (multiObjectEditing)
            {
                EditorGUILayout.LabelField(
                    L(language, "Multi-object editing active", "複数オブジェクト編集中", "多物体编辑已启用", "다중 오브젝트 편집 활성화") +
                    " (" + targets.Length + ")",
                    EditorStyles.miniLabel);
            }

            int negativeCooldownCount = CountNegativeCooldowns();
            if (negativeCooldownCount > 0)
            {
                EditorGUILayout.HelpBox(
                    L(language,
                        "Trigger Cooldown Seconds should be 0 or greater. Set it to 0 to disable cooldown.",
                        "Trigger Cooldown Seconds は 0 以上にしてください。0 にするとクールダウンを無効化します。",
                        "Trigger Cooldown Seconds 必须大于等于 0。设为 0 可禁用冷却。",
                        "Trigger Cooldown Seconds는 0 이상이어야 합니다. 0으로 설정하면 쿨다운이 비활성화됩니다.") +
                    CountSuffix(negativeCooldownCount),
                    MessageType.Warning);
            }

            int emptyTargetListCount = CountEmptyTargetLists();
            if (emptyTargetListCount > 0)
            {
                EditorGUILayout.HelpBox(
                    L(language,
                        "Add at least one frame target.",
                        "少なくとも 1 つのフレームターゲットを追加してください。",
                        "请至少添加一个相框目标。",
                        "프레임 대상을 하나 이상 추가하세요.") +
                    CountSuffix(emptyTargetListCount),
                    MessageType.Warning);
            }

            int nullTargetEntryCount = CountNullTargetEntries();
            if (nullTargetEntryCount > 0)
            {
                EditorGUILayout.HelpBox(
                    L(language,
                        "Some target slots are empty and will be skipped at runtime.",
                        "一部のターゲットスロットが空で、実行時にスキップされます。",
                        "部分目标槽位为空，运行时会被跳过。",
                        "일부 대상 슬롯이 비어 있어 런타임에 건너뜁니다。") +
                    " (" + nullTargetEntryCount + ")",
                    MessageType.Warning);
            }

            serializedObject.ApplyModifiedProperties();
        }

        private RemotePhotoInspectorLanguage ResolveLanguage(out bool missingServiceLink, out bool mixedServiceLanguages)
        {
            missingServiceLink = false;
            mixedServiceLanguages = false;

            if (RemotePhotoEditorLocalization.TryResolveProjectLanguage(out RemotePhotoInspectorLanguage projectLanguage, out bool projectLanguageConflict))
            {
                return projectLanguage;
            }

            if (projectLanguageConflict)
            {
                mixedServiceLanguages = true;
                return RemotePhotoInspectorLanguage.English;
            }

            RemotePhotoInspectorLanguage resolved = RemotePhotoInspectorLanguage.English;
            bool resolvedAny = false;

            int index = 0;
            while (index < targets.Length)
            {
                RemotePhotoGroup controller = targets[index] as RemotePhotoGroup;
                if (!RemotePhotoEditorLocalization.TryResolve(controller, out RemotePhotoInspectorLanguage candidate))
                {
                    missingServiceLink = true;
                    candidate = RemotePhotoInspectorLanguage.English;
                }

                if (!resolvedAny)
                {
                    resolved = candidate;
                    resolvedAny = true;
                }
                else if (resolved != candidate)
                {
                    mixedServiceLanguages = true;
                    return RemotePhotoInspectorLanguage.English;
                }

                index++;
            }

            return resolvedAny ? resolved : RemotePhotoInspectorLanguage.English;
        }

        private int CountEmptyTargetLists()
        {
            int count = 0;
            int index = 0;
            while (index < targets.Length)
            {
                RemotePhotoGroup controller = targets[index] as RemotePhotoGroup;
                if (controller == null || controller.targets == null || controller.targets.Length == 0)
                {
                    count++;
                }

                index++;
            }

            return count;
        }

        private int CountNullTargetEntries()
        {
            int count = 0;
            int controllerIndex = 0;
            while (controllerIndex < targets.Length)
            {
                RemotePhotoGroup controller = targets[controllerIndex] as RemotePhotoGroup;
                if (controller != null && controller.targets != null)
                {
                    int targetIndex = 0;
                    while (targetIndex < controller.targets.Length)
                    {
                        if (controller.targets[targetIndex] == null)
                        {
                            count++;
                        }

                        targetIndex++;
                    }
                }

                controllerIndex++;
            }

            return count;
        }

        private int CountNegativeCooldowns()
        {
            int count = 0;
            int index = 0;
            while (index < targets.Length)
            {
                RemotePhotoGroup controller = targets[index] as RemotePhotoGroup;
                if (controller != null && controller.triggerCooldownSeconds < 0f)
                {
                    count++;
                }

                index++;
            }

            return count;
        }

        private string CountSuffix(int count)
        {
            if (targets.Length <= 1)
            {
                return string.Empty;
            }

            return " (" + count + "/" + targets.Length + ")";
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
