using UdonSharp;
using UnityEngine;
using VRC.SDK3.Image;
using VRC.SDKBase;
using VRC.Udon.Common.Interfaces;

namespace RemotePhotoSystem
{
    [UdonBehaviourSyncMode(BehaviourSyncMode.Manual)]
    public class RemotePhotoManager : UdonSharpBehaviour
    {
        #region Inspector Fields

        public RemotePhotoInspectorLanguage inspectorLanguage = RemotePhotoInspectorLanguage.English;

        public TextAsset galleryConfigFile;

        public RemotePhotoPlayMode configuredPlayMode = RemotePhotoPlayMode.Random;

        public RemotePhotoLoadingMode loadingMode = RemotePhotoLoadingMode.Preload;

        public int preloadLandscapeCacheSize = 16;
        public int preloadPortraitCacheSize = 16;
        public int retryAttempts = 3;
        public float retryDelaySeconds = 2f;

        public bool loadOnceOnStart;
        public float loadOnceDelaySeconds = 1f;
        public RemotePhotoGroup[] managedGroups = new RemotePhotoGroup[0];

        public bool debugLogs;
        [HideInInspector] public Material preloadDownloadMaterial;
        [HideInInspector] public Material[] preloadDownloadMaterials = new Material[0];

        #endregion

        #region Baked Gallery

        [HideInInspector] public VRCUrl[] bakedLandscapeUrls = new VRCUrl[0];
        [HideInInspector] public VRCUrl[] bakedPortraitUrls = new VRCUrl[0];

        [HideInInspector] public VRCUrl[] landscapeUrls = new VRCUrl[0];
        [HideInInspector] public VRCUrl[] portraitUrls = new VRCUrl[0];

        #endregion

        #region Synced Playback State

        [UdonSynced] public VRCUrl[] sequencePreloadLandscapeUrls = new VRCUrl[0];
        [UdonSynced] public VRCUrl[] sequencePreloadPortraitUrls = new VRCUrl[0];
        [UdonSynced] public VRCUrl[] sequencePreloadOrderedUrls = new VRCUrl[0];
        [UdonSynced] public int preloadRevision;
        [UdonSynced] public bool preloadReady;
        [UdonSynced] public int nextLandscapeIndex;
        [UdonSynced] public int nextPortraitIndex;
        [UdonSynced] public int preloadNextLandscapeIndex;
        [UdonSynced] public int preloadNextPortraitIndex;
        [UdonSynced] public bool sequenceLandscapeInitialized;
        [UdonSynced] public bool sequencePortraitInitialized;
        [UdonSynced] public int[] sequenceLandscapePageIndices = new int[0];
        [UdonSynced] public int[] sequencePortraitPageIndices = new int[0];
        [UdonSynced] public int sequenceFocusGroupIndex = -1;
        [UdonSynced] public int sequenceFocusDirection = 1;

        #endregion

        #region Runtime Status

        [HideInInspector] public string lastGalleryError = string.Empty;
        [HideInInspector] public bool hasGalleryData;
        [HideInInspector] public string lastPreloadStatus = string.Empty;

        #endregion

        #region Runtime State

        private VRCImageDownloader _preloadDownloader;
        private IVRCImageDownload _currentPreloadDownload;
        private TextureInfo _preloadTextureInfo;
        private VRCUrl _activePreloadUrl;
        private string _activePreloadUrlString = string.Empty;
        private bool _activePreloadLandscape;
        private int _activePreloadRetryCount;
        private bool _preloadInProgress;
        private bool _preloadContinueScheduled;
        private int _preloadScanOrientation;
        private int _preloadScanIndex;
        private int _preloadScanOrderedIndex;
        private int _nextPreloadMaterialIndex;
        private int _activeSequenceLandscapeStart;
        private int _activeSequencePortraitStart;
        private int _activeSequenceLandscapeCursor;
        private int _activeSequencePortraitCursor;
        private int _activeSequenceGroupIndex = -1;
        private int _sequencePredictLandscapeStep;
        private int _sequencePredictPortraitStep;

        private string[] _cachedUrlStrings = new string[0];
        private VRCUrl[] _cachedUrls = new VRCUrl[0];
        private Texture2D[] _cachedTextures = new Texture2D[0];
        private IVRCImageDownload[] _cachedDownloads = new IVRCImageDownload[0];
        private int[] _cachedAccessTicks = new int[0];
        private RemotePhotoFrame[] _displayedFrames = new RemotePhotoFrame[0];
        private string[] _displayedUrlStrings = new string[0];
        private IVRCImageDownload[] _displayedDownloads = new IVRCImageDownload[0];
        private int _cacheAccessTick;
        private bool _startupWarmupActive;
        private int _startupWarmupLandscapeCount;
        private int _startupWarmupPortraitCount;
        private RemotePhotoGroup _activeRandomGroup;
        private int[] _activeRandomSlots = new int[0];
        private int _activeRandomRequestId;
        private int _activeRandomNextSlotIndex;
        private bool _activeRandomRequestActive;
        private int _nextReadyPoolGroupIndex;
        private int _nextReadyPoolTargetIndex;
        private bool _loadOnceStarted;
        private bool _loadOnceRetryScheduled;
        private int _loadOnceRetryCount;

        private string[] _failedUrlStrings = new string[0];

        #endregion

        #region Constants

        private const string PreloadTexturePropertyName = "_RemotePhotoPreloadTex";
        private const float PreloadDownloadIntervalSeconds = 5.1f;
        private const float LoadOnceRetryDelaySeconds = 1f;
        private const int MaxLoadOnceRetryCount = 10;

        #endregion

        #region Unity Lifecycle

        public void Start()
        {
            ApplyBakedGallery();
            EnsureCacheArrays();
            EnsureSequenceStateArrays();
            EnsureMasterOwnership();
            LogDebug("Remote Photo Manager started. Landscape=" + GetLandscapeCount() + ", Portrait=" + GetPortraitCount() + ", LoadingMode=" + GetLoadingModeName());

            if (IsPreloadEnabled())
            {
                PrepareStartupWarmup();
                EnsurePredictionQueue();
                StartPreloadingQueue();
            }

            if (loadOnceOnStart)
            {
                SendCustomEventDelayedSeconds(nameof(_LoadOnceOnStart), Mathf.Max(0f, loadOnceDelaySeconds));
            }
        }

        public override void OnDeserialization()
        {
            ApplyBakedGallery();
            EnsureSequenceStateArrays();
            StartPreloadingQueue();
        }

        public override void OnPlayerLeft(VRCPlayerApi player)
        {
            EnsureMasterOwnership();
            TryRunLoadOnceOnStart();
        }

        #endregion

        #region Startup Load Once

        public bool IsPreloadEnabled()
        {
            return loadingMode == RemotePhotoLoadingMode.Preload;
        }

        public void _LoadOnceOnStart()
        {
            TryRunLoadOnceOnStart();
        }

        public void _RetryLoadOnceOnStart()
        {
            _loadOnceRetryScheduled = false;
            TryRunLoadOnceOnStart();
        }

        public void TryRunLoadOnceOnStart()
        {
            if (!loadOnceOnStart || _loadOnceStarted)
            {
                return;
            }

            if (!Networking.IsMaster)
            {
                return;
            }

            EnsureMasterOwnership();
            if (!IsSystemReadyForInitialSelection())
            {
                ScheduleLoadOnceRetry();
                return;
            }

            _loadOnceStarted = true;
            int index = 0;
            while (index < managedGroups.Length)
            {
                RemotePhotoGroup group = managedGroups[index];
                if (group != null && group.CreateLoadOnceSelectionFromManager())
                {
                    LogDebug("[LoadOnce] CreateInitialSelection group=" + group.gameObject.name + ", mode=" + configuredPlayMode + ", preload=" + IsPreloadEnabled() + ", revision=" + group.selectionRevision);
                }

                index++;
            }
        }

        private bool IsSystemReadyForInitialSelection()
        {
            ApplyBakedGallery();
            EnsureSequenceStateArrays();

            if (!HasGalleryData())
            {
                LogDebug("[LoadOnce] Waiting for gallery data.");
                return false;
            }

            if (managedGroups == null || managedGroups.Length == 0)
            {
                LogDebug("[LoadOnce] No managed groups.");
                return false;
            }

            return true;
        }

        private void ScheduleLoadOnceRetry()
        {
            if (_loadOnceRetryScheduled || _loadOnceRetryCount >= MaxLoadOnceRetryCount)
            {
                return;
            }

            _loadOnceRetryScheduled = true;
            _loadOnceRetryCount++;
            SendCustomEventDelayedSeconds(nameof(_RetryLoadOnceOnStart), LoadOnceRetryDelaySeconds);
        }

        #endregion

        #region Cleanup

        public void OnDestroy()
        {
            DisposeCurrentPreloadDownload();
            DisposeCachedDownloads();
            DisposeDisplayedDownloads();

            if (_preloadDownloader != null)
            {
                _preloadDownloader.Dispose();
                _preloadDownloader = null;
            }
        }

        #endregion

        #region Gallery And Selection API

        public bool HasGalleryData()
        {
            return hasGalleryData;
        }

        public int GetLandscapeCount()
        {
            return landscapeUrls == null ? 0 : landscapeUrls.Length;
        }

        public int GetPortraitCount()
        {
            return portraitUrls == null ? 0 : portraitUrls.Length;
        }

        public VRCUrl GetLandscapeUrl(int index)
        {
            if (landscapeUrls == null || index < 0 || index >= landscapeUrls.Length)
            {
                return null;
            }

            return landscapeUrls[index];
        }

        public VRCUrl GetPortraitUrl(int index)
        {
            if (portraitUrls == null || index < 0 || index >= portraitUrls.Length)
            {
                return null;
            }

            return portraitUrls[index];
        }

        public void ApplyBakedGallery()
        {
            if (bakedLandscapeUrls == null)
            {
                if (landscapeUrls == null || landscapeUrls.Length != 0)
                {
                    landscapeUrls = new VRCUrl[0];
                }
            }
            else if (landscapeUrls != bakedLandscapeUrls)
            {
                landscapeUrls = bakedLandscapeUrls;
            }

            if (bakedPortraitUrls == null)
            {
                if (portraitUrls == null || portraitUrls.Length != 0)
                {
                    portraitUrls = new VRCUrl[0];
                }
            }
            else if (portraitUrls != bakedPortraitUrls)
            {
                portraitUrls = bakedPortraitUrls;
            }

            bool nextHasGalleryData = GetLandscapeCount() > 0 || GetPortraitCount() > 0;
            if (hasGalleryData != nextHasGalleryData)
            {
                hasGalleryData = nextHasGalleryData;
            }

            string nextError = hasGalleryData ? string.Empty : "Local gallery is empty.";
            if (lastGalleryError != nextError)
            {
                lastGalleryError = nextError;
            }
        }

        public VRCUrl SelectLandscapeUrl(VRCUrl[] selectedUrls, int selectedLength)
        {
            return SelectUrl(true, selectedUrls, selectedLength);
        }

        public VRCUrl SelectPortraitUrl(VRCUrl[] selectedUrls, int selectedLength)
        {
            return SelectUrl(false, selectedUrls, selectedLength);
        }

        public void BeginSequencePageSelection(RemotePhotoGroup group, bool nextPage, int landscapeCount, int portraitCount)
        {
            EnsureSequenceStateArrays();
            _activeSequenceGroupIndex = GetManagedGroupIndex(group);
            int direction = nextPage ? 1 : -1;
            sequenceFocusDirection = direction;
            sequenceFocusGroupIndex = _activeSequenceGroupIndex;
            _activeSequenceLandscapeStart = GetSequencePageVisualStart(true, _activeSequenceGroupIndex, direction, landscapeCount);
            _activeSequencePortraitStart = GetSequencePageVisualStart(false, _activeSequenceGroupIndex, direction, portraitCount);
            _activeSequenceLandscapeCursor = 0;
            _activeSequencePortraitCursor = 0;
            _sequencePredictLandscapeStep = 0;
            _sequencePredictPortraitStep = 0;
        }

        public VRCUrl SelectLandscapeSequencePageUrl()
        {
            return SelectSequencePageUrl(true);
        }

        public VRCUrl SelectPortraitSequencePageUrl()
        {
            return SelectSequencePageUrl(false);
        }

