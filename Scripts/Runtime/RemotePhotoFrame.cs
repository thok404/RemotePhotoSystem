using UdonSharp;
using UnityEngine;
using VRC.SDK3.Image;
using VRC.SDKBase;
using VRC.Udon.Common.Interfaces;

namespace RemotePhotoSystem
{
    [RequireComponent(typeof(MeshRenderer))]
    [UdonBehaviourSyncMode(BehaviourSyncMode.NoVariableSync)]
    public class RemotePhotoFrame : UdonSharpBehaviour
    {
        public RemotePhotoOrientation orientation = RemotePhotoOrientation.Landscape;
        public int materialSlot;
        public string texturePropertyName = "_MainTex";
        public Texture defaultTexture;
        public bool useFallbackTexture = true;
        public Texture fallbackTexture;
        public RemotePhotoFitMode photoFitMode = RemotePhotoFitMode.Crop;
        public RemotePhotoProjectionMode projectionMode = RemotePhotoProjectionMode.MeshUv;
        public bool boxProjectionHorizontalFlip;
        [Range(0f, 360f)]
        public float photoRotationDegrees;
        public Color backgroundColor = Color.black;
        public RemotePhotoAspectMode aspectMode = RemotePhotoAspectMode.Auto;
        public RemotePhotoAxisMode axisMode = RemotePhotoAxisMode.Auto;
        public RemotePhotoAxis frameWidthAxis = RemotePhotoAxis.X;
        public RemotePhotoAxis frameHeightAxis = RemotePhotoAxis.Y;
        public Vector3 referenceBoxCenter = Vector3.zero;
        public Vector3 referenceBoxSize = Vector3.one;
        public float manualAspectRatio = 1.7777778f;

        private Material _runtimeMaterial;
        private MeshRenderer _meshRenderer;
        private MeshFilter _meshFilter;
        private VRCImageDownloader _downloader;
        private IVRCImageDownload _currentDownload;
        private IVRCImageDownload _displayedDirectDownload;
        private TextureInfo _textureInfo;
        private VRCUrl _activeVrcUrl;
        private string _activeUrl = string.Empty;
        private string _pendingRetryUrl = string.Empty;
        private string _pendingGalleryCacheUrl = string.Empty;
        private int _activeFitMode;
        private int _activeRetryCount;
        private int _activeSelectionRevision = NoSelectionRevision;
        private RemotePhotoManager _activeManager;
        private RemotePhotoGroup _activeGroup;
        private int _activeSlotIndex = -1;
        private int _activeRequestSerial;
        private int _currentDownloadSelectionRevision = NoSelectionRevision;
        private RemotePhotoGroup _currentDownloadGroup;
        private int _currentDownloadSlotIndex = -1;
        private int _currentDownloadRequestSerial;
        private int _pendingRetryRevision = NoSelectionRevision;
        private int _pendingRetrySerial;
        private int _pendingGalleryCacheRevision = NoSelectionRevision;
        private int _pendingGalleryCacheSerial;
        private RemotePhotoManager _displayHandleManager;
        private float _resolvedFrameAspectRatio = 1.7777778f;
        private int _cachedShortestAxis = 2;
        private Vector3 _cachedBoundsCenter = Vector3.zero;
        private Vector3 _cachedBoundsSize = Vector3.one;
        private const int DefaultDownloadRetryAttempts = 3;
        private const int NoSelectionRevision = -1;
        private const float DefaultDownloadRetryDelaySeconds = 2f;
        private const float GalleryCachePollDelaySeconds = 1f;
        private const string RemotePhotoBackgroundColorPropertyName = "_RemotePhotoBackgroundColor";
        private const string RemotePhotoFitModePropertyName = "_RemotePhotoFitMode";
        private const string PhotoRotationDegreesPropertyName = "_PhotoRotationDegrees";
        private const string RemotePhotoProjectionModePropertyName = "_RemotePhotoProjectionMode";
        private const string RemotePhotoShortestAxisPropertyName = "_RemotePhotoShortestAxis";
        private const string RemotePhotoBoundsCenterXPropertyName = "_RemotePhotoBoundsCenterX";
        private const string RemotePhotoBoundsCenterYPropertyName = "_RemotePhotoBoundsCenterY";
        private const string RemotePhotoBoundsCenterZPropertyName = "_RemotePhotoBoundsCenterZ";
        private const string RemotePhotoBoundsSizeXPropertyName = "_RemotePhotoBoundsSizeX";
        private const string RemotePhotoBoundsSizeYPropertyName = "_RemotePhotoBoundsSizeY";
        private const string RemotePhotoBoundsSizeZPropertyName = "_RemotePhotoBoundsSizeZ";
        private const string RemotePhotoBoxHorizontalFlipPropertyName = "_RemotePhotoBoxHorizontalFlip";
        private const string RemotePhotoUvScaleXPropertyName = "_RemotePhotoUvScaleX";
        private const string RemotePhotoUvScaleYPropertyName = "_RemotePhotoUvScaleY";
        private const string RemotePhotoUvOffsetXPropertyName = "_RemotePhotoUvOffsetX";
        private const string RemotePhotoUvOffsetYPropertyName = "_RemotePhotoUvOffsetY";
        private const string RemotePhotoPreloadTexturePropertyName = "_RemotePhotoPreloadTex";

