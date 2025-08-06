using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

[CustomEditor(typeof(PlanetRenderer))]
public class PlanetRendererInspector : Editor
{
    private GameObject activeObject = null;
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();
        bool generate = GUILayout.Button("Generate");

        // sussy
        if (activeObject == null) activeObject = Selection.activeGameObject;
        PlanetRenderer pr = activeObject.GetComponent<PlanetRenderer>();
        if (pr == null) return;

        if (generate) pr.RenderPlanet();
    }
}