        public void CommitSequencePageSelection(int landscapeCount, int portraitCount)
        {
            if (landscapeCount > 0)
            {
                sequenceLandscapeInitialized = true;
            }

            if (portraitCount > 0)
            {
                sequencePortraitInitialized = true;
            }
        }

        #endregion

        #region Random Preload Requests

        public bool BeginRandomConsume(RemotePhotoGroup group)
        {
            if (group == null || !IsPreloadEnabled() || configuredPlayMode != RemotePhotoPlayMode.Random)
            {
                return false;
            }

            if (_activeRandomRequestActive)
            {
                LogDebug("Random request replaced by newer valid intent.");
                CancelActiveRandomRequest();
            }

            if (GetManagedGroupIndex(group) < 0)
            {
                return false;
            }

            RemotePhotoFrame[] targets = group.targets;
            int validCount = CountValidTargets(targets);
            if (validCount <= 0)
            {
                return false;
            }

            int requestId = group.BeginRandomPreloadSelectionFromManager();
            if (requestId <= 0)
            {
                return false;
            }

            _activeRandomSlots = new int[validCount];
            int writeIndex = 0;
            int index = 0;
            while (targets != null && index < targets.Length)
            {
                if (targets[index] != null)
                {
                    _activeRandomSlots[writeIndex] = index;
                    writeIndex++;
                }

                index++;
            }

            _activeRandomGroup = group;
            _activeRandomRequestId = requestId;
            _activeRandomNextSlotIndex = 0;
            _activeRandomRequestActive = true;
            lastPreloadStatus = "Random request active.";
            LogDebug("Random request started: " + group.gameObject.name + " Slots=" + validCount);
            TryFulfillActiveRandomRequest();
            CancelPreloadIfActiveRandomNeedsPriority();
            StartPreloadingQueue();
            return true;
        }

        #endregion

        #region Preload Coordination

        public void NotifySelectionStateChanged()
        {
            _startupWarmupActive = false;
            _failedUrlStrings = new string[0];
            preloadRevision++;
            if (Networking.IsOwner(gameObject))
            {
                if (_preloadInProgress &&
                    !string.IsNullOrEmpty(_activePreloadUrlString) &&
                    !IsCurrentSyncedUrlString(_activePreloadUrlString) &&
                    HasUncachedCurrentSyncedUrl())
                {
                    LogDebug("Current selection needs preload priority. Cancelling future preload: " + _activePreloadUrlString);
                    CancelPreloadForQueueMutation();
                }

                RequestSerialization();
                if (IsPreloadEnabled())
                {
                    RefreshPreloadPredictions();
                    return;
                }
            }

            WakePreloadQueue();
        }

        public void RefreshPreloadPredictions()
        {
            if (IsPreloadEnabled() && configuredPlayMode == RemotePhotoPlayMode.Random)
            {
                StartPreloadingQueue();
                return;
            }

            RequestPrepareForCounts(CountCurrentSyncedUrls(true), CountCurrentSyncedUrls(false));
        }

        public bool HasActiveRandomConsumeRequest()
        {
            return _activeRandomRequestActive;
        }

        private int CountValidTargets(RemotePhotoFrame[] targets)
        {
            int count = 0;
            int index = 0;
            while (targets != null && index < targets.Length)
            {
                if (targets[index] != null)
                {
                    count++;
                }

                index++;
            }

            return count;
        }

        private int GetSyncedSlotForPair(RemotePhotoGroup group, int pairIndex)
        {
            if (group == null || pairIndex < 0)
            {
                return -1;
            }

            int[] slots = group.syncedLoadOrderSlots;
            if (slots != null && pairIndex < slots.Length && slots[pairIndex] >= 0)
            {
                return slots[pairIndex];
            }

            return pairIndex;
        }

        private RemotePhotoFrame GetSyncedFrameForPair(RemotePhotoGroup group, int pairIndex)
        {
            if (group == null || group.targets == null)
            {
                return null;
            }

            int slot = GetSyncedSlotForPair(group, pairIndex);
            if (slot < 0 || slot >= group.targets.Length)
            {
                return null;
            }

            return group.targets[slot];
        }

        private void TryFulfillActiveRandomRequest()
        {
            if (!_activeRandomRequestActive || _activeRandomGroup == null || _activeRandomSlots == null)
            {
                return;
            }

            RemotePhotoFrame[] targets = _activeRandomGroup.targets;
            while (_activeRandomNextSlotIndex < _activeRandomSlots.Length)
            {
                int slot = _activeRandomSlots[_activeRandomNextSlotIndex];
                if (targets == null || slot < 0 || slot >= targets.Length)
                {
                    _activeRandomNextSlotIndex++;
                    continue;
                }

                RemotePhotoFrame frame = targets[slot];
                if (frame == null)
                {
                    _activeRandomNextSlotIndex++;
                    continue;
                }

                int cacheIndex = FindReadyCacheIndexForOrientation(frame.orientation == RemotePhotoOrientation.Landscape);
                if (cacheIndex < 0)
                {
                    break;
                }

                VRCUrl url = _cachedUrls[cacheIndex];
                Texture2D texture = _cachedTextures[cacheIndex];
                if (!RemotePhotoUrlUtility.IsValidVrcUrl(url) || texture == null)
                {
                    ClearCachedTextureAt(cacheIndex);
                    continue;
                }

                if (_activeRandomGroup.ApplyRandomPreloadSlotFromManager(slot, url, texture, this, _activeRandomRequestId))
                {
                    MoveCachedDownloadToDisplayedFrame(frame, GetSafeUrlString(url), _cachedDownloads[cacheIndex]);
                    ClearCachedTextureWithoutDisposingAt(cacheIndex);
                    _activeRandomNextSlotIndex++;
                    continue;
                }

                break;
            }

            if (_activeRandomNextSlotIndex >= _activeRandomSlots.Length)
            {
                CompleteActiveRandomRequest();
            }
        }

        private void CompleteActiveRandomRequest()
        {
            LogDebug("Random request completed.");
            _activeRandomRequestActive = false;
            _activeRandomGroup = null;
            _activeRandomSlots = new int[0];
            _activeRandomRequestId = 0;
            _activeRandomNextSlotIndex = 0;
            lastPreloadStatus = "Random request completed.";
        }

        private void CancelActiveRandomRequest()
        {
            _activeRandomRequestActive = false;
            _activeRandomGroup = null;
            _activeRandomSlots = new int[0];
            _activeRandomRequestId = 0;
            _activeRandomNextSlotIndex = 0;
            lastPreloadStatus = "Random request replaced.";
        }

        private void CancelPreloadIfActiveRandomNeedsPriority()
        {
            if (!_activeRandomRequestActive ||
                !_preloadInProgress ||
                !RemotePhotoUrlUtility.IsValidVrcUrl(_activePreloadUrl))
            {
                return;
            }

            int activeOrientation = GetActiveRandomNextOrientation();
            if (activeOrientation < 0)
            {
                return;
            }

            bool activeLandscape = activeOrientation == 0;
            if (_activePreloadLandscape == activeLandscape)
            {
                return;
            }

            LogDebug("Cancelling background preload because active random request needs " + (activeLandscape ? "landscape" : "portrait") + ".");
            CancelPreloadForQueueMutation();
        }

        private int GetActiveRandomNextOrientation()
        {
            if (!_activeRandomRequestActive || _activeRandomGroup == null || _activeRandomSlots == null)
            {
                return -1;
            }

            RemotePhotoFrame[] targets = _activeRandomGroup.targets;
            int index = _activeRandomNextSlotIndex;
            while (index < _activeRandomSlots.Length)
            {
                int slot = _activeRandomSlots[index];
                if (targets != null && slot >= 0 && slot < targets.Length && targets[slot] != null)
                {
                    return targets[slot].orientation == RemotePhotoOrientation.Landscape ? 0 : 1;
                }

                index++;
            }

            return -1;
        }

        private bool TryConsumeDownloadedTextureForActiveRandom(VRCUrl url, Texture2D texture, IVRCImageDownload download)
        {
            if (!_activeRandomRequestActive ||
                _activeRandomGroup == null ||
                _activeRandomSlots == null ||
                !RemotePhotoUrlUtility.IsValidVrcUrl(url) ||
                texture == null ||
                download == null)
            {
                return false;
            }

            RemotePhotoFrame[] targets = _activeRandomGroup.targets;
            while (_activeRandomNextSlotIndex < _activeRandomSlots.Length)
            {
                int slot = _activeRandomSlots[_activeRandomNextSlotIndex];
                if (targets == null || slot < 0 || slot >= targets.Length)
                {
                    _activeRandomNextSlotIndex++;
                    continue;
                }

                RemotePhotoFrame frame = targets[slot];
                if (frame == null)
                {
                    _activeRandomNextSlotIndex++;
                    continue;
                }

                bool targetLandscape = frame.orientation == RemotePhotoOrientation.Landscape;
                if (!IsUrlInOrientationPool(targetLandscape, url))
                {
                    return false;
                }

                if (_activeRandomGroup.ApplyRandomPreloadSlotFromManager(slot, url, texture, this, _activeRandomRequestId))
                {
                    MoveCachedDownloadToDisplayedFrame(frame, GetSafeUrlString(url), download);
                    _activeRandomNextSlotIndex++;
                    TryFulfillActiveRandomRequest();
                    return true;
                }

                return false;
            }

            CompleteActiveRandomRequest();
            return false;
        }

        private int CountCurrentSyncedUrls(bool landscape)
        {
            int count = 0;
            int groupIndex = 0;
            while (managedGroups != null && groupIndex < managedGroups.Length)
            {
                RemotePhotoGroup group = managedGroups[groupIndex];
                VRCUrl[] urls = group == null ? null : group.syncedUrls;
                int pairIndex = 0;
                while (urls != null && pairIndex < urls.Length)
                {
                    RemotePhotoFrame target = GetSyncedFrameForPair(group, pairIndex);
                    bool targetLandscape = target != null && target.orientation == RemotePhotoOrientation.Landscape;
                    if (target != null &&
                        targetLandscape == landscape &&
                        RemotePhotoUrlUtility.IsValidVrcUrl(urls[pairIndex]) &&
                        !IsFrameDisplayingUrl(target, GetSafeUrlString(urls[pairIndex])))
                    {
                        count++;
                    }

                    pairIndex++;
                }

                groupIndex++;
            }

            return count;
        }

        public void WakePreloadQueue()
        {
            if (!IsPreloadEnabled())
            {
                return;
            }

            if (!_preloadInProgress)
            {
                LogDebug("Preload queue wake requested.");
                StartPreloadingQueue();
            }
        }

        public void EnsurePredictionQueue()
        {
            RefreshPreloadPredictions();
        }

        public void RegisterPreloadDownloadMaterial(Material material)
        {
            if (material == null || ContainsPreloadDownloadMaterial(material))
            {
                return;
            }

            AddPreloadDownloadMaterial(material);
            LogDebug("Preload download material registered: " + material.name);
            StartPreloadingQueue();
        }

        public bool IsTextureRequestFailed(VRCUrl url)
        {
            return IsKnownFailedUrl(url);
        }

        public void ConsumeCachedTexture(VRCUrl url, RemotePhotoFrame frame)
        {
            string urlString = GetSafeUrlString(url);
            if (string.IsNullOrEmpty(urlString))
            {
                return;
            }

            int index = FindCachedUrlIndex(urlString);
            if (index < 0)
            {
                return;
            }

            MoveCachedDownloadToDisplayedFrame(frame, urlString, _cachedDownloads[index]);
            _cachedUrlStrings[index] = string.Empty;
            _cachedUrls[index] = null;
            _cachedTextures[index] = null;
            _cachedDownloads[index] = null;
            _cachedAccessTicks[index] = 0;

            LogDebug("Cache used by frame: " + urlString + ". Cached=" + GetCachedTextureCount() + "/" + GetMaxCachedTextures());
            StartPreloadingQueue();
        }

