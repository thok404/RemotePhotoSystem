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
        private VRCImageDownloader _downloader;
        private IVRCImageDownload _currentDownload;
        private TextureInfo _textureInfo;
        private VRCUrl _activeVrcUrl;
        private string _activeUrl = string.Empty;
        private string _pendingRetryUrl = string.Empty;
        private string _pendingGalleryCacheUrl = string.Empty;
        private int _activeFitMode;
        private int _activeRetryCount;
        private RemotePhotoManager _activeGalleryService;
        private float _resolvedFrameAspectRatio = 1.7777778f;
        private const int DefaultDownloadRetryAttempts = 3;
        private const float DefaultDownloadRetryDelaySeconds = 2f;
        private const float GalleryCachePollDelaySeconds = 0.25f;
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

        public void Start()
        {
            InitializeMaterial();
            EnsureDownloader();
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
            _activeGalleryService = null;

            if (_runtimeMaterial == null)
            {
                InitializeMaterial();
            }

            if (!RemotePhotoUrlUtility.IsValidVrcUrl(url))
            {
                _activeVrcUrl = null;
                _activeUrl = string.Empty;
                _pendingRetryUrl = string.Empty;
                _activeFitMode = RemotePhotoFitModeUtility.ToInt(photoFitMode);
                _activeRetryCount = 0;
                CancelCurrentDownload();
                ApplyFallback();
                return;
            }

            EnsureDownloader();
            CancelCurrentDownload();

            _activeVrcUrl = url;
            _activeUrl = url.Get();
            _pendingRetryUrl = string.Empty;
            _activeFitMode = RemotePhotoFitModeUtility.ToInt(photoFitMode);
            _activeRetryCount = 0;

            LogDownload("Download start: " + gameObject.name + " -> " + _activeUrl);
            StartActiveDownload();
        }

        public void LoadPhotoFromGallery(VRCUrl url, RemotePhotoManager galleryService)
        {
            _activeGalleryService = galleryService;

            if (_runtimeMaterial == null)
            {
                InitializeMaterial();
            }

            if (!RemotePhotoUrlUtility.IsValidVrcUrl(url))
            {
                _activeVrcUrl = null;
                _activeUrl = string.Empty;
                _pendingRetryUrl = string.Empty;
                _activeFitMode = RemotePhotoFitModeUtility.ToInt(photoFitMode);
                _activeRetryCount = 0;
                CancelCurrentDownload();
                ApplyFallback();
                return;
            }

            if (galleryService != null && galleryService.IsPreloadEnabled())
            {
                Texture2D cachedTexture = galleryService.GetCachedTexture(url);
                if (cachedTexture != null)
                {
                    _activeVrcUrl = url;
                    _activeUrl = url.Get();
                    _pendingRetryUrl = string.Empty;
                    _activeFitMode = RemotePhotoFitModeUtility.ToInt(photoFitMode);
                    _activeRetryCount = 0;
                    CancelCurrentDownload();
                    LogDownload("Cache hit: " + gameObject.name + " -> " + _activeUrl);
                    ApplyTexture(cachedTexture, _activeFitMode);
                    galleryService.ConsumeCachedTexture(url);
                    return;
                }

                _activeVrcUrl = url;
                _activeUrl = url.Get();
                _pendingRetryUrl = string.Empty;
                _pendingGalleryCacheUrl = _activeUrl;
                _activeFitMode = RemotePhotoFitModeUtility.ToInt(photoFitMode);
                _activeRetryCount = 0;
                CancelCurrentDownload();
                galleryService.LogDebug("Cache not ready, frame waits for preload queue: " + gameObject.name + " -> " + _activeUrl);
                galleryService.RequestPreloadPriority(url);
                galleryService.WakePreloadQueue();
                SendCustomEventDelayedSeconds(nameof(_ApplyGalleryCacheWhenReady), GalleryCachePollDelaySeconds);
                return;
            }

            LoadPhoto(url);
            _activeGalleryService = galleryService;
        }

        public void ClearPhoto()
        {
            _activeVrcUrl = null;
            _activeUrl = string.Empty;
            _pendingRetryUrl = string.Empty;
            _pendingGalleryCacheUrl = string.Empty;
            _activeGalleryService = null;
            _activeRetryCount = 0;
            CancelCurrentDownload();
            ApplyFallback();
        }

        public void _ApplyGalleryCacheWhenReady()
        {
            if (_activeGalleryService == null ||
                _pendingGalleryCacheUrl != _activeUrl ||
                !RemotePhotoUrlUtility.IsValidVrcUrl(_activeVrcUrl))
            {
                return;
            }

            Texture2D cachedTexture = _activeGalleryService.GetCachedTextureQuiet(_activeVrcUrl);
            if (cachedTexture != null)
            {
                LogDownload("Service queue image ready: " + gameObject.name + " -> " + _activeUrl);
                _pendingGalleryCacheUrl = string.Empty;
                ApplyTexture(cachedTexture, _activeFitMode);
                _activeGalleryService.ConsumeCachedTexture(_activeVrcUrl);
                return;
            }

            _activeGalleryService.WakePreloadQueue();
            _activeGalleryService.RequestPreloadPriority(_activeVrcUrl);
            SendCustomEventDelayedSeconds(nameof(_ApplyGalleryCacheWhenReady), GalleryCachePollDelaySeconds);
        }

        public override void OnImageLoadSuccess(IVRCImageDownload result)
        {
            if (result == null || result.Result == null || result.Url == null || result.Url.Get() != _activeUrl)
            {
                return;
            }

            _currentDownload = result;
            _activeRetryCount = 0;
            LogDownload("Download success: " + gameObject.name + " -> " + _activeUrl);
            if (_activeGalleryService != null)
            {
                _activeGalleryService.StoreCachedTexture(result.Url, result.Result);
            }

            ApplyTexture(result.Result, _activeFitMode);
        }

        public override void OnImageLoadError(IVRCImageDownload result)
        {
            if (result != null && result.Url != null && result.Url.Get() != _activeUrl)
            {
                return;
            }

            CancelCurrentDownload();
            LogDownload("Download failed: " + gameObject.name + " -> " + _activeUrl + GetImageDownloadErrorDetails(result));
            if (IsNonRetryableImageError(result))
            {
                _pendingRetryUrl = string.Empty;
                LogDownload("Download non-retryable error, applying fallback: " + gameObject.name + " -> " + _activeUrl);
                ApplyFallback();
                return;
            }

            int maxRetryAttempts = GetImageRetryAttempts();
            if (_activeRetryCount < maxRetryAttempts && RemotePhotoUrlUtility.IsValidVrcUrl(_activeVrcUrl))
            {
                _activeRetryCount++;
                _pendingRetryUrl = _activeUrl;
                float retryDelay = GetImageRetryDelaySeconds();
                LogDownload("Download retry " + _activeRetryCount + "/" + maxRetryAttempts + " in " + retryDelay + "s: " + gameObject.name + " -> " + _activeUrl);
                SendCustomEventDelayedSeconds(nameof(_RetryActiveDownload), retryDelay);
                return;
            }

            _pendingRetryUrl = string.Empty;
            LogDownload("Download gave up, applying fallback: " + gameObject.name + " -> " + _activeUrl);
            ApplyFallback();
        }

        public void _RetryActiveDownload()
        {
            if (_pendingRetryUrl != _activeUrl || !RemotePhotoUrlUtility.IsValidVrcUrl(_activeVrcUrl))
            {
                LogDownload("Download retry skipped because the active URL changed: " + gameObject.name);
                return;
            }

            _pendingRetryUrl = string.Empty;
            LogDownload("Download retry start: " + gameObject.name + " -> " + _activeUrl);
            StartActiveDownload();
        }

        public void OnDestroy()
        {
            CancelCurrentDownload();

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
        }

        private void StartActiveDownload()
        {
            if (!RemotePhotoUrlUtility.IsValidVrcUrl(_activeVrcUrl))
            {
                return;
            }

            _textureInfo = new TextureInfo();
            _textureInfo.GenerateMipMaps = false;
            _textureInfo.MaterialProperty = texturePropertyName;
            _textureInfo.WrapModeU = _activeFitMode == RemotePhotoFitModeUtility.ToInt(RemotePhotoFitMode.Tile)
                ? TextureWrapMode.Repeat
                : TextureWrapMode.Clamp;
            _textureInfo.WrapModeV = _textureInfo.WrapModeU;

            _currentDownload = _downloader.DownloadImage(_activeVrcUrl, _runtimeMaterial, (IUdonEventReceiver)this, _textureInfo);
        }

        private int GetImageRetryAttempts()
        {
            if (_activeGalleryService != null)
            {
                return _activeGalleryService.GetImageRetryAttempts();
            }

            return DefaultDownloadRetryAttempts;
        }

        private float GetImageRetryDelaySeconds()
        {
            if (_activeGalleryService != null)
            {
                return _activeGalleryService.GetImageRetryDelaySeconds();
            }

            return DefaultDownloadRetryDelaySeconds;
        }

        private void ApplyFallback()
        {
            if (fallbackTexture != null)
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

            MeshFilter meshFilter = GetComponent<MeshFilter>();
            if (meshFilter == null || meshFilter.sharedMesh == null)
            {
                _runtimeMaterial.SetFloat(RemotePhotoShortestAxisPropertyName, 2f);
                _runtimeMaterial.SetFloat(RemotePhotoBoundsCenterXPropertyName, 0f);
                _runtimeMaterial.SetFloat(RemotePhotoBoundsCenterYPropertyName, 0f);
                _runtimeMaterial.SetFloat(RemotePhotoBoundsCenterZPropertyName, 0f);
                _runtimeMaterial.SetFloat(RemotePhotoBoundsSizeXPropertyName, 1f);
                _runtimeMaterial.SetFloat(RemotePhotoBoundsSizeYPropertyName, 1f);
                _runtimeMaterial.SetFloat(RemotePhotoBoundsSizeZPropertyName, 1f);
                return;
            }

            Bounds bounds = meshFilter.sharedMesh.bounds;
            Vector3 size = bounds.size;
            int shortestAxis = GetShortestAxis(size);

            _runtimeMaterial.SetFloat(RemotePhotoShortestAxisPropertyName, GetAxisFloat(shortestAxis));
            _runtimeMaterial.SetFloat(RemotePhotoBoundsCenterXPropertyName, bounds.center.x);
            _runtimeMaterial.SetFloat(RemotePhotoBoundsCenterYPropertyName, bounds.center.y);
            _runtimeMaterial.SetFloat(RemotePhotoBoundsCenterZPropertyName, bounds.center.z);
            _runtimeMaterial.SetFloat(RemotePhotoBoundsSizeXPropertyName, Mathf.Max(0.0001f, Mathf.Abs(size.x)));
            _runtimeMaterial.SetFloat(RemotePhotoBoundsSizeYPropertyName, Mathf.Max(0.0001f, Mathf.Abs(size.y)));
            _runtimeMaterial.SetFloat(RemotePhotoBoundsSizeZPropertyName, Mathf.Max(0.0001f, Mathf.Abs(size.z)));
        }

        private void LogDownload(string message)
        {
            if (_activeGalleryService != null && _activeGalleryService.debugLogs)
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

            MeshFilter meshFilter = GetComponent<MeshFilter>();
            if (meshFilter == null || meshFilter.sharedMesh == null)
            {
                return 0f;
            }

            return BuildAspectRatio(meshFilter.sharedMesh.bounds.size, RemotePhotoAxisMode.Auto);
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
