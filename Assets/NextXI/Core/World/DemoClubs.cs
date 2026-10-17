using System;
using System.Collections.Generic;
using NextXI.Core.Data;
using NextXI.Core.Match;

namespace NextXI.Core.World
{
    /// <summary>
    /// Clearly-labelled FICTIONAL demo clubs. They exist so the game is playable without any
    /// licensed data; names, crests and kits are original inventions of this project.
    /// </summary>
    public static class DemoClubs
    {
        public sealed class Def
        {
            public string Id, Name, Short, City, Country, Stadium, Formation;
            public int Strength;
            public string Shirt, Shorts, Socks, Number, Keeper;
        }

        public static readonly IReadOnlyList<Def> Brazil = new List<Def>
        {
            new Def { Id = "bra-atl-portoalto", Name = "Atlético Porto Alto", Short = "APA", City = "Porto Alto", Country = "Brasil (fictício)", Stadium = "Arena das Marés", Formation = "4-3-3", Strength = 76, Shirt = "#C8102E", Shorts = "#FFFFFF", Socks = "#C8102E", Number = "#FFFFFF", Keeper = "#16A34A" },
            new Def { Id = "bra-uniao-serrana", Name = "União Serrana", Short = "UNS", City = "Vale da Serra", Country = "Brasil (fictício)", Stadium = "Estádio do Mirante", Formation = "4-2-3-1", Strength = 74, Shirt = "#1E3A8A", Shorts = "#1E3A8A", Socks = "#FFFFFF", Number = "#FACC15", Keeper = "#F97316" },
            new Def { Id = "bra-cruzeiro-litoral", Name = "Esporte Clube Litoral", Short = "ECL", City = "Baía Clara", Country = "Brasil (fictício)", Stadium = "Arena Litoral", Formation = "4-4-2", Strength = 72, Shirt = "#FACC15", Shorts = "#14532D", Socks = "#FACC15", Number = "#14532D", Keeper = "#7C3AED" },
            new Def { Id = "bra-operario-norte", Name = "Operário do Norte", Short = "OPN", City = "Ferrovia", Country = "Brasil (fictício)", Stadium = "Estádio da Usina", Formation = "4-4-2", Strength = 69, Shirt = "#111827", Shorts = "#111827", Socks = "#111827", Number = "#F5F5F5", Keeper = "#22D3EE" },
            new Def { Id = "bra-real-cerrado", Name = "Real Cerrado", Short = "RCE", City = "Planalto Verde", Country = "Brasil (fictício)", Stadium = "Arena do Ipê", Formation = "4-3-3", Strength = 71, Shirt = "#16A34A", Shorts = "#FFFFFF", Socks = "#16A34A", Number = "#FFFFFF", Keeper = "#111827" },
            new Def { Id = "bra-sport-capital", Name = "Sport Capital", Short = "SPC", City = "Capital", Country = "Brasil (fictício)", Stadium = "Monumental da Capital", Formation = "3-5-2", Strength = 73, Shirt = "#7C3AED", Shorts = "#F5F5F5", Socks = "#7C3AED", Number = "#F5F5F5", Keeper = "#FACC15" },
            new Def { Id = "bra-nautico-sul", Name = "Náutico do Sul", Short = "NSU", City = "Lagoa Fria", Country = "Brasil (fictício)", Stadium = "Estádio dos Ventos", Formation = "4-2-3-1", Strength = 68, Shirt = "#0EA5E9", Shorts = "#0C4A6E", Socks = "#0EA5E9", Number = "#FFFFFF", Keeper = "#DC2626" },
            new Def { Id = "bra-ferroviario", Name = "Ferroviário Paulista", Short = "FEP", City = "Nova Estação", Country = "Brasil (fictício)", Stadium = "Estádio dos Trilhos", Formation = "4-3-3", Strength = 66, Shirt = "#B91C1C", Shorts = "#111827", Socks = "#111827", Number = "#FFFFFF", Keeper = "#0EA5E9" },
            new Def { Id = "bra-gremio-vale", Name = "Grêmio do Vale", Short = "GRV", City = "Vale Dourado", Country = "Brasil (fictício)", Stadium = "Arena Vale", Formation = "4-4-2", Strength = 67, Shirt = "#F97316", Shorts = "#111827", Socks = "#F97316", Number = "#111827", Keeper = "#22C55E" },
            new Def { Id = "bra-america-delta", Name = "América do Delta", Short = "AMD", City = "Delta", Country = "Brasil (fictício)", Stadium = "Estádio da Foz", Formation = "4-3-3", Strength = 65, Shirt = "#DB2777", Shorts = "#FFFFFF", Socks = "#DB2777", Number = "#FFFFFF", Keeper = "#1E293B" },
        };