        public void RetainCachedTextureForFrame(VRCUrl url, RemotePhotoFrame frame)
        {
            string urlString = GetSafeUrlString(url);
            if (string.IsNullOrEmpty(urlString))
            {
                return;
            }

            int index = FindCachedUrlIndex(urlString);
            if (index < 0)
            {
                return;
            }

            _cacheAccessTick++;
            _cachedAccessTicks[index] = _cacheAccessTick;
            MoveCachedDownloadToDisplayedFrame(frame, urlString, _cachedDownloads[index]);
            if (IsCurrentSyncedUrlString(urlString) || !IsUrlInPreloadWindowString(urlString))
            {
                ClearCachedTextureWithoutDisposingAt(index);
            }

            LogDebug("Cache retained by frame: " + urlString + ". Cached=" + GetCachedTextureCount() + "/" + GetMaxCachedTextures());
            StartPreloadingQueue();
        }

        public void RequestPrepareForCounts(int landscapeCount, int portraitCount)
        {
            ApplyBakedGallery();
            if (!hasGalleryData)
            {
                lastPreloadStatus = lastGalleryError;
                LogDebug("Prepare skipped: " + lastGalleryError);
                return;
            }

            if (!Networking.IsOwner(gameObject))
            {
                lastPreloadStatus = "Waiting for manager owner preload order.";
                LogDebug("Prepare skipped on non-owner. Waiting for synced preload order.");
                return;
            }

            if (configuredPlayMode == RemotePhotoPlayMode.Random)
            {
                StartPreloadingQueue();
                return;
            }

            int desiredLandscapeCount = GetDesiredPrepareCount(true, landscapeCount, GetLandscapeCount());
            int desiredPortraitCount = GetDesiredPrepareCount(false, portraitCount, GetPortraitCount());
            LogDebug("Prepare requested. Need L=" + landscapeCount + ", P=" + portraitCount + ", Desired L=" + desiredLandscapeCount + ", P=" + desiredPortraitCount);

            _sequencePredictLandscapeStep = 0;
            _sequencePredictPortraitStep = 0;
            EnsurePreloadPoolSize(desiredLandscapeCount, desiredPortraitCount);
            if (_preloadInProgress && !string.IsNullOrEmpty(_activePreloadUrlString) && !IsUrlInPreloadWindowString(_activePreloadUrlString))
            {
                LogDebug("Preload active URL is obsolete. Cancelling: " + _activePreloadUrlString);
                CancelPreloadForQueueMutation();
            }

            _preloadScanOrderedIndex = 0;
            _preloadScanOrientation = 0;
            _preloadScanIndex = 0;
            preloadReady = !IsPreloadEnabled();
            preloadRevision++;
            lastPreloadStatus = preloadReady ? "Preload disabled." : "Preloading prediction cache.";
            LogDebug("Sequence prediction pool rebuilt. Ordered=" + (sequencePreloadOrderedUrls == null ? 0 : sequencePreloadOrderedUrls.Length) + ", Landscape=" + (sequencePreloadLandscapeUrls == null ? 0 : sequencePreloadLandscapeUrls.Length) + ", Portrait=" + (sequencePreloadPortraitUrls == null ? 0 : sequencePreloadPortraitUrls.Length));
            RequestSerialization();
            StartPreloadingQueue();
        }

        public Texture2D GetCachedTexture(VRCUrl url)
        {
            return GetCachedTextureInternal(url, true);
        }

        public Texture2D GetCachedTextureQuiet(VRCUrl url)
        {
            return GetCachedTextureInternal(url, false);
        }

        private Texture2D GetCachedTextureInternal(VRCUrl url, bool logMiss)
        {
            string urlString = GetSafeUrlString(url);
            if (string.IsNullOrEmpty(urlString))
            {
                return null;
            }

            int index = FindCachedUrlIndex(urlString);
            if (index < 0)
            {
                if (logMiss)
                {
                    LogDebug("Cache miss: " + urlString);
                }

                return null;
            }

            _cacheAccessTick++;
            _cachedAccessTicks[index] = _cacheAccessTick;
            LogDebug("Cache hit: " + urlString);
            return _cachedTextures[index];
        }

        private bool StoreCachedDownload(VRCUrl url, Texture2D texture, IVRCImageDownload download)
        {
            return StoreCachedTextureInternal(url, texture, download);
        }

        private bool StoreCachedTextureInternal(VRCUrl url, Texture2D texture, IVRCImageDownload download)
        {
            string urlString = GetSafeUrlString(url);
            if (string.IsNullOrEmpty(urlString) || texture == null || GetMaxCachedTextures() <= 0)
            {
                return false;
            }

            EnsureCacheArrays();
            int existingIndex = FindCachedUrlIndex(urlString);
            _cacheAccessTick++;
            if (existingIndex >= 0)
            {
                IVRCImageDownload previousDownload = _cachedDownloads[existingIndex];
                if (download != null && previousDownload != null && previousDownload != download)
                {
                    previousDownload.Dispose();
                }

                _cachedUrlStrings[existingIndex] = urlString;
                _cachedUrls[existingIndex] = url;
                _cachedTextures[existingIndex] = texture;
                if (download != null)
                {
                    _cachedDownloads[existingIndex] = download;
                }

                _cachedAccessTicks[existingIndex] = _cacheAccessTick;
                LogDebug("Cache updated: " + urlString);
                return true;
            }

            int storeIndex = FindCacheStoreIndex(urlString);

            if (storeIndex < 0)
            {
                LogDebug("Cache store skipped because protected preload entries fill the cache: " + urlString);
                return false;
            }

            DisposeCachedDownloadAt(storeIndex);

            _cachedUrlStrings[storeIndex] = urlString;
            _cachedUrls[storeIndex] = url;
            _cachedTextures[storeIndex] = texture;
            _cachedDownloads[storeIndex] = download;
            _cachedAccessTicks[storeIndex] = _cacheAccessTick;
            LogDebug("Cache stored: " + urlString);
            return true;
        }

        public int GetCachedTextureCount()
        {
            EnsureCacheArrays();
            int count = 0;
            int index = 0;
            while (index < _cachedTextures.Length)
            {
                if (_cachedTextures[index] != null && _cachedDownloads[index] != null && !string.IsNullOrEmpty(_cachedUrlStrings[index]))
                {
                    count++;
                }

                index++;
            }

            return count;
        }

        public override void OnImageLoadSuccess(IVRCImageDownload result)
        {
            if (!_preloadInProgress ||
                result == null ||
                result.Result == null ||
                result.Url == null ||
                result.Url.Get() != _activePreloadUrlString)
            {
                return;
            }

            if (configuredPlayMode == RemotePhotoPlayMode.Random &&
                _activeRandomRequestActive &&
                TryConsumeDownloadedTextureForActiveRandom(result.Url, result.Result, result))
            {
                _currentPreloadDownload = null;
                _activePreloadRetryCount = 0;
                AdvancePreloadCursor();
                ScheduleNextPreloadDownload();
                return;
            }

            bool stored = StoreCachedDownload(result.Url, result.Result, result);
            LogDebug(stored ? "Preload success: " + _activePreloadUrlString : "Preload discarded: " + _activePreloadUrlString);
            if (stored)
            {
                _currentPreloadDownload = null;
                if (configuredPlayMode == RemotePhotoPlayMode.Random)
                {
                    TryFulfillActiveRandomRequest();
                }
                else
                {
                    NotifyGroupsForCachedUrl(result.Url);
                }
            }
            else
            {
                DisposeCurrentPreloadDownload();
            }

            _activePreloadRetryCount = 0;
            AdvancePreloadCursor();
            ScheduleNextPreloadDownload();
        }

        public override void OnImageLoadError(IVRCImageDownload result)
        {
            if (!_preloadInProgress)
            {
                return;
            }

            if (result != null && result.Url != null && result.Url.Get() != _activePreloadUrlString)
            {
                return;
            }

            string errorDetails = GetImageDownloadErrorDetails(result);
            LogDebug("Preload error: " + _activePreloadUrlString + errorDetails);
            DisposeCurrentPreloadDownload();
            if (IsNonRetryableImageError(result))
            {
                if (IsCurrentSyncedUrlString(_activePreloadUrlString))
                {
                    MarkFailedUrl(_activePreloadUrlString);
                    LogDebug("Current selection preload non-retryable error; frame will finish locally: " + _activePreloadUrlString);
                    NotifyGroupsForCachedUrl(_activePreloadUrl);
                    _activePreloadRetryCount = 0;
                    AdvancePreloadCursor();
                    ScheduleNextPreloadDownload();
                    return;
                }

                if (configuredPlayMode != RemotePhotoPlayMode.Random)
                {
                    LogDebug("Sequence preload non-retryable error; keeping URL in sequence queue for future retries: " + _activePreloadUrlString);
                    _activePreloadRetryCount = 0;
                    AdvancePreloadCursor();
                    ScheduleNextPreloadDownload();
                    return;
                }

                MarkFailedUrl(_activePreloadUrlString);
                LogDebug("Random preload non-retryable error; selecting another random URL: " + _activePreloadUrlString);
                AdvancePreloadCursor();
                _activePreloadRetryCount = 0;
                ScheduleNextPreloadDownload();
                return;
            }

            if (_activePreloadRetryCount < GetImageRetryAttempts())
            {
                _activePreloadRetryCount++;
                float retryDelay = GetImageRetryDelaySeconds();
                LogDebug("Preload retry " + _activePreloadRetryCount + "/" + GetImageRetryAttempts() + " in " + retryDelay + "s: " + _activePreloadUrlString);
                SendCustomEventDelayedSeconds(nameof(_RetryActivePreloadDownload), retryDelay);
                return;
            }

            if (configuredPlayMode != RemotePhotoPlayMode.Random)
            {
                if (IsCurrentSyncedUrlString(_activePreloadUrlString))
                {
                    MarkFailedUrl(_activePreloadUrlString);
                    LogDebug("Current selection preload failed after retries; frame will finish locally: " + _activePreloadUrlString);
                    NotifyGroupsForCachedUrl(_activePreloadUrl);
                    _activePreloadRetryCount = 0;
                    AdvancePreloadCursor();
                    ScheduleNextPreloadDownload();
                    return;
                }

                LogDebug("Sequence preload failed after retries; keeping URL in sequence queue for future retries: " + _activePreloadUrlString);
                _activePreloadRetryCount = 0;
                AdvancePreloadCursor();
                ScheduleNextPreloadDownload();
                return;
            }

            if (IsCurrentSyncedUrlString(_activePreloadUrlString))
            {
                MarkFailedUrl(_activePreloadUrlString);
                LogDebug("Current selection preload failed after retries; frame will finish locally: " + _activePreloadUrlString);
                _activePreloadRetryCount = 0;
                AdvancePreloadCursor();
                ScheduleNextPreloadDownload();
                return;
            }

            MarkFailedUrl(_activePreloadUrlString);
            LogDebug("Random preload failed after retries; selecting another random URL: " + _activePreloadUrlString);
            AdvancePreloadCursor();
            _activePreloadRetryCount = 0;
            ScheduleNextPreloadDownload();
        }

        public void _RetryActivePreloadDownload()
        {
            if (!_preloadInProgress || !RemotePhotoUrlUtility.IsValidVrcUrl(_activePreloadUrl))
            {
                LogDebug("Preload retry skipped because active URL is no longer valid.");
                return;
            }

            LogDebug("Preload retry start: " + _activePreloadUrlString);
            StartActivePreloadDownload();
        }

        public int GetImageRetryAttempts()
        {
            return Mathf.Max(0, retryAttempts);
        }

        public float GetImageRetryDelaySeconds()
        {
            return Mathf.Max(0f, retryDelaySeconds);
        }

        public void _ContinuePreloadDownload()
        {
            if (!_preloadContinueScheduled)
            {
                return;
            }

            _preloadContinueScheduled = false;
            if (!_preloadInProgress)
            {
                return;
            }

            StartNextPreloadDownload();
        }

        private void StartPreloadingQueue()
        {
            if (!IsPreloadEnabled())
            {
                preloadReady = sequencePreloadLandscapeUrls != null && sequencePreloadPortraitUrls != null;
                lastPreloadStatus = "Preload disabled.";
                LogDebug("Preload disabled. Prediction cache is inactive.");
                if (Networking.IsOwner(gameObject))
                {
                    RequestSerialization();
                }

                return;
            }

            if (_preloadInProgress || (GetMaxCachedTextures() <= 0 && !_activeRandomRequestActive))
            {
                if (GetMaxCachedTextures() <= 0 && !_activeRandomRequestActive)
                {
                    LogDebug("Preload skipped because cache size is 0.");
                }

                return;
            }

            EnsurePreloadDownloader();
            EnsureCacheArrays();
            _preloadInProgress = true;
            _preloadScanOrientation = 0;
            _preloadScanIndex = 0;
            _preloadScanOrderedIndex = 0;
            _activePreloadRetryCount = 0;
            _preloadContinueScheduled = false;
            LogDebug("Preload scan started.");
            StartNextPreloadDownload();
        }

