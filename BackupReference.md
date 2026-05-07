```yaml
codex_project_backup:
  project_name: RemotePhotoSystem
  repository_root: .
  github_remote: https://github.com/thok404/RemotePhotoSystem.git
  project_type: VRChat World / Unity / UdonSharp package
  current_date_recorded: 2026-05-08
  developed_with:
    unity: 2022.3.22f1
    vrchat_sdk_worlds: 3.10.3
    vrchat_sdk_base: 3.10.3
    udonsharp: bundled with VRChat SDK - Worlds 3.10.3
    compatibility_note: lower dependency versions may miss APIs required by this package
  baseline_status:
    - current version is treated as the initial public version
    - old compatibility fields and starter tooling were removed
    - Docs and WebTool are separate documentation/tooling areas
    - package is distributed under Apache-2.0

  scope:
    included:
      - local gallery JSON imported into Unity
      - baked runtime URL arrays
      - remote image URL download
      - web gallery manager as primary gallery editing workflow
      - per-frame fit mode
      - opaque project shaders only
      - Manager-owned language and group management
    excluded:
      - remote JSON loading
      - Unity Inspector URL list editing
      - Excel or CSV as primary workflow
      - per-image fit mode
      - transparent shader support
      - backward compatibility shims

  runtime_roles:
    RemotePhotoManager:
      responsibilities:
        - baked gallery URL arrays
        - global play mode
        - loading mode
        - preload cache manager
        - shared retry settings
        - debug logs
        - managed group creation and ordering
        - project-level Inspector language
        - project shader validation
      important_fields:
        - galleryConfigFile
        - configuredPlayMode
        - loadingMode
        - loadOnceOnStart
        - loadOnceDelaySeconds
        - preloadLandscapeCacheSize
        - preloadPortraitCacheSize
        - retryAttempts
        - retryDelaySeconds
        - managedGroups
        - debugLogs
        - bakedLandscapeUrls
        - bakedPortraitUrls
        - preloadLandscapeUrls
        - preloadPortraitUrls
        - preloadOrderedUrls
        - preloadRevision
        - preloadReady
        - lastGalleryError
        - hasGalleryData
      main_methods:
        - ApplyBakedGallery()
        - HasGalleryData()
        - SelectLandscapeUrl()
        - SelectPortraitUrl()
        - BeginSequencePageSelection()
        - CommitSequencePageSelection()
        - NotifySelectionStateChanged()

    RemotePhotoGroup:
      responsibilities:
        - button permission
        - network cooldown
        - target frame list
        - synced current URL payload
      manager_link:
        field: manager
        visibility: hidden
        source_of_truth: RemotePhotoManager.managedGroups
      public_events:
        - Interact()
        - TriggerRandom()
        - TriggerPrevious()
        - TriggerNext()
      important_fields:
        - manager
        - permissionMode
        - triggerCooldownSeconds
        - nextAllowedTriggerServerTime
        - targets
        - syncedUrls
        - selectionRevision
      sequence_page_behavior:
        - Random works only when Manager play mode is Random
        - Previous and Next work only when Manager play mode is SequenceForward or SequenceReverse
        - first Previous or Next click loads the first sorted page
        - landscape frames advance only the landscape cursor
        - portrait frames advance only the portrait cursor
        - failed sequence URLs remain in the gallery and can be retried when revisited

    RemotePhotoFrame:
      responsibilities:
        - single frame display
        - material instance
        - cache-first load from RemotePhotoManager
        - direct download when preload is not used
        - fit/projection/rotation/aspect handling
      important_fields:
        - orientation
        - materialSlot
        - texturePropertyName
        - defaultTexture
        - fallbackTexture
        - backgroundColor
        - photoFitMode
        - projectionMode
        - boxProjectionHorizontalFlip
        - photoRotationDegrees
        - aspectMode
        - axisMode
        - manualAspectRatio
        - referenceBoxCenter
        - referenceBoxSize
      main_methods:
        - LoadPhoto()
        - LoadPhotoFromManager()
        - ClearPhoto()
      aspect_rules:
        - Manual uses manualAspectRatio and hides axisMode
        - Auto uses mesh bounds and always discards the shortest dimension
        - ReferenceBox uses referenceBoxSize
        - ReferenceBox is the only aspect mode that exposes axisMode
        - ReferenceBox axisMode Auto discards the shortest reference-box dimension
        - ReferenceBox axisMode ManualAxes uses frameWidthAxis and frameHeightAxis
      projection_rules:
        - MeshUv uses mesh UVs
        - Box treats the shortest mesh axis as thickness
        - Box projects only to large front/back faces
        - boxProjectionHorizontalFlip is shown only for Box
      rotation_rules:
        - Portrait adds automatic 90 degree orientation rotation
        - photoRotationDegrees is the final user offset
        - photoRotationDegrees works in both MeshUv and Box
        - shader applies Box mirroring first, then final rotation

    RemotePhotoButton:
      responsibilities:
        - bridge VRC Button or switch assets to a RemotePhotoGroup
        - expose one group reference and one button action dropdown
      public_events:
        - Interact()
        - TriggerSelectedAction()
        - TriggerRandom()
        - TriggerPrevious()
        - TriggerNext()
      current_mapping:
        Random: RemotePhotoGroup.TriggerRandom()
        Previous: RemotePhotoGroup.TriggerPrevious()
        Next: RemotePhotoGroup.TriggerNext()

  inspector_language:
    owner: RemotePhotoManager.inspectorLanguage
    behavior:
      - all RemotePhotoSystem Inspectors use the scene Manager language when a Manager exists
      - scripts do not need to be connected to the Manager before language takes effect
      - if no Manager exists, Inspectors fall back to English

  gallery_json:
    root_fields:
      - entries
    removed_fields:
      - version
      - enabled
    unity_reads:
      - url
      - orientation
    web_tool_only:
      - id
      - tags
      - note
      - metadata
    sample_shape:
      entries:
        - id: photo_001
          url: https://example.com/photo.jpg
          orientation: Landscape
          tags:
            - 1f
            - event
          note: ""
          metadata:
            width: 1920
            height: 1080
            checkedAt: 2026-04-24T00:00:00.000Z
            status: ok

  shaders:
    opaque_only: true
    files:
      - Shaders/RemotePhotoFrameDisplayUnlit.shader
      - Shaders/RemotePhotoFrameDisplayLit.shader
    hidden_preload_property: _RemotePhotoPreloadTex
    background_property: _RemotePhotoBackgroundColor
    required_for:
      - Background Color
      - Box projection
      - Photo rotation
      - Preload hidden texture slot
    manager_validation:
      - all connected frame materials should use RemotePhotoSystem/Photo Frame Display Unlit or RemotePhotoSystem/Photo Frame Display Lit
      - Manager Inspector provides buttons to apply project shaders to connected frames

  preload_design:
    enabled_by: RemotePhotoManager.loadingMode == Preload
    starts_on_world_start: true
    receiver_strategy: use registered managed frame runtime materials as hidden download slots
    cache_size_fields:
      - preloadLandscapeCacheSize
      - preloadPortraitCacheSize
    shared_retry_fields:
      - retryAttempts
      - retryDelaySeconds
    fixed_next_download_interval_seconds: 5
    retry_delay_rule:
      - retryDelaySeconds is not forced to include the 5 second preload queue interval
      - the 5 second interval is only for moving to the next preload queue image
    total_cache_capacity: landscape + portrait
    known_risk: cache eviction is total capacity, not strictly separated by orientation
    preload_order_rule: managedGroups order, then each group's targets order
    random_selection_rule: random mode can use synced preload order first, then falls back to normal gallery random when cache order is insufficient
    sequence_failure_rule: failed sequence URLs remain in the gallery and can be retried when revisited

  networking_design:
    current_photo_authority: RemotePhotoGroup.syncedUrls
    manager_cursor_sync: Group trigger calls RemotePhotoManager.NotifySelectionStateChanged()
    cooldown_sync:
      field: RemotePhotoGroup.nextAllowedTriggerServerTime
      time_source: Networking.GetServerTimeInSeconds()
      behavior: cooldown is checked before ownership takeover
    sync_preference:
      - clients show fallback on download failure
      - clients must not locally choose replacement URLs
      - owner next sync is the next chance to show a different URL

  load_once_on_start:
    owner: RemotePhotoManager
    applies_to:
      - Preload
      - NonPreload
    fields:
      - loadOnceOnStart
      - loadOnceDelaySeconds
    behavior:
      - when enabled, Manager triggers every managed group once after configured delay
      - Random play mode calls RemotePhotoGroup.TriggerRandom()
      - SequenceForward and SequenceReverse call RemotePhotoGroup.TriggerNext() so the first page loads

  non_preload_design:
    enabled_by: RemotePhotoManager.loadingMode == NonPreload
    inspector_behavior:
      - preload cache fields are hidden
      - global Load Once On Start remains visible
    behavior:
      - button trigger immediately selects URL
      - group syncs URL
      - frame directly downloads image

  editor_workflow:
    manager_creation_menu: GameObject/Remote Photo System/Create Manager
    group_management:
      - Add Group creates one child group and appends it to managedGroups
      - Add Group does not run Sync Groups From Children
      - Sync Groups From Children scans child RemotePhotoGroup components and rebuilds managedGroups
      - Remove Missing Groups removes null managedGroups entries
    removed_tooling:
      - Tools/Remote Photo System/Create Starter Assets
      - Unity URL list editor
      - RemotePhotoGalleryManifestAssetEditor

  web_tool:
    repository_path: WebTool
    responsibilities:
      - manual URL entry
      - bulk URL paste
      - import URL list
      - import existing JSON
      - bulk orientation edit
      - tags and notes
      - metadata probing
      - IndexedDB metadata cache
      - export Unity JSON

  documentation:
    repository_path: Docs
    status: ignored by package git repository

  license:
    type: Apache-2.0
    copyright_holder: thok404
    applies_to:
      - code
      - shaders
      - prefabs
      - models
      - textures
      - icons
      - samples
    notice_file: NOTICE.md
    asset_origin_summary:
      - bundled example landscape images were generated with ChatGPT
      - bundled simple geometric UI icons were generated with ChatGPT
      - bundled frame normal/detail texture was generated with ChatGPT
      - bundled assets are curated or edited for this project
    external_dependencies:
      - VRChat SDK is required but not distributed in this repository
      - UdonSharp is required but not distributed in this repository

  key_files:
    runtime:
      - Scripts/Runtime/RemotePhotoTypes.cs
      - Scripts/Runtime/RemotePhotoManager.cs
      - Scripts/Runtime/RemotePhotoGroup.cs
      - Scripts/Runtime/RemotePhotoFrame.cs
      - Scripts/Runtime/RemotePhotoButton.cs
    editor:
      - Scripts/Editor/RemotePhotoManagerEditor.cs
      - Scripts/Editor/RemotePhotoGroupEditor.cs
      - Scripts/Editor/RemotePhotoFrameEditor.cs
      - Scripts/Editor/RemotePhotoButtonEditor.cs
      - Scripts/Editor/RemotePhotoEditorLocalization.cs
      - Scripts/Editor/RemotePhotoFrameDisplayLitShaderGUI.cs
      - Scripts/Editor/RemotePhotoHierarchyMenu.cs
    shaders:
      - Shaders/RemotePhotoFrameDisplayUnlit.shader
      - Shaders/RemotePhotoFrameDisplayLit.shader

  release_cleanup:
    completed:
      - removed photoframetest.fbx
      - removed RemotePhotoStarterGalleryConfig sample
      - removed RemotePhotoGalleryManifestAssetEditor
      - renamed GalleryService semantics to Manager
      - renamed Manifest semantics to GalleryConfig/Gallery
      - renamed prepared preload queue semantics to preload
      - removed code comments from scripts and shaders
    latest_cleanup_commit:
      hash: 2df0b76
      message: Prepare RemotePhotoSystem release cleanup

  test_focus:
    - Unity/UdonSharp compile
    - bake current JSON shape
    - non-preload trigger/sync/late-join display
    - preload world-start cache warmup
    - multiple groups under one Manager
    - mixed landscape/portrait targets
    - trigger cooldown behavior
    - fallback behavior for bad URL, oversized image, SSL issue, denied domain
    - Box projection front/back consistency
    - Horizontal Flip behavior
    - ReferenceBox axis mode behavior

  known_risks:
    - VRChat image loading is not browser loading
    - MaximumDimensionExceeded is non-retryable
    - SSL certificate chain can fail in VRCImageDownloader even if browser works
    - preload mode remains more complex than non-preload mode
    - if preload release stability is poor, keep NonPreload as stable path

  external_references:
    vrchat_image_loading: https://creators.vrchat.com/worlds/udon/image-loading/
```