        public void Start()
        {
            InitializeMaterial();
            EnsureDownloader();
            RefreshCachedProjectionGeometry();
            RefreshResolvedFrameAspectRatio();
            ApplyDefault();
        }

        public Material GetRuntimeMaterial()
        {
            if (_runtimeMaterial == null)
            {
                InitializeMaterial();
            }

            return _runtimeMaterial;
        }

        public void LoadPhoto(VRCUrl url)
        {
            BeginDemandLoad(url, null, NoSelectionRevision, null, -1, 0);
        }

        public void LoadPhotoFromManager(VRCUrl url, RemotePhotoManager manager, int selectionRevision)
        {
            LoadPhotoFromManagerSlot(url, manager, selectionRevision, null, -1, 0);
        }

        public void ApplyManagerTexture(Texture texture, RemotePhotoManager manager)
        {
            _activeManager = manager;
            _activeGroup = null;
            _activeSlotIndex = -1;
            _activeRequestSerial = 0;
            _pendingRetryUrl = string.Empty;
            _pendingRetryRevision = NoSelectionRevision;
            _pendingRetrySerial = 0;
            _pendingGalleryCacheUrl = string.Empty;
            _pendingGalleryCacheRevision = NoSelectionRevision;
            _activeFitMode = RemotePhotoFitModeUtility.ToInt(photoFitMode);

            if (_runtimeMaterial == null)
            {
                InitializeMaterial();
            }

            if (texture == null)
            {
                return;
            }

            CancelCurrentDownload();
            IVRCImageDownload previousDirectDownload = _displayedDirectDownload;
            _displayedDirectDownload = null;
            ApplyTexture(texture, _activeFitMode);
            _displayHandleManager = manager;
            DisposeDownload(previousDirectDownload);
        }

        public void LoadPhotoFromManagerSlot(VRCUrl url, RemotePhotoManager manager, int selectionRevision, RemotePhotoGroup group, int slotIndex, int requestSerial)
        {
            _activeManager = manager;
            _activeGroup = group;
            _activeSlotIndex = slotIndex;
            _activeRequestSerial = requestSerial;

            if (_runtimeMaterial == null)
            {
                InitializeMaterial();
            }

            if (RemotePhotoUrlUtility.IsValidVrcUrl(url) &&
                selectionRevision == _activeSelectionRevision &&
                url.Get() == _activeUrl)
            {
                if (manager != null && !manager.IsPreloadEnabled() && _currentDownload != null)
                {
                    return;
                }

                if (string.IsNullOrEmpty(_pendingGalleryCacheUrl))
                {
                    NotifyGroupDisplayFinished();
                }
                else if (manager != null && manager.IsPreloadEnabled() && manager.configuredPlayMode != RemotePhotoPlayMode.Random)
                {
                    TryApplyManagerCacheOrFailure();
                }
                else
                {
                    _pendingGalleryCacheRevision = selectionRevision;
                    _pendingGalleryCacheSerial = requestSerial;
                    SendCustomEventDelayedSeconds(nameof(_ApplyGalleryCacheWhenReady), GalleryCachePollDelaySeconds);
                }

                return;
            }

            _activeSelectionRevision = selectionRevision;

            if (!RemotePhotoUrlUtility.IsValidVrcUrl(url))
            {
                _activeVrcUrl = null;
                _activeUrl = string.Empty;
                _pendingRetryUrl = string.Empty;
                _pendingRetryRevision = NoSelectionRevision;
                _pendingRetrySerial = 0;
                _pendingGalleryCacheUrl = string.Empty;
                _pendingGalleryCacheRevision = NoSelectionRevision;
                _activeFitMode = RemotePhotoFitModeUtility.ToInt(photoFitMode);
                _activeRetryCount = 0;
                CancelCurrentDownload();
                ApplyFallback();
                ReleaseDisplayedManagerDownloadIfFallbackApplied();
                ReleaseDisplayedDirectDownloadIfFallbackApplied();
                NotifyGroupDisplayFinished();
                return;
            }

            if (manager != null && manager.IsPreloadEnabled())
            {
                Texture2D cachedTexture = manager.GetCachedTexture(url);
                if (cachedTexture != null)
                {
                    CancelCurrentDownload();
                    IVRCImageDownload previousDirectDownload = _displayedDirectDownload;
                    _displayedDirectDownload = null;
                    _activeVrcUrl = url;
                    _activeUrl = url.Get();
                    _pendingRetryUrl = string.Empty;
                    _pendingRetryRevision = NoSelectionRevision;
                    _pendingRetrySerial = 0;
                    _pendingGalleryCacheUrl = string.Empty;
                    _pendingGalleryCacheRevision = NoSelectionRevision;
                    _activeFitMode = RemotePhotoFitModeUtility.ToInt(photoFitMode);
                    _activeRetryCount = 0;
                    LogDownload("Cache hit: " + gameObject.name + " -> " + _activeUrl);
                    ApplyTexture(cachedTexture, _activeFitMode);
                    if (manager.configuredPlayMode == RemotePhotoPlayMode.Random)
                    {
                        manager.ConsumeCachedTexture(url, this);
                    }
                    else
                    {
                        manager.RetainCachedTextureForFrame(url, this);
                    }

                    _displayHandleManager = manager;
                    DisposeDownload(previousDirectDownload);
                    NotifyGroupDisplayFinished();
                    return;
                }

                _activeVrcUrl = url;
                _activeUrl = url.Get();
                _pendingRetryUrl = string.Empty;
                _pendingRetryRevision = NoSelectionRevision;
                _pendingRetrySerial = 0;
                _pendingGalleryCacheUrl = _activeUrl;
                _pendingGalleryCacheRevision = selectionRevision;
                _pendingGalleryCacheSerial = requestSerial;
                _activeFitMode = RemotePhotoFitModeUtility.ToInt(photoFitMode);
                _activeRetryCount = 0;
                manager.LogDebug("Cache not ready, frame waits for preload queue: " + gameObject.name + " -> " + _activeUrl);
                manager.WakePreloadQueue();
                if (manager.configuredPlayMode == RemotePhotoPlayMode.Random)
                {
                    SendCustomEventDelayedSeconds(nameof(_ApplyGalleryCacheWhenReady), GalleryCachePollDelaySeconds);
                }

                return;
            }

            BeginDemandLoad(url, manager, selectionRevision, group, slotIndex, requestSerial);
        }