        private void StartNextPreloadDownload()
        {
            if (!_preloadInProgress)
            {
                return;
            }

            if (configuredPlayMode != RemotePhotoPlayMode.Random &&
                CountCachedPreloadWindowUrls() >= GetFutureCacheCapacity() &&
                !HasUncachedCurrentSyncedUrl())
            {
                FinishPreloadingQueue();
                return;
            }

            VRCUrl nextUrl = FindNextUncachedPreloadUrl();
            if (!RemotePhotoUrlUtility.IsValidVrcUrl(nextUrl))
            {
                FinishPreloadingQueue();
                return;
            }

            _activePreloadUrl = nextUrl;
            _activePreloadUrlString = nextUrl.Get();
            LogDebug("Preload start: " + _activePreloadUrlString);
            StartActivePreloadDownload();
        }

        private void ScheduleNextPreloadDownload()
        {
            if (!_preloadInProgress)
            {
                return;
            }

            float delay = PreloadDownloadIntervalSeconds;
            if (delay <= 0f)
            {
                StartNextPreloadDownload();
                return;
            }

            _preloadContinueScheduled = true;
            LogDebug("Preload next download scheduled in " + delay + "s.");
            SendCustomEventDelayedSeconds(nameof(_ContinuePreloadDownload), delay);
        }

        private void StartActivePreloadDownload()
        {
            if (!RemotePhotoUrlUtility.IsValidVrcUrl(_activePreloadUrl))
            {
                StartNextPreloadDownload();
                return;
            }

            EnsurePreloadDownloader();
            _preloadTextureInfo = new TextureInfo();
            _preloadTextureInfo.GenerateMipMaps = false;
            _preloadTextureInfo.MaterialProperty = PreloadTexturePropertyName;
            _preloadTextureInfo.WrapModeU = TextureWrapMode.Clamp;
            _preloadTextureInfo.WrapModeV = TextureWrapMode.Clamp;
            preloadDownloadMaterial = PickNextPreloadDownloadMaterial();
            if (preloadDownloadMaterial == null)
            {
                _preloadInProgress = false;
                lastPreloadStatus = "Waiting for preload download material.";
                LogDebug("Preload paused because no preload download material is registered.");
                return;
            }

            _currentPreloadDownload = _preloadDownloader.DownloadImage(_activePreloadUrl, preloadDownloadMaterial, (IUdonEventReceiver)this, _preloadTextureInfo);
        }

        private void AddPreloadDownloadMaterial(Material material)
        {
            if (material == null)
            {
                return;
            }

            int length = preloadDownloadMaterials == null ? 0 : preloadDownloadMaterials.Length;
            Material[] next = new Material[length + 1];
            int index = 0;
            while (index < length)
            {
                next[index] = preloadDownloadMaterials[index];
                index++;
            }

            next[length] = material;
            preloadDownloadMaterials = next;
            preloadDownloadMaterial = PickNextPreloadDownloadMaterial();
        }

        private bool ContainsPreloadDownloadMaterial(Material material)
        {
            if (material == null || preloadDownloadMaterials == null)
            {
                return false;
            }

            int index = 0;
            while (index < preloadDownloadMaterials.Length)
            {
                if (preloadDownloadMaterials[index] == material)
                {
                    return true;
                }

                index++;
            }

            return false;
        }

        private Material PickNextPreloadDownloadMaterial()
        {
            if (preloadDownloadMaterials == null || preloadDownloadMaterials.Length == 0)
            {
                return null;
            }

            int attempts = 0;
            while (attempts < preloadDownloadMaterials.Length)
            {
                int index = PositiveModulo(_nextPreloadMaterialIndex, preloadDownloadMaterials.Length);
                _nextPreloadMaterialIndex = index + 1;
                Material material = preloadDownloadMaterials[index];
                if (material != null)
                {
                    return material;
                }

                attempts++;
            }

            return null;
        }

        private VRCUrl FindNextUncachedPreloadUrl()
        {
            if (configuredPlayMode == RemotePhotoPlayMode.Random && _activeRandomRequestActive)
            {
                VRCUrl activeRandomUrl = FindNextRandomPreloadUrl();
                if (RemotePhotoUrlUtility.IsValidVrcUrl(activeRandomUrl))
                {
                    return activeRandomUrl;
                }
            }

            VRCUrl currentUrl = FindNextUncachedCurrentSyncedUrl();
            if (RemotePhotoUrlUtility.IsValidVrcUrl(currentUrl))
            {
                SetActivePreloadLocation(currentUrl);
                return currentUrl;
            }

            if (HasPendingCurrentSyncedDisplay())
            {
                return null;
            }

            if (configuredPlayMode == RemotePhotoPlayMode.Random)
            {
                return FindNextRandomPreloadUrl();
            }

            while (sequencePreloadOrderedUrls != null && _preloadScanOrderedIndex < sequencePreloadOrderedUrls.Length)
            {
                VRCUrl url = sequencePreloadOrderedUrls[_preloadScanOrderedIndex];
                string urlString = GetSafeUrlString(url);
                SetActivePreloadLocation(url);
                _preloadScanOrderedIndex++;

                if (RemotePhotoUrlUtility.IsValidVrcUrl(url) &&
                    !IsCurrentSyncedUrlString(urlString) &&
                    !IsDisplayedUrlString(urlString) &&
                    GetCachedTextureQuiet(url) == null)
                {
                    return url;
                }
            }

            while (_preloadScanOrientation < 2)
            {
                VRCUrl[] source = _preloadScanOrientation == 0 ? sequencePreloadLandscapeUrls : sequencePreloadPortraitUrls;
                while (source != null && _preloadScanIndex < source.Length)
                {
                    VRCUrl url = source[_preloadScanIndex];
                    string urlString = GetSafeUrlString(url);
                    _activePreloadLandscape = _preloadScanOrientation == 0;
                    _preloadScanIndex++;

                    if (RemotePhotoUrlUtility.IsValidVrcUrl(url) &&
                        !IsCurrentSyncedUrlString(urlString) &&
                        !IsDisplayedUrlString(urlString) &&
                        GetCachedTextureQuiet(url) == null)
                    {
                        return url;
                    }
                }

                _preloadScanOrientation++;
                _preloadScanIndex = 0;
            }

            return null;
        }

        private VRCUrl FindNextRandomPreloadUrl()
        {
            if (_activeRandomRequestActive)
            {
                VRCUrl activeUrl = BuildRandomUrlForActiveRequest();
                if (RemotePhotoUrlUtility.IsValidVrcUrl(activeUrl))
                {
                    SetActivePreloadLocation(activeUrl);
                    return activeUrl;
                }
            }

            int orientation = FindNextReadyPoolOrientation();
            if (orientation >= 0)
            {
                VRCUrl readyPoolUrl = BuildRandomReadyPoolUrl(orientation == 0);
                if (RemotePhotoUrlUtility.IsValidVrcUrl(readyPoolUrl))
                {
                    SetActivePreloadLocation(readyPoolUrl);
                    return readyPoolUrl;
                }
            }

            return null;
        }

        private int FindNextReadyPoolOrientation()
        {
            bool landscapeNeedsCache = CountReadyCachedUrls(true) < GetConfiguredPreloadCapacity(true);
            bool portraitNeedsCache = CountReadyCachedUrls(false) < GetConfiguredPreloadCapacity(false);
            if (!landscapeNeedsCache && !portraitNeedsCache)
            {
                return -1;
            }

            int groupCount = managedGroups == null ? 0 : managedGroups.Length;
            int attempts = 0;
            while (attempts < groupCount + 1)
            {
                int groupIndex = PositiveModulo(_nextReadyPoolGroupIndex, groupCount <= 0 ? 1 : groupCount);
                RemotePhotoGroup group = groupCount <= 0 ? null : managedGroups[groupIndex];
                RemotePhotoFrame[] targets = group == null ? null : group.targets;
                int targetCount = targets == null ? 0 : targets.Length;
                int targetAttempts = 0;
                while (targetAttempts < targetCount)
                {
                    int targetIndex = PositiveModulo(_nextReadyPoolTargetIndex, targetCount);
                    _nextReadyPoolTargetIndex = targetIndex + 1;
                    RemotePhotoFrame target = targets[targetIndex];
                    if (target != null)
                    {
                        bool landscape = target.orientation == RemotePhotoOrientation.Landscape;
                        if (landscape && landscapeNeedsCache && GetLandscapeCount() > 0)
                        {
                            return 0;
                        }

                        if (!landscape && portraitNeedsCache && GetPortraitCount() > 0)
                        {
                            return 1;
                        }
                    }

                    targetAttempts++;
                }

                _nextReadyPoolGroupIndex = groupIndex + 1;
                _nextReadyPoolTargetIndex = 0;
                attempts++;
            }

            if (landscapeNeedsCache && GetLandscapeCount() > 0)
            {
                return 0;
            }

            if (portraitNeedsCache && GetPortraitCount() > 0)
            {
                return 1;
            }

            return -1;
        }

        private bool HasCacheStoreRoomForFuturePreload()
        {
            int futureLimit = GetFutureCacheCapacity();
            if (FindEmptyCacheIndexWithin(futureLimit) >= 0)
            {
                return true;
            }

            return FindLeastRecentlyUsedCacheIndexWithRules(false, false, futureLimit) >= 0;
        }

        private VRCUrl BuildRandomUrlForActiveRequest()
        {
            if (!_activeRandomRequestActive || _activeRandomGroup == null || _activeRandomSlots == null)
            {
                return null;
            }

            RemotePhotoFrame[] targets = _activeRandomGroup.targets;
            int index = _activeRandomNextSlotIndex;
            while (index < _activeRandomSlots.Length)
            {
                int slot = _activeRandomSlots[index];
                if (targets != null && slot >= 0 && slot < targets.Length && targets[slot] != null)
                {
                    bool landscape = targets[slot].orientation == RemotePhotoOrientation.Landscape;
                    return BuildRandomReadyPoolCandidate(landscape, true);
                }

                index++;
            }

            return null;
        }

        private VRCUrl BuildRandomReadyPoolUrl(bool landscape)
        {
            int limit = GetConfiguredPreloadCapacity(landscape);
            if (limit <= 0 || CountReadyCachedUrls(landscape) >= limit)
            {
                return null;
            }

            return BuildRandomReadyPoolCandidate(landscape, false);
        }

        private VRCUrl BuildRandomReadyPoolCandidate(bool landscape, bool allowCurrentOrDisplayedFallback)
        {
            int poolCount = landscape ? GetLandscapeCount() : GetPortraitCount();
            if (poolCount <= 0)
            {
                return null;
            }

            int start = Random.Range(0, poolCount);
            int guard = 0;
            while (guard < poolCount)
            {
                int poolIndex = PositiveModulo(start + guard, poolCount);
                VRCUrl url = landscape ? GetLandscapeUrl(poolIndex) : GetPortraitUrl(poolIndex);
                string urlString = GetSafeUrlString(url);
                if (RemotePhotoUrlUtility.IsValidVrcUrl(url) &&
                    !IsKnownFailedUrl(url) &&
                    !IsReadyCachedUrlString(urlString) &&
                    !IsDisplayedUrlString(urlString) &&
                    !IsCurrentSyncedUrlString(urlString) &&
                    _activePreloadUrlString != urlString)
                {
                    return url;
                }

                guard++;
            }

            guard = 0;
            while (guard < poolCount)
            {
                int poolIndex = PositiveModulo(start + guard, poolCount);
                VRCUrl url = landscape ? GetLandscapeUrl(poolIndex) : GetPortraitUrl(poolIndex);
                string urlString = GetSafeUrlString(url);
                if (RemotePhotoUrlUtility.IsValidVrcUrl(url) &&
                    !IsKnownFailedUrl(url) &&
                    !IsReadyCachedUrlString(urlString) &&
                    (allowCurrentOrDisplayedFallback || (!IsDisplayedUrlString(urlString) && !IsCurrentSyncedUrlString(urlString))) &&
                    _activePreloadUrlString != urlString)
                {
                    return url;
                }

                guard++;
            }

            return null;
        }

