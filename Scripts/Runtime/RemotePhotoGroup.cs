using UdonSharp;
using UnityEngine;
using VRC.SDKBase;

namespace RemotePhotoSystem
{
    [UdonBehaviourSyncMode(BehaviourSyncMode.Manual)]
    public class RemotePhotoGroup : UdonSharpBehaviour
    {
        private const int TriggerActionRandom = 0;
        private const int TriggerActionPrevious = 1;
        private const int TriggerActionNext = 2;

        [HideInInspector] public RemotePhotoManager manager;
        public RemotePhotoPermissionMode permissionMode = RemotePhotoPermissionMode.Everyone;
        public float triggerCooldownSeconds = 2f;
        public RemotePhotoFrame[] targets = new RemotePhotoFrame[0];

        [UdonSynced] public VRCUrl[] syncedUrls = new VRCUrl[0];
        [UdonSynced] public int selectionRevision;
        [UdonSynced] public double nextAllowedTriggerServerTime;

        [HideInInspector] public string lastTriggerError = string.Empty;

        public void Start()
        {
            EnsureSyncedArrays();
            RegisterPreloadDownloadMaterial();
            ApplyCurrentSelection();
        }

        public override void Interact()
        {
            TriggerRandom();
        }

        public void TriggerRandom()
        {
            TriggerInternal(TriggerActionRandom);
        }

        public void TriggerPrevious()
        {
            TriggerInternal(TriggerActionPrevious);
        }

        public void TriggerNext()
        {
            TriggerInternal(TriggerActionNext);
        }

        private void TriggerInternal(int triggerAction)
        {
            lastTriggerError = string.Empty;

            if (!CanTrigger())
            {
                return;
            }

            if (!CanPassTriggerCooldown())
            {
                return;
            }

            if (!EnsureLocalOwnership())
            {
                return;
            }

            if (manager == null)
            {
                lastTriggerError = "Remote Photo Manager is missing.";
                return;
            }

            if (!manager.ContainsManagedGroup(this))
            {
                lastTriggerError = "This group is not managed by its Remote Photo Manager.";
                return;
            }

            manager.ApplyBakedGallery();
            if (!manager.HasGalleryData())
            {
                lastTriggerError = manager.lastGalleryError;
                return;
            }

            if (!EnsureGalleryOwnership())
            {
                lastTriggerError = "Could not take ownership of the Remote Photo Manager.";
                return;
            }

            if (triggerAction == TriggerActionRandom && manager.configuredPlayMode != RemotePhotoPlayMode.Random)
            {
                lastTriggerError = "Random button can only be used when the manager Play Mode is Random.";
                manager.LogDebug("Group random trigger ignored because manager is in sequence mode: " + gameObject.name);
                return;
            }

            if (triggerAction != TriggerActionRandom && manager.configuredPlayMode == RemotePhotoPlayMode.Random)
            {
                lastTriggerError = "Previous/Next buttons can only be used when the manager Play Mode is SequenceForward or SequenceReverse.";
                manager.LogDebug("Group page trigger ignored because manager is in random mode: " + gameObject.name);
                return;
            }

            int landscapeCount = CountTargetSlots(RemotePhotoOrientation.Landscape);
            int portraitCount = CountTargetSlots(RemotePhotoOrientation.Portrait);
            RegisterPreloadDownloadMaterial();
            manager.LogDebug("Group trigger requested: " + gameObject.name + " Action=" + GetTriggerActionName(triggerAction) + " L=" + landscapeCount + ", P=" + portraitCount);
            if (!GenerateSelectionFromGallery(landscapeCount, portraitCount, triggerAction))
            {
                manager.LogDebug("Group trigger blocked because the gallery does not have enough URLs for this group: " + gameObject.name);
                return;
            }

            MarkTriggerCooldown();
            manager.NotifySelectionStateChanged();
            RequestSerialization();
            manager.LogDebug("Group trigger applied: " + gameObject.name);
        }

        public override void OnDeserialization()
        {
            ApplyCurrentSelection();
        }

