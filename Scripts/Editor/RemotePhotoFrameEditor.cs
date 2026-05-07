using UnityEditor;
using UnityEditor.IMGUI.Controls;
using UnityEngine;

namespace RemotePhotoSystem.Editor
{
    [CustomEditor(typeof(RemotePhotoFrame))]
    [CanEditMultipleObjects]
    public class RemotePhotoFrameEditor : UnityEditor.Editor
    {
        private static int s_editingReferenceBoxInstanceId;

        private readonly BoxBoundsHandle _referenceBoxHandle = new BoxBoundsHandle();

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            RemotePhotoFrame display = (RemotePhotoFrame)target;
            bool multiObjectEditing = targets.Length > 1;
            RemotePhotoInspectorLanguage language = ResolveLanguage(out bool missingServiceLink, out bool hasConflict, out bool mixedServiceLanguages);

            if (hasConflict)
            {
                EditorGUILayout.HelpBox(
                    L(language,
                        "This frame is referenced by groups owned by different Remote Photo Managers. Inspector language falls back to English until the conflict is fixed.",
                        "このフレームは異なる Remote Photo Manager に属するグループから参照されています。競合が解消されるまで Inspector の言語は英語にフォールバックします。",
                        "此相框被不同 Remote Photo Manager 旗下的组同时引用。在修复冲突前，Inspector 语言会回退为英文。",
                        "이 프레임은 서로 다른 Remote Photo Manager에 속한 그룹에서 참조되고 있습니다. 충돌이 해결될 때까지 Inspector 언어는 영어로 대체됩니다."),
                    MessageType.Warning);
            }
            else if (missingServiceLink)
            {
                EditorGUILayout.HelpBox(
                    L(language,
                        "This frame is not referenced by any managed group. Inspector language falls back to English until the link is restored.",
                        "このフレームはどの管理グループからも参照されていません。リンクされるまで Inspector の言語は英語にフォールバックします。",
                        "此相框尚未被任何受管组引用。在恢复连接前，Inspector 语言会回退为英文。",
                        "이 프레임은 어떤 관리 그룹에서도 참조되지 않습니다. 연결이 복구될 때까지 Inspector 언어는 영어로 대체됩니다."),
                    MessageType.Warning);
            }

            if (mixedServiceLanguages)
            {
                EditorGUILayout.HelpBox(
                    L(language,
                        "Multiple selected frames resolve to different Remote Photo Manager languages. This inspector is temporarily shown in English.",
                        "選択中の複数フレームは異なる Remote Photo Manager の言語に解決されます。この Inspector は一時的に英語で表示されます。",
                        "当前多选的相框归属于不同语言设置的 Remote Photo Manager，因此此 Inspector 会暂时使用英文显示。",
                        "여러 선택된 프레임이 서로 다른 Remote Photo Manager 언어로 해석됩니다. 이 Inspector는 임시로 영어로 표시됩니다."),
                    MessageType.Warning);
            }

