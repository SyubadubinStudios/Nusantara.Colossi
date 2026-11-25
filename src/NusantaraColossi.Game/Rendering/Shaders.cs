namespace NusantaraColossi.Game.Rendering;

/// <summary>
/// WGSL surface hooks. They only change the surface before lighting, so ThreeNet's PBR, shadows,
/// SSAO and fog keep working. Colossus/stone shaders are UV-space (UVs are metres along each face),
/// so the pattern moves with the part; terrain is world-space.
/// </summary>
internal static class Shaders
{
    /// <summary>Low quality drops the two finest noise octaves: about 40% cheaper per pixel on integrated GPUs.</summary>
    public static string ForQuality(string source, int quality) => quality > 0 ? source : source.Replace(
        "return nc_noise(p) * 0.5 + nc_noise(p * 2.03) * 0.25 + nc_noise(p * 4.1) * 0.125 + nc_noise(p * 8.3) * 0.0625;",
        "return nc_noise(p) * 0.62 + nc_noise(p * 2.03) * 0.31;");

    private const string Noise = """
        fn nc_hash(p: vec3<f32>) -> f32 {
            let q = fract(p * 0.1031);
            let r = q + dot(q, q.yzx + vec3<f32>(33.33));
            return fract((r.x + r.y) * r.z);
        }
        fn nc_noise(p: vec3<f32>) -> f32 {
            let i = floor(p); let f = fract(p); let u = f*f*(3.0-2.0*f);
            return mix(mix(mix(nc_hash(i), nc_hash(i+vec3<f32>(1.0,0.0,0.0)), u.x),
                           mix(nc_hash(i+vec3<f32>(0.0,1.0,0.0)), nc_hash(i+vec3<f32>(1.0,1.0,0.0)), u.x), u.y),
                       mix(mix(nc_hash(i+vec3<f32>(0.0,0.0,1.0)), nc_hash(i+vec3<f32>(1.0,0.0,1.0)), u.x),
                           mix(nc_hash(i+vec3<f32>(0.0,1.0,1.0)), nc_hash(i+vec3<f32>(1.0,1.0,1.0)), u.x), u.y), u.z);
        }
        fn nc_fbm(p: vec3<f32>) -> f32 {
            return nc_noise(p) * 0.5 + nc_noise(p * 2.03) * 0.25 + nc_noise(p * 4.1) * 0.125 + nc_noise(p * 8.3) * 0.0625;
        }
        fn nc_bump(context: SurfaceContext, height: f32) -> vec3<f32> {
            let p = context.world_position;
            let dx = dpdx(p); let dy = dpdy(p);
            let n = normalize(context.world_normal);
            let rx = cross(dy, n); let ry = cross(n, dx);
            let det = dot(dx, rx);
            let gradient = (dpdx(height) * rx + dpdy(height) * ry) * sign(det);
            return normalize(n - gradient / max(abs(det), 0.00001));
        }
        """;

