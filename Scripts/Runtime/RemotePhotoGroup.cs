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
        [UdonSynced] public int[] syncedSlotRequestIds = new int[0];
        [UdonSynced] public int selectionRevision;
        [UdonSynced] public int loadOrderRevision;
        [UdonSynced] public double nextAllowedTriggerServerTime;

        [HideInInspector] public string lastTriggerError = string.Empty;

        private int _activeDisplayRevision = -1;
        private int _activeDisplayOrderIndex;
        private int _activeDisplaySerial;
        private bool _activeDisplaySequential;

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
            if (triggerAction == TriggerActionRandom && manager.IsPreloadEnabled())
            {
                if (!manager.BeginRandomConsume(this))
                {
                    lastTriggerError = "Random request is already active or could not start.";
                    return;
                }

                MarkTriggerCooldown();
                RequestSerialization();
                manager.LogDebug("Group random preload request accepted: " + gameObject.name);
                return;
            }

            if (!GenerateSelectionFromGallery(landscapeCount, portraitCount, triggerAction))
            {
                manager.LogDebug("Group trigger blocked because the gallery does not have enough URLs for this group: " + gameObject.name);
                return;
            }

            MarkTriggerCooldown();
            manager.NotifySelectionStateChanged();
            RequestSerialization();
            ApplyCurrentSelection();
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

        public void RefreshCurrentSelectionFromManager()
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

            if (syncedSlotRequestIds == null || syncedSlotRequestIds.Length != targetCount)
            {
                syncedSlotRequestIds = new int[targetCount];
                ResetSyncedSlotRequestIds();
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

        private void ResetSyncedUrls()
        {
            int index = 0;
            while (syncedUrls != null && index < syncedUrls.Length)
            {
                syncedUrls[index] = null;
                index++;
            }
        }

        private void ResetSelectionPairs()
        {
            ResetSyncedUrls();
            ResetSyncedLoadOrder();
            ResetSyncedSlotRequestIds();
        }

        private void ResetSyncedSlotRequestIds()
        {
            int index = 0;
            while (syncedSlotRequestIds != null && index < syncedSlotRequestIds.Length)
            {
                syncedSlotRequestIds[index] = -1;
                index++;
            }
        }

        private int FindSelectionPairIndexForSlot(int slotIndex)
        {
            int index = 0;
            while (syncedLoadOrderSlots != null && index < syncedLoadOrderSlots.Length)
            {
                if (syncedLoadOrderSlots[index] == slotIndex)
                {
                    return index;
                }

                index++;
            }

            return -1;
        }

        private int FindEmptySelectionPairIndex()
        {
            int index = 0;
            while (syncedLoadOrderSlots != null && index < syncedLoadOrderSlots.Length)
            {
                if (syncedLoadOrderSlots[index] < 0)
                {
                    return index;
                }

                index++;
            }

            return -1;
        }

        private bool WriteSelectionPair(int pairIndex, int slotIndex, VRCUrl url)
        {
            if (syncedUrls == null ||
                syncedLoadOrderSlots == null ||
                targets == null ||
                pairIndex < 0 ||
                pairIndex >= syncedUrls.Length ||
                pairIndex >= syncedLoadOrderSlots.Length ||
                slotIndex < 0 ||
                slotIndex >= targets.Length ||
                targets[slotIndex] == null ||
                !RemotePhotoUrlUtility.IsValidVrcUrl(url))
            {
                return false;
            }

            syncedUrls[pairIndex] = url;
            syncedLoadOrderSlots[pairIndex] = slotIndex;
            LogDebug("Selection pair: group=" + gameObject.name + ", revision=" + selectionRevision + ", pair=" + pairIndex + ", slot=" + slotIndex + ", frame=" + targets[slotIndex].gameObject.name + ", url=" + url.Get());
            return true;
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

        public int BeginRandomPreloadSelectionFromManager()
        {
            EnsureSyncedArrays();
            selectionRevision++;
            loadOrderRevision = selectionRevision;
            ResetSelectionPairs();
            _activeDisplayRevision = selectionRevision;
            _activeDisplayOrderIndex = 0;
            _activeDisplaySerial++;
            _activeDisplaySequential = false;
            RequestSerialization();
            return selectionRevision;
        }

        public bool ApplyRandomPreloadSlotFromManager(int slotIndex, VRCUrl url, Texture texture, RemotePhotoManager sourceManager, int requestId)
        {
            EnsureSyncedArrays();
            if (requestId != selectionRevision ||
                slotIndex < 0 ||
                targets == null ||
                syncedUrls == null ||
                syncedSlotRequestIds == null ||
                slotIndex >= targets.Length ||
                slotIndex >= syncedSlotRequestIds.Length ||
                targets[slotIndex] == null ||
                !RemotePhotoUrlUtility.IsValidVrcUrl(url) ||
                texture == null)
            {
                return false;
            }

            int pairIndex = FindSelectionPairIndexForSlot(slotIndex);
            if (pairIndex < 0)
            {
                pairIndex = FindEmptySelectionPairIndex();
            }

            if (!WriteSelectionPair(pairIndex, slotIndex, url))
            {
                return false;
            }

            syncedSlotRequestIds[slotIndex] = selectionRevision;
            loadOrderRevision = selectionRevision;
            targets[slotIndex].ApplyManagerTexture(texture, sourceManager);
            RequestSerialization();
            return true;
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
                manager.BeginSequencePageSelection(this, nextPage, landscapeCount, portraitCount);
                if (!FillSequencePageSelectionInTargetOrder())
                {
                    return false;
                }

                manager.CommitSequencePageSelection(landscapeCount, portraitCount);
            }

            selectionRevision++;
            loadOrderRevision = selectionRevision;
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
            ResetSelectionPairs();
            VRCUrl[] selectedLandscapeUrls = new VRCUrl[landscapeCount];
            VRCUrl[] selectedPortraitUrls = new VRCUrl[portraitCount];
            int selectedLandscapeCount = 0;
            int selectedPortraitCount = 0;
            int writeIndex = 0;
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
                    if (!WriteSelectionPair(writeIndex, index, selectedUrl))
                    {
                        return false;
                    }

                    writeIndex++;
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
                    if (!WriteSelectionPair(writeIndex, index, selectedUrl))
                    {
                        return false;
                    }

                    writeIndex++;
                }

                index++;
            }

            return true;
        }

        private bool FillSequencePageSelectionInTargetOrder()
        {
            ResetSelectionPairs();
            int writeIndex = 0;
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

                    if (!WriteSelectionPair(writeIndex, index, selectedUrl))
                    {
                        return false;
                    }

                    writeIndex++;
                }
                else
                {
                    VRCUrl selectedUrl = manager.SelectPortraitSequencePageUrl();
                    if (!RemotePhotoUrlUtility.IsValidVrcUrl(selectedUrl))
                    {
                        lastTriggerError = "Could not select a portrait sequence page photo.";
                        return false;
                    }

                    if (!WriteSelectionPair(writeIndex, index, selectedUrl))
                    {
                        return false;
                    }

                    writeIndex++;
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

            _activeDisplayRevision = selectionRevision;
            _activeDisplayOrderIndex = 0;
            _activeDisplaySerial++;
            _activeDisplaySequential = ShouldApplySelectionSequential();
            if (_activeDisplaySequential)
            {
                ApplyNextSelectionSlotInOrder();
                return;
            }

            ApplySelectionSlotsParallel();
        }

        private bool ShouldApplySelectionSequential()
        {
            return manager != null && !manager.IsPreloadEnabled();
        }

        public void NotifyFrameDisplayFinished(int slotIndex, int revision, int requestSerial)
        {
            if (!_activeDisplaySequential)
            {
                return;
            }

            if (revision != _activeDisplayRevision || requestSerial != _activeDisplaySerial)
            {
                return;
            }

            int currentSlot = GetDisplaySlotAtOrderIndex(_activeDisplayOrderIndex);
            if (currentSlot != slotIndex)
            {
                return;
            }

            _activeDisplayOrderIndex++;
            ApplyNextSelectionSlotInOrder();
        }

        private void ApplyNextSelectionSlotInOrder()
        {
            int maxCount = targets == null ? 0 : targets.Length;
            while (_activeDisplayOrderIndex < maxCount)
            {
                int slot = GetDisplaySlotAtOrderIndex(_activeDisplayOrderIndex);
                if (!IsValidSelectionSlot(slot))
                {
                    _activeDisplayOrderIndex++;
                    continue;
                }

                RemotePhotoFrame target = targets[slot];
                if (target == null)
                {
                    _activeDisplayOrderIndex++;
                    continue;
                }

                VRCUrl url = GetDisplayUrlAtOrderIndex(_activeDisplayOrderIndex);
                if (!RemotePhotoUrlUtility.IsValidVrcUrl(url))
                {
                    _activeDisplayOrderIndex++;
                    continue;
                }

                LogDebug("Selection apply: group=" + gameObject.name + ", revision=" + selectionRevision + ", pair=" + _activeDisplayOrderIndex + ", slot=" + slot + ", frame=" + target.gameObject.name + ", url=" + url.Get());
                target.LoadPhotoFromManagerSlot(url, manager, selectionRevision, this, slot, _activeDisplaySerial);
                return;
            }

            if (manager != null && manager.IsPreloadEnabled())
            {
                manager.RefreshPreloadPredictions();
            }
        }

        private void ApplySelectionSlotsParallel()
        {
            int maxCount = targets == null ? 0 : targets.Length;
            int orderIndex = 0;
            while (orderIndex < maxCount)
            {
                int slot = GetDisplaySlotAtOrderIndex(orderIndex);
                if (IsValidSelectionSlot(slot))
                {
                    RemotePhotoFrame target = targets[slot];
                    if (target != null)
                    {
                        VRCUrl url = GetDisplayUrlAtOrderIndex(orderIndex);
                        if (RemotePhotoUrlUtility.IsValidVrcUrl(url))
                        {
                            LogDebug("Selection apply: group=" + gameObject.name + ", revision=" + selectionRevision + ", pair=" + orderIndex + ", slot=" + slot + ", frame=" + target.gameObject.name + ", url=" + url.Get());
                            target.LoadPhotoFromManagerSlot(url, manager, selectionRevision, this, slot, _activeDisplaySerial);
                        }
                    }
                }

                orderIndex++;
            }

            if (manager != null && manager.IsPreloadEnabled())
            {
                manager.RefreshPreloadPredictions();
            }
        }

        private int GetDisplaySlotAtOrderIndex(int orderIndex)
        {
            if (HasValidSyncedLoadOrder())
            {
                return syncedLoadOrderSlots[orderIndex];
            }

            return orderIndex;
        }

        private VRCUrl GetDisplayUrlAtOrderIndex(int orderIndex)
        {
            if (syncedUrls == null || orderIndex < 0 || orderIndex >= syncedUrls.Length)
            {
                return null;
            }

            return syncedUrls[orderIndex];
        }

        private bool IsValidSelectionSlot(int index)
        {
            if (targets == null ||
                syncedUrls == null ||
                index < 0 ||
                index >= targets.Length ||
                index >= syncedUrls.Length)
            {
                return false;
            }

            return true;
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
                        !RemotePhotoUrlUtility.IsValidVrcUrl(syncedUrls[orderIndex]) ||
                        !IsSlotCurrentSelection(slot) ||
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
            while (targets != null && syncedUrls != null && syncedLoadOrderSlots != null && index < syncedUrls.Length && index < syncedLoadOrderSlots.Length)
            {
                int slot = syncedLoadOrderSlots[index];
                if (slot >= 0 &&
                    slot < targets.Length &&
                    targets[slot] != null &&
                    RemotePhotoUrlUtility.IsValidVrcUrl(syncedUrls[index]) &&
                    IsSlotCurrentSelection(slot))
                {
                    count++;
                }

                index++;
            }

            return count;
        }

        private bool IsSlotCurrentSelection(int slotIndex)
        {
            if (manager == null || manager.configuredPlayMode != RemotePhotoPlayMode.Random || !manager.IsPreloadEnabled())
            {
                return true;
            }

            return syncedSlotRequestIds != null &&
                slotIndex >= 0 &&
                slotIndex < syncedSlotRequestIds.Length &&
                syncedSlotRequestIds[slotIndex] == selectionRevision;
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

        private void LogDebug(string message)
        {
            if (manager != null && manager.debugLogs)
            {
                manager.LogDebug(message);
            }
        }
    }
}
