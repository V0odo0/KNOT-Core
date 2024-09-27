using System;
using UnityEditor;
using UnityEngine;

namespace Knot.Core.Editor
{
    internal abstract class ProjectSettingsEditor<TAsset> : UnityEditor.Editor where TAsset : ScriptableObject
    {
        protected TAsset Target { get; private set; }


        protected virtual void OnEnable()
        {
            Target = target as TAsset;
        }

        protected virtual void OnDisable()
        {

        }

        public override void OnInspectorGUI()
        {
            DrawInspectorWithoutScriptProperty();
        }



        protected static string GetSettingsPath(string name) => $"Project/{Utils.EditorRootPath}{name}";


        protected virtual void DrawInspectorWithoutScriptProperty()
        {
            if (Target == null)
                return;

            serializedObject.Update();
            SerializedProperty property = serializedObject.GetIterator();
            if (property.NextVisible(true))
            {
                do
                {
                    if (property.name == "m_Script")
                        continue;

                    EditorGUILayout.PropertyField(serializedObject.FindProperty(property.name), true);
                }
                while (property.NextVisible(false));
            }
            serializedObject.ApplyModifiedProperties();
        }

        protected static SettingsProvider GetSettingsProvider(TAsset asset, string settingsPath, Type customEditorType)
        {
            var provider = new SettingsProvider(settingsPath, SettingsScope.Project);
            var editor = CreateEditor(asset, customEditorType);

            provider.guiHandler += s =>
            {
                editor.OnInspectorGUI();
            };

            return provider;
        }
    }
}
