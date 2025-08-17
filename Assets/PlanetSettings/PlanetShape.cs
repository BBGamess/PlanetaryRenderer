using UnityEngine;

[CreateAssetMenu(fileName = "PlanetShape", menuName = "Scriptable Objects/PlanetShape")]
public class PlanetShape : ScriptableObject
{
    public float planetRadius = 100f;

    public NoiseFilter[] noiseFilters;

    [System.Serializable]
    public class NoiseFilter
    {
        public Vector3 offset;
        public int     octaves;
        public float   amplitude;
        public float   frequency;
        public float   lacunarity;
        public float   persistence;

        public NoiseFilter(Vector3 offset, int octaves, float amplitude, float frequency, float lacunarity, float persistence)
        {
            this.offset      = offset;
            this.octaves     = octaves;
            this.amplitude   = amplitude;
            this.frequency   = frequency;
            this.lacunarity  = lacunarity;
            this.persistence = persistence;
        }
    }
}
