using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using NextXI.Core;
using NextXI.Core.Data;
using NextXI.Core.Match;

// Usage: dotnet run -- [matches] [minutesPerHalf] [traceFile]
int matches = args.Length > 0 ? int.Parse(args[0]) : 5;
int minutes = args.Length > 1 ? int.Parse(args[1]) : 5;
string trace = args.Length > 2 ? args[2] : null;
Log.AddSink(new ConsoleSink());

int totalGoals = 0, homeWins = 0, awayWins = 0, draws = 0;
var sw = Stopwatch.StartNew();
for (int i = 0; i < matches; i++)
{
    var rng = new Rng((ulong)(1000 + i));
    var home = TeamSheetBuilder.Build("H", "Home FC", "HOM", PlayerGenerator.GenerateSquad(rng, "H", 72), "4-3-3", new KitColors());
    var away = TeamSheetBuilder.Build("A", "Away United", "AWY", PlayerGenerator.GenerateSquad(rng, "A", 72, "4-4-2"), "4-4-2", new KitColors { Shirt = "#1E3A8A" });
    var settings = new MatchSettings { RealMinutesPerHalf = minutes, Seed = (ulong)(77 + i) };
    var m = new MatchEngine(home, away, settings);
    StreamWriter tw = trace != null && i == 0 ? new StreamWriter(trace) : null;
    m.StartMatch();
    long steps = 0;
    while (!m.IsFinished && steps < 3_000_000)
    {
        if (m.Phase == MatchPhase.HalfTime) m.ContinueFromHalfTime();
        m.Step();
        steps++;
        if (tw != null && steps % 15 == 0)
        {
            tw.Write($"{m.SimTime:0.00},{m.Phase},{m.Ball.Position.X:0.00},{m.Ball.Position.Y:0.00},{m.Ball.Position.Z:0.00},{(m.Owner?.Id ?? -1)}");
            foreach (var p in m.Players) tw.Write($",{p.Id},{p.Team},{p.Pos.X:0.0},{p.Pos.Z:0.0},{p.Action},{p.Intent}");
            tw.WriteLine();
        }
    }
    tw?.Dispose();
    var s0 = m.Teams[0].Stats; var s1 = m.Teams[1].Stats;
    float poss = s0.PossessionSeconds / Math.Max(1, s0.PossessionSeconds + s1.PossessionSeconds);
    Console.WriteLine($"Match {i}: {s0.Goals}-{s1.Goals} | shots {s0.Shots}({s0.ShotsOnTarget})-{s1.Shots}({s1.ShotsOnTarget}) xG {s0.XG:0.0}-{s1.XG:0.0} | poss {poss:P0} | passes {s0.PassesCompleted}/{s0.Passes} - {s1.PassesCompleted}/{s1.Passes} | tackles {s0.TacklesWon}/{s0.Tackles}-{s1.TacklesWon}/{s1.Tackles} | fouls {s0.Fouls}-{s1.Fouls} | corners {s0.Corners}-{s1.Corners} | offs {s0.Offsides}-{s1.Offsides} | saves {s0.Saves}-{s1.Saves} | cards Y{s0.YellowCards}/{s1.YellowCards} R{s0.RedCards}/{s1.RedCards} | subs {s0.Substitutions}-{s1.Substitutions} | steps {steps} | clock {m.Clock.Display}");
    var stam = m.Players.Where(pl => pl.IsActive && !pl.IsGoalkeeper).Select(pl => pl.Stamina).ToList();
    Console.WriteLine($"   stamina avg {stam.Average():0.00} min {stam.Min():0.00} | dist avg {m.Players.Average(pl => pl.Stats.DistanceMeters)/1000:0.0}km | ratings {string.Join(",", m.Players.Select(pl => pl.Stats.Rating.ToString("0.0")))}");
    var counts = m.Events.All.GroupBy(e => e.Type).Select(g => $"{g.Key}:{g.Count()}");
    Console.WriteLine("   events: " + string.Join(" ", counts));
    totalGoals += s0.Goals + s1.Goals;
    if (s0.Goals > s1.Goals) homeWins++; else if (s1.Goals > s0.Goals) awayWins++; else draws++;
}
Console.WriteLine($"Avg goals {totalGoals / (float)matches:0.00}  H/D/A {homeWins}/{draws}/{awayWins}  time {sw.Elapsed.TotalSeconds:0.0}s");

class ConsoleSink : ILogSink
{
    public void Write(LogLevel level, string category, string message)
    {
        if (level >= LogLevel.Warning) Console.WriteLine($"[{level}] {category}: {message}");
    }
}
