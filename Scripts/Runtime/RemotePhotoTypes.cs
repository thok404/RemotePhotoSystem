using System;
using UnityEngine;
using VRC.SDKBase;

namespace RemotePhotoSystem
{
    public enum RemotePhotoPlayMode
    {
        Random = 0,
        SequenceForward = 1,
        SequenceReverse = 2
    }

    public enum RemotePhotoLoadingMode
    {
        Preload = 0,
        NonPreload = 1
    }

    public enum RemotePhotoPermissionMode
    {
        Everyone = 0,
        MasterOnly = 1,
        OwnerOnly = 2
    }

    public enum RemotePhotoButtonAction
    {
        Random = 0,
        Previous = 1,
        Next = 2
    }

    public enum RemotePhotoOrientation
    {
        Landscape = 0,
        Portrait = 1
    }

    public enum RemotePhotoFitMode
    {
        Crop = 0,
        Contain = 1,
        Stretch = 2,
        Tile = 3
    }

    public enum RemotePhotoProjectionMode
    {
        MeshUv = 0,
        Box = 1
    }

    public enum RemotePhotoAspectMode
    {
        Manual = 0,
        Auto = 1,
        ReferenceBox = 2
    }

    public enum RemotePhotoAxis
    {
        X = 0,
        Y = 1,
        Z = 2
    }

    public enum RemotePhotoAxisMode
    {
        Auto = 0,
        ManualAxes = 1
    }

    public enum RemotePhotoInspectorLanguage
    {
        English = 0,
        Japanese = 1,
        Chinese = 2,
        Korean = 3
    }

    [Serializable]
    public class RemotePhotoImageMetadata
    {
        public int width;
        public int height;
        public string checkedAt = string.Empty;
        public string status = string.Empty;
    }

    [Serializable]
    public class RemotePhotoGalleryConfigEntry
    {
        public string id = string.Empty;
        public string url = string.Empty;
        public RemotePhotoOrientation orientation = RemotePhotoOrientation.Landscape;
        public string[] tags = new string[0];
        public string note = string.Empty;
        public RemotePhotoImageMetadata metadata = new RemotePhotoImageMetadata();
    }

    [Serializable]
    public class RemotePhotoGalleryConfigDocument
    {
        public RemotePhotoGalleryConfigEntry[] entries = new RemotePhotoGalleryConfigEntry[0];
    }

    public static class RemotePhotoFitModeUtility
    {
        public static int ToInt(RemotePhotoFitMode fitMode)
        {
            return (int)fitMode;
        }

        public static RemotePhotoFitMode FromInt(int fitMode)
        {
            if (fitMode < 0 || fitMode > 3)
            {
                return RemotePhotoFitMode.Crop;
            }

            return (RemotePhotoFitMode)fitMode;
        }
    }

    public static class RemotePhotoGalleryConfigJsonUtility
    {
        [Serializable]
        private class JsonEntry
        {
            public string id = string.Empty;
            public string url = string.Empty;
            public string orientation = "Landscape";
            public string[] tags = new string[0];
            public string note = string.Empty;
            public RemotePhotoImageMetadata metadata = new RemotePhotoImageMetadata();
        }

        [Serializable]
        private class JsonDocument
        {
            public JsonEntry[] entries = new JsonEntry[0];
        }

        public static bool TryFromJson(string json, out RemotePhotoGalleryConfigDocument document)
        {
            document = null;

            if (string.IsNullOrWhiteSpace(json))
            {
                return false;
            }

            try
            {
                JsonDocument jsonDocument = JsonUtility.FromJson<JsonDocument>(json);
                if (jsonDocument == null)
                {
                    return false;
                }

                document = new RemotePhotoGalleryConfigDocument
                {
                    entries = BuildGalleryConfigEntries(jsonDocument.entries)
                };
            }
            catch
            {
                document = null;
                return false;
            }

            if (document == null)
            {
                return false;
            }

            if (document.entries == null)
            {
                document.entries = new RemotePhotoGalleryConfigEntry[0];
            }

            for (int i = 0; i < document.entries.Length; i++)
            {
                if (document.entries[i] == null)
                {
                    document.entries[i] = new RemotePhotoGalleryConfigEntry();
                }

                if (document.entries[i].tags == null)
                {
                    document.entries[i].tags = new string[0];
                }

                if (document.entries[i].metadata == null)
                {
                    document.entries[i].metadata = new RemotePhotoImageMetadata();
                }
            }

            return true;
        }

        private static RemotePhotoGalleryConfigEntry[] BuildGalleryConfigEntries(JsonEntry[] entries)
        {
            if (entries == null)
            {
                return new RemotePhotoGalleryConfigEntry[0];
            }

            RemotePhotoGalleryConfigEntry[] configEntries = new RemotePhotoGalleryConfigEntry[entries.Length];
            for (int i = 0; i < entries.Length; i++)
            {
                JsonEntry entry = entries[i] ?? new JsonEntry();
                configEntries[i] = new RemotePhotoGalleryConfigEntry
                {
                    id = entry.id ?? string.Empty,
                    url = entry.url ?? string.Empty,
                    orientation = IsCaseInsensitiveMatch(entry.orientation, "Portrait")
                        ? RemotePhotoOrientation.Portrait
                        : RemotePhotoOrientation.Landscape,
                    tags = entry.tags ?? new string[0],
                    note = entry.note ?? string.Empty,
                    metadata = entry.metadata ?? new RemotePhotoImageMetadata()
                };
            }

            return configEntries;
        }

        private static bool IsCaseInsensitiveMatch(string value, string expected)
        {
            if (string.IsNullOrEmpty(value) || string.IsNullOrEmpty(expected))
            {
                return false;
            }

            return value.ToLower() == expected.ToLower();
        }
    }

    public static class RemotePhotoUrlUtility
    {
        public static bool IsValidUrlString(string value)
        {
            if (string.IsNullOrEmpty(value))
            {
                return false;
            }

            return value.StartsWith("http://") || value.StartsWith("https://");
        }

        public static bool IsValidVrcUrl(VRCUrl value)
        {
            return value != null && IsValidUrlString(value.Get());
        }
    }
}
