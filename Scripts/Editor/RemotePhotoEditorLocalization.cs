using RemotePhotoSystem;
using UnityEngine;
using UnityEditor;

namespace RemotePhotoSystem.Editor
{
    internal static class RemotePhotoEditorLocalization
    {
        public static string T(RemotePhotoInspectorLanguage language, string english, string japanese, string chinese, string korean)
        {
            switch (language)
            {
                case RemotePhotoInspectorLanguage.Japanese:
                    return japanese;
                case RemotePhotoInspectorLanguage.Chinese:
                    return chinese;
                case RemotePhotoInspectorLanguage.Korean:
                    return korean;
                default:
                    return english;
            }
        }

        public static GUIContent G(
            RemotePhotoInspectorLanguage language,
            string englishLabel,
            string japaneseLabel,
            string chineseLabel,
            string koreanLabel,
            string englishTooltip,
            string japaneseTooltip,
            string chineseTooltip,
            string koreanTooltip)
        {
            return new GUIContent(
                T(language, englishLabel, japaneseLabel, chineseLabel, koreanLabel),
                T(language, englishTooltip, japaneseTooltip, chineseTooltip, koreanTooltip));
        }

        public static RemotePhotoInspectorLanguage Resolve(RemotePhotoManager service)
        {
            return service == null ? RemotePhotoInspectorLanguage.English : service.inspectorLanguage;
        }

        public static bool TryResolveProjectLanguage(out RemotePhotoInspectorLanguage language, out bool hasConflict)
        {
            language = RemotePhotoInspectorLanguage.English;
            hasConflict = false;

            RemotePhotoManager[] services = Object.FindObjectsOfType<RemotePhotoManager>(true);
            bool resolvedAny = false;

            int index = 0;
            while (services != null && index < services.Length)
            {
                RemotePhotoManager service = services[index];
                if (service != null)
                {
                    RemotePhotoInspectorLanguage candidate = service.inspectorLanguage;
                    if (!resolvedAny)
                    {
                        language = candidate;
                        resolvedAny = true;
                    }
                    else if (language != candidate)
                    {
                        language = RemotePhotoInspectorLanguage.English;
                        hasConflict = true;
                        return false;
                    }
                }

                index++;
            }

            return resolvedAny;
        }

        public static bool TryResolve(RemotePhotoGroup group, out RemotePhotoInspectorLanguage language)
        {
            bool hasConflict;
            if (TryResolveProjectLanguage(out language, out hasConflict))
            {
                return true;
            }

            if (group != null && group.galleryService != null)
            {
                language = group.galleryService.inspectorLanguage;
                return true;
            }

            language = RemotePhotoInspectorLanguage.English;
            return false;
        }

        public static bool TryResolve(RemotePhotoFrame display, out RemotePhotoInspectorLanguage language, out bool hasConflict)
        {
            language = RemotePhotoInspectorLanguage.English;
            hasConflict = false;

            if (TryResolveProjectLanguage(out language, out hasConflict))
            {
                return true;
            }

            if (hasConflict)
            {
                return false;
            }

            if (display == null)
            {
                return false;
            }

            RemotePhotoManager[] services = Object.FindObjectsOfType<RemotePhotoManager>(true);
            RemotePhotoManager resolvedService = null;

            int serviceIndex = 0;
            while (services != null && serviceIndex < services.Length)
            {
                RemotePhotoManager service = services[serviceIndex];
                RemotePhotoGroup[] groups = service == null ? null : service.managedGroups;
                int groupIndex = 0;
                while (groups != null && groupIndex < groups.Length)
                {
                    RemotePhotoGroup group = groups[groupIndex];
                    RemotePhotoFrame[] targets = group == null ? null : group.targets;
                    int targetIndex = 0;
                    while (targets != null && targetIndex < targets.Length)
                    {
                        if (targets[targetIndex] == display)
                        {
                            if (resolvedService == null)
                            {
                                resolvedService = service;
                                language = Resolve(service);
                            }
                            else if (resolvedService != service)
                            {
                                hasConflict = true;
                                return false;
                            }
                        }

                        targetIndex++;
                    }

                    groupIndex++;
                }

                serviceIndex++;
            }

            return resolvedService != null;
        }
    }
}