    /// <summary>
    /// custom0.xyz base colour, custom0.w relief strength (carved panels).
    /// custom1.xyz moss colour, custom1.w moss amount.
    /// custom2.x fur (0/1), custom2.y wood grain (0/1), custom2.z glass (0/1), custom2.w seed.
    /// custom3.xyz seam glow colour, custom3.w glow intensity (pulsed from C#).
    /// </summary>
    public const string Colossus = Noise + """
        fn user_surface(context: SurfaceContext, surface: Surface) -> Surface {
            var out = surface;
            let uv = context.uv;
            let seed = context.custom2.w;
            let q = vec3<f32>(uv.x, uv.y, seed);
            let broad = nc_fbm(q * 0.18);
            let grain = nc_noise(q * 3.1);
            // Carved relief: panels with grooves, and a kawung (four-petal) motif in alternate panels.
            let cell = uv / 2.4;
            let f = fract(cell) - vec2<f32>(0.5);
            let edge = max(abs(f.x), abs(f.y));
            let groove = smoothstep(0.43, 0.49, edge);
            let alt = step(0.5, fract((floor(cell.x) + floor(cell.y)) * 0.5));
            let petal = abs(length(abs(f) - vec2<f32>(0.17)) - 0.12);
            let motif = (1.0 - smoothstep(0.0, 0.025, petal)) * alt * (1.0 - groove);
            let relief = context.custom0.w;
            var height = (grain * 0.08 + broad * 0.25) - (groove * 0.5 + motif * 0.35) * relief;
            var albedo = context.custom0.xyz * (0.72 + broad * 0.45 + grain * 0.12);
            albedo = albedo * (1.0 - (groove * 0.45 + motif * 0.3) * relief);
            var roughness = 0.88 - broad * 0.1;
            // Fur: long strands along v.
            if (context.custom2.x > 0.5) {
                let strand = nc_noise(vec3<f32>(uv.x * 9.0, uv.y * 0.8, seed));
                albedo = context.custom0.xyz * (0.55 + strand * 0.6 + broad * 0.2);
                height = strand * 0.3;
                roughness = 0.97;
            }
            // Wood: rings and planks.
            if (context.custom2.y > 0.5) {
                let ring = fract(uv.y * 0.6 + nc_noise(q * vec3<f32>(0.6, 6.0, 1.0)) * 1.6);
                let plank = smoothstep(0.46, 0.5, abs(fract(uv.x / 0.9) - 0.5));
                albedo = context.custom0.xyz * (0.7 + ring * 0.35) * (1.0 - plank * 0.5);
                height = ring * 0.1 - plank * 0.4;
                roughness = 0.8;
            }
            // Moss gathers on upward faces, in noisy patches.
            let up = context.world_normal.y;
            let moss = smoothstep(0.15, 0.75, up + (broad - 0.5) * 0.9) * context.custom1.w;
            albedo = mix(albedo, context.custom1.xyz * (0.6 + grain * 0.5), moss);
            out.albedo = albedo;
            out.roughness = mix(roughness, 0.95, moss);
            out.reflectance = 0.25;
            if (context.custom2.z > 0.5) {
                // Glass curtain wall: dark, reflective, with mullions on the panel grid.
                let mullion = smoothstep(0.44, 0.48, edge);
                out.albedo = mix(vec3<f32>(0.03, 0.05, 0.07), vec3<f32>(0.25, 0.27, 0.3), mullion);
                out.metallic = 0.6 - mullion * 0.4;
                out.roughness = 0.12 + mullion * 0.5;
                out.reflectance = 0.9;
            }
            // Ancient seams glow (gold, lava or cyan) along the grooves.
            let glow = context.custom3.w * (groove * 0.9 + motif * 0.6) * (0.6 + 0.4 * sin(context.time * 2.0 + uv.y * 0.4));
            out.emissive = context.custom3.xyz * glow;
            out.normal = nc_bump(context, height * 0.06);
            return out;
        }
        """;

