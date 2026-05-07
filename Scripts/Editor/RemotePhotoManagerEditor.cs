using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using VRC.SDKBase;

namespace RemotePhotoSystem.Editor
{
    [CustomEditor(typeof(RemotePhotoManager))]
    public class RemotePhotoManagerEditor : UnityEditor.Editor
    {
        private const string PhotoFrameUnlitShaderName = "RemotePhotoSystem/Photo Frame Display Unlit";
        private const string PhotoFrameLitShaderName = "RemotePhotoSystem/Photo Frame Display Lit";

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            RemotePhotoManager service = (RemotePhotoManager)target;
            SerializedProperty languageProperty = serializedObject.FindProperty("inspectorLanguage");
            RemotePhotoInspectorLanguage language = (RemotePhotoInspectorLanguage)languageProperty.enumValueIndex;
            SerializedProperty loadingModeProperty = serializedObject.FindProperty("loadingMode");
            bool preloadMode = loadingModeProperty.enumValueIndex == (int)RemotePhotoLoadingMode.Preload;

            DrawCoreSettings(languageProperty, loadingModeProperty, language);
            DrawGallerySummary(service, loadingModeProperty, language);
            DrawLoadingModeSettings(language, preloadMode);
            DrawGroupManagement(service, language);
            DrawBakeTools(service, language);
            DrawValidation(service, language, preloadMode);

            serializedObject.ApplyModifiedProperties();
        }

        private void DrawCoreSettings(SerializedProperty languageProperty, SerializedProperty loadingModeProperty, RemotePhotoInspectorLanguage language)
        {
            EditorGUILayout.PropertyField(languageProperty,
                G(language,
                    "Language", "言語", "语言", "언어",
                    "Changes the editor text for this system.",
                    "このシステムのエディター表示文を切り替えます。",
                    "切换本系统的编辑器显示文本。",
                    "이 시스템의 에디터 표시 문구를 바꿉니다."));
            GUILayout.Space(24f);
            EditorGUILayout.PropertyField(serializedObject.FindProperty("galleryConfigFile"),
                G(language,
                    "Gallery Config JSON", "ギャラリー設定 JSON", "图库配置 JSON", "갤러리 설정 JSON",
                    "Drop the web tool JSON here.",
                    "Web ツールの JSON をここに指定します。",
                    "把网页工具导出的 JSON 放到这里。",
                    "웹 도구에서 내보낸 JSON을 여기에 넣습니다."));

            if (GUILayout.Button(new GUIContent(
                    L(language, "Import JSON Into Gallery", "JSON をギャラリーに取り込む", "把 JSON 导入图库", "JSON을 갤러리에 가져오기"),
                    L(language,
                        "Update the gallery from this JSON.",
                        "この JSON からギャラリーを更新します。",
                        "用这个 JSON 更新图库。",
                        "이 JSON으로 갤러리를 업데이트합니다."))))
            {
                BakeGalleryConfig((RemotePhotoManager)target);
            }

            GUILayout.Space(12f);
        }

        private void DrawGallerySummary(RemotePhotoManager service, SerializedProperty loadingModeProperty, RemotePhotoInspectorLanguage language)
        {
            int landscapeCount = service.bakedLandscapeUrls == null ? 0 : service.bakedLandscapeUrls.Length;
            int portraitCount = service.bakedPortraitUrls == null ? 0 : service.bakedPortraitUrls.Length;
            int totalCount = landscapeCount + portraitCount;

            EditorGUILayout.HelpBox(
                L(language, "Total photos", "写真総数", "图片总数", "전체 사진 수") + ": " + totalCount + "\n" +
                L(language, "Landscape photos", "横向き写真数", "横图数量", "가로 사진 수") + ": " + landscapeCount + "\n" +
                L(language, "Portrait photos", "縦向き写真数", "竖图数量", "세로 사진 수") + ": " + portraitCount,
                MessageType.Info);

            GUILayout.Space(24f);

            EditorGUILayout.PropertyField(serializedObject.FindProperty("configuredPlayMode"),
                G(language,
                    "Play Mode", "再生モード", "播放模式", "재생 모드",
                    "Sets how Groups pick photos.",
                    "Groups が写真を選ぶ方式を設定します。",
                    "设置 Groups 取图方式。",
                    "Groups가 사진을 고르는 방식을 설정합니다."));
            EditorGUILayout.PropertyField(loadingModeProperty,
                G(language,
                    "Loading Mode", "読み込みモード", "加载模式", "로딩 모드",
                    "Choose cached loading or direct loading.",
                    "キャッシュ読み込みか直接読み込みを選びます。",
                    "选择缓存加载或直接加载。",
                    "캐시 로딩 또는 직접 로딩을 선택합니다."));
        }

