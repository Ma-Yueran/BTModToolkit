using UnityEditor;
using UnityEngine;

public class DismemberPreviewWindow : EditorWindow
{
    private const string DefaultCapMaterialPath = "Effects/CutMeat";

    private Object targetObject;
    private RagdollDismembermentVisual visual;
    private SerializedObject serializedVisual;
    private Vector2 scrollPosition;
    private int selectedFragmentIndex = 1;
    private Material capMaterial;

    [MenuItem("Tools/Mod Toolkit/Dismember Preview")]
    public static void Open()
    {
        GetWindow<DismemberPreviewWindow>("Dismember Preview");
    }

    private void OnEnable()
    {
        if (capMaterial == null)
        {
            capMaterial = Resources.Load<Material>(DefaultCapMaterialPath);
        }

        SyncSelection();
    }

    private void OnSelectionChange()
    {
        SyncSelection();
        Repaint();
    }

    private void OnDisable()
    {
        DismemberPreviewUtility.ClearPreview();
    }

    private void OnGUI()
    {
        DrawTargetSelector();

        if (visual == null)
        {
            EditorGUILayout.HelpBox("Select a GameObject with RagdollDismembermentVisual.", MessageType.Info);
            return;
        }

        serializedVisual.Update();

        EditorGUILayout.Space();
        capMaterial = EditorGUILayout.ObjectField("Cap Material", capMaterial, typeof(Material), false) as Material;
        EditorGUILayout.Space();
        DrawFragmentSelector();
        EditorGUILayout.Space();
        DrawFragmentActions();
        EditorGUILayout.Space();
        DrawFragmentFields();

        serializedVisual.ApplyModifiedProperties();
    }

    private void DrawTargetSelector()
    {
        EditorGUI.BeginChangeCheck();
        targetObject = EditorGUILayout.ObjectField("Target", targetObject, typeof(GameObject), true);
        if (EditorGUI.EndChangeCheck())
        {
            ResolveVisualFromTarget();
        }

        if (GUILayout.Button("Use Current Selection"))
        {
            targetObject = Selection.activeGameObject;
            ResolveVisualFromTarget();
        }
    }

    private void DrawFragmentSelector()
    {
        var fragments = serializedVisual.FindProperty("Fragments");
        if (fragments == null || fragments.arraySize <= 1)
        {
            EditorGUILayout.HelpBox("No configured fragments found.", MessageType.Warning);
            return;
        }

        selectedFragmentIndex = Mathf.Clamp(selectedFragmentIndex, 1, fragments.arraySize - 1);

        string[] options = new string[fragments.arraySize - 1];
        for (int i = 1; i < fragments.arraySize; i++)
        {
            var fragment = fragments.GetArrayElementAtIndex(i);
            var nameProperty = fragment.FindPropertyRelative("Name");
            options[i - 1] = string.IsNullOrEmpty(nameProperty.stringValue) ? "Unnamed Fragment" : nameProperty.stringValue;
        }

        selectedFragmentIndex = EditorGUILayout.Popup("Fragment", selectedFragmentIndex - 1, options) + 1;

        var previewParent = DismemberPreviewUtility.FindParentFragment(visual, GetSelectedFragmentRuntime());
        using (new EditorGUI.DisabledScope(true))
        {
            EditorGUILayout.ObjectField("Preview Parent", previewParent != null ? previewParent.bone : null, typeof(Transform), true);
        }
    }

