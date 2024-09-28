using System;
using System.Linq;
using UnityEngine;


#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Knot.Core
{
    internal static class Utils
    {
        public const string EditorRootPath = "KNOT/";

        public static T GetProjectSettings<T>() where T : ScriptableObject
        {
            T settings;

#if UNITY_EDITOR
            string defaultPath = $"Assets/{typeof(T).Name}.asset";
            settings = AssetDatabase.LoadAssetAtPath<T>(defaultPath);

            if (settings == null)
                settings = PlayerSettings.GetPreloadedAssets().OfType<T>().FirstOrDefault();

            if (settings == null)
            {
                var allSettings =
                    AssetDatabase.FindAssets($"t:{typeof(T).Name}").
                        Select(AssetDatabase.GUIDToAssetPath).
                        Select(AssetDatabase.LoadAssetAtPath<T>).ToArray();

                if (allSettings.Length == 0)
                {
                    var instance = ScriptableObject.CreateInstance<T>();
                    AssetDatabase.CreateAsset(instance, defaultPath);
                    AssetDatabase.SaveAssets();
                    settings = instance;

                    var preloadedAssets = PlayerSettings.GetPreloadedAssets();
                    PlayerSettings.SetPreloadedAssets(preloadedAssets.Append(settings).ToArray());
                }
                else settings = allSettings.First(); }
            
#else
            settings = Resources.FindObjectsOfTypeAll<T>().FirstOrDefault();
#endif
            if (settings == null)
                Debug.LogWarning($"Unable to load or create {typeof(T).Name}");

            return settings;
        }
    }
}
