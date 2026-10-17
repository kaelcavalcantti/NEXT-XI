using System;
using System.IO;
using NextXI.Runtime.AudioSynth;

public static class AudioCheck
{
    public static void Run(string dir)
    {
        Directory.CreateDirectory(dir);
        var sw = System.Diagnostics.Stopwatch.StartNew();
        void W(string name, float[] x)
        {
            var (peak, rms, finite) = Synth.Analyze(x);
            Console.WriteLine($"{name,-14} {x.Length / (float)Synth.Rate,5:0.00}s peak {peak:0.00} rms {rms:0.000} finite {finite}");
            using var f = new BinaryWriter(File.Create(Path.Combine(dir, name + ".wav")));
            int n = x.Length;
            f.Write(System.Text.Encoding.ASCII.GetBytes("RIFF")); f.Write(36 + n * 2); f.Write(System.Text.Encoding.ASCII.GetBytes("WAVEfmt ")); f.Write(16); f.Write((short)1); f.Write((short)1);
            f.Write(Synth.Rate); f.Write(Synth.Rate * 2); f.Write((short)2); f.Write((short)16); f.Write(System.Text.Encoding.ASCII.GetBytes("data")); f.Write(n * 2);
            foreach (var v in x) f.Write((short)(Math.Clamp(v, -1f, 1f) * 32767));
        }
        W("crowd_bed", Synth.CrowdBed(8f, 3, 0.6f));
        W("goal_roar", Synth.GoalRoar(5));
        W("near_miss", Synth.NearMiss(6));
        W("boo", Synth.Boo(7));
        W("applause", Synth.Applause(3f, 8));
        W("chant", Synth.Chant(8f, 9));
        W("kick_soft", Synth.Kick(0.2f, 1));
        W("kick_hard", Synth.Kick(1f, 2));
        W("whistle", Synth.Whistle(0.5f, 3));
        W("whistle3", Synth.WhistleTriple(4));
        W("net", Synth.Net(5));
        W("post", Synth.Post(6));
        W("tackle", Synth.Tackle(7));
        W("ui_click", Synth.UiClick(1f));
        W("ui_confirm", Synth.UiConfirm());
        W("menu_music", Synth.MenuMusic(1));
        Console.WriteLine($"generated in {sw.ElapsedMilliseconds} ms");
    }
}
