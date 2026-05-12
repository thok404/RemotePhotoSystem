using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace RemotePhotoSystem.Editor
{
    internal sealed class RemotePhotoBuildProcessor : IPreprocessBuildWithReport, IPostprocessBuildWithReport
    {
        private static readonly Dictionary<int, TextAsset> GalleryConfigReferences = new Dictionary<int, TextAsset>();
        private static readonly Dictionary<int, bool> DebugLogStates = new Dictionary<int, bool>();

        public int callbackOrder => 0;

        public void OnPreprocessBuild(BuildReport report)
        {
            ApplyBuildOnlySettings();
        }

        public void OnPostprocessBuild(BuildReport report)
        {
            RestoreBuildOnlySettings();
        }

        private static void ApplyBuildOnlySettings()
        {
            GalleryConfigReferences.Clear();
            DebugLogStates.Clear();

            RemotePhotoManager[] managers = Object.FindObjectsOfType<RemotePhotoManager>(true);
            int index = 0;
            while (managers != null && index < managers.Length)
            {
                RemotePhotoManager manager = managers[index];
                if (manager != null)
                {
                    bool changed = false;
                    int instanceId = manager.GetInstanceID();

                    DebugLogStates[instanceId] = manager.debugLogs;
                    if (manager.debugLogs)
                    {
                        manager.debugLogs = false;
                        changed = true;
                    }

                    if (manager.galleryConfigFile != null)
                    {
                        GalleryConfigReferences[instanceId] = manager.galleryConfigFile;
                        manager.galleryConfigFile = null;
                        changed = true;
                    }

                    if (changed)
                    {
                        EditorUtility.SetDirty(manager);
                    }
                }

                index++;
            }
        }

        private static void RestoreBuildOnlySettings()
        {
            RemotePhotoManager[] managers = Object.FindObjectsOfType<RemotePhotoManager>(true);
            int index = 0;
            while (managers != null && index < managers.Length)
            {
                RemotePhotoManager manager = managers[index];
                if (manager != null)
                {
                    bool changed = false;
                    int instanceId = manager.GetInstanceID();

                    if (DebugLogStates.TryGetValue(instanceId, out bool debugLogs) && manager.debugLogs != debugLogs)
                    {
                        manager.debugLogs = debugLogs;
                        changed = true;
                    }

                    if (GalleryConfigReferences.TryGetValue(instanceId, out TextAsset galleryConfigFile) && manager.galleryConfigFile != galleryConfigFile)
                    {
                        manager.galleryConfigFile = galleryConfigFile;
                        changed = true;
                    }

                    if (changed)
                    {
                        EditorUtility.SetDirty(manager);
                    }
                }

                index++;
            }

            GalleryConfigReferences.Clear();
            DebugLogStates.Clear();
        }
    }
}