        public void ClearPhoto()
        {
            _activeVrcUrl = null;
            _activeUrl = string.Empty;
            _pendingRetryUrl = string.Empty;
            _pendingRetryRevision = NoSelectionRevision;
            _pendingRetrySerial = 0;
            _pendingGalleryCacheUrl = string.Empty;
            _pendingGalleryCacheRevision = NoSelectionRevision;
            _activeManager = null;
            _activeGroup = null;
            _activeSlotIndex = -1;
            _activeRequestSerial = 0;
            _activeSelectionRevision = NoSelectionRevision;
            _activeRetryCount = 0;
            CancelCurrentDownload();
            ApplyFallback();
            ReleaseDisplayedManagerDownloadIfFallbackApplied();
            ReleaseDisplayedDirectDownloadIfFallbackApplied();
        }

        public void _ApplyGalleryCacheWhenReady()
        {
            if (_activeManager == null ||
                _pendingGalleryCacheUrl != _activeUrl ||
                _pendingGalleryCacheRevision != _activeSelectionRevision ||
                _pendingGalleryCacheSerial != _activeRequestSerial ||
                !RemotePhotoUrlUtility.IsValidVrcUrl(_activeVrcUrl))
            {
                return;
            }

            if (TryApplyManagerCacheOrFailure())
            {
                return;
            }

            _activeManager.WakePreloadQueue();
            SendCustomEventDelayedSeconds(nameof(_ApplyGalleryCacheWhenReady), GalleryCachePollDelaySeconds);
        }

        private bool TryApplyManagerCacheOrFailure()
        {
            if (_activeManager == null || !RemotePhotoUrlUtility.IsValidVrcUrl(_activeVrcUrl))
            {
                return false;
            }

            Texture2D cachedTexture = _activeManager.GetCachedTextureQuiet(_activeVrcUrl);
            if (cachedTexture != null)
            {
                CancelCurrentDownload();
                IVRCImageDownload previousDirectDownload = _displayedDirectDownload;
                _displayedDirectDownload = null;
                LogDownload("Manager queue image ready: " + gameObject.name + " -> " + _activeUrl);
                _pendingGalleryCacheUrl = string.Empty;
                _pendingGalleryCacheRevision = NoSelectionRevision;
                ApplyTexture(cachedTexture, _activeFitMode);
                if (_activeManager.configuredPlayMode == RemotePhotoPlayMode.Random)
                {
                    _activeManager.ConsumeCachedTexture(_activeVrcUrl, this);
                }
                else
                {
                    _activeManager.RetainCachedTextureForFrame(_activeVrcUrl, this);
                }

                _displayHandleManager = _activeManager;
                DisposeDownload(previousDirectDownload);
                NotifyGroupDisplayFinished();
                return true;
            }

            if (_activeManager.IsTextureRequestFailed(_activeVrcUrl))
            {
                LogDownload("Manager queue image failed: " + gameObject.name + " -> " + _activeUrl);
                _pendingGalleryCacheUrl = string.Empty;
                _pendingGalleryCacheRevision = NoSelectionRevision;
                ApplyFallback();
                ReleaseDisplayedManagerDownloadIfFallbackApplied();
                ReleaseDisplayedDirectDownloadIfFallbackApplied();
                NotifyGroupDisplayFinished();
                return true;
            }

            return false;
        }