        private void AdvancePreloadCursor()
        {
            _activePreloadUrl = null;
            _activePreloadUrlString = string.Empty;
        }

        private void FinishPreloadingQueue()
        {
            _preloadInProgress = false;
            _preloadContinueScheduled = false;
            _activePreloadUrl = null;
            _activePreloadUrlString = string.Empty;
            preloadReady = true;
            lastPreloadStatus = "Prediction cache ready.";
            LogDebug("Preload scan finished. Prediction cache ready.");
            if (Networking.IsOwner(gameObject))
            {
                RequestSerialization();
            }
        }

        private void SetActivePreloadLocation(VRCUrl url)
        {
            _activePreloadLandscape = IsUrlInOrientationPool(true, url);
            if (configuredPlayMode == RemotePhotoPlayMode.Random)
            {
                return;
            }

            string urlString = GetSafeUrlString(url);
            int index = FindUrlInArray(sequencePreloadLandscapeUrls, urlString);
            if (index >= 0)
            {
                _activePreloadLandscape = true;
                return;
            }

            index = FindUrlInArray(sequencePreloadPortraitUrls, urlString);
            if (index >= 0)
            {
                _activePreloadLandscape = false;
            }
        }

        private int FindUrlInArray(VRCUrl[] source, string urlString)
        {
            if (source == null || string.IsNullOrEmpty(urlString))
            {
                return -1;
            }

            int index = 0;
            while (index < source.Length)
            {
                if (GetSafeUrlString(source[index]) == urlString)
                {
                    return index;
                }

                index++;
            }

            return -1;
        }

        private bool HasOrderedPreloadGroups()
        {
            return managedGroups != null && managedGroups.Length > 0;
        }

        private bool ShouldSkipSequenceNonFocusGroup(int groupIndex)
        {
            return configuredPlayMode != RemotePhotoPlayMode.Random &&
                sequenceFocusGroupIndex >= 0 &&
                groupIndex != sequenceFocusGroupIndex;
        }

        private bool IsUrlInPreloadWindowString(string url)
        {
            if (string.IsNullOrEmpty(url))
            {
                return false;
            }

            if (configuredPlayMode == RemotePhotoPlayMode.Random)
            {
                return IsReadyCachedUrlString(url);
            }

            return FindUrlInArray(sequencePreloadOrderedUrls, url) >= 0 ||
                   FindUrlInArray(sequencePreloadLandscapeUrls, url) >= 0 ||
                   FindUrlInArray(sequencePreloadPortraitUrls, url) >= 0;
        }

        private bool IsCurrentSyncedUrlString(string url)
        {
            if (string.IsNullOrEmpty(url))
            {
                return false;
            }

            int groupIndex = 0;
            while (managedGroups != null && groupIndex < managedGroups.Length)
            {
                RemotePhotoGroup group = managedGroups[groupIndex];
                VRCUrl[] urls = group == null ? null : group.syncedUrls;
                int index = 0;
                while (urls != null && index < urls.Length)
                {
                    if (GetSafeUrlString(urls[index]) == url)
                    {
                        return true;
                    }

                    index++;
                }

                groupIndex++;
            }

            return false;
        }

        private void NotifyGroupsForCachedUrl(VRCUrl url)
        {
            string urlString = GetSafeUrlString(url);
            if (string.IsNullOrEmpty(urlString))
            {
                return;
            }

            int groupIndex = 0;
            while (managedGroups != null && groupIndex < managedGroups.Length)
            {
                RemotePhotoGroup group = managedGroups[groupIndex];
                VRCUrl[] urls = group == null ? null : group.syncedUrls;
                int index = 0;
                bool matched = false;
                while (urls != null && index < urls.Length)
                {
                    if (GetSafeUrlString(urls[index]) == urlString)
                    {
                        matched = true;
                        break;
                    }

                    index++;
                }

                if (matched)
                {
                    group.RefreshCurrentSelectionFromManager();
                }

                groupIndex++;
            }
        }

        private bool HasUncachedCurrentSyncedUrl()
        {
            return RemotePhotoUrlUtility.IsValidVrcUrl(FindNextUncachedCurrentSyncedUrl());
        }

        private VRCUrl FindNextUncachedCurrentSyncedUrl()
        {
            int groupIndex = 0;
            while (managedGroups != null && groupIndex < managedGroups.Length)
            {
                RemotePhotoGroup group = managedGroups[groupIndex];
                VRCUrl[] urls = group == null ? null : group.syncedUrls;
                int index = 0;
                while (urls != null && index < urls.Length)
                {
                    RemotePhotoFrame target = GetSyncedFrameForPair(group, index);
                    VRCUrl url = urls[index];
                    string urlString = GetSafeUrlString(url);
                    if (target != null &&
                        RemotePhotoUrlUtility.IsValidVrcUrl(url) &&
                        !IsFrameDisplayingUrl(target, urlString) &&
                        GetCachedTextureQuiet(url) == null)
                    {
                        return url;
                    }

                    index++;
                }

                groupIndex++;
            }

            return null;
        }

        private bool HasPendingCurrentSyncedDisplay()
        {
            int groupIndex = 0;
            while (managedGroups != null && groupIndex < managedGroups.Length)
            {
                RemotePhotoGroup group = managedGroups[groupIndex];
                VRCUrl[] urls = group == null ? null : group.syncedUrls;
                int index = 0;
                while (urls != null && index < urls.Length)
                {
                    RemotePhotoFrame target = GetSyncedFrameForPair(group, index);
                    VRCUrl url = urls[index];
                    if (target != null &&
                        RemotePhotoUrlUtility.IsValidVrcUrl(url) &&
                        !IsFrameDisplayingUrl(target, GetSafeUrlString(url)))
                    {
                        return true;
                    }

                    index++;
                }

                groupIndex++;
            }

            return false;
        }

        private int CountCachedPreloadWindowUrls()
        {
            int count = 0;
            int index = 0;
            if (configuredPlayMode == RemotePhotoPlayMode.Random)
            {
                return CountReadyCachedUrls(true) + CountReadyCachedUrls(false);
            }

            while (sequencePreloadOrderedUrls != null && index < sequencePreloadOrderedUrls.Length)
            {
                VRCUrl url = sequencePreloadOrderedUrls[index];
                string urlString = GetSafeUrlString(url);
                if (RemotePhotoUrlUtility.IsValidVrcUrl(url) &&
                    !IsCurrentSyncedUrlString(urlString) &&
                    !IsDisplayedUrlString(urlString) &&
                    GetCachedTextureQuiet(url) != null)
                {
                    count++;
                }

                index++;
            }

            return count;
        }

        private int CountReadyCachedUrls(bool landscape)
        {
            EnsureCacheArrays();
            int count = 0;
            int index = 0;
            int limit = GetFutureCacheCapacity();
            while (index < _cachedTextures.Length && index < limit)
            {
                VRCUrl url = _cachedUrls == null || index >= _cachedUrls.Length ? null : _cachedUrls[index];
                string urlString = _cachedUrlStrings == null || index >= _cachedUrlStrings.Length ? string.Empty : _cachedUrlStrings[index];
                if (_cachedTextures[index] != null &&
                    _cachedDownloads[index] != null &&
                    RemotePhotoUrlUtility.IsValidVrcUrl(url) &&
                    !IsCurrentSyncedUrlString(urlString) &&
                    !IsDisplayedUrlString(urlString) &&
                    IsUrlInOrientationPool(landscape, url))
                {
                    count++;
                }

                index++;
            }

            return count;
        }

        private bool IsReadyCachedUrlString(string urlString)
        {
            return FindCachedUrlIndex(urlString) >= 0;
        }

        private bool IsUrlInOrientationPool(bool landscape, VRCUrl url)
        {
            string urlString = GetSafeUrlString(url);
            if (string.IsNullOrEmpty(urlString))
            {
                return false;
            }

            return FindUrlInArray(landscape ? landscapeUrls : portraitUrls, urlString) >= 0;
        }

        private void EnsurePreloadPoolSize(int landscapeCount, int portraitCount)
        {
            if (HasOrderedPreloadGroups())
            {
                sequencePreloadOrderedUrls = BuildOrderedPreloadUrls(landscapeCount, portraitCount);
                sequencePreloadLandscapeUrls = ExtractPreloadUrlsByOrientation(true, sequencePreloadOrderedUrls, landscapeCount);
                sequencePreloadPortraitUrls = ExtractPreloadUrlsByOrientation(false, sequencePreloadOrderedUrls, portraitCount);
                return;
            }

            sequencePreloadLandscapeUrls = EnsurePreloadUrls(true, sequencePreloadLandscapeUrls, landscapeCount);
            sequencePreloadPortraitUrls = EnsurePreloadUrls(false, sequencePreloadPortraitUrls, portraitCount);
            sequencePreloadOrderedUrls = MergePreloadUrls(sequencePreloadLandscapeUrls, sequencePreloadPortraitUrls);
        }

        private VRCUrl[] BuildOrderedPreloadUrls(int landscapeCount, int portraitCount)
        {
            int desiredTotal = Mathf.Max(0, landscapeCount) + Mathf.Max(0, portraitCount);
            if (desiredTotal <= 0)
            {
                return new VRCUrl[0];
            }

            VRCUrl[] result = new VRCUrl[desiredTotal];
            int filled = 0;
            int landscapeFilled = 0;
            int portraitFilled = 0;
            int groupIndex = 0;
            while (managedGroups != null && groupIndex < managedGroups.Length && filled < desiredTotal)
            {
                if (ShouldSkipSequenceNonFocusGroup(groupIndex))
                {
                    groupIndex++;
                    continue;
                }

                RemotePhotoGroup group = managedGroups[groupIndex];
                VRCUrl[] urls = group == null ? null : group.syncedUrls;
                int pairIndex = 0;
                while (urls != null && pairIndex < urls.Length && filled < desiredTotal)
                {
                    RemotePhotoFrame target = GetSyncedFrameForPair(group, pairIndex);
                    VRCUrl currentUrl = urls[pairIndex];
                    if (target != null &&
                        RemotePhotoUrlUtility.IsValidVrcUrl(currentUrl) &&
                        !IsDisplayedUrlString(GetSafeUrlString(currentUrl)))
                    {
                        bool landscape = target.orientation == RemotePhotoOrientation.Landscape;
                        bool canFill = landscape ? landscapeFilled < landscapeCount : portraitFilled < portraitCount;
                        if (canFill && !ContainsUrl(result, filled, currentUrl))
                        {
                            result[filled] = currentUrl;
                            filled++;
                            if (landscape)
                            {
                                landscapeFilled++;
                            }
                            else
                            {
                                portraitFilled++;
                            }
                        }
                    }

                    pairIndex++;
                }

                groupIndex++;
            }

            groupIndex = 0;
            while (managedGroups != null && groupIndex < managedGroups.Length && filled < desiredTotal)
            {
                if (ShouldSkipSequenceNonFocusGroup(groupIndex))
                {
                    groupIndex++;
                    continue;
                }

                RemotePhotoGroup group = managedGroups[groupIndex];
                RemotePhotoFrame[] targets = group == null ? null : group.targets;
                int targetIndex = 0;
                while (targets != null && targetIndex < targets.Length && filled < desiredTotal)
                {
                    RemotePhotoFrame target = targets[targetIndex];
                    if (target != null)
                    {
                        bool landscape = target.orientation == RemotePhotoOrientation.Landscape;
                        bool canFill = landscape ? landscapeFilled < landscapeCount : portraitFilled < portraitCount;
                        if (canFill)
                        {
                            VRCUrl nextUrl = BuildPreloadCandidateUrl(landscape, result, filled);
                            if (RemotePhotoUrlUtility.IsValidVrcUrl(nextUrl))
                            {
                                result[filled] = nextUrl;
                                filled++;
                                if (landscape)
                                {
                                    landscapeFilled++;
                                }
                                else
                                {
                                    portraitFilled++;
                                }
                            }
                        }
                    }

                    targetIndex++;
                }

                groupIndex++;
            }

            while (landscapeFilled < landscapeCount && filled < desiredTotal)
            {
                VRCUrl nextUrl = BuildPreloadCandidateUrl(true, result, filled);
                if (!RemotePhotoUrlUtility.IsValidVrcUrl(nextUrl))
                {
                    break;
                }

                result[filled] = nextUrl;
                filled++;
                landscapeFilled++;
            }

            while (portraitFilled < portraitCount && filled < desiredTotal)
            {
                VRCUrl nextUrl = BuildPreloadCandidateUrl(false, result, filled);
                if (!RemotePhotoUrlUtility.IsValidVrcUrl(nextUrl))
                {
                    break;
                }

                result[filled] = nextUrl;
                filled++;
                portraitFilled++;
            }

            return CompactUrlArray(result, filled);
        }

