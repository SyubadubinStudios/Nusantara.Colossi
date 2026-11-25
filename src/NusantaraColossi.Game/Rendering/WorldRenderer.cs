using System.Numerics;
using NusantaraColossi.Core;
using ThreeNet;
using ThreeNet.Effects;

namespace NusantaraColossi.Game.Rendering;

/// <summary>
/// Owns every GPU resource of one hunt: region environment, colossus, hunters, GARUDA drones and VFX.
/// Reads the <see cref="Encounter"/> each frame; never changes it.
/// </summary>
public sealed partial class WorldRenderer : IDisposable
{
    public Scene Scene { get; } = new();
    public Node Camera { get; }
    public Encounter Encounter { get; }
    public int Quality { get; }
    public float Time { get; private set; }
    /// <summary>Raised when a storm lightning flash happens (audio plays thunder).</summary>
    public event Action<float>? Lightning;
    public string? AssetWarning { get; private set; }

    private readonly Geometry box;
    private readonly Geometry sphere;
    private readonly Geometry cylinder;
    private readonly Geometry torus;
    private readonly Shader colossusShader;
    private readonly Shader terrainShader;
    private readonly Shader buildingShader;
    private readonly Shader foliageShader;
    private readonly Shader particleShader;
    private readonly Shader smokeShader;
    private readonly Node sun;
    private readonly Light sunLight;
    private readonly Dictionary<string, Material> palette = [];
    private WaterSurface? water;
    private CloudLayer? clouds;
    private float lightningTimer = 6;
    private float flash;

    public WorldRenderer(Encounter encounter, int quality)
    {
        Encounter = encounter;
        Quality = Math.Clamp(quality, 0, 2);
        var region = encounter.Region;
        try
        {
            box = Scene.CreateBoxGeometry();
            sphere = Scene.CreateSphereGeometry(1, 20, 14);
            cylinder = Scene.CreateCylinderGeometry(.5f, .5f, 1, 12);
            torus = Scene.CreateTorusGeometry(1, .04f, 8, 64);
            colossusShader = Scene.CreateShader(Shaders.ForQuality(Shaders.Colossus, Quality), ShaderLanguage.Wgsl, "colossus");
            terrainShader = Scene.CreateShader(Shaders.ForQuality(Shaders.Terrain, Quality), ShaderLanguage.Wgsl, "terrain");
            buildingShader = Scene.CreateShader(Shaders.Building, ShaderLanguage.Wgsl, "building");
            foliageShader = Scene.CreateShader(Shaders.FoliageSway, ShaderLanguage.Wgsl, "foliage");
            particleShader = Scene.CreateShader(EffectShaders.Particle, ShaderLanguage.Wgsl, "particle");
            smokeShader = Scene.CreateShader(EffectShaders.Smoke, ShaderLanguage.Wgsl, "smoke");

            var night = region.Weather is Weather.Night;
            var storm = region.Weather is Weather.Storm;
            Scene.Environment = SceneEnvironment.Default with
            {
                Background = new Vector4(region.SkyColor, 1),
                Sky = SkyMode.Procedural,
                SunDirection = region.SunDirection,
                SkyIntensity = night ? .18f : storm ? .55f : 1f,
                SkyHaze = region.Weather is Weather.Fog ? .75f : region.Weather is Weather.Dawn ? .45f : storm ? .9f : .3f,
                SkyClouds = storm ? .95f : region.Weather == Weather.Clear ? .35f : .6f,
                AmbientColor = Vector3.Lerp(region.SkyColor, region.FogColor, .5f),
                // The sky already lights the scene (image-based); ambient is only a floor.
                AmbientIntensity = night ? .03f : .06f,
                FogColor = region.FogColor,
                FogDensity = region.FogDensity,
                // ThreeNet's defaults also apply linear fog from 10 m to 100 m, which would erase a
                // 70 m guardian. Push it past the far plane and let exponential fog do the work.
                FogStart = 2400,
                FogEnd = 2600
            };
            sunLight = Light.Directional(region.SunColor, night ? 1.1f : storm ? 1.6f : 3.2f) with
            {
                CastShadow = Quality > 0,
                ShadowBias = .0006f,
                ShadowNormalBias = .6f
            };
            sun = Scene.AddLight(sunLight, name: "sun");
            sun.Position = region.SunDirection * 400;
            sun.LookAt(Vector3.Zero);
            Scene.AddLight(Light.Hemisphere(Vector3.Lerp(region.SkyColor, Vector3.One, .3f), region.GroundLow * .6f, night ? .15f : .3f));
            Camera = Scene.AddCamera(ThreeNet.Camera.Perspective(58 * MathF.PI / 180, .2f, 2600));

            BuildEnvironment();
            BuildColossus();
            BuildCharacters();
            BuildEffects();
            BuildSignatures();
        }
        catch
        {
            Scene.Dispose();
            throw;
        }
    }

    internal Material Mat(string key, Func<MaterialOptions> create)
    {
        if (!palette.TryGetValue(key, out var material))
            palette[key] = material = Scene.CreateMaterial(create());
        return material;
    }

    internal static Vector4 Rgba(Vector3 c, float a = 1) => new(c, a);

    internal static Vector4 Hex(string hex, float alpha = 1)
    {
        var v = Convert.ToUInt32(hex.TrimStart('#'), 16);
        return MathHelpers.FromHex(v, alpha);
    }

    internal Node Mesh(Geometry geometry, Material material, Vector3 position, Vector3 scale, Node? parent = null, string? name = null)
    {
        var node = Scene.AddMesh(geometry, material, parent, name);
        node.Position = position;
        node.Scale = scale;
        return node;
    }

    /// <summary>Per-frame update. <paramref name="localSlot"/> is the hunter this screen follows.</summary>
    public void Update(float dt, CameraRig cameraRig, int localSlot, bool hunterInstinct)
    {
        Time += dt;
        cameraRig.Apply(Camera);
        UpdateColossus(dt);
        UpdateCharacters(dt, localSlot);
        UpdateEffects(dt, localSlot, hunterInstinct);
        UpdateSignatures(dt);
        UpdateWeather(dt);
        Scene.UpdateAnimations(dt);
    }

    private void UpdateWeather(float dt)
    {
        water?.Update(dt);
        clouds?.Update(dt, Camera.WorldPosition);
        if (Encounter.Region.Weather == Weather.Storm)
        {
            lightningTimer -= dt;
            if (lightningTimer <= 0)
            {
                lightningTimer = 5 + Random.Shared.NextSingle() * 9;
                flash = 1;
                Lightning?.Invoke(.4f + Random.Shared.NextSingle() * 1.6f);
            }
        }
        if (flash > 0 || Encounter.GustStrength > 0)
        {
            flash = MathF.Max(0, flash - dt * 3.2f);
            var flicker = flash * (MathF.Sin(Time * 60) > 0 ? 1 : .4f);
            sun.Light = sunLight with { Intensity = sunLight.Intensity * (1 + flicker * 5), Color = Vector3.Lerp(sunLight.Color, new Vector3(.8f, .85f, 1), flicker) };
        }
    }

    public void Dispose()
    {
        DisposeEffects();
        Scene.Dispose();
    }
}