        public override void OnImageLoadSuccess(IVRCImageDownload result)
        {
            if (!IsCurrentDownloadResult(result) || result.Result == null)
            {
                return;
            }

            IVRCImageDownload previousDirectDownload = _displayedDirectDownload;
            _displayedDirectDownload = result;
            _currentDownload = null;
            _activeRetryCount = 0;
            _pendingRetryUrl = string.Empty;
            _pendingRetryRevision = NoSelectionRevision;
            _pendingRetrySerial = 0;
            _pendingGalleryCacheUrl = string.Empty;
            _pendingGalleryCacheRevision = NoSelectionRevision;
            LogDownload("Download success: " + gameObject.name + " Revision=" + _activeSelectionRevision + " Slot=" + _activeSlotIndex + " Serial=" + _activeRequestSerial + " -> " + _activeUrl);

            ApplyTexture(result.Result, _activeFitMode);
            ReleaseDisplayedManagerDownload();
            DisposeDownload(previousDirectDownload);
            NotifyGroupDisplayFinished();
        }

        public override void OnImageLoadError(IVRCImageDownload result)
        {
            if (!IsCurrentDownloadResult(result))
            {
                return;
            }

            CancelCurrentDownload();
            LogDownload("Download failed: " + gameObject.name + " Revision=" + _activeSelectionRevision + " Slot=" + _activeSlotIndex + " Serial=" + _activeRequestSerial + " -> " + _activeUrl + GetImageDownloadErrorDetails(result));
            if (IsNonRetryableImageError(result))
            {
                _pendingRetryUrl = string.Empty;
                _pendingRetryRevision = NoSelectionRevision;
                _pendingRetrySerial = 0;
                LogDownload("Download non-retryable error, applying fallback: " + gameObject.name + " -> " + _activeUrl);
                ApplyFallback();
                ReleaseDisplayedManagerDownloadIfFallbackApplied();
                ReleaseDisplayedDirectDownloadIfFallbackApplied();
                NotifyGroupDisplayFinished();
                return;
            }

            int maxRetryAttempts = GetImageRetryAttempts();
            if (_activeRetryCount < maxRetryAttempts && RemotePhotoUrlUtility.IsValidVrcUrl(_activeVrcUrl))
            {
                _activeRetryCount++;
                _pendingRetryUrl = _activeUrl;
                _pendingRetryRevision = _activeSelectionRevision;
                _pendingRetrySerial = _activeRequestSerial;
                float retryDelay = GetImageRetryDelaySeconds();
                LogDownload("Download retry " + _activeRetryCount + "/" + maxRetryAttempts + " in " + retryDelay + "s: " + gameObject.name + " -> " + _activeUrl);
                SendCustomEventDelayedSeconds(nameof(_RetryActiveDownload), retryDelay);
                return;
            }

            _pendingRetryUrl = string.Empty;
            _pendingRetryRevision = NoSelectionRevision;
            _pendingRetrySerial = 0;
            LogDownload("Download gave up, applying fallback: " + gameObject.name + " -> " + _activeUrl);
            ApplyFallback();
            ReleaseDisplayedManagerDownloadIfFallbackApplied();
            ReleaseDisplayedDirectDownloadIfFallbackApplied();
            NotifyGroupDisplayFinished();
        }

        public void _RetryActiveDownload()
        {
            if (_pendingRetryUrl != _activeUrl ||
                _pendingRetryRevision != _activeSelectionRevision ||
                _pendingRetrySerial != _activeRequestSerial ||
                !RemotePhotoUrlUtility.IsValidVrcUrl(_activeVrcUrl))
            {
                LogDownload("Download retry skipped because the active URL changed: " + gameObject.name);
                return;
            }

            _pendingRetryUrl = string.Empty;
            _pendingRetryRevision = NoSelectionRevision;
            _pendingRetrySerial = 0;
            LogDownload("Download retry start: " + gameObject.name + " -> " + _activeUrl);
            StartActiveDownload();
        }

        public void OnDestroy()
        {
            ReleaseDisplayedManagerDownload();
            CancelCurrentDownload();
            ReleaseDisplayedDirectDownload();

            if (_downloader != null)
            {
                _downloader.Dispose();
                _downloader = null;
            }
        }

