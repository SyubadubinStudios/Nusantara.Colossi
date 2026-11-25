#:project ../../src/NusantaraColossi.Core/NusantaraColossi.Core.csproj
#:property PublishAot=false
#:property JsonSerializerIsReflectionEnabledByDefault=true
// Dumps every guardian's parts at the rest pose (game space: Y up, guardians face +Z) for the
// Blender body-mesh pipeline (assets/sources/blender/process_bodies.py).
// Run: dotnet run tools/rigdump/rigdump.cs -- assets/sources/rodin/rigs.json
using System.Numerics;
using System.Text.Json;
using NusantaraColossi.Core;

var outPath = args.Length > 0 ? args[0] : "rigs.json";
var all = new Dictionary<string, object>();
foreach (var bp in Colossi.All)
{
    var world = new Matrix4x4[bp.Parts.Length];
    ColossusRig.Evaluate(bp, new ColossusPose(), world);
    all[bp.Id] = new
    {
        archetype = bp.Archetype.ToString(),
        height = bp.Height,
        rootHeight = bp.RootHeight,
        parts = bp.Parts.Select((p, i) => new
        {
            id = p.Id,
            role = p.Role.ToString(),
            surface = p.Surface.ToString(),
            center = new[] { p.Center.X, p.Center.Y, p.Center.Z },
            half = new[] { p.Half.X, p.Half.Y, p.Half.Z },
            // Row-vector matrix (local × world), row-major.
            world = new[]
            {
                world[i].M11, world[i].M12, world[i].M13, world[i].M14,
                world[i].M21, world[i].M22, world[i].M23, world[i].M24,
                world[i].M31, world[i].M32, world[i].M33, world[i].M34,
                world[i].M41, world[i].M42, world[i].M43, world[i].M44,
            },
        }).ToArray(),
    };
}
File.WriteAllText(outPath, JsonSerializer.Serialize(all, new JsonSerializerOptions { WriteIndented = true }));
Console.WriteLine($"{all.Count} guardians -> {outPath}");
