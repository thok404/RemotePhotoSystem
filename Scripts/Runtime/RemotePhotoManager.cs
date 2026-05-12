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

        [HideInInspector] public VRCUrl[] bakedLandscapeUrls = new VRCUrl[0];
        [HideInInspector] public VRCUrl[] bakedPortraitUrls = new VRCUrl[0];

        [HideInInspector] public VRCUrl[] landscapeUrls = new VRCUrl[0];
        [HideInInspector] public VRCUrl[] portraitUrls = new VRCUrl[0];

        [UdonSynced] public VRCUrl[] preloadLandscapeUrls = new VRCUrl[0];
        [UdonSynced] public VRCUrl[] preloadPortraitUrls = new VRCUrl[0];
        [UdonSynced] public VRCUrl[] preloadOrderedUrls = new VRCUrl[0];
        [UdonSynced] public int preloadRevision;
        [UdonSynced] public bool preloadReady;
        [UdonSynced] public int nextLandscapeIndex;
        [UdonSynced] public int nextPortraitIndex;
        [UdonSynced] public int preloadNextLandscapeIndex;
        [UdonSynced] public int preloadNextPortraitIndex;
        [UdonSynced] public bool sequenceLandscapeInitialized;
        [UdonSynced] public bool sequencePortraitInitialized;

        [HideInInspector] public string lastGalleryError = string.Empty;
        [HideInInspector] public bool hasGalleryData;
        [HideInInspector] public string lastPreloadStatus = string.Empty;

        private VRCImageDownloader _preloadDownloader;
        private IVRCImageDownload _currentPreloadDownload;
        private TextureInfo _preloadTextureInfo;
        private VRCUrl _activePreloadUrl;
        private string _activePreloadUrlString = string.Empty;
        private bool _activePreloadLandscape;
        private int _activePreloadIndex;
        private int _activePreloadRetryCount;
        private bool _preloadInProgress;
        private bool _preloadContinueScheduled;
        private int _preloadScanOrientation;
        private int _preloadScanIndex;
        private int _preloadScanOrderedIndex;
        private int _activePreloadOrderedIndex = -1;
        private int _nextPreloadMaterialIndex;
        private int _activeSequenceLandscapeStart;
        private int _activeSequencePortraitStart;
        private int _activeSequenceLandscapeCursor;
        private int _activeSequencePortraitCursor;

        private string[] _cachedUrlStrings = new string[0];
        private Texture2D[] _cachedTextures = new Texture2D[0];
        private IVRCImageDownload[] _cachedDownloads = new IVRCImageDownload[0];
        private int[] _cachedAccessTicks = new int[0];
        private IVRCImageDownload[] _displayedDownloads = new IVRCImageDownload[0];
        private int _displayedDownloadIndex;
        private int _cacheAccessTick;
        private VRCUrl[] _priorityPreloadUrls = new VRCUrl[0];

        private string[] _failedUrlStrings = new string[0];
        private const string PreloadTexturePropertyName = "_RemotePhotoPreloadTex";
        private const float PreloadDownloadIntervalSeconds = 5f;

        public void Start()
        {
            ApplyBakedGallery();
            EnsureCacheArrays();
            LogDebug("Remote Photo Manager started. Landscape=" + GetLandscapeCount() + ", Portrait=" + GetPortraitCount() + ", LoadingMode=" + GetLoadingModeName());

            if (IsPreloadEnabled())
            {
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
            StartPreloadingQueue();
        }

        public bool IsPreloadEnabled()
        {
            return loadingMode == RemotePhotoLoadingMode.Preload;
        }

        public void _LoadOnceOnStart()
        {
            if (!loadOnceOnStart || managedGroups == null)
            {
                return;
            }

            int index = 0;
            while (index < managedGroups.Length)
            {
                RemotePhotoGroup group = managedGroups[index];
                if (group != null)
                {
                    LogDebug("Load once on start: " + group.gameObject.name);
                    if (configuredPlayMode == RemotePhotoPlayMode.Random)
                    {
                        group.TriggerRandom();
                    }
                    else
                    {
                        group.TriggerNext();
                    }
                }

                index++;
            }
        }

        public void OnDestroy()
        {
            DisposeCurrentPreloadDownload();
            DisposeCachedDownloads();

            if (_preloadDownloader != null)
            {
                _preloadDownloader.Dispose();
                _preloadDownloader = null;
            }
        }

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

        public void BeginSequencePageSelection(bool nextPage, int landscapeCount, int portraitCount)
        {
            _activeSequenceLandscapeStart = GetSequencePageStart(true, nextPage, landscapeCount);
            _activeSequencePortraitStart = GetSequencePageStart(false, nextPage, portraitCount);
            _activeSequenceLandscapeCursor = _activeSequenceLandscapeStart;
            _activeSequencePortraitCursor = _activeSequencePortraitStart;
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
            CommitSequencePageCursor(true, landscapeCount);
            CommitSequencePageCursor(false, portraitCount);
        }

        public void NotifySelectionStateChanged()
        {
            preloadRevision++;
            RequestSerialization();
            WakePreloadQueue();
        }

        public void RefreshPreloadPredictions()
        {
            RequestPrepareForCounts(0, 0);
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

        public void RequestPreloadPriority(VRCUrl url)
        {
            if (!IsPreloadEnabled() || !RemotePhotoUrlUtility.IsValidVrcUrl(url) || GetCachedTextureQuiet(url) != null)
            {
                return;
            }

            if (!IsUrlInAnyPool(url) || ContainsUrl(_priorityPreloadUrls, _priorityPreloadUrls == null ? 0 : _priorityPreloadUrls.Length, url))
            {
                return;
            }

            int length = _priorityPreloadUrls == null ? 0 : _priorityPreloadUrls.Length;
            VRCUrl[] next = new VRCUrl[length + 1];
            int index = 0;
            while (index < length)
            {
                next[index] = _priorityPreloadUrls[index];
                index++;
            }

            next[length] = url;
            _priorityPreloadUrls = next;
            LogDebug("Preload priority queued: " + GetSafeUrlString(url));
            WakePreloadQueue();
        }

        public void EnsurePredictionQueue()
        {
            RequestPrepareForCounts(0, 0);
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

        public void ConsumeCachedTexture(VRCUrl url)
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

            if (!Networking.IsOwner(gameObject))
            {
                LogDebug("Cache used locally by frame: " + urlString + ". Synced preload order is owner-managed.");
                return;
            }

            RemovePreloadUrl(url);
            LogDebug("Cache used by frame: " + urlString + ". Cached=" + GetCachedTextureCount() + "/" + GetMaxCachedTextures());
            RefreshPreloadPredictions();
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

            int desiredLandscapeCount = GetDesiredPrepareCount(true, landscapeCount, GetLandscapeCount());
            int desiredPortraitCount = GetDesiredPrepareCount(false, portraitCount, GetPortraitCount());
            LogDebug("Prepare requested. Need L=" + landscapeCount + ", P=" + portraitCount + ", Desired L=" + desiredLandscapeCount + ", P=" + desiredPortraitCount);

            EnsurePreloadPoolSize(desiredLandscapeCount, desiredPortraitCount);
            _preloadScanOrderedIndex = 0;
            _preloadScanOrientation = 0;
            _preloadScanIndex = 0;
            preloadReady = !IsPreloadEnabled();
            preloadRevision++;
            lastPreloadStatus = preloadReady ? "Preload disabled." : "Preloading prediction cache.";
            LogDebug("Prediction pool rebuilt. Ordered=" + (preloadOrderedUrls == null ? 0 : preloadOrderedUrls.Length) + ", Landscape=" + (preloadLandscapeUrls == null ? 0 : preloadLandscapeUrls.Length) + ", Portrait=" + (preloadPortraitUrls == null ? 0 : preloadPortraitUrls.Length));
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

        public void StoreCachedTexture(VRCUrl url, Texture2D texture)
        {
            StoreCachedTextureInternal(url, texture, null);
        }

        private void StoreCachedDownload(VRCUrl url, Texture2D texture, IVRCImageDownload download)
        {
            StoreCachedTextureInternal(url, texture, download);
        }

        private void StoreCachedTextureInternal(VRCUrl url, Texture2D texture, IVRCImageDownload download)
        {
            string urlString = GetSafeUrlString(url);
            if (string.IsNullOrEmpty(urlString) || texture == null || GetMaxCachedTextures() <= 0)
            {
                return;
            }

            EnsureCacheArrays();
            int existingIndex = FindCachedUrlIndex(urlString);
            _cacheAccessTick++;
            if (existingIndex >= 0)
            {
                _cachedTextures[existingIndex] = texture;
                if (download != null)
                {
                    DisposeCachedDownloadAt(existingIndex);
                    _cachedDownloads[existingIndex] = download;
                }

                _cachedAccessTicks[existingIndex] = _cacheAccessTick;
                LogDebug("Cache updated: " + urlString);
                return;
            }

            int storeIndex = FindEmptyCacheIndex();
            if (storeIndex < 0)
            {
                storeIndex = FindLeastRecentlyUsedCacheIndex();
            }

            if (storeIndex < 0)
            {
                return;
            }

            KeepDisplayedDownload(_cachedDownloads == null || storeIndex < 0 || storeIndex >= _cachedDownloads.Length ? null : _cachedDownloads[storeIndex]);
            if (_cachedDownloads != null && storeIndex >= 0 && storeIndex < _cachedDownloads.Length)
            {
                _cachedDownloads[storeIndex] = null;
            }

            _cachedUrlStrings[storeIndex] = urlString;
            _cachedTextures[storeIndex] = texture;
            _cachedDownloads[storeIndex] = download;
            _cachedAccessTicks[storeIndex] = _cacheAccessTick;
            LogDebug("Cache stored: " + urlString);
        }

        public int GetCachedTextureCount()
        {
            EnsureCacheArrays();
            int count = 0;
            int index = 0;
            while (index < _cachedTextures.Length)
            {
                if (_cachedTextures[index] != null && !string.IsNullOrEmpty(_cachedUrlStrings[index]))
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

            StoreCachedDownload(result.Url, result.Result, result);
            LogDebug("Preload success: " + _activePreloadUrlString);
            _currentPreloadDownload = null;
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
                if (configuredPlayMode != RemotePhotoPlayMode.Random)
                {
                    LogDebug("Sequence preload non-retryable error; keeping URL in sequence queue for future retries: " + _activePreloadUrlString);
                    _activePreloadRetryCount = 0;
                    AdvancePreloadCursor();
                    ScheduleNextPreloadDownload();
                    return;
                }

                if (Networking.IsOwner(gameObject))
                {
                    LogDebug("Preload non-retryable error, replacing this prediction slot immediately: " + _activePreloadUrlString);
                    ReplaceFailedPreloadUrl(_activePreloadLandscape, _activePreloadIndex);
                }
                else
                {
                    LogDebug("Preload non-retryable error on non-owner, keeping synced prediction slot: " + _activePreloadUrlString);
                    AdvancePreloadCursor();
                }

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
                LogDebug("Sequence preload failed after retries; keeping URL in sequence queue for future retries: " + _activePreloadUrlString);
                _activePreloadRetryCount = 0;
                AdvancePreloadCursor();
                ScheduleNextPreloadDownload();
                return;
            }

            if (Networking.IsOwner(gameObject))
            {
                LogDebug("Preload failed after retries, replacing this prediction slot only: " + _activePreloadUrlString);
                ReplaceFailedPreloadUrl(_activePreloadLandscape, _activePreloadIndex);
            }
            else
            {
                LogDebug("Preload failed after retries on non-owner, keeping synced prediction slot: " + _activePreloadUrlString);
                AdvancePreloadCursor();
            }

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
                preloadReady = preloadLandscapeUrls != null && preloadPortraitUrls != null;
                lastPreloadStatus = "Preload disabled.";
                LogDebug("Preload disabled. Prediction cache is inactive.");
                if (Networking.IsOwner(gameObject))
                {
                    RequestSerialization();
                }

                return;
            }

            if (_preloadInProgress || GetMaxCachedTextures() <= 0)
            {
                if (GetMaxCachedTextures() <= 0)
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
            _activePreloadOrderedIndex = -1;
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
            VRCUrl priorityUrl = FindNextPriorityPreloadUrl();
            if (RemotePhotoUrlUtility.IsValidVrcUrl(priorityUrl))
            {
                return priorityUrl;
            }

            while (preloadOrderedUrls != null && _preloadScanOrderedIndex < preloadOrderedUrls.Length)
            {
                VRCUrl url = preloadOrderedUrls[_preloadScanOrderedIndex];
                _activePreloadOrderedIndex = _preloadScanOrderedIndex;
                SetActivePreloadLocation(url);
                _preloadScanOrderedIndex++;

                if (RemotePhotoUrlUtility.IsValidVrcUrl(url) && GetCachedTextureQuiet(url) == null)
                {
                    return url;
                }
            }

            while (_preloadScanOrientation < 2)
            {
                VRCUrl[] source = _preloadScanOrientation == 0 ? preloadLandscapeUrls : preloadPortraitUrls;
                while (source != null && _preloadScanIndex < source.Length)
                {
                    VRCUrl url = source[_preloadScanIndex];
                    _activePreloadLandscape = _preloadScanOrientation == 0;
                    _activePreloadIndex = _preloadScanIndex;
                    _activePreloadOrderedIndex = FindUrlInArray(preloadOrderedUrls, GetSafeUrlString(url));
                    _preloadScanIndex++;

                    if (RemotePhotoUrlUtility.IsValidVrcUrl(url) && GetCachedTextureQuiet(url) == null)
                    {
                        return url;
                    }
                }

                _preloadScanOrientation++;
                _preloadScanIndex = 0;
            }

            return null;
        }

        private VRCUrl FindNextPriorityPreloadUrl()
        {
            while (_priorityPreloadUrls != null && _priorityPreloadUrls.Length > 0)
            {
                VRCUrl url = _priorityPreloadUrls[0];
                _priorityPreloadUrls = RemoveFirstUrlFromArray(_priorityPreloadUrls);

                if (RemotePhotoUrlUtility.IsValidVrcUrl(url) &&
                    IsUrlInAnyPool(url) &&
                    GetCachedTextureQuiet(url) == null)
                {
                    SetActivePreloadLocation(url);
                    LogDebug("Preload priority selected: " + GetSafeUrlString(url));
                    return url;
                }
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

        private void ReplaceFailedPreloadUrl(bool landscape, int index)
        {
            if (!Networking.IsOwner(gameObject))
            {
                return;
            }

            VRCUrl replacement = landscape ? BuildReplacementLandscapeUrl() : BuildReplacementPortraitUrl();
            if (_activePreloadOrderedIndex >= 0 && preloadOrderedUrls != null && _activePreloadOrderedIndex < preloadOrderedUrls.Length)
            {
                preloadOrderedUrls[_activePreloadOrderedIndex] = replacement;
                _preloadScanOrderedIndex = _activePreloadOrderedIndex;
            }

            if (landscape)
            {
                if (preloadLandscapeUrls != null && index >= 0 && index < preloadLandscapeUrls.Length)
                {
                    preloadLandscapeUrls[index] = replacement;
                    _preloadScanOrientation = 0;
                    _preloadScanIndex = index;
                }
            }
            else
            {
                if (preloadPortraitUrls != null && index >= 0 && index < preloadPortraitUrls.Length)
                {
                    preloadPortraitUrls[index] = replacement;
                    _preloadScanOrientation = 1;
                    _preloadScanIndex = index;
                }
            }

            preloadRevision++;
            LogDebug("Prediction URL replaced at " + (landscape ? "landscape" : "portrait") + " index " + index);
            RequestSerialization();
        }

        private void SetActivePreloadLocation(VRCUrl url)
        {
            string urlString = GetSafeUrlString(url);
            _activePreloadOrderedIndex = FindUrlInArray(preloadOrderedUrls, urlString);
            _activePreloadLandscape = IsUrlInOrientationPool(true, url);
            _activePreloadIndex = -1;
            int index = FindUrlInArray(preloadLandscapeUrls, urlString);
            if (index >= 0)
            {
                _activePreloadLandscape = true;
                _activePreloadIndex = index;
                return;
            }

            index = FindUrlInArray(preloadPortraitUrls, urlString);
            if (index >= 0)
            {
                _activePreloadLandscape = false;
                _activePreloadIndex = index;
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

        private bool ContainsPreloadUrl(VRCUrl url)
        {
            string urlString = GetSafeUrlString(url);
            if (string.IsNullOrEmpty(urlString))
            {
                return false;
            }

            return FindUrlInArray(preloadOrderedUrls, urlString) >= 0 ||
                FindUrlInArray(preloadLandscapeUrls, urlString) >= 0 ||
                FindUrlInArray(preloadPortraitUrls, urlString) >= 0;
        }

        private bool HasOrderedPreloadGroups()
        {
            return managedGroups != null && managedGroups.Length > 0;
        }

        private bool HasPreloadUrlForOrientation(bool landscape)
        {
            VRCUrl[] source = landscape ? preloadLandscapeUrls : preloadPortraitUrls;
            return source != null && source.Length > 0;
        }

        private bool IsUrlInAnyPool(VRCUrl url)
        {
            return IsUrlInOrientationPool(true, url) || IsUrlInOrientationPool(false, url);
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
                preloadOrderedUrls = BuildOrderedPreloadUrls(landscapeCount, portraitCount);
                preloadLandscapeUrls = ExtractPreloadUrlsByOrientation(true, preloadOrderedUrls, landscapeCount);
                preloadPortraitUrls = ExtractPreloadUrlsByOrientation(false, preloadOrderedUrls, portraitCount);
                return;
            }

            preloadLandscapeUrls = EnsurePreloadUrls(true, preloadLandscapeUrls, landscapeCount);
            preloadPortraitUrls = EnsurePreloadUrls(false, preloadPortraitUrls, portraitCount);
            preloadOrderedUrls = MergePreloadUrls(preloadLandscapeUrls, preloadPortraitUrls);
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
                selected = IsPreloadEnabled() ? SelectOrderedPreloadUrl(landscape, selectedUrls, selectedLength) : null;
                if (!RemotePhotoUrlUtility.IsValidVrcUrl(selected))
                {
                    selected = BuildActualRandomUrl(landscape, selectedUrls, selectedLength);
                }
            }
            else
            {
                selected = BuildNextActualSequentialUrl(landscape);
            }

            LogDebug("Actual selection " + (landscape ? "landscape" : "portrait") + ": " + GetSafeUrlString(selected));
            return selected;
        }

        private VRCUrl SelectOrderedPreloadUrl(bool landscape, VRCUrl[] selectedUrls, int selectedLength)
        {
            if (!Networking.IsOwner(gameObject))
            {
                return null;
            }

            int index = 0;
            while (preloadOrderedUrls != null && index < preloadOrderedUrls.Length)
            {
                VRCUrl url = preloadOrderedUrls[index];
                if (RemotePhotoUrlUtility.IsValidVrcUrl(url) &&
                    IsUrlInOrientationPool(landscape, url) &&
                    !ContainsUrl(selectedUrls, selectedLength, url))
                {
                    preloadOrderedUrls = RemoveUrlAtIndex(preloadOrderedUrls, index);
                    preloadLandscapeUrls = RemoveUrlFromArray(preloadLandscapeUrls, url);
                    preloadPortraitUrls = RemoveUrlFromArray(preloadPortraitUrls, url);
                    LogDebug("Actual random uses synced preload order: " + GetSafeUrlString(url));
                    return url;
                }

                index++;
            }

            return null;
        }

        private VRCUrl SelectPredictedUrl(bool landscape, VRCUrl[] selectedUrls, int selectedLength)
        {
            VRCUrl[] source = landscape ? preloadLandscapeUrls : preloadPortraitUrls;
            int index = 0;
            while (source != null && index < source.Length)
            {
                VRCUrl url = source[index];
                if (RemotePhotoUrlUtility.IsValidVrcUrl(url) && !ContainsUrl(selectedUrls, selectedLength, url))
                {
                    if (configuredPlayMode != RemotePhotoPlayMode.Random)
                    {
                        AdvanceActualCursorAfterSelection(landscape, url);
                    }

                    return url;
                }

                index++;
            }

            return null;
        }

        private void AdvanceActualCursorAfterSelection(bool landscape, VRCUrl selectedUrl)
        {
            int poolCount = landscape ? GetLandscapeCount() : GetPortraitCount();
            if (poolCount <= 0 || !RemotePhotoUrlUtility.IsValidVrcUrl(selectedUrl))
            {
                return;
            }

            string selected = GetSafeUrlString(selectedUrl);
            int index = 0;
            while (index < poolCount)
            {
                VRCUrl url = landscape ? GetLandscapeUrl(index) : GetPortraitUrl(index);
                if (GetSafeUrlString(url) == selected)
                {
                    int direction = configuredPlayMode == RemotePhotoPlayMode.SequenceReverse ? -1 : 1;
                    if (landscape)
                    {
                        nextLandscapeIndex = PositiveModulo(index + direction, poolCount);
                        sequenceLandscapeInitialized = true;
                    }
                    else
                    {
                        nextPortraitIndex = PositiveModulo(index + direction, poolCount);
                        sequencePortraitInitialized = true;
                    }

                    return;
                }

                index++;
            }
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
                if (RemotePhotoUrlUtility.IsValidVrcUrl(url) && !ContainsUrl(existing, existingLength, url))
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

        private int GetSequencePageStart(bool landscape, bool nextPage, int pageCount)
        {
            int poolCount = landscape ? GetLandscapeCount() : GetPortraitCount();
            if (poolCount <= 0)
            {
                return 0;
            }

            int direction = GetSequenceDirection();
            int cursor = landscape ? nextLandscapeIndex : nextPortraitIndex;
            bool initialized = landscape ? sequenceLandscapeInitialized : sequencePortraitInitialized;
            if (!initialized)
            {
                return direction < 0 ? poolCount - 1 : 0;
            }

            if (nextPage)
            {
                return PositiveModulo(cursor, poolCount);
            }

            int safePageCount = Mathf.Max(0, pageCount);
            int currentPageStart = PositiveModulo(cursor - safePageCount * direction, poolCount);
            return PositiveModulo(currentPageStart - safePageCount * direction, poolCount);
        }

        private VRCUrl SelectSequencePageUrl(bool landscape)
        {
            int poolCount = landscape ? GetLandscapeCount() : GetPortraitCount();
            if (poolCount <= 0)
            {
                return null;
            }

            int direction = GetSequenceDirection();
            int cursor = landscape ? _activeSequenceLandscapeCursor : _activeSequencePortraitCursor;
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

        private void CommitSequencePageCursor(bool landscape, int pageCount)
        {
            int poolCount = landscape ? GetLandscapeCount() : GetPortraitCount();
            if (poolCount <= 0 || pageCount <= 0)
            {
                return;
            }

            int start = landscape ? _activeSequenceLandscapeStart : _activeSequencePortraitStart;
            int nextIndex = PositiveModulo(start + Mathf.Max(0, pageCount) * GetSequenceDirection(), poolCount);
            if (landscape)
            {
                nextLandscapeIndex = nextIndex;
                sequenceLandscapeInitialized = true;
            }
            else
            {
                nextPortraitIndex = nextIndex;
                sequencePortraitInitialized = true;
            }
        }

        private int GetSequenceDirection()
        {
            return configuredPlayMode == RemotePhotoPlayMode.SequenceReverse ? -1 : 1;
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

            int direction = configuredPlayMode == RemotePhotoPlayMode.SequenceReverse ? -1 : 1;
            int cursor = landscape ? preloadNextLandscapeIndex : preloadNextPortraitIndex;
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
                        preloadNextLandscapeIndex = PositiveModulo(cursor, poolCount);
                    }
                    else
                    {
                        preloadNextPortraitIndex = PositiveModulo(cursor, poolCount);
                    }

                    return url;
                }
            }

            return null;
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

        private VRCUrl BuildReplacementLandscapeUrl()
        {
            if (configuredPlayMode == RemotePhotoPlayMode.Random)
            {
                return BuildRandomUrl(true, preloadLandscapeUrls, preloadLandscapeUrls == null ? 0 : preloadLandscapeUrls.Length);
            }

            return BuildSequentialReplacementUrl(true);
        }

        private VRCUrl BuildReplacementPortraitUrl()
        {
            if (configuredPlayMode == RemotePhotoPlayMode.Random)
            {
                return BuildRandomUrl(false, preloadPortraitUrls, preloadPortraitUrls == null ? 0 : preloadPortraitUrls.Length);
            }

            return BuildSequentialReplacementUrl(false);
        }

        private VRCUrl BuildSequentialReplacementUrl(bool landscape)
        {
            return BuildNextPredictedSequentialUrl(landscape);
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

            int desired = requestedCount > targetCount ? requestedCount : targetCount;
            if (desired <= 0)
            {
                desired = 1;
            }

            return desired > poolCount ? poolCount : desired;
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
            _activePreloadOrderedIndex = -1;
        }

        private void RemovePreloadUrl(VRCUrl url)
        {
            if (!Networking.IsOwner(gameObject))
            {
                return;
            }

            preloadOrderedUrls = RemoveUrlFromArray(preloadOrderedUrls, url);
            preloadLandscapeUrls = RemoveUrlFromArray(preloadLandscapeUrls, url);
            preloadPortraitUrls = RemoveUrlFromArray(preloadPortraitUrls, url);
            preloadRevision++;
            RequestSerialization();
        }

        private VRCUrl[] RemoveUrlFromArray(VRCUrl[] source, VRCUrl url)
        {
            string urlString = GetSafeUrlString(url);
            if (source == null || source.Length == 0 || string.IsNullOrEmpty(urlString))
            {
                return source == null ? new VRCUrl[0] : source;
            }

            int keepCount = 0;
            int index = 0;
            while (index < source.Length)
            {
                if (GetSafeUrlString(source[index]) != urlString)
                {
                    keepCount++;
                }

                index++;
            }

            if (keepCount == source.Length)
            {
                return source;
            }

            VRCUrl[] result = new VRCUrl[keepCount];
            int writeIndex = 0;
            index = 0;
            while (index < source.Length)
            {
                if (GetSafeUrlString(source[index]) != urlString)
                {
                    result[writeIndex] = source[index];
                    writeIndex++;
                }

                index++;
            }

            return result;
        }

        private VRCUrl[] RemoveUrlAtIndex(VRCUrl[] source, int removeIndex)
        {
            if (source == null || source.Length == 0 || removeIndex < 0 || removeIndex >= source.Length)
            {
                return source == null ? new VRCUrl[0] : source;
            }

            VRCUrl[] result = new VRCUrl[source.Length - 1];
            int sourceIndex = 0;
            int writeIndex = 0;
            while (sourceIndex < source.Length)
            {
                if (sourceIndex != removeIndex)
                {
                    result[writeIndex] = source[sourceIndex];
                    writeIndex++;
                }

                sourceIndex++;
            }

            return result;
        }

        private VRCUrl[] RemoveFirstUrlFromArray(VRCUrl[] source)
        {
            if (source == null || source.Length <= 1)
            {
                return new VRCUrl[0];
            }

            VRCUrl[] result = new VRCUrl[source.Length - 1];
            int index = 1;
            while (index < source.Length)
            {
                result[index - 1] = source[index];
                index++;
            }

            return result;
        }

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

        private void EnsureCacheArrays()
        {
            int safeCapacity = GetMaxCachedTextures();
            if (_cachedUrlStrings != null && _cachedUrlStrings.Length == safeCapacity)
            {
                return;
            }

            DisposeCachedDownloads();
            _cachedUrlStrings = new string[safeCapacity];
            _cachedTextures = new Texture2D[safeCapacity];
            _cachedDownloads = new IVRCImageDownload[safeCapacity];
            _displayedDownloads = new IVRCImageDownload[safeCapacity];
            _displayedDownloadIndex = 0;
            _cachedAccessTicks = new int[safeCapacity];
        }

        private int GetMaxCachedTextures()
        {
            return Mathf.Max(0, preloadLandscapeCacheSize) + Mathf.Max(0, preloadPortraitCacheSize);
        }

        private void DisposeCachedDownloads()
        {
            DisposeDisplayedDownloads();
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
        }

        private void KeepDisplayedDownload(IVRCImageDownload download)
        {
            if (download == null || _displayedDownloads == null || _displayedDownloads.Length == 0)
            {
                return;
            }

            int index = PositiveModulo(_displayedDownloadIndex, _displayedDownloads.Length);
            if (_displayedDownloads[index] != null)
            {
                _displayedDownloads[index].Dispose();
            }

            _displayedDownloads[index] = download;
            _displayedDownloadIndex = index + 1;
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
                    _displayedDownloads[index].Dispose();
                    _displayedDownloads[index] = null;
                }

                index++;
            }
        }

        private int FindCachedUrlIndex(string url)
        {
            EnsureCacheArrays();
            int index = 0;
            while (index < _cachedUrlStrings.Length)
            {
                if (_cachedTextures[index] != null && _cachedUrlStrings[index] == url)
                {
                    return index;
                }

                index++;
            }

            return -1;
        }

        private int FindEmptyCacheIndex()
        {
            int index = 0;
            while (index < _cachedTextures.Length)
            {
                if (_cachedTextures[index] == null || string.IsNullOrEmpty(_cachedUrlStrings[index]))
                {
                    return index;
                }

                index++;
            }

            return -1;
        }

        private int FindLeastRecentlyUsedCacheIndex()
        {
            if (_cachedAccessTicks == null || _cachedAccessTicks.Length == 0)
            {
                return -1;
            }

            int selectedIndex = 0;
            int selectedTick = _cachedAccessTicks[0];
            int index = 1;
            while (index < _cachedAccessTicks.Length)
            {
                if (_cachedAccessTicks[index] < selectedTick)
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

        public bool ContainsManagedGroup(RemotePhotoGroup group)
        {
            if (group == null || managedGroups == null)
            {
                return false;
            }

            int index = 0;
            while (index < managedGroups.Length)
            {
                if (managedGroups[index] == group)
                {
                    return true;
                }

                index++;
            }

            return false;
        }

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
    }
}