        public void OnDrawGizmosSelected()
        {
            if (aspectMode != RemotePhotoAspectMode.ReferenceBox)
            {
                return;
            }

            Matrix4x4 previousMatrix = Gizmos.matrix;
            Color previousColor = Gizmos.color;

            Gizmos.matrix = transform.localToWorldMatrix;
            Gizmos.color = new Color(1f, 0.75f, 0.2f, 0.9f);
            Gizmos.DrawWireCube(referenceBoxCenter, referenceBoxSize);

            Gizmos.matrix = previousMatrix;
            Gizmos.color = previousColor;
        }

        private void InitializeMaterial()
        {
            if (_meshRenderer == null)
            {
                _meshRenderer = GetComponent<MeshRenderer>();
            }

            if (_meshFilter == null)
            {
                _meshFilter = GetComponent<MeshFilter>();
            }

            RefreshCachedProjectionGeometry();

            if (_meshRenderer == null)
            {
                return;
            }

            Material[] materials = _meshRenderer.materials;
            if (materials == null || materialSlot < 0 || materialSlot >= materials.Length)
            {
                return;
            }

            _runtimeMaterial = materials[materialSlot];
        }

        private void EnsureDownloader()
        {
            if (_downloader == null)
            {
                _downloader = new VRCImageDownloader();
            }
        }

        private void CancelCurrentDownload()
        {
            if (_currentDownload != null)
            {
                _currentDownload.Dispose();
                _currentDownload = null;
            }

            _currentDownloadSelectionRevision = NoSelectionRevision;
            _currentDownloadGroup = null;
            _currentDownloadSlotIndex = -1;
            _currentDownloadRequestSerial = 0;
        }

        private void DisposeDownload(IVRCImageDownload download)
        {
            if (download == null)
            {
                return;
            }

            if (download == _currentDownload)
            {
                _currentDownload = null;
            }

            download.Dispose();
        }

        private void ReleaseDisplayedDirectDownload()
        {
            if (_displayedDirectDownload != null)
            {
                _displayedDirectDownload.Dispose();
                _displayedDirectDownload = null;
            }
        }

        private void NotifyGroupDisplayFinished()
        {
            if (_activeGroup != null)
            {
                _activeGroup.NotifyFrameDisplayFinished(_activeSlotIndex, _activeSelectionRevision, _activeRequestSerial);
            }
        }

        private bool IsCurrentDownloadResult(IVRCImageDownload result)
        {
            if (result == null ||
                result != _currentDownload ||
                result.Url == null ||
                result.Url.Get() != _activeUrl)
            {
                return false;
            }

            if (_currentDownloadSelectionRevision != _activeSelectionRevision ||
                _currentDownloadGroup != _activeGroup ||
                _currentDownloadSlotIndex != _activeSlotIndex ||
                _currentDownloadRequestSerial != _activeRequestSerial)
            {
                return false;
            }

            return true;
        }

        private void ReleaseDisplayedManagerDownload()
        {
            if (_displayHandleManager != null)
            {
                _displayHandleManager.ReleaseDisplayedDownload(this);
                _displayHandleManager = null;
            }
        }

        private void ReleaseDisplayedManagerDownloadIfFallbackApplied()
        {
            if (useFallbackTexture && fallbackTexture != null)
            {
                ReleaseDisplayedManagerDownload();
            }
        }

        private void ReleaseDisplayedDirectDownloadIfFallbackApplied()
        {
            if (useFallbackTexture && fallbackTexture != null)
            {
                ReleaseDisplayedDirectDownload();
            }
        }

        private void StartActiveDownload()
        {
            if (!RemotePhotoUrlUtility.IsValidVrcUrl(_activeVrcUrl))
            {
                return;
            }

            CancelCurrentDownload();

            _textureInfo = new TextureInfo();
            _textureInfo.GenerateMipMaps = false;
            _textureInfo.MaterialProperty = RemotePhotoPreloadTexturePropertyName;
            _textureInfo.WrapModeU = _activeFitMode == RemotePhotoFitModeUtility.ToInt(RemotePhotoFitMode.Tile)
                ? TextureWrapMode.Repeat
                : TextureWrapMode.Clamp;
            _textureInfo.WrapModeV = _textureInfo.WrapModeU;

            _currentDownloadSelectionRevision = _activeSelectionRevision;
            _currentDownloadGroup = _activeGroup;
            _currentDownloadSlotIndex = _activeSlotIndex;
            _currentDownloadRequestSerial = _activeRequestSerial;
            _currentDownload = _downloader.DownloadImage(_activeVrcUrl, _runtimeMaterial, (IUdonEventReceiver)this, _textureInfo);
        }