    private void DrawFragmentActions()
    {
        var fragment = GetSelectedFragmentRuntime();
        if (fragment == null)
        {
            return;
        }

        if (GUILayout.Button("Preview Fragment"))
        {
            if (!DismemberPreviewUtility.PreviewFragment(visual, fragment, capMaterial))
            {
                EditorUtility.DisplayDialog("Dismember Preview", "Preview generation failed. Check fragment bone, plane, and parent meshes.", "OK");
            }
        }

        if (GUILayout.Button("Clear Preview"))
        {
            DismemberPreviewUtility.ClearPreview();
        }

        //if (GUILayout.Button("Update Plane"))
        //{
        //    Undo.RecordObject(visual, "Update Dismember Plane");
        //    visual.UpdatePlane(fragment.Name);
        //    EditorUtility.SetDirty(visual);
        //}

        //if (GUILayout.Button("Copy From Mirror"))
        //{
        //    Undo.RecordObject(visual, "Copy Dismember From Mirror");
        //    visual.CopyBoundFromMirror(fragment.Name);
        //    EditorUtility.SetDirty(visual);
        //}

        //if (GUILayout.Button("Normalize Plane"))
        //{
        //    var fragmentProperty = GetSelectedFragmentProperty();
        //    var normal = fragmentProperty.FindPropertyRelative("cutPlaneNormal").vector3Value.normalized;
        //    var binormal = fragmentProperty.FindPropertyRelative("cutPlaneBinormal").vector3Value.normalized;
        //    fragmentProperty.FindPropertyRelative("cutPlaneNormal").vector3Value = normal;
        //    fragmentProperty.FindPropertyRelative("cutPlaneBinormal").vector3Value = binormal;
        //    serializedVisual.ApplyModifiedProperties();
        //    EditorUtility.SetDirty(visual);
        //}
    }

    private void DrawFragmentFields()
    {
        var fragmentProperty = GetSelectedFragmentProperty();
        if (fragmentProperty == null)
        {
            return;
        }

        scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition);
        EditorGUILayout.PropertyField(fragmentProperty.FindPropertyRelative("Name"));
        EditorGUILayout.PropertyField(fragmentProperty.FindPropertyRelative("bone"));
        EditorGUILayout.PropertyField(fragmentProperty.FindPropertyRelative("Position"));
        EditorGUILayout.PropertyField(fragmentProperty.FindPropertyRelative("Rotation"));
        EditorGUILayout.PropertyField(fragmentProperty.FindPropertyRelative("Size"));
        EditorGUILayout.PropertyField(fragmentProperty.FindPropertyRelative("cutPlanePos"));
        EditorGUILayout.PropertyField(fragmentProperty.FindPropertyRelative("cutPlaneNormal"));
        EditorGUILayout.PropertyField(fragmentProperty.FindPropertyRelative("cutPlaneBinormal"));
        EditorGUILayout.PropertyField(fragmentProperty.FindPropertyRelative("cutRadius"));
        EditorGUILayout.PropertyField(fragmentProperty.FindPropertyRelative("cutAngleOnBinormal"));
        EditorGUILayout.EndScrollView();
    }

    private void SyncSelection()
    {
        if (targetObject == null)
        {
            targetObject = Selection.activeGameObject;
        }

        ResolveVisualFromTarget();
    }

    private void ResolveVisualFromTarget()
    {
        visual = null;
        serializedVisual = null;

        var gameObject = targetObject as GameObject;
        if (gameObject == null)
        {
            return;
        }

        visual = gameObject.GetComponentInChildren<RagdollDismembermentVisual>(true);
        if (visual == null)
        {
            return;
        }

        serializedVisual = new SerializedObject(visual);
        selectedFragmentIndex = Mathf.Clamp(selectedFragmentIndex, 1, Mathf.Max(1, visual.Fragments.Count - 1));
    }

    private SerializedProperty GetSelectedFragmentProperty()
    {
        var fragments = serializedVisual.FindProperty("Fragments");
        if (fragments == null || fragments.arraySize <= selectedFragmentIndex)
        {
            return null;
        }

        return fragments.GetArrayElementAtIndex(selectedFragmentIndex);
    }

    private BodyFragment GetSelectedFragmentRuntime()
    {
        if (visual == null || visual.Fragments == null || visual.Fragments.Count <= selectedFragmentIndex)
        {
            return null;
        }

        return visual.Fragments[selectedFragmentIndex];
    }
}
