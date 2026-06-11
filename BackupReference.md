```yaml
codex_project_backup:
  project_name: RemotePhotoSystem
  agent_operating_rule: "Do not run git commands or modify git state unless the user explicitly asks for git work."
  repository_root: .
  github_remote: https://github.com/thok404/RemotePhotoSystem.git
  documentation_url: https://thok404.github.io/RemotePhotoDocs/
  project_type: VRChat World / Unity / UdonSharp package
current_date_recorded: 2026-06-04
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
        - Random ReadyPool manager
        - single active Random consume request
        - Sequence per-group page index state
        - Sequence preload focus tracking
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
        - sequenceLandscapePageIndices
        - sequencePortraitPageIndices
        - sequenceFocusGroupIndex
        - sequenceFocusDirection
        - lastGalleryError
        - hasGalleryData
      main_methods:
        - ApplyBakedGallery()
        - HasGalleryData()
        - SelectLandscapeUrl()
        - SelectPortraitUrl()
        - BeginSequencePageSelection()
        - CommitSequencePageSelection()
        - BeginRandomConsume()
        - RefreshPreloadPredictions()
        - NotifySelectionStateChanged()

    RemotePhotoGroup:
      responsibilities:
        - button trigger request entry
        - target frame list
        - synced current URL payload
        - synced load slot order
        - synced Random per-slot request ids
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
        - targets
        - syncedUrls
        - syncedLoadOrderSlots
        - syncedSlotRequestIds
        - selectionRevision
      sequence_page_behavior:
        - Random works only when Manager play mode is Random
        - Previous and Next work only when Manager play mode is SequenceForward or SequenceReverse
        - first Previous or Next click loads the first sorted page
        - each Group has its own sequence page index
        - landscape frames use the landscape page index
        - portrait frames use the portrait page index
        - page index changes immediately when Previous or Next is accepted
        - image loading does not block page changes
        - old display sessions are invalidated by newer selectionRevision values
        - failed sequence URLs remain in the gallery and can be retried when revisited
      trigger_authority:
        - all players may press trigger buttons
        - non-Master clients only broadcast trigger requests
        - current Master is the only client that selects URLs, advances cursors, writes synced state, and serializes
        - Group trigger cooldown was removed; current request state and Master arbitration are the remaining trigger guards

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
        - LoadPhotoFromManagerSlot()
        - ApplyManagerTexture()
        - ClearPhoto()
      random_preload_rule:
        - Frame does not select Random URLs
        - Frame does not insert preload priority requests
        - Frame displays Manager-provided Texture when consumed from ReadyPool
        - Frame keeps the current image while waiting or when fallback is disabled
      sequence_preload_rule:
        - Frame does not select Sequence URLs
        - Frame does not insert preload priority requests
        - Sequence cache hits are retained by Manager instead of consumed
        - Sequence cache misses do not make Frame poll continuously
        - Manager notifies affected Groups when a Sequence URL becomes cached or fails
        - selectionRevision and request serial prevent old page results from applying to a newer page
      aspect_rules:
        - Manual uses manualAspectRatio and hides axisMode
        - Auto uses mesh bounds and always discards the shortest dimension
        - ReferenceBox uses referenceBoxSize
        - PhotoSurface uses mesh surface bounds and does not swap width and height
        - PhotoSurface supports aspect ratios below 1 for vertically oriented irregular photo surfaces
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
    fixed_next_download_interval_seconds: 5.1
    retry_delay_rule:
      - retryDelaySeconds is not forced to include the 5.1 second preload queue interval
      - the 5.1 second interval is only for moving to the next preload queue image
    total_cache_capacity: landscape + portrait
    random_ready_pool:
      enabled_when:
        - loadingMode == Preload
        - configuredPlayMode == Random
      design:
        - no Group Page Queue
        - no Future Page queue
        - no page cache window
        - Manager continuously downloads random images into a global ReadyPool split by orientation
        - ReadyPool stores URL, Texture2D, download handle, and access tick
        - ReadyPool capacity counts only unassigned preloaded images
        - images already assigned to Frames do not consume ReadyPool capacity
        - active request may keep downloading even if ReadyPool capacity is full
      active_consume_request:
        - only one active Random consume request may exist globally
        - new Random trigger is rejected while active request is incomplete
        - request snapshots valid Group target slot order at trigger time
        - slots are filled strictly in Group.targets order
        - each ReadyPool image is consumed by one Frame only
        - if ReadyPool is insufficient, already available slots display immediately and remaining slots keep their old image until future downloads fill them
      download_priority:
        - active Random consume request missing orientation
        - current synced URLs not yet cached or displayed
        - ReadyPool background refill based on managedGroups and targets direction order
      refill_rule:
        - background Random refill follows managedGroups order and each group's targets orientation
        - it no longer fills all landscape cache before portrait cache
      failure_rule:
        - failed random preload URL is marked failed
        - failed random preload does not advance an active consume slot
        - Manager continues downloading another random URL
    sequence_failure_rule: failed sequence URLs remain in the gallery and can be retried when revisited
    sequence_design:
      enabled_when:
        - loadingMode == Preload
        - configuredPlayMode == SequenceForward or SequenceReverse
      design:
        - Sequence does not use Random ReadyPool
        - Sequence uses URL cache semantics, so the same downloaded Texture can be reused by multiple Groups or pages
        - each managed Group has independent landscape and portrait page indices
        - SequenceForward and SequenceReverse use one gallery list per orientation and map visual index by sort direction
        - pageIndex 0 is the first visual page under the current sort direction
        - Previous and Next immediately create a new selectionRevision and synced URL page
        - cached slots can display immediately
        - uncached slots keep the current display until Manager downloads their target URL
        - old selection results are rejected by selectionRevision and request serial checks
      preload_focus:
        - only the last interacted Group is the Sequence preload focus
        - preload plan follows the focus Group pageIndex and interaction direction
        - current focus page missing URLs have highest priority
        - nearby pages around the focus page are predicted after current page demand
        - old non-focus Group preloads are not protected beyond currently displayed textures
      cache_lifecycle:
        - Sequence cached textures are retained in Manager cache when displayed
        - displayed cached downloads are not disposed while still stored in Manager cache
        - cache eviction skips displayed URLs and current synced URLs
        - current page demand can be downloaded even when future preload capacity is full

  networking_design:
    current_photo_authority: RemotePhotoGroup.syncedUrls
    synced_slot_order: RemotePhotoGroup.syncedLoadOrderSlots
    random_slot_request_ids: RemotePhotoGroup.syncedSlotRequestIds
    manager_cursor_sync: Sequence Group trigger calls RemotePhotoManager.NotifySelectionStateChanged()
    sequence_page_sync:
      - Manager syncs sequenceLandscapePageIndices
      - Manager syncs sequencePortraitPageIndices
      - Manager syncs sequenceFocusGroupIndex
      - Manager syncs sequenceFocusDirection
      - Group syncs current page URLs through syncedUrls
      - Group syncs slot order through syncedLoadOrderSlots
      - clients apply only the latest selectionRevision for each Group
    random_trigger_sync:
      - TriggerRandom is a request entry
      - non-Master sends RequestTriggerRandomNetwork to all clients
      - only Networking.IsMaster executes the request
      - Master creates ActiveConsumeRequest through RemotePhotoManager.BeginRandomConsume()
      - Master writes each filled Random slot URL and syncedSlotRequestIds incrementally
      - Random slot updates may serialize per slot while the active request is being fulfilled
    sync_preference:
      - clients show fallback on download failure
      - clients must not locally choose replacement URLs
      - Master next sync is the next chance to show a different URL
      - Texture is never network-synced, only URL and slot metadata are synced

  load_once_on_start:
    owner: RemotePhotoManager
    applies_to:
      - Preload
      - NonPreload
    fields:
      - loadOnceOnStart
      - loadOnceDelaySeconds
    behavior:
      - Load Once is treated as an initial formal selection/session
      - it no longer simulates button clicks
      - it does not call TriggerRandom or TriggerNext
      - only Master creates initial selections
      - Remote clients only apply synced selection data
      - each eligible Group runs Load Once at most once
      - Groups already interacted with by users are skipped
      - Groups that already have synced URLs are skipped
      - startup selection writes syncedUrls, syncedLoadOrderSlots, syncedSlotRequestIds, selectionRevision, selectionSessionId, and loadOrderRevision
      - Random mode builds an immediate random selection in Group.targets order
      - SequenceForward and SequenceReverse build page 0 using the current visual sort direction
      - Load Once selections set selectionSequentialApply so all clients apply them in syncedLoadOrderSlots order
      - normal Preload button selections remain parallel unless NonPreload forces serial display
      - Preload current-demand download scans all current synced Groups instead of only the sequence focus Group
      - old Load Once callbacks are rejected by selectionRevision, selectionSessionId, slot, and request serial checks
      - if a player triggers a Group before Load Once runs, user interaction wins and Load Once skips that Group

  non_preload_design:
    enabled_by: RemotePhotoManager.loadingMode == NonPreload
    inspector_behavior:
      - preload cache fields are hidden
      - global Load Once On Start remains visible
    behavior:
      - button trigger immediately selects URL
      - group syncs URL
      - frame directly downloads image
      - NonPreload Random still selects URLs at trigger time because it has no ReadyPool

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
    public_url: https://thok404.github.io/RemotePhotoDocs/

  release_distribution:
    release_directory: Release
    release_directory_status: ignored by package git repository
    github_release_assets:
      - Release/RemotePhotoSystem_v1.00.unitypackage
    webtool_release_source: WebTool

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
    - fallback behavior for bad URL, oversized image, SSL issue, denied domain
    - Random + Preload ReadyPool refill after multiple rounds
    - Random + Preload active request rejection while incomplete
    - Random + Preload mixed landscape/portrait ReadyPool balance
    - Random + Preload multi-group trigger behavior under Master arbitration
    - Sequence + Preload immediate page change under rapid Previous/Next clicking
    - Sequence + Preload latest page only receives late download results
    - Sequence + Preload focus switches when another Group is interacted
    - Sequence + Preload cached URL reuse across Groups
    - SequenceForward visual page order
    - SequenceReverse visual page order
    - Box projection front/back consistency
    - Horizontal Flip behavior
    - ReferenceBox axis mode behavior

  known_risks:
    - VRChat image loading is not browser loading
    - MaximumDimensionExceeded is non-retryable
    - SSL certificate chain can fail in VRCImageDownloader even if browser works
    - preload mode remains more complex than non-preload mode
    - Random + Preload responsiveness is bounded by VRChat image download interval and image source reliability
    - if ReadyPool does not contain enough images for a requested group, only available slots change immediately
    - if preload release stability is poor, keep NonPreload as stable path

latest_local_work:
  summary: Add manual Tile scale and offset
  commit:
    message: Add tile scale offset controls
  files_changed:
    - Scripts/Runtime/RemotePhotoFrame.cs
    - Scripts/Runtime/RemotePhotoFrame.asset
    - Scripts/Editor/RemotePhotoFrameEditor.cs
    - Shaders/RemotePhotoFrameDisplayLit.shader
    - Shaders/RemotePhotoFrameDisplayUnlit.shader
    - Samples/SAMPLE_SCENE.unity
    - BackupReference.md
  behavior_changes:
    - Photo Fit Mode Tile exposes per-Frame Tile Scale and Tile Offset
    - Tile mode no longer uses automatic aspect-based UV scaling
    - Tile Scale uses Unity tiling semantics; larger values repeat the photo more
    - Tile Offset shifts the tiled photo UV origin
    - Tile Scale values at or below 0 are clamped to 0.001 at runtime and warned in Inspector
    - Lit and Unlit shaders wrap Tile sampling with frac so the whole photo surface repeats instead of clamping edge pixels
    - Crop, Contain, and Stretch keep their existing automatic UV behavior
  validation:
    - dotnet build PhotoFrame.sln -nologo passed
    - Unity shader import validation still required inside Unity editor
  pending_runtime_tests:
    - Tile + Mesh UV repeats the remote photo across the full photo surface
    - Tile + Box repeats the remote photo on the Box photo faces without changing side/background behavior
    - Tile Scale changes repeat count and Tile Offset changes tile origin
    - Crop, Contain, and Stretch do not regress
    release_assets:
      local_unitypackage: Release/RemotePhotoSystem_v1.00.unitypackage
      local_unitypackage_size: 7478827
      local_webtool_zip: Release/RemotePhotoSystem_WebTool_v1.00.zip
      local_webtool_zip_size: 14208
      note: Release folder is local release staging and is not tracked by git.

  external_references:
    vrchat_image_loading: https://creators.vrchat.com/worlds/udon/image-loading/
```
