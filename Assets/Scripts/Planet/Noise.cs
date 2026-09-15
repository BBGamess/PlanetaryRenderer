using UnityEngine;

// CPU port of the exact shader code for Simplex Noise
public class Noise
{
    static float Mod289(float x)
    {
        return x - Mathf.Floor(x / 289f) * 289f;
    }

    static Vector3 Mod289(Vector3 v)
    {
        return new Vector3(
            Mod289(v.x),
            Mod289(v.y),
            Mod289(v.z)
        );
    }

    static Vector4 Mod289(Vector4 v)
    {
        return new Vector4(
            Mod289(v.x),
            Mod289(v.y),
            Mod289(v.z),
            Mod289(v.w)
        );
    }

    static Vector4 Permute(Vector4 x)
    {
        return Mod289(Mul(x * 34f + Vector4.one, x));
    }

    static Vector4 TaylorInvSqrt(Vector4 r)
    {
        return new Vector4(
            1.79284291400159f - 0.85373472095314f * r.x,
            1.79284291400159f - 0.85373472095314f * r.y,
            1.79284291400159f - 0.85373472095314f * r.z,
            1.79284291400159f - 0.85373472095314f * r.w
        );
    }

    static Vector4 Abs(Vector4 v) =>
    new Vector4(Mathf.Abs(v.x), Mathf.Abs(v.y), Mathf.Abs(v.z), Mathf.Abs(v.w));

    static Vector4 Floor(Vector4 v) =>
        new Vector4(Mathf.Floor(v.x), Mathf.Floor(v.y), Mathf.Floor(v.z), Mathf.Floor(v.w));

    static Vector4 Step(Vector4 edge, Vector4 x) =>
        new Vector4(
            x.x < edge.x ? 0f : 1f,
            x.y < edge.y ? 0f : 1f,
            x.z < edge.z ? 0f : 1f,
            x.w < edge.w ? 0f : 1f
        );

    static Vector4 Mul(Vector4 a, Vector4 b)
    {
        return new Vector4(
            a.x * b.x,
            a.y * b.y,
            a.z * b.z,
            a.w * b.w
        );
    }

    public static float SimplexNoise(Vector3 v)
    {
        const float Cx = 1f / 6f;
        const float Cy = 1f / 3f;

        // First corner
        float s = (v.x + v.y + v.z) * Cy;
        Vector3 i = new Vector3(
            Mathf.Floor(v.x + s),
            Mathf.Floor(v.y + s),
            Mathf.Floor(v.z + s)
        );

        float t = (i.x + i.y + i.z) * Cx;
        Vector3 x0 = v - i + new Vector3(t, t, t);

        // Other corners
        Vector3 g = new Vector3(
            x0.x >= x0.y ? 1f : 0f,
            x0.y >= x0.z ? 1f : 0f,
            x0.z >= x0.x ? 1f : 0f
        );

        Vector3 l = Vector3.one - g;
        Vector3 i1 = Vector3.Min(g, new Vector3(l.z, l.x, l.y));
        Vector3 i2 = Vector3.Max(g, new Vector3(l.z, l.x, l.y));

        Vector3 x1 = x0 - i1 + Vector3.one * Cx;
        Vector3 x2 = x0 - i2 + Vector3.one * Cy;
        Vector3 x3 = x0 - Vector3.one * 0.5f;

        // Permutations
        i = Mod289(i);

        Vector4 p = Permute(
            Permute(
                Permute(
                    new Vector4(i.z, i.z + i1.z, i.z + i2.z, i.z + 1f)
                ) + new Vector4(i.y, i.y + i1.y, i.y + i2.y, i.y + 1f)
            ) + new Vector4(i.x, i.x + i1.x, i.x + i2.x, i.x + 1f)
        );

        // Gradients
        Vector4 j = p - 49f * new Vector4(
            Mathf.Floor(p.x / 49f),
            Mathf.Floor(p.y / 49f),
            Mathf.Floor(p.z / 49f),
            Mathf.Floor(p.w / 49f)
        );

        Vector4 x_ = new Vector4(
            Mathf.Floor(j.x / 7f),
            Mathf.Floor(j.y / 7f),
            Mathf.Floor(j.z / 7f),
            Mathf.Floor(j.w / 7f)
        );

        Vector4 y_ = j - x_ * 7f;

        Vector4 x = (x_ * 2f + Vector4.one * 0.5f) / 7f - Vector4.one;
        Vector4 y = (y_ * 2f + Vector4.one * 0.5f) / 7f - Vector4.one;

        Vector4 h = Vector4.one - Abs(x) - Abs(y);

        Vector4 b0 = new Vector4(x.x, x.y, y.x, y.y);
        Vector4 b1 = new Vector4(x.z, x.w, y.z, y.w);

        Vector4 s0 = Floor(b0) * 2f + Vector4.one;
        Vector4 s1 = Floor(b1) * 2f + Vector4.one;
        Vector4 sh = -Step(h, Vector4.zero);

        Vector4 a0 = new Vector4(
            b0.x + s0.x * sh.x,
            b0.z + s0.z * sh.x,
            b0.y + s0.y * sh.y,
            b0.w + s0.w * sh.y
        );

        Vector4 a1 = new Vector4(
            b1.x + s1.x * sh.z,
            b1.z + s1.z * sh.z,
            b1.y + s1.y * sh.w,
            b1.w + s1.w * sh.w
        );

        Vector3 g0 = new Vector3(a0.x, a0.y, h.x);
        Vector3 g1 = new Vector3(a0.z, a0.w, h.y);
        Vector3 g2 = new Vector3(a1.x, a1.y, h.z);
        Vector3 g3 = new Vector3(a1.z, a1.w, h.w);

        Vector4 norm = TaylorInvSqrt(new Vector4(
            Vector3.Dot(g0, g0),
            Vector3.Dot(g1, g1),
            Vector3.Dot(g2, g2),
            Vector3.Dot(g3, g3)
        ));

        g0 *= norm.x;
        g1 *= norm.y;
        g2 *= norm.z;
        g3 *= norm.w;

        Vector4 m = new Vector4(
            Mathf.Max(0.6f - Vector3.Dot(x0, x0), 0f),
            Mathf.Max(0.6f - Vector3.Dot(x1, x1), 0f),
            Mathf.Max(0.6f - Vector3.Dot(x2, x2), 0f),
            Mathf.Max(0.6f - Vector3.Dot(x3, x3), 0f)
        );

        m = Mul(m, m);
        m = Mul(m, m);

        Vector4 px = new Vector4(
            Vector3.Dot(x0, g0),
            Vector3.Dot(x1, g1),
            Vector3.Dot(x2, g2),
            Vector3.Dot(x3, g3)
        );

        return 42f * Vector4.Dot(m, px);
    }
}
