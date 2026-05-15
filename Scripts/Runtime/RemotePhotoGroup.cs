using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using VRC.Udon.Common.Interfaces;

namespace RemotePhotoSystem
{
    [UdonBehaviourSyncMode(BehaviourSyncMode.Manual)]
    public class RemotePhotoGroup : UdonSharpBehaviour
    {
        private const int TriggerActionRandom = 0;
        private const int TriggerActionPrevious = 1;
        private const int TriggerActionNext = 2;

        [HideInInspector] public RemotePhotoManager manager;
        public float triggerCooldownSeconds = 2f;
        public RemotePhotoFrame[] targets = new RemotePhotoFrame[0];

        [UdonSynced] public VRCUrl[] syncedUrls = new VRCUrl[0];
        [UdonSynced] public int[] syncedLoadOrderSlots = new int[0];
        [UdonSynced] public int selectionRevision;
        [UdonSynced] public int loadOrderRevision;
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

        public void _RequestTriggerRandom()
        {
            TriggerInternalAsMaster(TriggerActionRandom);
        }

        public void _RequestTriggerPrevious()
        {
            TriggerInternalAsMaster(TriggerActionPrevious);
        }

        public void _RequestTriggerNext()
        {
            TriggerInternalAsMaster(TriggerActionNext);
        }

        private void TriggerInternal(int triggerAction)
        {
            lastTriggerError = string.Empty;

            if (!Networking.IsMaster)
            {
                SendTriggerRequest(triggerAction);
                return;
            }

            TriggerInternalAsMaster(triggerAction);
        }

        private void TriggerInternalAsMaster(int triggerAction)
        {
            lastTriggerError = string.Empty;

            if (!Networking.IsMaster)
            {
                return;
            }

            if (manager == null)
            {
                lastTriggerError = "Remote Photo Manager is missing.";
                return;
            }

            manager.EnsureMasterOwnership();
            if (!Networking.IsOwner(gameObject) || !Networking.IsOwner(manager.gameObject))
            {
                lastTriggerError = "Master could not take ownership of this group or its Remote Photo Manager.";
                return;
            }

            if (!CanPassTriggerCooldown())
            {
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

        private void SendTriggerRequest(int triggerAction)
        {
            if (triggerAction == TriggerActionRandom)
            {
                SendCustomNetworkEvent(NetworkEventTarget.All, nameof(_RequestTriggerRandom));
                return;
            }

            if (triggerAction == TriggerActionPrevious)
            {
                SendCustomNetworkEvent(NetworkEventTarget.All, nameof(_RequestTriggerPrevious));
                return;
            }

            SendCustomNetworkEvent(NetworkEventTarget.All, nameof(_RequestTriggerNext));
        }

        public override void OnDeserialization()
        {
            ApplyCurrentSelection();
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

            if (syncedLoadOrderSlots == null || syncedLoadOrderSlots.Length != targetCount)
            {
                syncedLoadOrderSlots = new int[targetCount];
                ResetSyncedLoadOrder();
            }
        }

        private void ResetSyncedLoadOrder()
        {
            int index = 0;
            while (syncedLoadOrderSlots != null && index < syncedLoadOrderSlots.Length)
            {
                syncedLoadOrderSlots[index] = -1;
                index++;
            }
        }

        private void BuildSyncedLoadOrder()
        {
            EnsureSyncedArrays();
            ResetSyncedLoadOrder();

            int writeIndex = 0;
            int slotIndex = 0;
            while (targets != null && syncedUrls != null && slotIndex < targets.Length && slotIndex < syncedUrls.Length)
            {
                if (targets[slotIndex] != null && RemotePhotoUrlUtility.IsValidVrcUrl(syncedUrls[slotIndex]))
                {
                    syncedLoadOrderSlots[writeIndex] = slotIndex;
                    writeIndex++;
                }

                slotIndex++;
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
            BuildSyncedLoadOrder();
            loadOrderRevision = selectionRevision;
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

            if (HasValidSyncedLoadOrder())
            {
                int orderIndex = 0;
                while (orderIndex < syncedLoadOrderSlots.Length)
                {
                    int slot = syncedLoadOrderSlots[orderIndex];
                    if (slot >= 0)
                    {
                        ApplySelectionSlot(slot);
                    }

                    orderIndex++;
                }

                return;
            }

            int index = 0;
            while (index < targets.Length)
            {
                ApplySelectionSlot(index);
                index++;
            }
        }

        private void ApplySelectionSlot(int index)
        {
            if (targets == null ||
                syncedUrls == null ||
                index < 0 ||
                index >= targets.Length ||
                index >= syncedUrls.Length)
            {
                return;
            }

            RemotePhotoFrame target = targets[index];
            if (target == null)
            {
                return;
            }

            if (!RemotePhotoUrlUtility.IsValidVrcUrl(syncedUrls[index]))
            {
                target.ClearPhoto();
                return;
            }

            target.LoadPhotoFromManager(syncedUrls[index], manager, selectionRevision);
        }

        private bool HasValidSyncedLoadOrder()
        {
            if (syncedLoadOrderSlots == null ||
                targets == null ||
                syncedUrls == null ||
                syncedLoadOrderSlots.Length != targets.Length ||
                syncedUrls.Length != targets.Length)
            {
                return false;
            }

            int expectedCount = CountValidSelectionSlots();
            int actualCount = 0;
            int orderIndex = 0;
            while (orderIndex < syncedLoadOrderSlots.Length)
            {
                int slot = syncedLoadOrderSlots[orderIndex];
                if (slot >= 0)
                {
                    if (slot >= targets.Length ||
                        targets[slot] == null ||
                        !RemotePhotoUrlUtility.IsValidVrcUrl(syncedUrls[slot]) ||
                        IsLoadOrderSlotRepeated(slot, orderIndex))
                    {
                        return false;
                    }

                    actualCount++;
                }

                orderIndex++;
            }

            return actualCount == expectedCount;
        }

        private int CountValidSelectionSlots()
        {
            int count = 0;
            int index = 0;
            while (targets != null && syncedUrls != null && index < targets.Length && index < syncedUrls.Length)
            {
                if (targets[index] != null && RemotePhotoUrlUtility.IsValidVrcUrl(syncedUrls[index]))
                {
                    count++;
                }

                index++;
            }

            return count;
        }

        private bool IsLoadOrderSlotRepeated(int slot, int beforeIndex)
        {
            int index = 0;
            while (syncedLoadOrderSlots != null && index < beforeIndex)
            {
                if (syncedLoadOrderSlots[index] == slot)
                {
                    return true;
                }

                index++;
            }

            return false;
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