        private void BeginDemandLoad(VRCUrl url, RemotePhotoManager manager, int selectionRevision, RemotePhotoGroup group, int slotIndex, int requestSerial)
        {
            _activeManager = manager;
            _activeGroup = group;
            _activeSlotIndex = slotIndex;
            _activeRequestSerial = requestSerial;
            _activeSelectionRevision = selectionRevision;

            if (_runtimeMaterial == null)
            {
                InitializeMaterial();
            }

            if (!RemotePhotoUrlUtility.IsValidVrcUrl(url))
            {
                CancelCurrentDownload();
                _activeVrcUrl = null;
                _activeUrl = string.Empty;
                _pendingRetryUrl = string.Empty;
                _pendingRetryRevision = NoSelectionRevision;
                _pendingRetrySerial = 0;
                _pendingGalleryCacheUrl = string.Empty;
                _pendingGalleryCacheRevision = NoSelectionRevision;
                _activeFitMode = RemotePhotoFitModeUtility.ToInt(photoFitMode);
                _activeRetryCount = 0;
                ApplyFallback();
                ReleaseDisplayedManagerDownloadIfFallbackApplied();
                ReleaseDisplayedDirectDownloadIfFallbackApplied();
                NotifyGroupDisplayFinished();
                return;
            }

            EnsureDownloader();

            _activeVrcUrl = url;
            _activeUrl = url.Get();
            _pendingRetryUrl = string.Empty;
            _pendingRetryRevision = NoSelectionRevision;
            _pendingRetrySerial = 0;
            _pendingGalleryCacheUrl = string.Empty;
            _pendingGalleryCacheRevision = NoSelectionRevision;
            _activeFitMode = RemotePhotoFitModeUtility.ToInt(photoFitMode);
            _activeRetryCount = 0;

            LogDownload("Download start: " + gameObject.name + " Revision=" + _activeSelectionRevision + " Slot=" + _activeSlotIndex + " Serial=" + _activeRequestSerial + " -> " + _activeUrl);
            StartActiveDownload();
        }

        private int GetImageRetryAttempts()
        {
            if (_activeManager != null)
            {
                return _activeManager.GetImageRetryAttempts();
            }

            return DefaultDownloadRetryAttempts;
        }

        private float GetImageRetryDelaySeconds()
        {
            if (_activeManager != null)
            {
                return _activeManager.GetImageRetryDelaySeconds();
            }

            return DefaultDownloadRetryDelaySeconds;
        }

        private void ApplyFallback()
        {
            if (useFallbackTexture && fallbackTexture != null)
            {
                ApplyTexture(fallbackTexture, RemotePhotoFitModeUtility.ToInt(RemotePhotoFitMode.Contain));
            }
        }

        private void ApplyDefault()
        {
            if (defaultTexture != null)
            {
                ApplyTexture(defaultTexture, RemotePhotoFitModeUtility.ToInt(RemotePhotoFitMode.Contain));
            }
        }

        private void ApplyTexture(Texture texture, int fitMode)
        {
            if (_runtimeMaterial == null)
            {
                return;
            }

            _runtimeMaterial.SetTexture(texturePropertyName, texture);
            _runtimeMaterial.SetColor(RemotePhotoBackgroundColorPropertyName, backgroundColor);
            _runtimeMaterial.SetFloat(RemotePhotoFitModePropertyName, GetFitModeFloat(fitMode));
            _runtimeMaterial.SetFloat(PhotoRotationDegreesPropertyName, GetResolvedPhotoRotationDegrees());
            ApplyProjectionProperties();
            SetMaterialUv(fitMode, texture);
        }

        private void ApplyProjectionProperties()
        {
            _runtimeMaterial.SetFloat(RemotePhotoProjectionModePropertyName, GetProjectionModeFloat());
            _runtimeMaterial.SetFloat(RemotePhotoBoxHorizontalFlipPropertyName, GetBoxHorizontalFlipFloat());
            _runtimeMaterial.SetFloat(RemotePhotoShortestAxisPropertyName, GetAxisFloat(_cachedShortestAxis));
            _runtimeMaterial.SetFloat(RemotePhotoBoundsCenterXPropertyName, _cachedBoundsCenter.x);
            _runtimeMaterial.SetFloat(RemotePhotoBoundsCenterYPropertyName, _cachedBoundsCenter.y);
            _runtimeMaterial.SetFloat(RemotePhotoBoundsCenterZPropertyName, _cachedBoundsCenter.z);
            _runtimeMaterial.SetFloat(RemotePhotoBoundsSizeXPropertyName, _cachedBoundsSize.x);
            _runtimeMaterial.SetFloat(RemotePhotoBoundsSizeYPropertyName, _cachedBoundsSize.y);
            _runtimeMaterial.SetFloat(RemotePhotoBoundsSizeZPropertyName, _cachedBoundsSize.z);
        }