            EditorGUILayout.PropertyField(serializedObject.FindProperty("orientation"),
                G(language,
                    "Orientation", "向き", "图片方向池", "방향",
                    "Makes this Frame use Landscape or Portrait photos.",
                    "この Frame が Landscape / Portrait のどちらを使うか決めます。",
                    "决定此 Frame 使用 Landscape 还是 Portrait 图片。",
                    "이 Frame이 Landscape 또는 Portrait 사진 중 무엇을 사용할지 정합니다."));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("materialSlot"),
                G(language,
                    "Material Slot", "マテリアルスロット", "材质槽位", "머티리얼 슬롯",
                    "Selects which material gets the photo.",
                    "写真を書き込むマテリアルを選びます。",
                    "选择要写入图片的材质。",
                    "사진을 넣을 머티리얼을 선택합니다."));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("texturePropertyName"),
                G(language,
                    "Texture Property", "テクスチャプロパティ", "贴图属性名", "텍스처 프로퍼티",
                    "Target texture slot in the Shader.",
                    "Shader 内の書き込み先テクスチャスロットです。",
                    "Shader 中要写入的贴图槽。",
                    "Shader에서 텍스처를 넣을 슬롯입니다."));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("defaultTexture"),
                G(language,
                    "Default Texture", "デフォルトテクスチャ", "默认贴图", "기본 텍스처",
                    "Shown until a remote photo replaces it.",
                    "リモート写真に置き換わるまで表示します。",
                    "在远程图片替换前显示。",
                    "원격 사진으로 바뀌기 전까지 표시됩니다."));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("fallbackTexture"),
                G(language,
                    "Fallback Texture", "フォールバックテクスチャ", "回退贴图", "폴백 텍스처",
                    "Shown when the current photo cannot load.",
                    "現在の写真を読み込めない時に表示します。",
                    "当前图片无法加载时显示。",
                    "현재 사진을 불러올 수 없을 때 표시됩니다."));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("backgroundColor"),
                G(language,
                    "Background Color", "背景色", "背景颜色", "배경색",
                    "Used where no photo pixels are drawn.",
                    "写真ピクセルが描かれない部分に使います。",
                    "用于没有图片像素覆盖的位置。",
                    "사진 픽셀이 그려지지 않는 곳에 사용됩니다."));
            RemotePhotoProjectionMode projectionMode =
                (RemotePhotoProjectionMode)serializedObject.FindProperty("projectionMode").enumValueIndex;
            RemotePhotoFitMode photoFitMode =
                (RemotePhotoFitMode)serializedObject.FindProperty("photoFitMode").enumValueIndex;

            EditorGUILayout.PropertyField(serializedObject.FindProperty("photoFitMode"),
                G(language,
                    "Photo Fit Mode", "写真フィットモード", "图片适配模式", "사진 맞춤 모드",
                    "Controls scaling inside the Frame.",
                    "Frame 内での写真スケールを制御します。",
                    "控制图片在 Frame 内的缩放方式。",
                    "Frame 안에서 사진 스케일 방식을 제어합니다."));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("projectionMode"),
                G(language,
                    "Projection Mode", "投影モード", "投射模式", "투영 모드",
                    "Choose mesh UVs or Box front/back projection.",
                    "メッシュ UV か Box 前後面投影を選びます。",
                    "选择网格 UV 或 Box 正反面投射。",
                    "메쉬 UV 또는 Box 앞/뒤 면 투영을 선택합니다."));

            if (projectionMode == RemotePhotoProjectionMode.Box)
            {
                EditorGUILayout.PropertyField(serializedObject.FindProperty("boxProjectionHorizontalFlip"),
                    G(language,
                        "Horizontal Flip", "水平反転", "水平翻转", "수평 반전",
                        "Fix mirrored Box photos.",
                        "Box 写真の左右反転を直します。",
                        "修正 Box 图片镜像问题。",
                        "Box 사진의 좌우 반전을 고칩니다."));
            }

            EditorGUILayout.Slider(serializedObject.FindProperty("photoRotationDegrees"), 0f, 360f,
                G(language,
                    "Photo Rotation Offset", "写真回転オフセット", "图片旋转补偿", "사진 회전 보정",
                    "Rotates the final displayed photo.",
                    "最終表示される写真を回転します。",
                    "旋转最终显示的图片。",
                    "최종 표시되는 사진을 회전합니다."));

            EditorGUILayout.PropertyField(serializedObject.FindProperty("aspectMode"),
                G(language,
                    "Aspect Mode", "アスペクトモード", "画幅比例模式", "비율 모드",
                    "Sets the Frame ratio used by fit modes.",
                    "Fit Mode が使う Frame 比率を設定します。",
                    "设置适配模式使用的 Frame 比例。",
                    "Fit Mode가 사용할 Frame 비율을 설정합니다."));

            RemotePhotoAspectMode aspectMode =
                (RemotePhotoAspectMode)serializedObject.FindProperty("aspectMode").enumValueIndex;
            if (aspectMode == RemotePhotoAspectMode.ReferenceBox)
            {
                DrawReferenceBoxInspector(language, display);
                EditorGUILayout.PropertyField(serializedObject.FindProperty("axisMode"),
                    G(language,
                        "Reference Box Axis Mode", "Reference Box 軸モード", "Reference Box 轴向模式", "Reference Box 축 모드",
                        "Choose how the Reference Box becomes a 2D ratio.",
                        "Reference Box を 2D 比率にする方法を選びます。",
                        "选择 Reference Box 如何转换为 2D 比例。",
                        "Reference Box를 2D 비율로 바꾸는 방식을 선택합니다."));

                RemotePhotoAxisMode axisMode =
                    (RemotePhotoAxisMode)serializedObject.FindProperty("axisMode").enumValueIndex;
                if (axisMode == RemotePhotoAxisMode.ManualAxes)
                {
                    EditorGUILayout.PropertyField(serializedObject.FindProperty("frameWidthAxis"),
                        G(language,
                            "Width Axis", "幅軸", "宽度轴", "너비 축",
                            "Axis used for the ratio width.",
                            "比率の幅に使う軸です。",
                            "用于比例宽度的轴。",
                            "비율의 너비에 사용할 축입니다."));
                    EditorGUILayout.PropertyField(serializedObject.FindProperty("frameHeightAxis"),
                        G(language,
                            "Height Axis", "高さ軸", "高度轴", "높이 축",
                            "Axis used for the ratio height.",
                            "比率の高さに使う軸です。",
                            "用于比例高度的轴。",
                            "비율의 높이에 사용할 축입니다."));
                }
            }

            if (aspectMode == RemotePhotoAspectMode.Manual)
            {
                EditorGUILayout.PropertyField(serializedObject.FindProperty("manualAspectRatio"),
                G(language,
                    "Manual Aspect Ratio", "手動アスペクト比", "手动宽高比", "수동 비율",
                    "Use this exact width-to-height ratio.",
                    "この幅:高さ比率をそのまま使います。",
                    "直接使用这个宽高比。",
                    "이 너비:높이 비율을 그대로 사용합니다."));
            }

            if (multiObjectEditing)
            {
                EditorGUILayout.LabelField(
                    L(language, "Multi-object editing active", "複数オブジェクト編集中", "多物体编辑已启用", "다중 오브젝트 편집 활성화") +
                    " (" + targets.Length + ")",
                    EditorStyles.miniLabel);
            }

            int missingMeshRendererCount = CountMissingMeshRenderers();
            if (missingMeshRendererCount > 0)
            {
                EditorGUILayout.HelpBox(
                    L(language,
                        "This component must be placed on a GameObject with a MeshRenderer.",
                        "このコンポーネントは MeshRenderer を持つ GameObject に配置する必要があります。",
                        "此组件必须挂在带有 MeshRenderer 的物体上。",
                        "이 컴포넌트는 MeshRenderer가 있는 GameObject에 배치되어야 합니다.") +
                    " (" + missingMeshRendererCount + "/" + targets.Length + ")",
                    MessageType.Warning);
            }

            if (aspectMode == RemotePhotoAspectMode.Manual && CountInvalidManualAspectRatios() > 0)
            {
                EditorGUILayout.HelpBox(
                    L(language,
                        "Manual Aspect Ratio should be greater than 0.",
                        "Manual Aspect Ratio は 0 より大きい値にしてください。",
                        "Manual Aspect Ratio 必须大于 0。",
                        "Manual Aspect Ratio는 0보다 커야 합니다."),
                    MessageType.Warning);
            }

            if (aspectMode == RemotePhotoAspectMode.ReferenceBox && CountInvalidReferenceBoxSizes() > 0)
            {
                EditorGUILayout.HelpBox(
                    L(language,
                        "Reference Box size should be greater than 0 on all three axes.",
                        "Reference Box のサイズは 3 軸すべてで 0 より大きい値にしてください。",
                        "Reference Box 尺寸在三个轴向上都必须大于 0。",
                        "Reference Box 크기는 세 축 모두에서 0보다 커야 합니다."),
                    MessageType.Warning);
            }

            if (aspectMode != RemotePhotoAspectMode.Manual && CountInvalidAutomaticAspectRatios() > 0)
            {
                EditorGUILayout.HelpBox(
                    L(language,
                        "Automatic aspect calculation failed. Please switch to another aspect mode or provide valid geometry/reference box dimensions.",
                        "自動アスペクト計算に失敗しました。別のアスペクトモードに切り替えるか、有効なジオメトリ/Reference Box 寸法を指定してください。",
                        "自动比例计算失败。请切换到其他比例模式，或提供有效的几何体/参考框尺寸。",
                        "자동 비율 계산에 실패했습니다. 다른 비율 모드로 전환하거나 유효한 지오메트리/Reference Box 크기를 제공하세요."),
                    MessageType.Warning);
            }

            if (aspectMode == RemotePhotoAspectMode.ReferenceBox &&
                CountDuplicateManualAxes() > 0)
            {
                EditorGUILayout.HelpBox(
                    L(language,
                        "Width Axis and Height Axis should be different.",
                        "Width Axis と Height Axis は異なる必要があります。",
                        "Width Axis 和 Height Axis 不能相同。",
                        "Width Axis와 Height Axis는 서로 달라야 합니다."),
                    MessageType.Warning);
            }

            DrawResolvedAspectRatio(language, display, multiObjectEditing);

            serializedObject.ApplyModifiedProperties();
        }

        private RemotePhotoInspectorLanguage ResolveLanguage(out bool missingServiceLink, out bool hasConflict, out bool mixedServiceLanguages)
        {
            missingServiceLink = false;
            hasConflict = false;
            mixedServiceLanguages = false;

            RemotePhotoInspectorLanguage resolved = RemotePhotoInspectorLanguage.English;
            bool resolvedAny = false;

            int index = 0;
            while (index < targets.Length)
            {
                RemotePhotoFrame display = targets[index] as RemotePhotoFrame;
                bool localConflict;
                bool found = RemotePhotoEditorLocalization.TryResolve(display, out RemotePhotoInspectorLanguage candidate, out localConflict);
                if (localConflict)
                {
                    hasConflict = true;
                    return RemotePhotoInspectorLanguage.English;
                }

                if (!found)
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

        private void DrawReferenceBoxInspector(RemotePhotoInspectorLanguage language, RemotePhotoFrame display)
        {
            EditorGUILayout.PropertyField(serializedObject.FindProperty("referenceBoxCenter"),
                G(language,
                    "Reference Box Center", "Reference Box 中心", "参考框中心", "Reference Box 중심",
                    "Moves the Reference Box without resizing it.",
                    "サイズを変えずに Reference Box を移動します。",
                    "移动 Reference Box，不改变尺寸。",
                    "크기는 유지하고 Reference Box를 이동합니다."));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("referenceBoxSize"),
                G(language,
                    "Reference Box Size", "Reference Box サイズ", "参考框尺寸", "Reference Box 크기",
                    "Resizes the Reference Box.",
                    "Reference Box のサイズを変更します。",
                    "调整 Reference Box 的尺寸。",
                    "Reference Box의 크기를 조절합니다."));

            GUIContent editContent = EditorGUIUtility.IconContent("EditCollider");
            if (editContent == null || editContent.image == null)
            {
                editContent = new GUIContent();
            }

            editContent.text = IsEditingReferenceBox(display)
                ? L(language, "Finish Reference Box Edit", "Reference Box 編集を終了", "结束参考框编辑", "Reference Box 편집 종료")
                : L(language, "Edit Reference Box", "Reference Box を編集", "编辑参考框", "Reference Box 편집");
            editContent.tooltip = L(language,
                "Toggle Scene view handles for the Reference Box.",
                "Reference Box の Scene ビュー操作ハンドルを切り替えます。",
                "切换 Reference Box 的 Scene 视图编辑手柄。",
                "Reference Box의 Scene 뷰 핸들을 켜거나 끕니다.");

            bool nextEditingState = GUILayout.Toggle(IsEditingReferenceBox(display), editContent, "Button");
            if (nextEditingState != IsEditingReferenceBox(display))
            {
                SetReferenceBoxEditing(display, nextEditingState);
            }
        }

        private int CountMissingMeshRenderers()
        {
            int count = 0;
            int index = 0;
            while (index < targets.Length)
            {
                RemotePhotoFrame display = targets[index] as RemotePhotoFrame;
                if (display == null || display.GetComponent<MeshRenderer>() == null)
                {
                    count++;
                }

                index++;
            }

            return count;
        }

        private int CountInvalidManualAspectRatios()
        {
            int count = 0;
            int index = 0;
            while (index < targets.Length)
            {
                RemotePhotoFrame display = targets[index] as RemotePhotoFrame;
                if (display != null && display.aspectMode == RemotePhotoAspectMode.Manual && display.manualAspectRatio <= 0f)
                {
                    count++;
                }

                index++;
            }

            return count;
        }

        private int CountInvalidReferenceBoxSizes()
        {
            int count = 0;
            int index = 0;
            while (index < targets.Length)
            {
                RemotePhotoFrame display = targets[index] as RemotePhotoFrame;
                if (display != null && display.aspectMode == RemotePhotoAspectMode.ReferenceBox && !HasValidReferenceBoxSize(display))
                {
                    count++;
                }

                index++;
            }

            return count;
        }

        private int CountInvalidAutomaticAspectRatios()
        {
            int count = 0;
            int index = 0;
            while (index < targets.Length)
            {
                RemotePhotoFrame display = targets[index] as RemotePhotoFrame;
                if (display != null && display.aspectMode != RemotePhotoAspectMode.Manual && !display.HasValidAutomaticAspectRatio())
                {
                    count++;
                }

                index++;
            }

            return count;
        }

        private int CountDuplicateManualAxes()
        {
            int count = 0;
            int index = 0;
            while (index < targets.Length)
            {
                RemotePhotoFrame display = targets[index] as RemotePhotoFrame;
                if (display != null &&
                    display.aspectMode == RemotePhotoAspectMode.ReferenceBox &&
                    display.axisMode == RemotePhotoAxisMode.ManualAxes &&
                    display.frameWidthAxis == display.frameHeightAxis)
                {
                    count++;
                }

                index++;
            }

            return count;
        }

        private void DrawResolvedAspectRatio(RemotePhotoInspectorLanguage language, RemotePhotoFrame display, bool multiObjectEditing)
        {
            display.RefreshResolvedFrameAspectRatio();
            EditorGUILayout.LabelField(
                G(language,
                    "Resolved Aspect Ratio", "最終アスペクト比", "最终宽高比", "최종 비율",
                    multiObjectEditing
                        ? "Shows the active Frame's current ratio."
                        : "Shows the ratio currently used for fitting.",
                    multiObjectEditing
                        ? "アクティブな Frame の現在比率を表示します。"
                        : "Fit に使われる現在比率を表示します。",
                    multiObjectEditing
                        ? "显示当前激活 Frame 的比例。"
                        : "显示当前用于适配的比例。",
                    multiObjectEditing
                        ? "활성 Frame의 현재 비율을 표시합니다."
                        : "맞춤에 현재 사용되는 비율을 표시합니다."),
                new GUIContent(display.GetResolvedFrameAspectRatio().ToString("0.###")));
        }

        private void OnSceneGUI()
        {
            RemotePhotoFrame display = (RemotePhotoFrame)target;
            if (display == null ||
                display.aspectMode != RemotePhotoAspectMode.ReferenceBox ||
                !IsEditingReferenceBox(display))
            {
                return;
            }

            Transform handleTransform = display.transform;
            using (new Handles.DrawingScope(handleTransform.localToWorldMatrix))
            {
                _referenceBoxHandle.center = display.referenceBoxCenter;
                _referenceBoxHandle.size = display.referenceBoxSize;

                EditorGUI.BeginChangeCheck();
                _referenceBoxHandle.DrawHandle();
                if (EditorGUI.EndChangeCheck())
                {
                    Undo.RecordObject(display, "Edit Reference Box");
                    display.referenceBoxCenter = _referenceBoxHandle.center;
                    display.referenceBoxSize = SanitizeSize(_referenceBoxHandle.size);
                    display.RefreshResolvedFrameAspectRatio();
                    EditorUtility.SetDirty(display);
                }
            }
        }

        private void OnDisable()
        {
            if (target != null && s_editingReferenceBoxInstanceId == target.GetInstanceID())
            {
                s_editingReferenceBoxInstanceId = 0;
            }
        }

        private bool IsEditingReferenceBox(RemotePhotoFrame display)
        {
            return display != null && s_editingReferenceBoxInstanceId == display.GetInstanceID();
        }

        private void SetReferenceBoxEditing(RemotePhotoFrame display, bool isEditing)
        {
            s_editingReferenceBoxInstanceId = isEditing && display != null ? display.GetInstanceID() : 0;
            SceneView.RepaintAll();
            Repaint();
        }

        private bool HasValidReferenceBoxSize(RemotePhotoFrame display)
        {
            if (display == null)
            {
                return false;
            }

            return display.referenceBoxSize.x > 0f &&
                   display.referenceBoxSize.y > 0f &&
                   display.referenceBoxSize.z > 0f;
        }

        private Vector3 SanitizeSize(Vector3 size)
        {
            const float minSize = 0.0001f;
            return new Vector3(
                Mathf.Max(minSize, Mathf.Abs(size.x)),
                Mathf.Max(minSize, Mathf.Abs(size.y)),
                Mathf.Max(minSize, Mathf.Abs(size.z)));
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