        private VRCUrl BuildPreloadCandidateUrl(bool landscape, VRCUrl[] existing, int existingLength)
        {
            return configuredPlayMode == RemotePhotoPlayMode.Random
                ? BuildRandomUrl(landscape, existing, existingLength)
                : BuildNextPredictedSequentialUrl(landscape);
        }

        private VRCUrl[] ExtractPreloadUrlsByOrientation(bool landscape, VRCUrl[] orderedUrls, int desiredCount)
        {
            if (desiredCount <= 0)
            {
                return new VRCUrl[0];
            }

            VRCUrl[] result = new VRCUrl[desiredCount];
            int filled = 0;
            int index = 0;
            while (orderedUrls != null && index < orderedUrls.Length && filled < desiredCount)
            {
                VRCUrl url = orderedUrls[index];
                if (IsUrlInOrientationPool(landscape, url))
                {
                    result[filled] = url;
                    filled++;
                }

                index++;
            }

            return CompactUrlArray(result, filled);
        }

        private VRCUrl[] MergePreloadUrls(VRCUrl[] landscapePreload, VRCUrl[] portraitPreload)
        {
            int landscapeLength = landscapePreload == null ? 0 : landscapePreload.Length;
            int portraitLength = portraitPreload == null ? 0 : portraitPreload.Length;
            VRCUrl[] result = new VRCUrl[landscapeLength + portraitLength];
            int writeIndex = 0;
            int index = 0;
            while (index < landscapeLength)
            {
                result[writeIndex] = landscapePreload[index];
                writeIndex++;
                index++;
            }

            index = 0;
            while (index < portraitLength)
            {
                result[writeIndex] = portraitPreload[index];
                writeIndex++;
                index++;
            }

            return result;
        }

        private VRCUrl[] CompactUrlArray(VRCUrl[] source, int length)
        {
            if (length <= 0)
            {
                return new VRCUrl[0];
            }

            VRCUrl[] result = new VRCUrl[length];
            int index = 0;
            while (index < length && source != null && index < source.Length)
            {
                result[index] = source[index];
                index++;
            }

            return result;
        }

        private VRCUrl[] EnsurePreloadUrls(bool landscape, VRCUrl[] current, int desiredCount)
        {
            if (desiredCount <= 0)
            {
                return new VRCUrl[0];
            }

            VRCUrl[] result = new VRCUrl[desiredCount];
            int filled = 0;
            int index = 0;
            while (current != null && index < current.Length && filled < desiredCount)
            {
                VRCUrl existingUrl = current[index];
                if (RemotePhotoUrlUtility.IsValidVrcUrl(existingUrl) &&
                    (configuredPlayMode != RemotePhotoPlayMode.Random || !IsKnownFailedUrl(existingUrl)))
                {
                    result[filled] = existingUrl;
                    filled++;
                }

                index++;
            }

            while (filled < desiredCount)
            {
                VRCUrl nextUrl = configuredPlayMode == RemotePhotoPlayMode.Random
                    ? BuildRandomUrl(landscape, result, filled)
                    : BuildNextPredictedSequentialUrl(landscape);

                if (!RemotePhotoUrlUtility.IsValidVrcUrl(nextUrl))
                {
                    break;
                }

                result[filled] = nextUrl;
                filled++;
            }

            if (filled == desiredCount)
            {
                return result;
            }

            VRCUrl[] compact = new VRCUrl[filled];
            index = 0;
            while (index < filled)
            {
                compact[index] = result[index];
                index++;
            }

            return compact;
        }

        private VRCUrl SelectUrl(bool landscape, VRCUrl[] selectedUrls, int selectedLength)
        {
            VRCUrl selected = null;
            if (configuredPlayMode == RemotePhotoPlayMode.Random)
            {
                selected = BuildActualRandomUrl(landscape, selectedUrls, selectedLength);
            }
            else
            {
                selected = BuildNextActualSequentialUrl(landscape);
            }

            LogDebug("Actual selection " + (landscape ? "landscape" : "portrait") + ": " + GetSafeUrlString(selected));
            return selected;
        }

        private VRCUrl BuildActualRandomUrl(bool landscape, VRCUrl[] existing, int existingLength)
        {
            int poolCount = landscape ? GetLandscapeCount() : GetPortraitCount();
            if (poolCount <= 0)
            {
                return null;
            }

            int start = Random.Range(0, poolCount);
            int guard = 0;
            while (guard < poolCount)
            {
                int poolIndex = PositiveModulo(start + guard, poolCount);
                VRCUrl url = landscape ? GetLandscapeUrl(poolIndex) : GetPortraitUrl(poolIndex);
                string urlString = GetSafeUrlString(url);
                if (RemotePhotoUrlUtility.IsValidVrcUrl(url) &&
                    !ContainsUrl(existing, existingLength, url) &&
                    !IsCurrentSyncedUrlString(urlString) &&
                    !IsDisplayedUrlString(urlString))
                {
                    return url;
                }

                guard++;
            }

            guard = 0;
            while (guard < poolCount)
            {
                int poolIndex = PositiveModulo(start + guard, poolCount);
                VRCUrl url = landscape ? GetLandscapeUrl(poolIndex) : GetPortraitUrl(poolIndex);
                if (RemotePhotoUrlUtility.IsValidVrcUrl(url))
                {
                    return url;
                }

                guard++;
            }

            return null;
        }

        private int GetSequencePageVisualStart(bool landscape, int groupIndex, int pageDirection, int slotCount)
        {
            int poolCount = landscape ? GetLandscapeCount() : GetPortraitCount();
            if (poolCount <= 0 || slotCount <= 0 || groupIndex < 0)
            {
                return 0;
            }

            int pageCount = GetSequencePageCount(poolCount, slotCount);
            if (pageCount <= 0)
            {
                return 0;
            }

            int currentPage = GetSequenceGroupPageIndex(landscape, groupIndex);
            int nextPage = currentPage < 0 ? 0 : PositiveModulo(currentPage + pageDirection, pageCount);
            SetSequenceGroupPageIndex(landscape, groupIndex, nextPage);
            return nextPage * slotCount;
        }

        private int GetSequencePageCount(int poolCount, int slotCount)
        {
            if (poolCount <= 0 || slotCount <= 0)
            {
                return 0;
            }

            return Mathf.Max(1, (poolCount + slotCount - 1) / slotCount);
        }

        private VRCUrl SelectSequencePageUrl(bool landscape)
        {
            int poolCount = landscape ? GetLandscapeCount() : GetPortraitCount();
            if (poolCount <= 0)
            {
                return null;
            }

            int pageStart = landscape ? _activeSequenceLandscapeStart : _activeSequencePortraitStart;
            int cursor = landscape ? _activeSequenceLandscapeCursor : _activeSequencePortraitCursor;
            int guard = 0;
            while (guard < poolCount)
            {
                VRCUrl url = GetSequenceUrlAtVisualIndex(landscape, pageStart + cursor);
                cursor++;
                guard++;

                if (RemotePhotoUrlUtility.IsValidVrcUrl(url))
                {
                    if (landscape)
                    {
                        _activeSequenceLandscapeCursor = cursor;
                    }
                    else
                    {
                        _activeSequencePortraitCursor = cursor;
                    }

                    return url;
                }
            }

            return null;
        }

        private VRCUrl BuildNextActualSequentialUrl(bool landscape)
        {
            int poolCount = landscape ? GetLandscapeCount() : GetPortraitCount();
            if (poolCount <= 0)
            {
                return null;
            }

            int direction = configuredPlayMode == RemotePhotoPlayMode.SequenceReverse ? -1 : 1;
            int cursor = landscape ? nextLandscapeIndex : nextPortraitIndex;
            bool initialized = landscape ? sequenceLandscapeInitialized : sequencePortraitInitialized;
            if (direction < 0 && !initialized && cursor == 0)
            {
                cursor = poolCount - 1;
            }

            int guard = 0;
            while (guard < poolCount)
            {
                int poolIndex = PositiveModulo(cursor, poolCount);
                VRCUrl url = landscape ? GetLandscapeUrl(poolIndex) : GetPortraitUrl(poolIndex);
                cursor += direction;
                guard++;

                if (RemotePhotoUrlUtility.IsValidVrcUrl(url))
                {
                    if (landscape)
                    {
                        nextLandscapeIndex = PositiveModulo(cursor, poolCount);
                        sequenceLandscapeInitialized = true;
                    }
                    else
                    {
                        nextPortraitIndex = PositiveModulo(cursor, poolCount);
                        sequencePortraitInitialized = true;
                    }

                    return url;
                }
            }

            return null;
        }

        private VRCUrl BuildNextPredictedSequentialUrl(bool landscape)
        {
            int poolCount = landscape ? GetLandscapeCount() : GetPortraitCount();
            if (poolCount <= 0)
            {
                return null;
            }

            int groupIndex = GetSequencePredictionGroupIndex();
            RemotePhotoGroup group = managedGroups == null || groupIndex < 0 || groupIndex >= managedGroups.Length ? null : managedGroups[groupIndex];
            int slotCount = CountGroupSlots(group, landscape);
            if (slotCount <= 0)
            {
                slotCount = 1;
            }

            int currentPage = GetSequenceGroupPageIndex(landscape, groupIndex);
            if (currentPage < 0)
            {
                currentPage = 0;
            }

            int step = landscape ? _sequencePredictLandscapeStep : _sequencePredictPortraitStep;
            if (landscape)
            {
                _sequencePredictLandscapeStep++;
            }
            else
            {
                _sequencePredictPortraitStep++;
            }

            int pageStep = step / slotCount;
            int slotOffset = step % slotCount;
            int pageOffset = GetSequencePredictionPageOffset(pageStep);
            return GetSequenceUrlAtVisualIndex(landscape, (currentPage + pageOffset) * slotCount + slotOffset);
        }

        private int GetSequencePredictionPageOffset(int pageStep)
        {
            int direction = sequenceFocusDirection == 0 ? 1 : sequenceFocusDirection;
            if (pageStep <= 0)
            {
                return 0;
            }

            if (pageStep == 1)
            {
                return direction;
            }

            if (pageStep == 2)
            {
                return -direction;
            }

            if (pageStep == 3)
            {
                return direction * 2;
            }

            if (pageStep == 4)
            {
                return direction * 3;
            }

            if (pageStep == 5)
            {
                return -direction * 2;
            }

            return direction * (pageStep - 2);
        }

        private VRCUrl GetSequenceUrlAtVisualIndex(bool landscape, int visualIndex)
        {
            int poolCount = landscape ? GetLandscapeCount() : GetPortraitCount();
            if (poolCount <= 0)
            {
                return null;
            }

            int visual = PositiveModulo(visualIndex, poolCount);
            int poolIndex = configuredPlayMode == RemotePhotoPlayMode.SequenceReverse
                ? poolCount - 1 - visual
                : visual;
            return landscape ? GetLandscapeUrl(poolIndex) : GetPortraitUrl(poolIndex);
        }

        private void EnsureSequenceStateArrays()
        {
            int groupCount = managedGroups == null ? 0 : managedGroups.Length;
            if (sequenceLandscapePageIndices == null || sequenceLandscapePageIndices.Length != groupCount)
            {
                sequenceLandscapePageIndices = BuildSequencePageIndexArray(sequenceLandscapePageIndices, groupCount);
            }

            if (sequencePortraitPageIndices == null || sequencePortraitPageIndices.Length != groupCount)
            {
                sequencePortraitPageIndices = BuildSequencePageIndexArray(sequencePortraitPageIndices, groupCount);
            }

            if (sequenceFocusGroupIndex >= groupCount)
            {
                sequenceFocusGroupIndex = groupCount > 0 ? 0 : -1;
            }
        }

