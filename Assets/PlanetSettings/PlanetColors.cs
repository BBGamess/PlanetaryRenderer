using Unity.VisualScripting;
using UnityEngine;

[CreateAssetMenu(fileName = "PlanetColors", menuName = "Scriptable Objects/PlanetColors")]
public class PlanetColors : ScriptableObject
{

    public Gradient planetGradient;

    private Texture2D m_Texture;

    [HideInInspector] public int colorResolution = 100;

    public PlanetColors(int colorResolution)
    {
        this.colorResolution = colorResolution;
    }

    public Texture2D GetColorTexture()
    {
        if (colorResolution <= 0) return null;

        if (m_Texture == null || m_Texture.width != colorResolution) m_Texture = new Texture2D(colorResolution, 1);
        Color32[] colors = new Color32[colorResolution];
        for (int i = 0; i < colors.Length; i++)
        {
            colors[i] = planetGradient.Evaluate(i / (colorResolution - 1.0f));
        }
        m_Texture.SetPixels32(colors);
        m_Texture.Apply();

        return m_Texture;
    }
}
