using UnityEditor;
using UnityEngine;

namespace RemotePhotoSystem.Editor
{
    internal static class RemotePhotoHierarchyMenu
    {
        [MenuItem("GameObject/Remote Photo System/Create Manager", false, 10)]
        private static void CreateManager(MenuCommand menuCommand)
        {
            GameObject managerObject = new GameObject("RemotePhotoManager");
            GameObjectUtility.SetParentAndAlign(managerObject, menuCommand.context as GameObject);
            Undo.RegisterCreatedObjectUndo(managerObject, "Create Remote Photo Manager");
            Undo.AddComponent<RemotePhotoManager>(managerObject);
            Selection.activeGameObject = managerObject;
        }
    }
}