        private int[] BuildSequencePageIndexArray(int[] source, int length)
        {
            int[] result = new int[length];
            int index = 0;
            while (index < length)
            {
                result[index] = -1;
                index++;
            }

            int copyCount = source == null ? 0 : source.Length;
            if (copyCount > length)
            {
                copyCount = length;
            }

            index = 0;
            while (index < copyCount)
            {
                result[index] = source[index];
                index++;
            }

            return result;
        }

        private int GetSequenceGroupPageIndex(bool landscape, int groupIndex)
        {
            EnsureSequenceStateArrays();
            int[] source = landscape ? sequenceLandscapePageIndices : sequencePortraitPageIndices;
            if (source == null || groupIndex < 0 || groupIndex >= source.Length)
            {
                return -1;
            }

            return source[groupIndex];
        }

        private void SetSequenceGroupPageIndex(bool landscape, int groupIndex, int pageIndex)
        {
            EnsureSequenceStateArrays();
            int[] source = landscape ? sequenceLandscapePageIndices : sequencePortraitPageIndices;
            if (source == null || groupIndex < 0 || groupIndex >= source.Length)
            {
                return;
            }

            source[groupIndex] = pageIndex;
        }

        private int GetSequencePredictionGroupIndex()
        {
            EnsureSequenceStateArrays();
            if (sequenceFocusGroupIndex >= 0 && managedGroups != null && sequenceFocusGroupIndex < managedGroups.Length && managedGroups[sequenceFocusGroupIndex] != null)
            {
                return sequenceFocusGroupIndex;
            }

            int index = 0;
            while (managedGroups != null && index < managedGroups.Length)
            {
                if (managedGroups[index] != null)
                {
                    return index;
                }

                index++;
            }

            return -1;
        }

        private int CountGroupSlots(RemotePhotoGroup group, bool landscape)
        {
            RemotePhotoFrame[] targets = group == null ? null : group.targets;
            int count = 0;
            int index = 0;
            while (targets != null && index < targets.Length)
            {
                RemotePhotoFrame target = targets[index];
                if (target != null && (target.orientation == RemotePhotoOrientation.Landscape) == landscape)
                {
                    count++;
                }

                index++;
            }

            return count;
        }

        private VRCUrl BuildRandomUrl(bool landscape, VRCUrl[] existing, int existingLength)
        {
            int poolCount = landscape ? GetLandscapeCount() : GetPortraitCount();
            if (poolCount <= 0)
            {
                return null;
            }

            int start = Random.Range(0, poolCount);
            int guard = 0;
            while (guard < poolCount)
            {
                int poolIndex = PositiveModulo(start + guard, poolCount);
                VRCUrl url = landscape ? GetLandscapeUrl(poolIndex) : GetPortraitUrl(poolIndex);
                if (RemotePhotoUrlUtility.IsValidVrcUrl(url) &&
                    !IsKnownFailedUrl(url) &&
                    !ContainsUrl(existing, existingLength, url))
                {
                    return url;
                }

                guard++;
            }

            guard = 0;
            while (guard < poolCount)
            {
                int poolIndex = PositiveModulo(start + guard, poolCount);
                VRCUrl url = landscape ? GetLandscapeUrl(poolIndex) : GetPortraitUrl(poolIndex);
                if (RemotePhotoUrlUtility.IsValidVrcUrl(url) && !IsKnownFailedUrl(url))
                {
                    return url;
                }

                guard++;
            }

            return null;
        }

        private int GetDesiredPrepareCount(bool landscape, int requestedCount, int poolCount)
        {
            if (poolCount <= 0)
            {
                return 0;
            }

            int targetCount = landscape
                ? Mathf.Max(0, preloadLandscapeCacheSize)
                : Mathf.Max(0, preloadPortraitCacheSize);

            int desired = requestedCount + targetCount;
            if (desired <= 0)
            {
                desired = 1;
            }

            return desired > poolCount ? poolCount : desired;
        }

        private void PrepareStartupWarmup()
        {
            _startupWarmupActive = !loadOnceOnStart;
            _startupWarmupLandscapeCount = _startupWarmupActive ? CountManagedFrameSlots(true) : 0;
            _startupWarmupPortraitCount = _startupWarmupActive ? CountManagedFrameSlots(false) : 0;
        }

        private int CountManagedFrameSlots(bool landscape)
        {
            int count = 0;
            int groupIndex = 0;
            while (managedGroups != null && groupIndex < managedGroups.Length)
            {
                RemotePhotoGroup group = managedGroups[groupIndex];
                RemotePhotoFrame[] targets = group == null ? null : group.targets;
                int targetIndex = 0;
                while (targets != null && targetIndex < targets.Length)
                {
                    RemotePhotoFrame target = targets[targetIndex];
                    bool targetLandscape = target != null && target.orientation == RemotePhotoOrientation.Landscape;
                    if (target != null && targetLandscape == landscape)
                    {
                        count++;
                    }

                    targetIndex++;
                }

                groupIndex++;
            }

            return count;
        }

        private int GetStartupWarmupCount(bool landscape)
        {
            if (!_startupWarmupActive)
            {
                return 0;
            }

            return landscape ? _startupWarmupLandscapeCount : _startupWarmupPortraitCount;
        }

        private void EnsurePreloadDownloader()
        {
            if (_preloadDownloader == null)
            {
                _preloadDownloader = new VRCImageDownloader();
            }
        }

        private void DisposeCurrentPreloadDownload()
        {
            if (_currentPreloadDownload != null)
            {
                _currentPreloadDownload.Dispose();
                _currentPreloadDownload = null;
            }
        }

        private void CancelPreloadForQueueMutation()
        {
            _preloadInProgress = false;
            DisposeCurrentPreloadDownload();
            _activePreloadUrl = null;
            _activePreloadUrlString = string.Empty;
            _activePreloadRetryCount = 0;
            _preloadContinueScheduled = false;
            _preloadScanOrientation = 0;
            _preloadScanIndex = 0;
            _preloadScanOrderedIndex = 0;
        }

        #endregion

        #region Diagnostics

        public void LogDebug(string message)
        {
            if (debugLogs)
            {
                Debug.Log("[RemotePhotoSystem] " + message);
            }
        }

        private string GetLoadingModeName()
        {
            return IsPreloadEnabled() ? "Preload" : "NonPreload";
        }

        #endregion

        #region Cache And Utility Methods

        private void EnsureCacheArrays()
        {
            int safeCapacity = GetMaxCachedTextures();
            if (_cachedUrlStrings != null && _cachedUrlStrings.Length >= safeCapacity)
            {
                return;
            }

            string[] nextUrlStrings = new string[safeCapacity];
            VRCUrl[] nextUrls = new VRCUrl[safeCapacity];
            Texture2D[] nextTextures = new Texture2D[safeCapacity];
            IVRCImageDownload[] nextDownloads = new IVRCImageDownload[safeCapacity];
            int[] nextAccessTicks = new int[safeCapacity];
            int copyCount = _cachedUrlStrings == null ? 0 : _cachedUrlStrings.Length;
            if (copyCount > safeCapacity)
            {
                copyCount = safeCapacity;
            }

            int index = 0;
            while (index < copyCount)
            {
                nextUrlStrings[index] = _cachedUrlStrings[index];
                nextUrls[index] = _cachedUrls == null || index >= _cachedUrls.Length ? null : _cachedUrls[index];
                nextTextures[index] = _cachedTextures[index];
                nextDownloads[index] = _cachedDownloads[index];
                nextAccessTicks[index] = _cachedAccessTicks[index];
                index++;
            }

            _cachedUrlStrings = nextUrlStrings;
            _cachedUrls = nextUrls;
            _cachedTextures = nextTextures;
            _cachedDownloads = nextDownloads;
            _cachedAccessTicks = nextAccessTicks;
        }

        private int GetMaxCachedTextures()
        {
            return GetConfiguredPreloadCapacity() + GetTemporaryStagingCapacity();
        }

        private int GetFutureCacheCapacity()
        {
            return GetConfiguredPreloadCapacity();
        }

        private int GetConfiguredPreloadCapacity()
        {
            return GetConfiguredPreloadCapacity(true) + GetConfiguredPreloadCapacity(false);
        }

        private int GetConfiguredPreloadCapacity(bool landscape)
        {
            return landscape ? Mathf.Max(0, preloadLandscapeCacheSize) : Mathf.Max(0, preloadPortraitCacheSize);
        }

        private int GetTemporaryStagingCapacity()
        {
            return GetStartupWarmupCount(true) +
                   GetStartupWarmupCount(false) +
                   GetCurrentSyncedStagingCount();
        }

        private int GetCurrentSyncedStagingCount()
        {
            int count = 0;
            int groupIndex = 0;
            while (managedGroups != null && groupIndex < managedGroups.Length)
            {
                RemotePhotoGroup group = managedGroups[groupIndex];
                VRCUrl[] urls = group == null ? null : group.syncedUrls;
                int index = 0;
                while (urls != null && index < urls.Length)
                {
                    RemotePhotoFrame target = GetSyncedFrameForPair(group, index);
                    VRCUrl url = urls[index];
                    string urlString = GetSafeUrlString(url);
                    if (target != null &&
                        RemotePhotoUrlUtility.IsValidVrcUrl(url) &&
                        !IsFrameDisplayingUrl(target, urlString))
                    {
                        count++;
                    }

                    index++;
                }

                groupIndex++;
            }

            return count;
        }

        private void DisposeCachedDownloads()
        {
            if (_cachedDownloads == null)
            {
                return;
            }

            int index = 0;
            while (index < _cachedDownloads.Length)
            {
                DisposeCachedDownloadAt(index);
                index++;
            }
        }

        private void DisposeCachedDownloadAt(int index)
        {
            if (_cachedDownloads == null || index < 0 || index >= _cachedDownloads.Length)
            {
                return;
            }

            if (_cachedDownloads[index] != null)
            {
                _cachedDownloads[index].Dispose();
                _cachedDownloads[index] = null;
            }

            ClearCachedTextureWithoutDisposingAt(index);
        }

        private void ClearCachedTextureAt(int index)
        {
            DisposeCachedDownloadAt(index);
        }

        private void ClearCachedTextureWithoutDisposingAt(int index)
        {
            if (_cachedUrlStrings != null && index >= 0 && index < _cachedUrlStrings.Length)
            {
                _cachedUrlStrings[index] = string.Empty;
            }

            if (_cachedUrls != null && index >= 0 && index < _cachedUrls.Length)
            {
                _cachedUrls[index] = null;
            }

            if (_cachedTextures != null && index >= 0 && index < _cachedTextures.Length)
            {
                _cachedTextures[index] = null;
            }

            if (_cachedDownloads != null && index >= 0 && index < _cachedDownloads.Length)
            {
                _cachedDownloads[index] = null;
            }

            if (_cachedAccessTicks != null && index >= 0 && index < _cachedAccessTicks.Length)
            {
                _cachedAccessTicks[index] = 0;
            }
        }

        public void ReleaseDisplayedDownload(RemotePhotoFrame frame)
        {
            int index = FindDisplayedFrameIndex(frame);
            if (index < 0)
            {
                return;
            }

            if (_displayedDownloads[index] != null)
            {
                DisposeDisplayedDownloadIfUncached(_displayedDownloads[index]);
                _displayedDownloads[index] = null;
            }

            _displayedFrames[index] = null;
            if (_displayedUrlStrings != null && index < _displayedUrlStrings.Length)
            {
                _displayedUrlStrings[index] = string.Empty;
            }
        }

        private void MoveCachedDownloadToDisplayedFrame(RemotePhotoFrame frame, string urlString, IVRCImageDownload download)
        {
            if (frame == null || string.IsNullOrEmpty(urlString) || download == null)
            {
                return;
            }

            int index = FindDisplayedFrameIndex(frame);
            if (index < 0)
            {
                index = FindEmptyDisplayedFrameIndex();
            }

            if (index < 0)
            {
                index = AppendDisplayedFrameSlot();
            }

            if (index < 0)
            {
                return;
            }

            if (_displayedDownloads[index] != null && _displayedDownloads[index] != download)
            {
                DisposeDisplayedDownloadIfUncached(_displayedDownloads[index]);
            }

            _displayedFrames[index] = frame;
            _displayedUrlStrings[index] = urlString;
            _displayedDownloads[index] = download;
        }

