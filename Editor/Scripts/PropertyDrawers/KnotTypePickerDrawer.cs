using System;
using System.Collections.Generic;
using System.Linq;
using Knot.Core.Editor;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace Knot.Core
{
    [CustomPropertyDrawer(typeof(KnotTypePickerAttribute))]
    public class KnotTypePickerDrawer : PropertyDrawer
    {
        public KnotTypePickerAttribute Attribute => attribute as KnotTypePickerAttribute;
        public Type BaseType => Attribute?.BaseType;


        bool IsValidProperty(SerializedProperty property)
        {
            return BaseType != null &&
                   property.propertyType == SerializedPropertyType.ManagedReference &&
                   BaseType.GetDerivedTypesInfo().Any();
        }

        /// <summary>
        /// UI Toolkit inspector: a foldout with the type picker in its header and the selected type's fields as native
        /// <see cref="PropertyField"/>s. Drawing it all in one IMGUIContainer instead left the fields uneditable inside
        /// list views (the list item takes the pointer events the IMGUI controls need).
        /// </summary>
        public override VisualElement CreatePropertyGUI(SerializedProperty property)
        {
            if (!IsValidProperty(property))
                return property.GetFallbackPropertyGUI();

#if UNITY_2022_2_OR_NEWER
            var label = string.IsNullOrEmpty(preferredLabel) ? property.displayName : preferredLabel;
#else
            var label = property.displayName;
#endif
            return new TypePickerElement(this, property, label);
        }

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            if (!IsValidProperty(property))
            {
                base.OnGUI(position, property, label);
                return;
            }

            EditorGUI.BeginProperty(position, label, property);

            Rect popupPos = new Rect(position.x + EditorGUIUtility.labelWidth, position.y,
                position.width - EditorGUIUtility.labelWidth, EditorGUIUtility.singleLineHeight);

            Type selectedType = property.GetManagedReferenceType();

            if (selectedType == null)
                EditorGUI.PrefixLabel(position, label);

            if (EditorGUI.DropdownButton(popupPos, GetTypeContent(selectedType), FocusType.Keyboard))
                ShowTypeMenu(popupPos, property, null);

            if (selectedType != null)
            {
                EditorGUI.BeginChangeCheck();
                EditorGUI.PropertyField(position, property, label, true);
                if (EditorGUI.EndChangeCheck())
                    property.serializedObject.ApplyModifiedProperties();
            }

            EditorGUI.EndProperty();
        }

        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            if (!IsValidProperty(property))
                return base.GetPropertyHeight(property, label);

            return EditorGUI.GetPropertyHeight(property, true) + 3;
        }


        /// <summary>The dropdown's caption for <paramref name="selectedType"/> ("[Select Type]" when none).</summary>
        GUIContent GetTypeContent(Type selectedType)
        {
            if (selectedType == null || BaseType == null)
                return EditorGUIUtility.TrTextContent("[Select Type]");

            var typeInfo = BaseType.GetDerivedTypesInfo().FirstOrDefault(t => t.Type == selectedType);
            return typeInfo == null ? EditorGUIUtility.TrTextContent(selectedType.Name) : typeInfo.Content;
        }

        /// <summary>The menu of the types <paramref name="property"/> can hold; <paramref name="changed"/> runs after one is picked.</summary>
        void ShowTypeMenu(Rect position, SerializedProperty property, Action changed)
        {
            var selectedType = property.GetManagedReferenceType();
            var types = BaseType.GetDerivedTypesInfo();
            GenericMenu menu = new GenericMenu();

            menu.AddItem(EditorGUIUtility.TrTextContent("[Null]"), selectedType == null, () =>
            {
                property.managedReferenceValue = null;
                property.serializedObject.ApplyModifiedProperties();
                changed?.Invoke();
            });
            menu.AddSeparator(string.Empty);

            HashSet<Type> typeConstraints = null;
            if (!Attribute.AllowSameTypeInArray)
            {
                SerializedProperty parentProperty = property.FindParentProperty();
                if (parentProperty != null && parentProperty.isArray)
                {
                    typeConstraints = new HashSet<Type>();
                    for (int i = 0; i < parentProperty.arraySize; i++)
                    {
                        typeConstraints.Add(parentProperty.GetArrayElementAtIndex(i).GetManagedReferenceType());
                    }
                }
            }

            foreach (var t in types)
            {
                bool canSelect = typeConstraints == null || !typeConstraints.Contains(t.Type);
                bool isSelected = t.Type == selectedType;
                if (canSelect)
                {
                    menu.AddItem(EditorGUIUtility.TrTextContent(t.Info.MenuName), isSelected, () =>
                    {
                        try
                        {
                            property.managedReferenceValue = t.GetInstance();
                            property.serializedObject.ApplyModifiedProperties();
                            changed?.Invoke();
                        }
                        catch
                        {
                            //
                        }
                    });
                }
                else menu.AddDisabledItem(EditorGUIUtility.TrTextContent(t.Info.MenuName), isSelected);
            }

            menu.DropDown(position);
        }


        /// <summary>
        /// UI Toolkit view of a type-picked managed reference: a foldout labelled with the field, the type dropdown in its
        /// header, and the referenced object's visible fields as bound <see cref="PropertyField"/>s below. Rebuilt when the
        /// referenced type changes (picked here, by undo, or from anywhere else).
        /// </summary>
        class TypePickerElement : VisualElement
        {
            readonly KnotTypePickerDrawer _drawer;
            readonly SerializedProperty _property;
            readonly Foldout _foldout;
            readonly VisualElement _typeDropdown;
            readonly VisualElement _typeIcon;
            readonly TextElement _typeText;
            readonly VisualElement _toggleCheckmark;

            Type _builtType;
            bool _isBuilt;


            public TypePickerElement(KnotTypePickerDrawer drawer, SerializedProperty property, string label)
            {
                _drawer = drawer;
                _property = property.Copy();

                // A list element shows no "Element N" label, its index sits at the end of the line instead.
                int index = GetArrayIndex(_property.propertyPath);
                bool isArrayElement = index >= 0;

                // The label is the type field's (aligned with the other fields' values), not the foldout's.
                _foldout = new Foldout { text = string.Empty, value = _property.isExpanded };
                _foldout.RegisterValueChangedCallback(e =>
                {
                    // Nested foldouts (the fields' own) send their changes up through this one.
                    if (e.target != _foldout)
                        return;

                    _property.isExpanded = e.newValue;
                });

                var toggle = _foldout.Q<Toggle>();
                _toggleCheckmark = toggle.Q(className: Toggle.checkmarkUssClassName);
                toggle.style.marginRight = 0;
                var header = toggle.Q(className: Toggle.inputUssClassName) ?? toggle;
                header.style.flexGrow = 1;

                var foldoutLabel = toggle.Q<Label>();
                if (foldoutLabel != null)
                    foldoutLabel.style.display = DisplayStyle.None;

                // The type dropdown, styled as the editor's popup fields and filling the rest of the line.
                _typeDropdown = new VisualElement();
                _typeDropdown.AddToClassList(BasePopupField<string, string>.inputUssClassName);
                _typeDropdown.AddToClassList(PopupField<string>.inputUssClassName);
                _typeDropdown.style.flexDirection = FlexDirection.Row;
                _typeDropdown.style.flexGrow = 1;
                _typeDropdown.style.flexShrink = 1;

                _typeIcon = new VisualElement { pickingMode = PickingMode.Ignore };
                _typeIcon.style.width = _typeIcon.style.height = 14;
                _typeIcon.style.marginRight = 3;
                _typeIcon.style.alignSelf = Align.Center;
                _typeIcon.style.flexShrink = 0;
                _typeDropdown.Add(_typeIcon);

                _typeText = new TextElement { pickingMode = PickingMode.Ignore };
                _typeText.AddToClassList(BasePopupField<string, string>.textUssClassName);
                _typeText.style.flexGrow = 1;
                _typeText.style.flexShrink = 1;
                _typeDropdown.Add(_typeText);

                var arrow = new VisualElement { pickingMode = PickingMode.Ignore };
                arrow.AddToClassList(BasePopupField<string, string>.arrowUssClassName);
                _typeDropdown.Add(arrow);

                // Opens like a popup field, on press; the press must not also fold the foldout.
                _typeDropdown.RegisterCallback<PointerDownEvent>(e =>
                {
                    if (e.button != 0)
                        return;

                    OpenTypeMenu();
                    e.StopPropagation();
                });
                _typeDropdown.RegisterCallback<MouseDownEvent>(e => e.StopPropagation());
                _typeDropdown.RegisterCallback<PointerUpEvent>(e => e.StopPropagation());
                _typeDropdown.RegisterCallback<ClickEvent>(e => e.StopPropagation());

                var typeField = new TypeField(isArrayElement ? null : label, _typeDropdown);
                if (!isArrayElement)
                {
#if UNITY_2022_2_OR_NEWER
                    typeField.AddToClassList(BaseField<string>.alignedFieldUssClassName);
#endif
                    // The field label's default negative margin pushes its first letter out of the header, where it is clipped.
                    typeField.labelElement.style.marginLeft = 0;
                }
                typeField.style.flexGrow = 1;
                typeField.style.flexShrink = 1;
                typeField.style.marginLeft = 0;
                typeField.style.marginRight = 0;
                header.Add(typeField);

                if (isArrayElement)
                {
                    var indexLabel = new Label(index.ToString()) { pickingMode = PickingMode.Ignore };
                    indexLabel.style.flexShrink = 0;
                    indexLabel.style.minWidth = 18;
                    indexLabel.style.marginLeft = 4;
                    indexLabel.style.unityTextAlign = TextAnchor.MiddleRight;
                    indexLabel.style.opacity = 0.6f;
                    header.Add(indexLabel);
                }

                Add(_foldout);

                this.TrackPropertyValue(_property, _ => Refresh());
                RegisterCallback<AttachToPanelEvent>(_ => Refresh());
            }


            /// <summary>A labelled field around the type dropdown, so it lines up like the inspector's other fields.</summary>
            class TypeField : BaseField<string>
            {
                public TypeField(string label, VisualElement visualInput) : base(label, visualInput)
                {
                }
            }

            /// <summary>The index in "....Array.data[N]", or -1 when the property is not an array element.</summary>
            static int GetArrayIndex(string propertyPath)
            {
                if (string.IsNullOrEmpty(propertyPath) || !propertyPath.EndsWith("]"))
                    return -1;

                int open = propertyPath.LastIndexOf('[');
                return open >= 0 && int.TryParse(propertyPath.Substring(open + 1, propertyPath.Length - open - 2), out var index) ? index : -1;
            }

            void OpenTypeMenu()
            {
                _drawer.ShowTypeMenu(_typeDropdown.worldBound, _property, Refresh);
            }

            /// <summary>Updates the caption, and rebuilds the fields when the referenced type changed.</summary>
            void Refresh()
            {
                if (!IsPropertyAlive())
                    return;

                _property.serializedObject.Update();
                var type = _property.GetManagedReferenceType();
                var content = _drawer.GetTypeContent(type);
                _typeText.text = content.text;
                var icon = content.image as Texture2D;
                _typeIcon.style.backgroundImage = icon;
                _typeIcon.style.display = icon != null ? DisplayStyle.Flex : DisplayStyle.None;

                if (_isBuilt && type == _builtType)
                    return;

                _isBuilt = true;
                _builtType = type;
                RebuildFields();
            }

            void RebuildFields()
            {
                _foldout.contentContainer.Clear();

                int count = 0;
                if (_builtType != null)
                {
                    var iterator = _property.Copy();
                    var end = iterator.GetEndProperty();
                    if (iterator.NextVisible(true))
                    {
                        do
                        {
                            if (SerializedProperty.EqualContents(iterator, end))
                                break;

                            var field = new PropertyField(iterator.Copy());
                            field.BindProperty(iterator.Copy());
                            _foldout.contentContainer.Add(field);
                            count++;
                        } while (iterator.NextVisible(false));
                    }
                }

                // Nothing to unfold: no arrow.
                if (_toggleCheckmark != null)
                    _toggleCheckmark.style.visibility = count > 0 ? Visibility.Visible : Visibility.Hidden;
            }

            bool IsPropertyAlive()
            {
                try
                {
                    return _property.serializedObject.targetObject != null && _property.propertyPath != null &&
                           _property.serializedObject.FindProperty(_property.propertyPath) != null;
                }
                catch (Exception)
                {
                    // The property's object (or the list element) is gone.
                    return false;
                }
            }
        }
    }
}
