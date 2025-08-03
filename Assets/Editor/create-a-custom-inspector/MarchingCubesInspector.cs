using UnityEditor;
using UnityEngine.UIElements;
using UnityEditor.UIElements;
using UnityEngine;

[CustomEditor(typeof(MarchingCubes))]
public class MarchingCubesInspector : Editor
{
    private GameObject activeObject = null;
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();
        bool generateCPU = GUILayout.Button("Generate (CPU)");
        bool generateGPU = GUILayout.Button("Generate (GPU)");

        // sussy
        if (activeObject == null) activeObject = Selection.activeGameObject;
        MarchingCubes mc = activeObject.GetComponent<MarchingCubes>();
        if (mc == null) return;

        if (generateCPU) mc.Generate();
        if (generateGPU) mc.GenerateGPU();
    }
}