        private void LogDownload(string message)
        {
            if (_activeManager != null && _activeManager.debugLogs)
            {
                Debug.Log("[RemotePhotoSystem] " + message);
            }
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

        private float NormalizeRotationDegrees(float value)
        {
            float result = value % 360f;
            if (result < 0f)
            {
                result += 360f;
            }

            return result;
        }

        private float GetResolvedPhotoRotationDegrees()
        {
            float orientationRotation = 0f;
            if (orientation == RemotePhotoOrientation.Portrait)
            {
                orientationRotation = 90f;
            }

            return NormalizeRotationDegrees(orientationRotation + photoRotationDegrees);
        }

        private bool IsResolvedPhotoRotationSideways()
        {
            float rotation = GetResolvedPhotoRotationDegrees();
            if (rotation >= 45f && rotation < 135f)
            {
                return true;
            }

            return rotation >= 225f && rotation < 315f;
        }

        public void RefreshResolvedFrameAspectRatio()
        {
            float nextAspect = ResolveFrameAspectRatio();
            _resolvedFrameAspectRatio = nextAspect > 0f ? nextAspect : 1f;
        }

        public float GetResolvedFrameAspectRatio()
        {
            if (_resolvedFrameAspectRatio <= 0f)
            {
                RefreshResolvedFrameAspectRatio();
            }

            return _resolvedFrameAspectRatio;
        }

        public bool HasValidAutomaticAspectRatio()
        {
            if (aspectMode == RemotePhotoAspectMode.Manual)
            {
                return true;
            }

            return TryResolveAutomaticFrameAspectRatio() > 0f;
        }

        private void SetMaterialUv(int fitMode, Texture texture)
        {
            if (_runtimeMaterial == null)
            {
                return;
            }

            Vector2 scale = Vector2.one;
            Vector2 offset = Vector2.zero;

            float textureAspect = 1f;
            if (texture != null && texture.height > 0)
            {
                textureAspect = (float)texture.width / texture.height;
                if (textureAspect > 0f && IsResolvedPhotoRotationSideways())
                {
                    textureAspect = 1f / textureAspect;
                }
            }

            float safeFrameAspect = GetResolvedFrameAspectRatio();

            if (fitMode == RemotePhotoFitModeUtility.ToInt(RemotePhotoFitMode.Crop))
            {
                if (textureAspect > safeFrameAspect)
                {
                    scale.x = safeFrameAspect / textureAspect;
                    offset.x = (1f - scale.x) * 0.5f;
                }
                else
                {
                    scale.y = textureAspect / safeFrameAspect;
                    offset.y = (1f - scale.y) * 0.5f;
                }
            }
            else if (fitMode == RemotePhotoFitModeUtility.ToInt(RemotePhotoFitMode.Contain))
            {
                if (textureAspect > safeFrameAspect)
                {
                    scale.y = textureAspect / safeFrameAspect;
                    offset.y = (1f - scale.y) * 0.5f;
                }
                else
                {
                    scale.x = safeFrameAspect / textureAspect;
                    offset.x = (1f - scale.x) * 0.5f;
                }
            }
            else if (fitMode == RemotePhotoFitModeUtility.ToInt(RemotePhotoFitMode.Tile))
            {
                if (textureAspect > safeFrameAspect)
                {
                    scale.x = textureAspect / safeFrameAspect;
                }
                else
                {
                    scale.y = safeFrameAspect / textureAspect;
                }
            }

            _runtimeMaterial.SetTextureScale(texturePropertyName, scale);
            _runtimeMaterial.SetTextureOffset(texturePropertyName, offset);
            _runtimeMaterial.SetFloat(RemotePhotoUvScaleXPropertyName, scale.x);
            _runtimeMaterial.SetFloat(RemotePhotoUvScaleYPropertyName, scale.y);
            _runtimeMaterial.SetFloat(RemotePhotoUvOffsetXPropertyName, offset.x);
            _runtimeMaterial.SetFloat(RemotePhotoUvOffsetYPropertyName, offset.y);
        }

        private float ResolveFrameAspectRatio()
        {
            if (aspectMode == RemotePhotoAspectMode.Manual)
            {
                return manualAspectRatio <= 0f ? 1f : manualAspectRatio;
            }

            float automaticAspect = TryResolveAutomaticFrameAspectRatio();
            if (automaticAspect > 0f)
            {
                return automaticAspect;
            }

            return 1f;
        }

        private float TryResolveAutomaticFrameAspectRatio()
        {
            if (aspectMode == RemotePhotoAspectMode.Manual)
            {
                return 0f;
            }

            if (aspectMode == RemotePhotoAspectMode.ReferenceBox)
            {
                float boxAspect = TryGetReferenceBoxAspectRatio();
                if (boxAspect > 0f)
                {
                    return boxAspect;
                }
            }
            else if (aspectMode == RemotePhotoAspectMode.Auto)
            {
                float rendererAspect = TryGetRendererAspectRatio();
                if (rendererAspect > 0f)
                {
                    return rendererAspect;
                }
            }

            return 0f;
        }

        private float TryGetReferenceBoxAspectRatio()
        {
            if (referenceBoxSize.x <= 0f || referenceBoxSize.y <= 0f || referenceBoxSize.z <= 0f)
            {
                return 0f;
            }

            return BuildAspectRatio(referenceBoxSize, axisMode);
        }

        private float TryGetRendererAspectRatio()
        {
            if (_meshRenderer == null)
            {
                _meshRenderer = GetComponent<MeshRenderer>();
            }

            if (_meshRenderer == null)
            {
                return 0f;
            }

            if (_meshFilter == null)
            {
                _meshFilter = GetComponent<MeshFilter>();
            }

            if (_meshFilter == null || _meshFilter.sharedMesh == null)
            {
                return 0f;
            }

            return BuildAspectRatio(_meshFilter.sharedMesh.bounds.size, RemotePhotoAxisMode.Auto);
        }

        private void RefreshCachedProjectionGeometry()
        {
            if (_meshFilter == null)
            {
                _meshFilter = GetComponent<MeshFilter>();
            }

            if (_meshFilter == null || _meshFilter.sharedMesh == null)
            {
                _cachedShortestAxis = 2;
                _cachedBoundsCenter = Vector3.zero;
                _cachedBoundsSize = Vector3.one;
                return;
            }

            Bounds bounds = _meshFilter.sharedMesh.bounds;
            Vector3 size = bounds.size;
            _cachedShortestAxis = GetShortestAxis(size);
            _cachedBoundsCenter = bounds.center;
            _cachedBoundsSize = new Vector3(
                Mathf.Max(0.0001f, Mathf.Abs(size.x)),
                Mathf.Max(0.0001f, Mathf.Abs(size.y)),
                Mathf.Max(0.0001f, Mathf.Abs(size.z)));
        }

        private float BuildAspectRatio(Vector3 size, RemotePhotoAxisMode selectedAxisMode)
        {
            if (selectedAxisMode == RemotePhotoAxisMode.Auto)
            {
                return BuildAutoPlaneAspectRatio(size);
            }

            float width = GetAxisSize(size, frameWidthAxis);
            float height = GetAxisSize(size, frameHeightAxis);
            if (width <= 0f || height <= 0f)
            {
                return 0f;
            }

            return width / height;
        }

        private float BuildAutoPlaneAspectRatio(Vector3 size)
        {
            float scaledX = Mathf.Abs(size.x);
            float scaledY = Mathf.Abs(size.y);
            float scaledZ = Mathf.Abs(size.z);

            float shortest = scaledX;
            if (scaledY < shortest)
            {
                shortest = scaledY;
            }

            if (scaledZ < shortest)
            {
                shortest = scaledZ;
            }

            float width = 0f;
            float height = 0f;

            if (shortest == scaledX)
            {
                width = scaledY;
                height = scaledZ;
            }
            else if (shortest == scaledY)
            {
                width = scaledX;
                height = scaledZ;
            }
            else
            {
                width = scaledX;
                height = scaledY;
            }

            if (width <= 0f || height <= 0f)
            {
                return 0f;
            }

            if (height > width)
            {
                float swap = width;
                width = height;
                height = swap;
            }

            return width / height;
        }

        private int GetShortestAxis(Vector3 size)
        {
            float scaledX = Mathf.Abs(size.x);
            float scaledY = Mathf.Abs(size.y);
            float scaledZ = Mathf.Abs(size.z);

            if (scaledX <= scaledY && scaledX <= scaledZ)
            {
                return 0;
            }

            if (scaledY <= scaledX && scaledY <= scaledZ)
            {
                return 1;
            }

            return 2;
        }

        private float GetProjectionModeFloat()
        {
            if (projectionMode == RemotePhotoProjectionMode.Box)
            {
                return 1f;
            }

            return 0f;
        }

        private float GetBoxHorizontalFlipFloat()
        {
            if (boxProjectionHorizontalFlip)
            {
                return 1f;
            }

            return 0f;
        }

        private float GetFitModeFloat(int fitMode)
        {
            if (fitMode == RemotePhotoFitModeUtility.ToInt(RemotePhotoFitMode.Contain))
            {
                return 1f;
            }

            if (fitMode == RemotePhotoFitModeUtility.ToInt(RemotePhotoFitMode.Stretch))
            {
                return 2f;
            }

            if (fitMode == RemotePhotoFitModeUtility.ToInt(RemotePhotoFitMode.Tile))
            {
                return 3f;
            }

            return 0f;
        }

        private float GetAxisFloat(int axis)
        {
            if (axis == 0)
            {
                return 0f;
            }

            if (axis == 1)
            {
                return 1f;
            }

            return 2f;
        }

        private float GetAxisSize(Vector3 size, RemotePhotoAxis axis)
        {
            if (axis == RemotePhotoAxis.X)
            {
                return Mathf.Abs(size.x);
            }

            if (axis == RemotePhotoAxis.Y)
            {
                return Mathf.Abs(size.y);
            }

            return Mathf.Abs(size.z);
        }
    }
}
