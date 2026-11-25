using System.Numerics;
using NusantaraColossi.Core;
using ThreeNet;
using ThreeNet.Interop;

namespace NusantaraColossi.Game.Rendering;

internal static class MeshFactory
{
    private static float Noise3(Vector3 p, int seed) =>
        (Noise.Fbm(p.X + p.Z * .71f, p.Y + p.Z * .37f, seed, 3) + Noise.Fbm(p.Y * 1.13f + p.X * .29f, p.Z - p.X * .41f, seed + 5, 3)) * .5f;

    /// <summary>
    /// A rounded, noise-sculpted box that stays inside <paramref name="half"/> (the gameplay collision box).
    /// UVs are metres along each face so UV-space shaders keep a constant pattern scale.
    /// </summary>
    public static Geometry SculptedBox(Scene scene, Vector3 half, float roundness, float noiseAmplitude, int seed, float density = .8f, int maxSegments = 26)
    {
        var r = MathF.Min(roundness, MathF.Min(half.X, MathF.Min(half.Y, half.Z)) * .48f);
        var inner = half - new Vector3(r);
        var vertices = new List<Vertex>(2048);
        var indices = new List<uint>(8192);
        for (var axis = 0; axis < 3; axis++)
            for (var s = -1; s <= 1; s += 2)
            {
                var u = (axis + 1) % 3;
                var v = (axis + 2) % 3;
                var hu = Mathx.Component(half, u);
                var hv = Mathx.Component(half, v);
                var nu = Math.Clamp((int)MathF.Ceiling(2 * hu * density), 2, maxSegments);
                var nv = Math.Clamp((int)MathF.Ceiling(2 * hv * density), 2, maxSegments);
                var faceNormal = Mathx.Axis(axis, s);
                var start = (uint)vertices.Count;
                for (var j = 0; j <= nv; j++)
                    for (var i = 0; i <= nu; i++)
                    {
                        var pu = -hu + 2 * hu * i / nu;
                        var pv = -hv + 2 * hv * j / nv;
                        var p = Mathx.WithComponent(Mathx.WithComponent(Mathx.WithComponent(Vector3.Zero, axis, s * Mathx.Component(half, axis)), u, pu), v, pv);
                        var q = Vector3.Clamp(p, -inner, inner);
                        var dir = p - q;
                        var n = dir.LengthSquared() > 1e-8f ? Vector3.Normalize(dir) : faceNormal;
                        var rounded = q + n * r;
                        var d = (Noise3(p * .45f, seed) - .5f) * 2 * noiseAmplitude - noiseAmplitude * .5f;
                        var position = rounded + n * d;
                        vertices.Add(new Vertex(position, n, new Vector2(pu + s * 0.0f, pv)));
                    }
                for (var j = 0; j < nv; j++)
                    for (var i = 0; i < nu; i++)
                    {
                        var a = start + (uint)(j * (nu + 1) + i);
                        var b = a + 1;
                        var c = a + (uint)(nu + 1);
                        var dd = c + 1;
                        if (s > 0) indices.AddRange([a, b, dd, a, dd, c]);
                        else indices.AddRange([a, dd, b, a, c, dd]);
                    }
            }
        var geometry = scene.CreateGeometry(vertices.ToArray(), indices.ToArray());
        geometry.ComputeNormals();
        return geometry;
    }

    /// <summary>Terrain grid sampled from the region's deterministic height function.</summary>
    public static Geometry Heightfield(Scene scene, RegionDefinition region, float size, int resolution)
    {
        var stride = resolution + 1;
        var vertices = new Vertex[stride * stride];
        var indices = new uint[resolution * resolution * 6];
        var step = size / resolution;
        for (var z = 0; z <= resolution; z++)
            for (var x = 0; x <= resolution; x++)
            {
                var wx = -size * .5f + x * step;
                var wz = -size * .5f + z * step;
                vertices[z * stride + x] = new Vertex(new Vector3(wx, region.Height(wx, wz), wz), region.Normal(wx, wz), new Vector2(wx * .1f, wz * .1f));
            }
        var k = 0;
        for (var z = 0; z < resolution; z++)
            for (var x = 0; x < resolution; x++)
            {
                var a = (uint)(z * stride + x);
                var b = a + 1;
                var c = a + (uint)stride;
                var d = c + 1;
                indices[k++] = a; indices[k++] = c; indices[k++] = b;
                indices[k++] = b; indices[k++] = c; indices[k++] = d;
            }
        return scene.CreateGeometry(vertices, indices);
    }

    /// <summary>A single grass/rice blade: tapered quad pair, uv.y = 0 at root and 1 at tip.</summary>
    public static Geometry Blade(Scene scene, float width, float height)
    {
        var w = width * .5f;
        Vertex V(float x, float y, float z, float tip) => new(new Vector3(x, y, z), Vector3.UnitY, new Vector2(.5f, tip));
        var vertices = new[]
        {
            V(-w, 0, 0, 0), V(w, 0, 0, 0), V(w * .2f, height, 0, 1), V(-w * .2f, height, 0, 1),
            V(0, 0, -w, 0), V(0, 0, w, 0), V(0, height, w * .2f, 1), V(0, height, -w * .2f, 1)
        };
        uint[] indices = [0, 1, 2, 0, 2, 3, 2, 1, 0, 3, 2, 0, 4, 5, 6, 4, 6, 7, 6, 5, 4, 7, 6, 4];
        return scene.CreateGeometry(vertices, indices);
    }
}
