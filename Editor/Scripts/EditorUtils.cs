using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;
using PackageInfo = UnityEditor.PackageManager.PackageInfo;

namespace Knot.Core.Editor
{
    internal static class EditorUtils
    {
        internal static MethodInfo GetIconActiveStateMethod { get; } = typeof(EditorUtility).GetMethod(
            "GetIconInActiveState",
            BindingFlags.Static | BindingFlags.NonPublic);

        private static Dictionary<string, Texture> _cachedIcons = new Dictionary<string, Texture>();
        private static Dictionary<string, Texture> _cachedIconsActiveState = new Dictionary<string, Texture>();


        public static Object RequestCreateAsset(Type type, string name = "", bool ping = false, bool select = false)
        {
            if (type == null || !type.IsSubclassOf(typeof(ScriptableObject)))
                return null;

            name = string.IsNullOrEmpty(name) ? type.Name : name;
            string path = EditorUtility.SaveFilePanelInProject($"Create {name}", name, "asset", "");
            if (string.IsNullOrEmpty(path))
                return null;

            var instance = ScriptableObject.CreateInstance(type);
            instance.name = name;
            AssetDatabase.CreateAsset(instance, path);

            if (ping)
                EditorGUIUtility.PingObject(instance);
            if (select)
                Selection.activeObject = instance;

            return instance;
        }

        public static T RequestCreateAsset<T>(string name = "", bool ping = false, bool select = false) where T : ScriptableObject
        {
            return RequestCreateAsset(typeof(T), name, ping, select) as T;
        }

        public static Texture GetIcon(string iconName)
        {
            if (_cachedIcons.ContainsKey(iconName))
                return _cachedIcons[iconName];

            Debug.unityLogger.logEnabled = false;
            Texture icon = EditorGUIUtility.IconContent(iconName)?.image;
            Debug.unityLogger.logEnabled = true;

            if (icon == null)
                icon = Resources.Load<Texture>(iconName);

            if (icon == null)
                return null;

            if (!_cachedIcons.ContainsKey(iconName))
                _cachedIcons.Add(iconName, icon);

            return icon;
        }

        public static Texture GetIconActiveState(string iconName)
        {
            if (_cachedIconsActiveState.ContainsKey(iconName))
                return _cachedIconsActiveState[iconName];

            if (GetIconActiveStateMethod == null)
                return GetIcon(iconName);

            Debug.unityLogger.logEnabled = false;
            Texture2D icon = (Texture2D)GetIconActiveStateMethod.Invoke(null, new object[] { GetIcon(iconName) });
            Debug.unityLogger.logEnabled = true;

            if (icon == null)
                return GetIcon(iconName);

            if (!_cachedIconsActiveState.ContainsKey(iconName))
                _cachedIconsActiveState.Add(iconName, icon);

            return icon;
        }

        public static bool IsUpmPackage(Type type)
        {
            return PackageInfo.FindForAssembly(Assembly.GetAssembly(type)) != null;
        }
    }
}