        private void DrawLoadingModeSettings(RemotePhotoInspectorLanguage language, bool preloadMode)
        {
            EditorGUILayout.PropertyField(serializedObject.FindProperty("loadOnceOnStart"),
                G(language,
                    "Load Once On Start", "開始時に一度読み込む", "开始时加载一次", "시작 시 한 번 로드",
                    "Shows one page automatically after startup.",
                    "起動後に 1 ページを自動表示します。",
                    "启动后自动显示一页。",
                    "시작 후 한 페이지를 자동으로 표시합니다."));

            if (serializedObject.FindProperty("loadOnceOnStart").boolValue)
            {
                EditorGUILayout.PropertyField(serializedObject.FindProperty("loadOnceDelaySeconds"),
                    G(language,
                        "Load Once Delay Seconds", "一度読み込み遅延秒数", "首次加载延迟秒数", "1회 로드 지연 초",
                        "Wait time before the startup page loads.",
                        "起動時ページを読み込むまでの待ち時間です。",
                        "启动页加载前的等待时间。",
                        "시작 페이지를 로드하기 전 대기 시간입니다."));
            }

            EditorGUILayout.PropertyField(serializedObject.FindProperty("retryAttempts"),
                G(language,
                    "Retry Attempts", "再試行回数", "重试次数", "재시도 횟수",
                    "How many extra tries a failed URL gets.",
                    "失敗した URL を追加で試す回数です。",
                    "失败 URL 的额外尝试次数。",
                    "실패한 URL을 추가로 시도할 횟수입니다."));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("retryDelaySeconds"),
                G(language,
                    "Retry Delay Seconds", "再試行間隔秒数", "重试间隔秒数", "재시도 간격 초",
                    "Wait time between retries for one URL.",
                    "同じ URL の再試行間隔です。",
                    "同一个 URL 两次重试之间的等待时间。",
                    "같은 URL을 다시 시도하는 간격입니다."));

            if (preloadMode)
            {
                GUILayout.Space(12f);
                EditorGUILayout.PropertyField(serializedObject.FindProperty("preloadLandscapeCacheSize"),
                    G(language,
                        "Landscape Cache Size", "横向きキャッシュ数", "横图缓存数量", "가로 캐시 수",
                        "Target number of cached Landscape photos.",
                        "キャッシュする Landscape 写真の目標数です。",
                        "Landscape 图片缓存目标数量。",
                        "캐시할 Landscape 사진 목표 수입니다."));
                EditorGUILayout.PropertyField(serializedObject.FindProperty("preloadPortraitCacheSize"),
                    G(language,
                        "Portrait Cache Size", "縦向きキャッシュ数", "竖图缓存数量", "세로 캐시 수",
                        "Target number of cached Portrait photos.",
                        "キャッシュする Portrait 写真の目標数です。",
                        "Portrait 图片缓存目标数量。",
                        "캐시할 Portrait 사진 목표 수입니다."));
            }