        private bool IsDisplayedUrlString(string urlString)
        {
            if (string.IsNullOrEmpty(urlString) || _displayedUrlStrings == null)
            {
                return false;
            }

            int index = 0;
            while (index < _displayedUrlStrings.Length)
            {
                if (_displayedUrlStrings[index] == urlString)
                {
                    return true;
                }

                index++;
            }

            return false;
        }

        private bool IsFrameDisplayingUrl(RemotePhotoFrame frame, string urlString)
        {
            int index = FindDisplayedFrameIndex(frame);
            return index >= 0 &&
                _displayedUrlStrings != null &&
                index < _displayedUrlStrings.Length &&
                _displayedUrlStrings[index] == urlString;
        }

        private int FindDisplayedFrameIndex(RemotePhotoFrame frame)
        {
            if (frame == null || _displayedFrames == null)
            {
                return -1;
            }

            int index = 0;
            while (index < _displayedFrames.Length)
            {
                if (_displayedFrames[index] == frame)
                {
                    return index;
                }

                index++;
            }

            return -1;
        }

        private int FindEmptyDisplayedFrameIndex()
        {
            int index = 0;
            while (_displayedFrames != null && index < _displayedFrames.Length)
            {
                if (_displayedFrames[index] == null)
                {
                    return index;
                }

                index++;
            }

            return -1;
        }

        private int AppendDisplayedFrameSlot()
        {
            int length = _displayedFrames == null ? 0 : _displayedFrames.Length;
            RemotePhotoFrame[] nextFrames = new RemotePhotoFrame[length + 1];
            string[] nextUrls = new string[length + 1];
            IVRCImageDownload[] nextDownloads = new IVRCImageDownload[length + 1];
            int index = 0;
            while (index < length)
            {
                nextFrames[index] = _displayedFrames[index];
                nextUrls[index] = _displayedUrlStrings[index];
                nextDownloads[index] = _displayedDownloads[index];
                index++;
            }

            _displayedFrames = nextFrames;
            _displayedUrlStrings = nextUrls;
            _displayedDownloads = nextDownloads;
            return length;
        }

        private void DisposeDisplayedDownloads()
        {
            if (_displayedDownloads == null)
            {
                return;
            }

            int index = 0;
            while (index < _displayedDownloads.Length)
            {
                if (_displayedDownloads[index] != null)
                {
                    DisposeDisplayedDownloadIfUncached(_displayedDownloads[index]);
                    _displayedDownloads[index] = null;
                }

                if (_displayedFrames != null && index < _displayedFrames.Length)
                {
                    _displayedFrames[index] = null;
                }

                if (_displayedUrlStrings != null && index < _displayedUrlStrings.Length)
                {
                    _displayedUrlStrings[index] = string.Empty;
                }

                index++;
            }
        }

        private void DisposeDisplayedDownloadIfUncached(IVRCImageDownload download)
        {
            if (download == null || IsCachedDownload(download))
            {
                return;
            }

            download.Dispose();
        }

        private bool IsCachedDownload(IVRCImageDownload download)
        {
            if (download == null || _cachedDownloads == null)
            {
                return false;
            }

            int index = 0;
            while (index < _cachedDownloads.Length)
            {
                if (_cachedDownloads[index] == download)
                {
                    return true;
                }

                index++;
            }

            return false;
        }

        private int FindCachedUrlIndex(string url)
        {
            EnsureCacheArrays();
            int index = 0;
            while (index < _cachedUrlStrings.Length)
            {
                if (_cachedTextures[index] != null && _cachedDownloads[index] != null && _cachedUrlStrings[index] == url)
                {
                    return index;
                }

                index++;
            }

            return -1;
        }

        private int FindEmptyCacheIndex()
        {
            return FindEmptyCacheIndexWithin(GetMaxCachedTextures());
        }

        private int FindEmptyCacheIndexWithin(int limit)
        {
            int maxLimit = GetMaxCachedTextures();
            if (limit > maxLimit)
            {
                limit = maxLimit;
            }

            int index = 0;
            while (index < _cachedTextures.Length && index < limit)
            {
                if (_cachedTextures[index] == null || string.IsNullOrEmpty(_cachedUrlStrings[index]))
                {
                    return index;
                }

                index++;
            }

            return -1;
        }

        private int FindEmptyCacheIndexBetween(int start, int limit)
        {
            int maxLimit = GetMaxCachedTextures();
            if (start < 0)
            {
                start = 0;
            }

            if (limit > maxLimit)
            {
                limit = maxLimit;
            }

            int index = start;
            while (index < _cachedTextures.Length && index < limit)
            {
                if (_cachedTextures[index] == null || string.IsNullOrEmpty(_cachedUrlStrings[index]))
                {
                    return index;
                }

                index++;
            }

            return -1;
        }

        private int FindReadyCacheIndexForOrientation(bool landscape)
        {
            EnsureCacheArrays();
            int index = 0;
            int limit = GetFutureCacheCapacity();
            while (index < _cachedUrlStrings.Length && index < limit)
            {
                VRCUrl url = _cachedUrls == null || index >= _cachedUrls.Length ? null : _cachedUrls[index];
                string urlString = _cachedUrlStrings == null || index >= _cachedUrlStrings.Length ? string.Empty : _cachedUrlStrings[index];
                if (_cachedTextures[index] != null &&
                    _cachedDownloads[index] != null &&
                    RemotePhotoUrlUtility.IsValidVrcUrl(url) &&
                    !IsCurrentSyncedUrlString(urlString) &&
                    !IsDisplayedUrlString(urlString) &&
                    IsUrlInOrientationPool(landscape, url))
                {
                    return index;
                }

                index++;
            }

            return -1;
        }

        private int FindCacheStoreIndex(string incomingUrl)
        {
            bool currentSynced = IsCurrentSyncedUrlString(incomingUrl);
            int futureLimit = GetFutureCacheCapacity();
            int maxLimit = GetMaxCachedTextures();
            if (currentSynced)
            {
                int stagingEmptyIndex = FindEmptyCacheIndexBetween(futureLimit, maxLimit);
                if (stagingEmptyIndex >= 0)
                {
                    return stagingEmptyIndex;
                }

                int stagingStaleIndex = FindLeastRecentlyUsedCacheIndexWithRules(false, true, futureLimit, maxLimit);
                if (stagingStaleIndex >= 0)
                {
                    return stagingStaleIndex;
                }
            }

            int limit = currentSynced ? maxLimit : futureLimit;
            int emptyIndex = FindEmptyCacheIndexWithin(limit);
            if (emptyIndex >= 0)
            {
                return emptyIndex;
            }

            int staleIndex = FindLeastRecentlyUsedCacheIndexWithRules(false, false, limit);
            if (staleIndex >= 0)
            {
                return staleIndex;
            }

            if (currentSynced)
            {
                return FindLeastRecentlyUsedCacheIndexWithRules(false, true, GetMaxCachedTextures());
            }

            return -1;
        }

        private int FindLeastRecentlyUsedCacheIndexWithRules(bool allowCurrentSynced, bool allowPreloadWindow)
        {
            return FindLeastRecentlyUsedCacheIndexWithRules(allowCurrentSynced, allowPreloadWindow, GetMaxCachedTextures());
        }

        private int FindLeastRecentlyUsedCacheIndexWithRules(bool allowCurrentSynced, bool allowPreloadWindow, int limit)
        {
            return FindLeastRecentlyUsedCacheIndexWithRules(allowCurrentSynced, allowPreloadWindow, 0, limit);
        }

        private int FindLeastRecentlyUsedCacheIndexWithRules(bool allowCurrentSynced, bool allowPreloadWindow, int start, int limit)
        {
            if (_cachedAccessTicks == null || _cachedAccessTicks.Length == 0)
            {
                return -1;
            }

            if (limit <= 0)
            {
                return -1;
            }

            int maxLimit = GetMaxCachedTextures();
            if (limit > maxLimit)
            {
                limit = maxLimit;
            }

            if (limit > _cachedAccessTicks.Length)
            {
                limit = _cachedAccessTicks.Length;
            }

            int selectedIndex = -1;
            int selectedTick = 0;
            int index = start < 0 ? 0 : start;
            while (index < limit)
            {
                string cachedUrl = _cachedUrlStrings == null || index >= _cachedUrlStrings.Length ? string.Empty : _cachedUrlStrings[index];
                bool canUse = !string.IsNullOrEmpty(cachedUrl);
                if (canUse && !allowCurrentSynced && IsCurrentSyncedUrlString(cachedUrl))
                {
                    canUse = false;
                }

                if (canUse && IsDisplayedUrlString(cachedUrl))
                {
                    canUse = false;
                }

                if (canUse && !allowPreloadWindow && IsUrlInPreloadWindowString(cachedUrl))
                {
                    canUse = false;
                }

                if (canUse && (selectedIndex < 0 || _cachedAccessTicks[index] < selectedTick))
                {
                    selectedIndex = index;
                    selectedTick = _cachedAccessTicks[index];
                }

                index++;
            }

            return selectedIndex;
        }

        private void MarkFailedUrl(string url)
        {
            if (string.IsNullOrEmpty(url) || IsKnownFailedUrlString(url))
            {
                return;
            }

            string[] next = new string[_failedUrlStrings.Length + 1];
            int index = 0;
            while (index < _failedUrlStrings.Length)
            {
                next[index] = _failedUrlStrings[index];
                index++;
            }

            next[_failedUrlStrings.Length] = url;
            _failedUrlStrings = next;
        }

        private bool IsKnownFailedUrl(VRCUrl url)
        {
            return IsKnownFailedUrlString(GetSafeUrlString(url));
        }

        private bool IsKnownFailedUrlString(string url)
        {
            if (string.IsNullOrEmpty(url))
            {
                return false;
            }

            int index = 0;
            while (_failedUrlStrings != null && index < _failedUrlStrings.Length)
            {
                if (_failedUrlStrings[index] == url)
                {
                    return true;
                }

                index++;
            }

            return false;
        }

        private bool ContainsUrl(VRCUrl[] urls, int length, VRCUrl url)
        {
            string value = GetSafeUrlString(url);
            if (string.IsNullOrEmpty(value))
            {
                return false;
            }

            int index = 0;
            while (urls != null && index < length && index < urls.Length)
            {
                if (GetSafeUrlString(urls[index]) == value)
                {
                    return true;
                }

                index++;
            }

            return false;
        }

        #endregion

        #region Ownership And Managed Groups

        public bool ContainsManagedGroup(RemotePhotoGroup group)
        {
            return GetManagedGroupIndex(group) >= 0;
        }

        private int GetManagedGroupIndex(RemotePhotoGroup group)
        {
            if (group == null || managedGroups == null)
            {
                return -1;
            }

            int index = 0;
            while (index < managedGroups.Length)
            {
                if (managedGroups[index] == group)
                {
                    return index;
                }

                index++;
            }

            return -1;
        }

        public void EnsureMasterOwnership()
        {
            if (!Networking.IsMaster)
            {
                return;
            }

            if (!Networking.IsOwner(gameObject))
            {
                Networking.SetOwner(Networking.LocalPlayer, gameObject);
            }

            int index = 0;
            while (managedGroups != null && index < managedGroups.Length)
            {
                RemotePhotoGroup group = managedGroups[index];
                if (group != null && !Networking.IsOwner(group.gameObject))
                {
                    Networking.SetOwner(Networking.LocalPlayer, group.gameObject);
                }

                index++;
            }
        }

        #endregion

        #region Low Level Helpers

        private string GetSafeUrlString(VRCUrl url)
        {
            if (url == null)
            {
                return string.Empty;
            }

            return url.Get();
        }

        private string GetImageDownloadErrorDetails(IVRCImageDownload result)
        {
            if (result == null)
            {
                return " | Error=<null result>";
            }

            return " | Error=" + result.Error.ToString() + " | Message=" + result.ErrorMessage;
        }

        private bool IsNonRetryableImageError(IVRCImageDownload result)
        {
            if (result == null)
            {
                return false;
            }

            string message = result.ErrorMessage;
            if (string.IsNullOrEmpty(message))
            {
                return false;
            }

            return message.Contains("MaximumDimensionExceeded");
        }

        private int PositiveModulo(int value, int modulo)
        {
            if (modulo <= 0)
            {
                return 0;
            }

            int result = value % modulo;
            if (result < 0)
            {
                result += modulo;
            }

            return result;
        }

        #endregion
    }
}