        private bool CanTrigger()
        {
            if (permissionMode == RemotePhotoPermissionMode.MasterOnly && !Networking.IsMaster)
            {
                lastTriggerError = "Only the master can trigger this group.";
                return false;
            }

            if (permissionMode == RemotePhotoPermissionMode.OwnerOnly && !Networking.IsOwner(gameObject))
            {
                lastTriggerError = "Only the owner can trigger this group.";
                return false;
            }

            return true;
        }

        private bool EnsureLocalOwnership()
        {
            if (permissionMode == RemotePhotoPermissionMode.MasterOnly && !Networking.IsMaster)
            {
                return false;
            }

            if (!Networking.IsOwner(gameObject))
            {
                Networking.SetOwner(Networking.LocalPlayer, gameObject);
            }

            return Networking.IsOwner(gameObject);
        }

        private bool EnsureGalleryOwnership()
        {
            if (manager == null)
            {
                return false;
            }

            if (!Networking.IsOwner(manager.gameObject))
            {
                Networking.SetOwner(Networking.LocalPlayer, manager.gameObject);
            }

            return Networking.IsOwner(manager.gameObject);
        }

        private bool CanPassTriggerCooldown()
        {
            if (triggerCooldownSeconds <= 0f)
            {
                return true;
            }

            double now = Networking.GetServerTimeInSeconds();
            if (now < nextAllowedTriggerServerTime)
            {
                lastTriggerError = "Trigger is cooling down.";
                return false;
            }

            return true;
        }

        private void MarkTriggerCooldown()
        {
            if (triggerCooldownSeconds <= 0f)
            {
                nextAllowedTriggerServerTime = 0d;
                return;
            }

            nextAllowedTriggerServerTime = Networking.GetServerTimeInSeconds() + triggerCooldownSeconds;
        }

        private void EnsureSyncedArrays()
        {
            int targetCount = targets == null ? 0 : targets.Length;

            if (syncedUrls == null || syncedUrls.Length != targetCount)
            {
                syncedUrls = new VRCUrl[targetCount];
            }

        }

        private void RegisterPreloadDownloadMaterial()
        {
            if (manager == null || targets == null)
            {
                return;
            }

            int index = 0;
            while (index < targets.Length)
            {
                RemotePhotoFrame target = targets[index];
                if (target != null)
                {
                    Material material = target.GetRuntimeMaterial();
                    if (material != null)
                    {
                        manager.RegisterPreloadDownloadMaterial(material);
                    }
                }

                index++;
            }
        }

        private bool GenerateSelectionFromGallery(int landscapeCount, int portraitCount, int triggerAction)
        {
            EnsureSyncedArrays();

            if (landscapeCount > 0 && manager.GetLandscapeCount() <= 0)
            {
                lastTriggerError = "Landscape gallery is empty.";
                return false;
            }

            if (portraitCount > 0 && manager.GetPortraitCount() <= 0)
            {
                lastTriggerError = "Portrait gallery is empty.";
                return false;
            }

            if (triggerAction == TriggerActionRandom)
            {
                if (!FillRandomSelectionInTargetOrder(landscapeCount, portraitCount))
                {
                    return false;
                }
            }
            else
            {
                bool nextPage = triggerAction == TriggerActionNext;
                manager.BeginSequencePageSelection(nextPage, landscapeCount, portraitCount);
                if (!FillSequencePageSelectionInTargetOrder())
                {
                    return false;
                }

                manager.CommitSequencePageSelection(landscapeCount, portraitCount);
            }

            selectionRevision++;
            ApplyCurrentSelection();
            return true;
        }

        private string GetTriggerActionName(int triggerAction)
        {
            if (triggerAction == TriggerActionRandom)
            {
                return "Random";
            }

            if (triggerAction == TriggerActionPrevious)
            {
                return "Previous";
            }

            return "Next";
        }

        private bool FillRandomSelectionInTargetOrder(int landscapeCount, int portraitCount)
        {
            if (!FillGallerySelectionInTargetOrder(landscapeCount, portraitCount))
            {
                return false;
            }

            return true;
        }