    /// <summary>
    /// custom0.xyz ground low, custom1.xyz ground high, custom2.xyz rock, custom3.x water level (or -999), custom3.y biome index, custom3.z wetness.
    /// </summary>
    public const string Terrain = Noise + """
        fn user_surface(context: SurfaceContext, surface: Surface) -> Surface {
            var out = surface;
            let p = context.world_position;
            let n = normalize(context.world_normal);
            let slope = 1.0 - n.y;
            let broad = nc_fbm(p * 0.025);
            let mid = nc_noise(p * 0.21);
            let detail = nc_noise(p * 1.7);
            var ground = mix(context.custom0.xyz, context.custom1.xyz, smoothstep(0.3, 0.7, broad + p.y * 0.006));
            ground = ground * (0.78 + mid * 0.3 + detail * 0.12);
            let rockiness = smoothstep(0.22, 0.45, slope + (broad - 0.5) * 0.25);
            let rock = context.custom2.xyz * (0.75 + detail * 0.3 + mid * 0.2);
            var albedo = mix(ground, rock, rockiness);
            var roughness = mix(0.94, 0.82, rockiness);
            var height = detail * 0.05 + mid * 0.12;
            let biome = context.custom3.y;
            let wet = context.custom3.z;
            // City: asphalt with lane markings and puddles.
            if (biome > 0.5 && biome < 1.5) {
                let road = abs(fract(p.x / 42.0) - 0.5) < 0.18 || abs(fract(p.z / 42.0) - 0.5) < 0.18;
                let dash = step(0.5, fract(p.z * 0.25)) * step(abs(fract(p.x / 42.0) - 0.5), 0.004);
                albedo = select(vec3<f32>(0.32, 0.31, 0.3) * (0.8 + mid * 0.3), vec3<f32>(0.08, 0.085, 0.09) * (0.8 + detail * 0.4), road);
                albedo = mix(albedo, vec3<f32>(0.75, 0.68, 0.4), dash);
                let puddle = smoothstep(0.55, 0.62, broad + mid * 0.2);
                roughness = mix(0.7, 0.08, puddle * wet);
                out.metallic = puddle * wet * 0.2;
            }
            // Terraces: flooded paddies mirror the sky.
            if (biome > 1.5 && biome < 2.5 && n.y > 0.985) {
                let paddy = smoothstep(0.35, 0.5, nc_noise(p * 0.05));
                albedo = mix(albedo, vec3<f32>(0.1, 0.16, 0.12), paddy * 0.7);
                roughness = mix(roughness, 0.05, paddy);
            }
            // Crater: sulfur crusts in the hollows.
            if (biome > 5.5 && biome < 6.5) {
                let sulfur = smoothstep(0.55, 0.75, mid + (1.0 - smoothstep(0.0, 18.0, p.y)) * 0.4);
                albedo = mix(albedo, vec3<f32>(0.85, 0.72, 0.18) * (0.8 + detail * 0.3), sulfur * (1.0 - rockiness));
            }
            // Shoreline darkening and wet sheen.
            let water = context.custom3.x;
            if (water > -900.0) {
                let shore = 1.0 - smoothstep(water - 0.3, water + 1.5, p.y);
                albedo = mix(albedo, albedo * 0.55, shore);
                roughness = mix(roughness, 0.35, shore);
            }
            roughness = mix(roughness, roughness * 0.45, wet * (1.0 - rockiness) * 0.6);
            out.albedo = albedo;
            out.roughness = roughness;
            out.reflectance = 0.2;
            out.normal = nc_bump(context, height);
            return out;
        }
        """;

    /// <summary>custom0.xyz facade colour, custom1.x lit-window fraction, custom1.y night (0..1), custom1.z seed.</summary>
    public const string Building = Noise + """
        fn user_surface(context: SurfaceContext, surface: Surface) -> Surface {
            var out = surface;
            let uv = context.uv;
            let cell = vec2<f32>(uv.x / 3.2, uv.y / 3.8);
            let id = floor(cell);
            let f = fract(cell);
            let frame = step(0.12, f.x) * step(f.x, 0.88) * step(0.2, f.y) * step(f.y, 0.85);
            let lit = step(1.0 - context.custom1.x, nc_hash(vec3<f32>(id.x, id.y, context.custom1.z)));
            let warm = nc_hash(vec3<f32>(id.y, id.x, 7.0));
            let windowColor = mix(vec3<f32>(1.0, 0.78, 0.45), vec3<f32>(0.55, 0.85, 1.0), step(0.6, warm));
            out.albedo = mix(context.custom0.xyz, vec3<f32>(0.05, 0.06, 0.08), frame);
            out.metallic = frame * 0.5;
            out.roughness = mix(0.75, 0.1, frame);
            out.reflectance = 0.6;
            out.emissive = windowColor * frame * lit * context.custom1.y * 2.2;
            return out;
        }
        """;

    /// <summary>Foliage tint with per-instance variation. custom0.xyz base, custom1.xyz tip colour.</summary>
    public const string Foliage = Noise + """
        fn user_surface(context: SurfaceContext, surface: Surface) -> Surface {
            var out = surface;
            let p = context.world_position;
            let v = nc_noise(p * 0.08);
            let tip = clamp(context.uv.y, 0.0, 1.0);
            out.albedo = mix(context.custom0.xyz, context.custom1.xyz, tip * 0.7 + v * 0.3) * (0.75 + v * 0.4);
            out.roughness = 0.9;
            return out;
        }
        """;

    /// <summary>Gentle wind sway for instanced grass and leaves (vertex hook). custom2.x strength.</summary>
    public const string FoliageSway = """
        fn user_vertex(context: VertexContext) -> vec3<f32> {
            let h = max(context.position.y, 0.0);
            let s = sin(context.time * 1.7 + context.position.x * 0.7 + context.position.z * 0.5) * 0.08 * context.custom2.x;
            return context.position + vec3<f32>(s * h, 0.0, s * h * 0.6);
        }
        """ + Foliage;
}
