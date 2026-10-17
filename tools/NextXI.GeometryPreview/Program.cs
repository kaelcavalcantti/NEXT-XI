using System;
using System.Globalization;
using System.IO;
using System.Text;
using NextXI.Core.Data;
using NextXI.Core.Match;
using NextXI.Runtime;
using UnityEngine;

// Usage: dotnet run -- <outDir>
if (args.Length > 1 && args[1] == "audio") { AudioCheck.Run(args[0]); return; }
string outDir = args.Length > 0 ? args[0] : "preview";
Directory.CreateDirectory(outDir);
CultureInfo.DefaultThreadCurrentCulture = CultureInfo.InvariantCulture;

var profiles = new (string name, int h, int w, HairStyle hair, BeardStyle beard, float skin, Position pos)[]
{
    ("avg", 180, 76, HairStyle.Short, BeardStyle.None, 0.35f, Position.CM),
    ("tall", 196, 92, HairStyle.Afro, BeardStyle.Short, 0.85f, Position.CB),
    ("short", 168, 64, HairStyle.Medium, BeardStyle.Stubble, 0.15f, Position.LW),
    ("keeper", 191, 86, HairStyle.Bun, BeardStyle.Full, 0.55f, Position.GK),
};
foreach (var pr in profiles)
{
    var p = new PlayerProfile { HeightCm = pr.h, WeightKg = pr.w, Position = pr.pos, Appearance = new Appearance { Hair = pr.hair, Beard = pr.beard, SkinTone = pr.skin } };
    var body = AthleteBody.Generate(BodyParams.From(p));
    var mb = body.Mesh;
    int inverted = mb.CountInvertedTriangles();
    int badW = 0;
    foreach (var w in mb.W) if (Math.Abs(w.weight0 + w.weight1 + w.weight2 + w.weight3 - 1f) > 1e-3f) badW++;
    float minY = float.MaxValue, maxY = float.MinValue;
    foreach (var v in mb.V) { minY = Math.Min(minY, v.y); maxY = Math.Max(maxY, v.y); }
    int tris = 0; foreach (var s in mb.Sub) tris += s.Count / 3;
    Console.WriteLine($"{pr.name}: verts {mb.V.Count} tris {tris} inverted {inverted} badWeights {badW} height {maxY - minY:0.000} (target {pr.h / 100f:0.00}) minY {minY:0.000}");
    WriteObj(Path.Combine(outDir, $"{pr.name}_bind.obj"), body, null);

    if (pr.name != "avg" && pr.name != "keeper") continue;
    var poses = new (string, AnimInput, float)[]
    {
        ("idle", new AnimInput { Time = 1f }, 1f),
        ("jog", new AnimInput { LocalVelocity = new Vector3(0, 0, 4f) }, 0.73f),
        ("sprint", new AnimInput { LocalVelocity = new Vector3(0, 0, 9f) }, 0.37f),
        ("shot_windup", new AnimInput { Action = PlayerAction.Shot, ActionTime = 0.24f, ActionDuration = 0.62f, ContactTime = 0.26f, KickSide = 1, KickPower = 0.8f }, 0.24f),
        ("shot_follow", new AnimInput { Action = PlayerAction.Shot, ActionTime = 0.45f, ActionDuration = 0.62f, ContactTime = 0.26f, KickSide = 1, KickPower = 0.8f }, 0.45f),
        ("slide", new AnimInput { Action = PlayerAction.Slide, ActionTime = 0.45f, ActionDuration = 1.05f, KickSide = 1, LocalVelocity = new Vector3(0, 0, 5) }, 0.45f),
        ("dive", new AnimInput { Action = PlayerAction.KeeperDive, ActionTime = 0.4f, ActionDuration = 1f, DiveSide = 1, Goalkeeper = true }, 0.4f),
        ("throwin", new AnimInput { Action = PlayerAction.Throw, ActionTime = 0.38f, ActionDuration = 0.9f, ContactTime = 0.42f }, 0.38f),
        ("celebrate", new AnimInput { Action = PlayerAction.Celebrate, ActionTime = 1f, ActionDuration = 4f, LocalVelocity = new Vector3(0, 0, 3f), Time = 2f }, 1f),
        ("jockey", new AnimInput { Jockey = true, LocalVelocity = new Vector3(1f, 0, 0) }, 1f),
        ("header", new AnimInput { Action = PlayerAction.Header, ActionTime = 0.12f, ActionDuration = 0.55f }, 0.12f),
        ("dejected", new AnimInput { Action = PlayerAction.Dejected, ActionTime = 1f, ActionDuration = 3.5f }, 1f),
    };
    foreach (var (pname, input, simTime) in poses)
    {
        var pose = new AthletePose(body.Pos(AthleteBone.Hips).y);
        // Run the solver for `simTime` so smoothing converges and gait phase advances.
        float dt = 1f / 60f;
        var inp = input;
        float t0 = inp.ActionTime;
        int steps = Math.Max(1, (int)(Math.Max(simTime, 0.6f) / dt));
        for (int i = 0; i < steps; i++)
        {
            float remaining = (steps - 1 - i) * dt;
            inp.ActionTime = Math.Max(0f, t0 - remaining);
            if (input.Action != PlayerAction.None && t0 - remaining < 0) inp.Action = PlayerAction.None; else inp.Action = input.Action;
            pose.Update(inp, dt);
        }
        WriteObj(Path.Combine(outDir, $"{pr.name}_{pname}.obj"), body, pose);
    }
}

static void WriteObj(string path, AthleteBody body, AthletePose pose)
{
    int n = (int)AthleteBone.Count;
    var wr = new Quaternion[n];
    var wp = new Vector3[n];
    for (int b = 0; b < n; b++)
    {
        int par = body.Parents[b];
        var local = pose != null ? Quaternion.Euler(pose.Euler[b]) : Quaternion.identity;
        Vector3 offset = par < 0 ? body.BindPositions[b] : body.BindPositions[b] - body.BindPositions[par];
        if (b == (int)AthleteBone.Hips && pose != null) offset += pose.HipsOffset;
        if (par < 0) { wr[b] = local; wp[b] = offset; }
        else { wr[b] = wr[par] * local; wp[b] = wp[par] + wr[par] * offset; }
    }
    var mb = body.Mesh;
    var sb = new StringBuilder();
    for (int i = 0; i < mb.V.Count; i++)
    {
        var v = mb.V[i]; var w = mb.W[i];
        Vector3 Skin(int bi) => wp[bi] + wr[bi] * (v - body.BindPositions[bi]);
        Vector3 r = Skin(w.boneIndex0) * w.weight0;
        if (w.weight1 > 0) r += Skin(w.boneIndex1) * w.weight1;
        sb.AppendLine($"v {r.x:0.0000} {r.y:0.0000} {r.z:0.0000}");
    }
    string[] mats = { "skin", "shirt", "shorts", "socks", "boots", "hair", "eyes" };
    for (int s = 0; s < mb.Sub.Count; s++)
    {
        sb.AppendLine("usemtl " + mats[s]);
        var l = mb.Sub[s];
        for (int i = 0; i < l.Count; i += 3) sb.AppendLine($"f {l[i] + 1} {l[i + 1] + 1} {l[i + 2] + 1}");
    }
    File.WriteAllText(path, sb.ToString());
}