        public static readonly IReadOnlyList<Def> Europe = new List<Def>
        {
            new Def { Id = "eur-kingsport", Name = "Kingsport Athletic", Short = "KAT", City = "Kingsport", Country = "Inglaterra (fictício)", Stadium = "Harbour Park", Formation = "4-3-3", Strength = 82, Shirt = "#1D4ED8", Shorts = "#FFFFFF", Socks = "#1D4ED8", Number = "#FFFFFF", Keeper = "#F59E0B" },
            new Def { Id = "eur-real-almira", Name = "Real Almira CF", Short = "RAL", City = "Almira", Country = "Espanha (fictício)", Stadium = "Estadio del Faro", Formation = "4-3-3", Strength = 81, Shirt = "#F5F5F5", Shorts = "#F5F5F5", Socks = "#1E293B", Number = "#B91C1C", Keeper = "#16A34A" },
            new Def { Id = "eur-fc-rheinau", Name = "FC Rheinau 1898", Short = "FCR", City = "Rheinau", Country = "Alemanha (fictício)", Stadium = "Flussarena", Formation = "4-2-3-1", Strength = 80, Shirt = "#DC2626", Shorts = "#FFFFFF", Socks = "#DC2626", Number = "#FFFFFF", Keeper = "#111827" },
            new Def { Id = "eur-sporting-alvor", Name = "Sporting Alvor", Short = "SAL", City = "Alvor", Country = "Portugal (fictício)", Stadium = "Estádio do Farol", Formation = "4-4-2", Strength = 77, Shirt = "#15803D", Shorts = "#FFFFFF", Socks = "#15803D", Number = "#FFFFFF", Keeper = "#F97316" },
            new Def { Id = "eur-ac-veneta", Name = "AC Veneta", Short = "ACV", City = "Veneta", Country = "Itália (fictício)", Stadium = "Stadio della Laguna", Formation = "3-5-2", Strength = 79, Shirt = "#0F172A", Shorts = "#0F172A", Socks = "#0F172A", Number = "#22D3EE", Keeper = "#FACC15" },
            new Def { Id = "eur-olympique-mistral", Name = "Olympique Mistral", Short = "OMI", City = "Mistral", Country = "França (fictício)", Stadium = "Stade du Mistral", Formation = "4-3-3", Strength = 78, Shirt = "#38BDF8", Shorts = "#F5F5F5", Socks = "#38BDF8", Number = "#0F172A", Keeper = "#DC2626" },
            new Def { Id = "eur-dynamo-oostburg", Name = "Dynamo Oostburg", Short = "DOO", City = "Oostburg", Country = "Holanda (fictício)", Stadium = "Polderstadion", Formation = "4-3-3", Strength = 75, Shirt = "#F97316", Shorts = "#111827", Socks = "#F97316", Number = "#FFFFFF", Keeper = "#7C3AED" },
            new Def { Id = "eur-celtic-arran", Name = "Arran Rovers", Short = "ARR", City = "Arran", Country = "Escócia (fictício)", Stadium = "Glenview", Formation = "4-4-2", Strength = 73, Shirt = "#16A34A", Shorts = "#F5F5F5", Socks = "#16A34A", Number = "#F5F5F5", Keeper = "#F59E0B" },
        };

        public static IEnumerable<Def> All
        {
            get { foreach (var d in Brazil) yield return d; foreach (var d in Europe) yield return d; }
        }

        public static Def Find(string id)
        {
            foreach (var d in All) if (d.Id == id) return d;
            return null;
        }

        public static KitColors Kit(Def d) => new KitColors { Shirt = d.Shirt, Shorts = d.Shorts, Socks = d.Socks, Number = d.Number, Keeper = d.Keeper };

        /// <summary>Alternate kit used when shirt colours clash.</summary>
        public static KitColors AwayKit(Def d) => new KitColors { Shirt = "#F5F5F5", Shorts = d.Shirt, Socks = "#F5F5F5", Number = d.Shirt, Keeper = d.Keeper };

        /// <summary>Generates a deterministic fictional squad for quick matches.</summary>
        public static List<PlayerProfile> Squad(Def d, int seasonYear = 2026)
        {
            ulong seed = 1469598103934665603UL;
            foreach (char c in d.Id) seed = (seed ^ c) * 1099511628211UL;
            return PlayerGenerator.GenerateSquad(new Rng(seed), d.Id, d.Strength, d.Formation, seasonYear);
        }

        /// <summary>True when two kits are too similar to be told apart (simple RGB distance).</summary>
        public static bool Clash(string a, string b)
        {
            int Hex(string s, int i) => Convert.ToInt32(s.Substring(1 + i * 2, 2), 16);
            if (a == null || b == null || a.Length < 7 || b.Length < 7) return false;
            int dr = Hex(a, 0) - Hex(b, 0), dg = Hex(a, 1) - Hex(b, 1), db = Hex(a, 2) - Hex(b, 2);
            return dr * dr + dg * dg + db * db < 110 * 110;
        }
    }
}