        private int CountTargetSlots(RemotePhotoOrientation orientation)
        {
            int total = targets == null ? 0 : targets.Length;
            int count = 0;
            int index = 0;
            while (index < total)
            {
                if (targets[index] != null && targets[index].orientation == orientation)
                {
                    count++;
                }

                index++;
            }

            return count;
        }

        private bool FillGallerySelectionInTargetOrder(int landscapeCount, int portraitCount)
        {
            VRCUrl[] selectedLandscapeUrls = new VRCUrl[landscapeCount];
            VRCUrl[] selectedPortraitUrls = new VRCUrl[portraitCount];
            int selectedLandscapeCount = 0;
            int selectedPortraitCount = 0;
            int index = 0;
            while (targets != null && index < targets.Length)
            {
                RemotePhotoFrame target = targets[index];
                if (target == null)
                {
                    index++;
                    continue;
                }

                if (target.orientation == RemotePhotoOrientation.Landscape)
                {
                    VRCUrl selectedUrl = manager.SelectLandscapeUrl(selectedLandscapeUrls, selectedLandscapeCount);
                    if (!RemotePhotoUrlUtility.IsValidVrcUrl(selectedUrl))
                    {
                        lastTriggerError = "Could not select a landscape photo.";
                        return false;
                    }

                    selectedLandscapeUrls[selectedLandscapeCount] = selectedUrl;
                    selectedLandscapeCount++;
                    syncedUrls[index] = selectedUrl;
                }
                else
                {
                    VRCUrl selectedUrl = manager.SelectPortraitUrl(selectedPortraitUrls, selectedPortraitCount);
                    if (!RemotePhotoUrlUtility.IsValidVrcUrl(selectedUrl))
                    {
                        lastTriggerError = "Could not select a portrait photo.";
                        return false;
                    }

                    selectedPortraitUrls[selectedPortraitCount] = selectedUrl;
                    selectedPortraitCount++;
                    syncedUrls[index] = selectedUrl;
                }

                index++;
            }

            return true;
        }

        private bool FillSequencePageSelectionInTargetOrder()
        {
            int index = 0;
            while (targets != null && index < targets.Length)
            {
                RemotePhotoFrame target = targets[index];
                if (target == null)
                {
                    index++;
                    continue;
                }

                if (target.orientation == RemotePhotoOrientation.Landscape)
                {
                    VRCUrl selectedUrl = manager.SelectLandscapeSequencePageUrl();
                    if (!RemotePhotoUrlUtility.IsValidVrcUrl(selectedUrl))
                    {
                        lastTriggerError = "Could not select a landscape sequence page photo.";
                        return false;
                    }

                    syncedUrls[index] = selectedUrl;
                }
                else
                {
                    VRCUrl selectedUrl = manager.SelectPortraitSequencePageUrl();
                    if (!RemotePhotoUrlUtility.IsValidVrcUrl(selectedUrl))
                    {
                        lastTriggerError = "Could not select a portrait sequence page photo.";
                        return false;
                    }

                    syncedUrls[index] = selectedUrl;
                }

                index++;
            }

            return true;
        }

        private void ApplyCurrentSelection()
        {
            EnsureSyncedArrays();

            if (targets == null)
            {
                return;
            }

            if (selectionRevision == 0 && !HasAnySyncedUrl())
            {
                return;
            }

            int index = 0;
            while (index < targets.Length)
            {
                RemotePhotoFrame target = targets[index];
                if (target == null)
                {
                    index++;
                    continue;
                }

                if (syncedUrls[index] == null || !RemotePhotoUrlUtility.IsValidVrcUrl(syncedUrls[index]))
                {
                    target.ClearPhoto();
                }
                else
                {
                    target.LoadPhotoFromManager(syncedUrls[index], manager);
                }

                index++;
            }
        }

        private bool HasAnySyncedUrl()
        {
            if (syncedUrls == null)
            {
                return false;
            }

            int index = 0;
            while (index < syncedUrls.Length)
            {
                if (RemotePhotoUrlUtility.IsValidVrcUrl(syncedUrls[index]))
                {
                    return true;
                }

                index++;
            }

            return false;
        }
    }
}
