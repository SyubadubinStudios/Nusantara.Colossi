using System.Numerics;
using System.Runtime.CompilerServices;

namespace NusantaraColossi.Core;

/// <summary>Small allocation-free math helpers shared by simulation and rendering.</summary>
public static class Mathx
{
    public const float Tau = MathF.PI * 2f;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static float Saturate(float v) => v < 0 ? 0 : v > 1 ? 1 : v;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static float Lerp(float a, float b, float t) => a + (b - a) * t;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static float SmoothStep(float e0, float e1, float x)
    {
        var t = Saturate((x - e0) / (e1 - e0));
        return t * t * (3 - 2 * t);
    }

    /// <summary>Frame-rate independent exponential approach (1 - e^(-k dt)).</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static float Damp(float current, float target, float sharpness, float dt) =>
        Lerp(current, target, 1 - MathF.Exp(-sharpness * dt));

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector3 Damp(Vector3 current, Vector3 target, float sharpness, float dt) =>
        Vector3.Lerp(current, target, 1 - MathF.Exp(-sharpness * dt));

    /// <summary>Wraps an angle into (-PI, PI].</summary>
    public static float WrapAngle(float a)
    {
        a %= Tau;
        if (a <= -MathF.PI) a += Tau;
        else if (a > MathF.PI) a -= Tau;
        return a;
    }

    /// <summary>Rotates <paramref name="current"/> toward <paramref name="target"/> by at most <paramref name="maxStep"/> radians.</summary>
    public static float TurnToward(float current, float target, float maxStep)
    {
        var delta = WrapAngle(target - current);
        return WrapAngle(current + Math.Clamp(delta, -maxStep, maxStep));
    }

    /// <summary>Heading convention used everywhere: yaw 0 faces +Z, positive yaw turns toward +X.</summary>
    public static Vector3 Forward(float yaw) => new(MathF.Sin(yaw), 0, MathF.Cos(yaw));

    public static float YawOf(Vector3 direction) => MathF.Atan2(direction.X, direction.Z);

    public static Vector3 Flat(Vector3 v) => new(v.X, 0, v.Z);

    public static Vector3 SafeNormalize(Vector3 v, Vector3 fallback)
    {
        var l = v.Length();
        return l > 1e-5f ? v / l : fallback;
    }

    public static float Component(Vector3 v, int axis) => axis switch { 0 => v.X, 1 => v.Y, _ => v.Z };

    public static Vector3 WithComponent(Vector3 v, int axis, float value) => axis switch
    {
        0 => v with { X = value },
        1 => v with { Y = value },
        _ => v with { Z = value }
    };

    public static Vector3 Axis(int axis, float sign) => axis switch
    {
        0 => new(sign, 0, 0),
        1 => new(0, sign, 0),
        _ => new(0, 0, sign)
    };
}

/// <summary>Deterministic value noise. Identical on every platform so co-op peers agree on terrain.</summary>
public static class Noise
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static float Hash(int x, int y, int seed)
    {
        unchecked
        {
            var h = (uint)(x * 374761393 + y * 668265263 + seed * 1442695041);
            h = (h ^ (h >> 13)) * 1274126177u;
            h ^= h >> 16;
            return (h & 0xFFFFFF) / 16777215f;
        }
    }

    public static float Value(float x, float y, int seed)
    {
        var xi = (int)MathF.Floor(x);
        var yi = (int)MathF.Floor(y);
        var fx = x - xi;
        var fy = y - yi;
        var ux = fx * fx * (3 - 2 * fx);
        var uy = fy * fy * (3 - 2 * fy);
        var a = Hash(xi, yi, seed);
        var b = Hash(xi + 1, yi, seed);
        var c = Hash(xi, yi + 1, seed);
        var d = Hash(xi + 1, yi + 1, seed);
        return Mathx.Lerp(Mathx.Lerp(a, b, ux), Mathx.Lerp(c, d, ux), uy);
    }

    /// <summary>Fractal Brownian motion in [0, 1].</summary>
    public static float Fbm(float x, float y, int seed, int octaves = 4)
    {
        float sum = 0, amp = .5f, norm = 0, freq = 1;
        for (var i = 0; i < octaves; i++)
        {
            sum += Value(x * freq, y * freq, seed + i * 31) * amp;
            norm += amp;
            amp *= .5f;
            freq *= 2.03f;
        }
        return sum / norm;
    }

    /// <summary>Ridged noise in [0, 1]; sharp crests for volcanic ridges.</summary>
    public static float Ridged(float x, float y, int seed, int octaves = 4)
    {
        float sum = 0, amp = .5f, norm = 0, freq = 1;
        for (var i = 0; i < octaves; i++)
        {
            var n = 1 - MathF.Abs(Value(x * freq, y * freq, seed + i * 17) * 2 - 1);
            sum += n * n * amp;
            norm += amp;
            amp *= .5f;
            freq *= 2.1f;
        }
        return sum / norm;
    }
}