            GUILayout.Space(16f);
            EditorGUILayout.PropertyField(serializedObject.FindProperty("debugLogs"),
                G(language,
                    "Debug Logs", "デバッグログ", "调试日志", "디버그 로그",
                    "Print loading and selection logs.",
                    "読み込みと選択のログを出力します。",
                    "输出加载和选图日志。",
                    "로딩과 선택 로그를 출력합니다."));

        }

        private void DrawGroupManagement(RemotePhotoManager service, RemotePhotoInspectorLanguage language)
        {
            GUILayout.Space(12f);
            EditorGUILayout.PropertyField(serializedObject.FindProperty("managedGroups"),
                G(language,
                    "Managed Groups", "管理グループ", "受管组", "관리 그룹",
                    "Only listed Groups are controlled by this Manager.",
                    "ここにある Groups だけをこの Manager が制御します。",
                    "只有列在这里的 Groups 会被此 Manager 控制。",
                    "여기에 있는 Groups만 이 Manager가 제어합니다."),
                true);

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button(new GUIContent(
                    L(language, "Add Group", "グループを追加", "添加组", "그룹 추가"),
                    L(language,
                        "Create one child Group and add it to Managed Groups.",
                        "子 Group を 1 つ作成して追加します。",
                        "创建并添加一个子 Group。",
                        "자식 Group 하나를 만들고 추가합니다."))))
            {
                AddManagedGroup(service);
            }

            if (GUILayout.Button(new GUIContent(
                    L(language, "Sync Groups From Children", "子オブジェクトから同期", "从子对象同步组", "자식에서 그룹 동기화"),
                    L(language,
                        "Rebuild Managed Groups from child Group objects.",
                        "子 Group から一覧を作り直します。",
                        "根据子 Group 重建列表。",
                        "자식 Group으로 목록을 다시 만듭니다."))))
            {
                SyncManagedGroupsFromChildren(service);
            }

            if (GUILayout.Button(new GUIContent(
                    L(language, "Remove Missing Groups", "欠損グループ参照を除去", "移除丢失的组引用", "누락된 그룹 제거"),
                    L(language,
                        "Remove empty references from Managed Groups.",
                        "Managed Groups の空欄を削除します。",
                        "删除 Managed Groups 中的空位。",
                        "Managed Groups의 빈 항목을 제거합니다."))))
            {
                RemoveMissingManagedGroups(service);
            }

            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space();
        }

        private void DrawBakeTools(RemotePhotoManager service, RemotePhotoInspectorLanguage language)
        {
            if (service.galleryConfigFile == null)
            {
                EditorGUILayout.HelpBox(
                    L(language,
                        "Assign a gallery JSON first.",
                        "先にギャラリー JSON を指定してください。",
                        "请先指定图库 JSON。",
                        "먼저 갤러리 JSON을 지정하세요."),
                    MessageType.Warning);
            }

        }

        private void DrawValidation(RemotePhotoManager service, RemotePhotoInspectorLanguage language, bool preloadMode)
        {
            int landscapeCount = service.bakedLandscapeUrls == null ? 0 : service.bakedLandscapeUrls.Length;
            int portraitCount = service.bakedPortraitUrls == null ? 0 : service.bakedPortraitUrls.Length;
            if (landscapeCount == 0 && portraitCount == 0)
            {
                EditorGUILayout.HelpBox(
                    L(language,
                        "Bake at least one valid gallery entry.",
                        "少なくとも 1 件の有効なギャラリー項目を Bake してください。",
                        "请至少 Bake 一条有效的图库条目。",
                        "유효한 갤러리 항목을 하나 이상 Bake 하세요."),
                    MessageType.Warning);
            }

            if (service.retryDelaySeconds < 0f ||
                service.retryAttempts < 0)
            {
                EditorGUILayout.HelpBox(
                    L(language,
                        "Image retry settings should be 0 or greater.",
                        "再試行設定は 0 以上にしてください。",
                        "重试设置必须大于等于 0。",
                        "재시도 설정은 0 이상이어야 합니다."),
                    MessageType.Warning);
            }

            if (preloadMode &&
                (service.preloadLandscapeCacheSize < 0 ||
                 service.preloadPortraitCacheSize < 0))
            {
                EditorGUILayout.HelpBox(
                    L(language,
                        "Preload numeric settings should be 0 or greater.",
                        "Preload 関連の数値設定は 0 以上にしてください。",
                        "Preload 相关数值设置必须大于等于 0。",
                        "Preload 관련 수치 설정은 0 이상이어야 합니다."),
                    MessageType.Warning);
            }

            if (service.loadOnceOnStart && service.loadOnceDelaySeconds < 0f)
            {
                EditorGUILayout.HelpBox(
                    L(language,
                        "Load Once Delay Seconds should be 0 or greater.",
                        "Load Once Delay Seconds は 0 以上にしてください。",
                        "Load Once Delay Seconds 必须大于等于 0。",
                        "Load Once Delay Seconds는 0 이상이어야 합니다."),
                    MessageType.Warning);
            }

            if (CountMissingManagedServiceBackrefs(service) > 0)
            {
                EditorGUILayout.HelpBox(
                    L(language,
                        "Some managed groups are not linked back to this Remote Photo Manager. Use Sync Groups From Children to repair them.",
                        "一部の管理グループがこの Remote Photo Manager に逆リンクしていません。Sync Groups From Children で修復してください。",
                        "部分受管组没有正确回写到此 Remote Photo Manager。请使用 Sync Groups From Children 修复。",
                        "일부 관리 그룹이 이 Remote Photo Manager를 다시 참조하지 않고 있습니다. Sync Groups From Children으로 복구하세요."),
                    MessageType.Warning);
            }

            int incompatibleShaderCount = CountConnectedFramesWithoutRemotePhotoShader(service);
            if (incompatibleShaderCount > 0)
            {
                EditorGUILayout.HelpBox(
                    L(
                        language,
                        "RemotePhotoSystem requires every connected frame material to use a project photo frame shader: RemotePhotoSystem/Photo Frame Display Unlit or RemotePhotoSystem/Photo Frame Display Lit.",
                        "RemotePhotoSystem では、接続されたすべてのフレームマテリアルがプロジェクト専用シェーダー RemotePhotoSystem/Photo Frame Display Unlit または RemotePhotoSystem/Photo Frame Display Lit を使う必要があります。",
                        "RemotePhotoSystem 要求所有连接的相框材质都使用项目专用着色器：RemotePhotoSystem/Photo Frame Display Unlit 或 RemotePhotoSystem/Photo Frame Display Lit。",
                        "RemotePhotoSystem은 연결된 모든 프레임 머티리얼이 프로젝트 전용 셰이더 RemotePhotoSystem/Photo Frame Display Unlit 또는 RemotePhotoSystem/Photo Frame Display Lit 를 사용해야 합니다。") +
                    "\n" +
                    L(language, "Invalid connected frames", "不正な接続フレーム", "不符合要求的已连接相框", "잘못 연결된 프레임") +
                    ": " + incompatibleShaderCount + BuildInvalidShaderFramePreview(service),
                    MessageType.Warning);

                EditorGUILayout.BeginHorizontal();
                if (GUILayout.Button(new GUIContent(
                        L(language, "Apply Unlit Shader To Connected Frames", "接続フレームへ Unlit Shader を適用", "为已连接相框应用 Unlit Shader", "연결된 프레임에 Unlit Shader 적용"),
                        L(language,
                            "Apply the project Unlit shader to connected Frames.",
                            "接続済み Frames に Unlit Shader を適用します。",
                            "给已连接 Frames 套用 Unlit Shader。",
                            "연결된 Frames에 Unlit Shader를 적용합니다."))))
                {
                    ApplyShaderToConnectedFrames(service, PhotoFrameUnlitShaderName);
                }

                if (GUILayout.Button(new GUIContent(
                        L(language, "Apply Lit Shader To Connected Frames", "接続フレームへ Lit Shader を適用", "为已连接相框应用 Lit Shader", "연결된 프레임에 Lit Shader 적용"),
                        L(language,
                            "Apply the project Lit shader to connected Frames.",
                            "接続済み Frames に Lit Shader を適用します。",
                            "给已连接 Frames 套用 Lit Shader。",
                            "연결된 Frames에 Lit Shader를 적용합니다."))))
                {
                    ApplyShaderToConnectedFrames(service, PhotoFrameLitShaderName);
                }

                EditorGUILayout.EndHorizontal();
            }
        }

        private void AddManagedGroup(RemotePhotoManager service)
        {
            GameObject groupObject = new GameObject(GetNextGroupName(service));
            Undo.RegisterCreatedObjectUndo(groupObject, "Create Remote Photo Group");
            Undo.SetTransformParent(groupObject.transform, service.transform, "Parent Remote Photo Group");
            groupObject.transform.localPosition = Vector3.zero;
            groupObject.transform.localRotation = Quaternion.identity;
            groupObject.transform.localScale = Vector3.one;

            RemotePhotoGroup group = Undo.AddComponent<RemotePhotoGroup>(groupObject);
            Undo.RecordObject(group, "Assign Remote Photo Manager");
            group.galleryService = service;
            EditorUtility.SetDirty(group);

            AppendManagedGroup(service, group);
            MarkSceneDirty(service.gameObject.scene);
            Selection.activeGameObject = service.gameObject;
        }

        private void AppendManagedGroup(RemotePhotoManager service, RemotePhotoGroup group)
        {
            int oldLength = service.managedGroups == null ? 0 : service.managedGroups.Length;
            RemotePhotoGroup[] groups = new RemotePhotoGroup[oldLength + 1];

            int index = 0;
            while (index < oldLength)
            {
                groups[index] = service.managedGroups[index];
                index++;
            }

            groups[oldLength] = group;

            Undo.RecordObject(service, "Add Remote Photo Managed Group");
            service.managedGroups = groups;
            EditorUtility.SetDirty(service);
            serializedObject.Update();
        }

        private string GetNextGroupName(RemotePhotoManager service)
        {
            int suffix = 1;
            string baseName = "FrameGroup";
            string candidate = baseName;

            while (HasChildWithName(service.transform, candidate))
            {
                suffix++;
                candidate = baseName + " " + suffix;
            }

            return candidate;
        }

        private bool HasChildWithName(Transform parent, string candidate)
        {
            int index = 0;
            while (index < parent.childCount)
            {
                if (parent.GetChild(index).name == candidate)
                {
                    return true;
                }

                index++;
            }

            return false;
        }

        private void SyncManagedGroupsFromChildren(RemotePhotoManager service)
        {
            SyncManagedGroupsFromChildren(service, true);
        }

        private void SyncManagedGroupsFromChildren(RemotePhotoManager service, bool showLog)
        {
            RemotePhotoGroup[] groups = service.GetComponentsInChildren<RemotePhotoGroup>(true);
            Undo.RecordObject(service, "Sync Remote Photo Managed Groups");
            service.managedGroups = groups == null ? new RemotePhotoGroup[0] : groups;
            EditorUtility.SetDirty(service);

            int changedBackrefs = 0;
            int index = 0;
            while (groups != null && index < groups.Length)
            {
                RemotePhotoGroup group = groups[index];
                if (group != null && group.galleryService != service)
                {
                    Undo.RecordObject(group, "Assign Remote Photo Manager");
                    group.galleryService = service;
                    EditorUtility.SetDirty(group);
                    changedBackrefs++;
                }

                index++;
            }

            MarkSceneDirty(service.gameObject.scene);

            if (showLog)
            {
                Debug.Log("[RemotePhotoSystem] Synced " + (groups == null ? 0 : groups.Length) + " group(s) from child objects. Updated back-references: " + changedBackrefs, service);
            }

            serializedObject.Update();
        }

        private void RemoveMissingManagedGroups(RemotePhotoManager service)
        {
            List<RemotePhotoGroup> groups = new List<RemotePhotoGroup>();
            int removed = 0;
            int index = 0;
            while (service.managedGroups != null && index < service.managedGroups.Length)
            {
                RemotePhotoGroup group = service.managedGroups[index];
                if (group == null)
                {
                    removed++;
                }
                else
                {
                    groups.Add(group);
                }

                index++;
            }

            Undo.RecordObject(service, "Remove Missing Remote Photo Groups");
            service.managedGroups = groups.ToArray();
            EditorUtility.SetDirty(service);
            MarkSceneDirty(service.gameObject.scene);
            serializedObject.Update();
            Debug.Log("[RemotePhotoSystem] Removed " + removed + " missing group reference(s).", service);
        }

        private int CountMissingManagedServiceBackrefs(RemotePhotoManager service)
        {
            int count = 0;
            int index = 0;
            while (service.managedGroups != null && index < service.managedGroups.Length)
            {
                RemotePhotoGroup group = service.managedGroups[index];
                if (group != null && group.galleryService != service)
                {
                    count++;
                }

                index++;
            }

            return count;
        }

        private void ApplyShaderToConnectedFrames(RemotePhotoManager service, string shaderName)
        {
            Shader shader = Shader.Find(shaderName);
            if (shader == null)
            {
                Debug.LogError("[RemotePhotoSystem] Could not find shader: " + shaderName, service);
                return;
            }

            int changedCount = 0;
            int groupIndex = 0;
            while (service.managedGroups != null && groupIndex < service.managedGroups.Length)
            {
                RemotePhotoGroup group = service.managedGroups[groupIndex];
                if (group == null || group.targets == null)
                {
                    groupIndex++;
                    continue;
                }

                int targetIndex = 0;
                while (targetIndex < group.targets.Length)
                {
                    RemotePhotoFrame frame = group.targets[targetIndex];
                    Material material = GetFrameSharedMaterial(frame);
                    if (frame != null && material != null && !IsRemotePhotoFrameShader(material.shader))
                    {
                        Undo.RecordObject(material, "Apply Remote Photo Frame Shader");
                        material.shader = shader;
                        material.SetColor("_RemotePhotoBackgroundColor", frame.backgroundColor);
                        EditorUtility.SetDirty(material);

                        if (frame.texturePropertyName != "_MainTex")
                        {
                            Undo.RecordObject(frame, "Set Remote Photo Texture Property");
                            frame.texturePropertyName = "_MainTex";
                            EditorUtility.SetDirty(frame);
                        }

                        changedCount++;
                    }

                    targetIndex++;
                }

                groupIndex++;
            }

            Debug.Log("[RemotePhotoSystem] Applied shader to " + changedCount + " connected frame material(s).", service);
        }

        private int CountConnectedFramesWithoutRemotePhotoShader(RemotePhotoManager service)
        {
            int count = 0;
            int groupIndex = 0;
            while (service.managedGroups != null && groupIndex < service.managedGroups.Length)
            {
                RemotePhotoGroup group = service.managedGroups[groupIndex];
                if (group == null || group.targets == null)
                {
                    groupIndex++;
                    continue;
                }

                int targetIndex = 0;
                while (targetIndex < group.targets.Length)
                {
                    RemotePhotoFrame frame = group.targets[targetIndex];
                    if (frame != null && !FrameUsesRemotePhotoShader(frame))
                    {
                        count++;
                    }

                    targetIndex++;
                }

                groupIndex++;
            }

            return count;
        }

        private string BuildInvalidShaderFramePreview(RemotePhotoManager service)
        {
            string preview = string.Empty;
            int listed = 0;
            int groupIndex = 0;
            while (service.managedGroups != null && groupIndex < service.managedGroups.Length && listed < 5)
            {
                RemotePhotoGroup group = service.managedGroups[groupIndex];
                if (group == null || group.targets == null)
                {
                    groupIndex++;
                    continue;
                }

                int targetIndex = 0;
                while (targetIndex < group.targets.Length && listed < 5)
                {
                    RemotePhotoFrame frame = group.targets[targetIndex];
                    if (frame != null && !FrameUsesRemotePhotoShader(frame))
                    {
                        preview += "\n- " + frame.gameObject.name;
                        listed++;
                    }

                    targetIndex++;
                }

                groupIndex++;
            }

            return preview;
        }

        private bool FrameUsesRemotePhotoShader(RemotePhotoFrame frame)
        {
            Material material = GetFrameSharedMaterial(frame);
            return material != null && IsRemotePhotoFrameShader(material.shader);
        }

        private Material GetFrameSharedMaterial(RemotePhotoFrame frame)
        {
            if (frame == null)
            {
                return null;
            }

            MeshRenderer meshRenderer = frame.GetComponent<MeshRenderer>();
            if (meshRenderer == null)
            {
                return null;
            }

            Material[] materials = meshRenderer.sharedMaterials;
            if (materials == null || frame.materialSlot < 0 || frame.materialSlot >= materials.Length)
            {
                return null;
            }

            return materials[frame.materialSlot];
        }

        private bool IsRemotePhotoFrameShader(Shader shader)
        {
            if (shader == null)
            {
                return false;
            }

            return shader.name == PhotoFrameUnlitShaderName || shader.name == PhotoFrameLitShaderName;
        }

        private void BakeGalleryConfig(RemotePhotoManager service)
        {
            if (service.galleryConfigFile == null)
            {
                Debug.LogWarning("[RemotePhotoSystem] Assign a gallery config JSON TextAsset first.", service);
                return;
            }

            if (!RemotePhotoManifestJsonUtility.TryFromJson(service.galleryConfigFile.text, out RemotePhotoManifestDocument document))
            {
                Debug.LogError("[RemotePhotoSystem] Failed to parse gallery config JSON.", service);
                return;
            }

            List<VRCUrl> landscapeUrls = new List<VRCUrl>();
            List<VRCUrl> portraitUrls = new List<VRCUrl>();
            HashSet<string> seenUrls = new HashSet<string>();
            int invalidCount = 0;
            int duplicateCount = 0;

            foreach (RemotePhotoManifestEntry entry in document.entries)
            {
                if (entry == null || !RemotePhotoUrlUtility.IsValidUrlString(entry.url))
                {
                    invalidCount++;
                    continue;
                }

                string normalizedUrl = entry.url.Trim();
                if (!seenUrls.Add(normalizedUrl))
                {
                    duplicateCount++;
                    continue;
                }

                VRCUrl url = new VRCUrl(normalizedUrl);
                if (entry.orientation == RemotePhotoOrientation.Portrait)
                {
                    portraitUrls.Add(url);
                }
                else
                {
                    landscapeUrls.Add(url);
                }
            }

            Undo.RecordObject(service, "Bake Remote Photo Gallery Config");
            service.bakedLandscapeUrls = landscapeUrls.ToArray();
            service.bakedPortraitUrls = portraitUrls.ToArray();
            EditorUtility.SetDirty(service);

            Debug.Log(
                "[RemotePhotoSystem] Bake complete. " +
                "Landscape: " + service.bakedLandscapeUrls.Length + ", " +
                "Portrait: " + service.bakedPortraitUrls.Length + ", " +
                "Invalid skipped: " + invalidCount + ", " +
                "Duplicate skipped: " + duplicateCount,
                service);
        }

        private void MarkSceneDirty(Scene scene)
        {
            if (scene.IsValid())
            {
                EditorSceneManager.MarkSceneDirty(scene);
            }
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
