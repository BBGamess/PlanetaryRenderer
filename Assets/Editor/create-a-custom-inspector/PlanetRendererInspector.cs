using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

[CustomEditor(typeof(PlanetRenderer))]
public class PlanetRendererInspector : Editor
{
    private PlanetRenderer pr;

    Editor colorEditor;
    Editor shapeEditor;

    public override void OnInspectorGUI()
    {
        if (pr == null) pr = (PlanetRenderer)target;
        if (pr == null) return;

        base.OnInspectorGUI();
        bool generateMesh = GUILayout.Button("Generate Mesh");
        bool generateMeshAsync = GUILayout.Button("Generate Mesh Asynchronously");
        bool updateColors = GUILayout.Button("Update Colors");

        if (generateMesh) pr.RenderPlanetSequential();
        //if (generateMeshAsync) pr.RenderPlanetAsync();
        if (updateColors) pr.UpdatePlanetColors();

        DrawScriptableEditor(pr.planetColors, pr.UpdatePlanetColors, ref colorEditor);
        DrawScriptableEditor(pr.planetShape, pr.UpdateShapeSettings, ref shapeEditor);
    }

    private void DrawScriptableEditor(Object scriptable, System.Action update, ref Editor editor)
    {
        if (scriptable == null) return;

        EditorGUILayout.InspectorTitlebar(true, scriptable);
        using (EditorGUI.ChangeCheckScope check = new EditorGUI.ChangeCheckScope())
        {
            CreateCachedEditor(scriptable, null, ref editor);
            editor.OnInspectorGUI();

            if (check.changed && update != null)
            {
                update();
            }
        }
    }
}
