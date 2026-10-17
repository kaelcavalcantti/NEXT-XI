using System;
using System.Collections.Generic;
using System.Linq;
using NextXI.Core.Career;
using NextXI.Core.Match;
using NextXI.Core.World;

namespace NextXI.Core.News
{
    /// <summary>
    /// Original in-game sports newspaper ("Diário NEXT") and fan social feed. Every item is
    /// generated from simulation facts (results, scorers, ratings, table, transfers, coaches),
    /// picks among several phrasings and avoids reusing a template recently.
    /// </summary>
    public static class NewsGenerator
    {
        private static string Pick(CareerState s, Rng rng, string group, params string[] variants)
        {
            var fresh = Enumerable.Range(0, variants.Length).Where(i => !s.RecentTemplates.Contains(group + i)).ToList();
            int idx = fresh.Count > 0 ? fresh[rng.Range(0, fresh.Count)] : rng.Range(0, variants.Length);
            s.RecentTemplates.Add(group + idx);
            if (s.RecentTemplates.Count > 40) s.RecentTemplates.RemoveAt(0);
            return variants[idx];
        }

        private static NewsItem Add(CareerState s, NewsCategory cat, string headline, string body, int importance, string club = null, string player = null)
        {
            var n = new NewsItem
            {
                Id = s.NewId("n"), Season = s.World.Season, Day = s.Day, Category = cat, Headline = headline, Body = body,
                Importance = importance, ClubId = club, PlayerId = player
            };
            s.News.Insert(0, n);
            if (s.News.Count > 300) s.News.RemoveAt(s.News.Count - 1);
            return n;
        }

        private static string Name(WorldState w, string playerId) => w.Player(playerId)?.DisplayName ?? "?";
        private static string Club(WorldState w, string clubId) => w.Club(clubId)?.Name ?? "?";

        /// <summary>Headlines after the athlete's club played.</summary>
        public static void AfterUserMatch(CareerState s, Fixture f, MatchReport r, Rng rng)
        {
            var w = s.World;
            string me = s.AthleteId;
            var line = r.Line(me);
            bool home = f.HomeId == s.ClubId;
            int gf = home ? f.HomeGoals : f.AwayGoals, ga = home ? f.AwayGoals : f.HomeGoals;
            string club = Club(w, s.ClubId), opp = Club(w, home ? f.AwayId : f.HomeId);
            string athlete = Name(w, me);
            string score = $"{f.HomeGoals}-{f.AwayGoals}";
            string comp = w.Competition(f.CompetitionId)?.Name ?? "";

            int myGoals = line?.Goals ?? 0;
            var lastGoal = r.Goals.LastOrDefault(g => g.Team == (home ? 0 : 1));
            bool decisive = gf - ga == 1 && lastGoal != null && lastGoal.ScorerId == me && lastGoal.Minute >= 75;

            if (decisive)
                Add(s, NewsCategory.Player, Pick(s, rng, "dec",
                    $"{athlete} decide no fim e {club} vence o {opp}",
                    $"Herói da noite: {athlete} marca aos {lastGoal.Minute}' e garante a vitória",
                    $"Gol salvador de {athlete} dá três pontos ao {club}"),
                    $"Em jogo válido pela {comp}, o {club} venceu por {score}. {athlete} balançou as redes aos {lastGoal.Minute} minutos e foi o nome da partida.", 5, s.ClubId, me);
            else if (myGoals >= 3)
                Add(s, NewsCategory.Record, Pick(s, rng, "hat", $"Hat-trick! {athlete} faz {myGoals} e brilha", $"Noite histórica: {athlete} marca {myGoals} vezes"),
                    $"{athlete} anotou {myGoals} gols no {score} entre {Club(w, f.HomeId)} e {Club(w, f.AwayId)}.", 5, s.ClubId, me);
            else if (myGoals > 0 && gf > ga)
                Add(s, NewsCategory.Result, Pick(s, rng, "win-goal",
                    $"Com gol de {athlete}, {club} vence o {opp}",
                    $"{club} supera o {opp}; {athlete} deixa sua marca",
                    $"{athlete} marca e ajuda o {club} a bater o {opp}"),
                    $"Placar final: {score}. {athlete} terminou com nota {line?.Rating:0.0}.", 3, s.ClubId, me);
            else if (gf > ga)
                Add(s, NewsCategory.Result, Pick(s, rng, "win", $"{club} vence o {opp} por {score}", $"Vitória do {club} sobre o {opp}", $"{club} confirma favoritismo diante do {opp}"),
                    BuildScorers(w, r, f), 2, s.ClubId);
            else if (gf == ga)
                Add(s, NewsCategory.Result, Pick(s, rng, "draw", $"{club} e {opp} empatam em {score}", $"Igualdade no placar entre {club} e {opp}", $"Tudo igual: {Club(w, f.HomeId)} {score} {Club(w, f.AwayId)}"),
                    BuildScorers(w, r, f), 2, s.ClubId);
            else
                Add(s, NewsCategory.Result, Pick(s, rng, "loss", $"{club} perde para o {opp}", $"Tropeço: {opp} vence o {club} por {score}", $"Noite difícil para o {club} contra o {opp}"),
                    BuildScorers(w, r, f), 2, s.ClubId);

            // Social reactions tied to the athlete's performance.
            if (line != null)
            {
                float rt = line.Rating;
                string[] fans = { "@arquibancada_fiel", "@torcedor_raiz", "@bolanarede", "@fanaticos_fc", "@visao_tatica", "@setor_norte" };
                int sentiment = rt >= 7.3f ? 1 : rt <= 5.8f ? -1 : 0;
                string text = sentiment > 0
                    ? Pick(s, rng, "soc+", $"{athlete} jogou demais hoje! Nota {rt:0.0} é pouco!", $"Que partida do {athlete}. Esse moleque é diferente.", $"Alguém segura o {athlete}? Craque!")
                    : sentiment < 0
                        ? Pick(s, rng, "soc-", $"{athlete} sumiu do jogo hoje... precisa melhorar.", $"Dia ruim do {athlete}. Acontece, mas preocupa.", $"Treinador precisa conversar com o {athlete}.")
                        : Pick(s, rng, "soc0", $"Jogo correto do {athlete}. Dá pra mais.", $"{athlete} cumpriu o papel hoje.", $"Partida ok do {athlete}, sem brilho.");
                s.Social.Insert(0, new SocialPost { Season = w.Season, Day = s.Day, Author = "Torcedor", Handle = fans[rng.Range(0, fans.Length)], Text = text, Likes = rng.Range(20, 2500) * (sentiment != 0 ? 3 : 1), Sentiment = sentiment });
                if (s.Social.Count > 200) s.Social.RemoveAt(s.Social.Count - 1);
            }
        }

        private static string BuildScorers(WorldState w, MatchReport r, Fixture f)
        {
            if (r.Goals.Count == 0) return "Sem gols na partida.";
            var parts = r.Goals.Select(g => $"{Name(w, g.ScorerId)} {g.Minute}'{(g.OwnGoal ? " (contra)" : "")}");
            return $"{Club(w, f.HomeId)} {f.HomeGoals}-{f.AwayGoals} {Club(w, f.AwayId)}. Gols: {string.Join(", ", parts)}.";
        }

        /// <summary>Round round-up for the user's league: leader, big results.</summary>
        public static void RoundUp(CareerState s, Competition comp, IEnumerable<Fixture> played, Rng rng)
        {
            var w = s.World;
            var list = played.ToList();
            if (list.Count == 0) return;
            var big = list.OrderByDescending(f => Math.Abs(f.HomeGoals - f.AwayGoals)).First();
            if (Math.Abs(big.HomeGoals - big.AwayGoals) >= 3)
                Add(s, NewsCategory.Result, Pick(s, rng, "rout", $"Goleada: {Club(w, big.WinnerId)} atropela", $"{Club(w, big.WinnerId)} aplica {Math.Max(big.HomeGoals, big.AwayGoals)} gols na rodada"),
                    $"{Club(w, big.HomeId)} {big.HomeGoals}-{big.AwayGoals} {Club(w, big.AwayId)} pela {comp.Name}.", 2, big.WinnerId);
            var table = comp.Table();
            if (table.Count > 0 && table[0].Played > 2)
                Add(s, NewsCategory.Club, Pick(s, rng, "lead", $"{Club(w, table[0].ClubId)} lidera a {comp.Name}", $"Na ponta: {Club(w, table[0].ClubId)} soma {table[0].Points} pontos"),
                    $"Após {table[0].Played} rodadas, {Club(w, table[0].ClubId)} tem {table[0].Points} pontos. {(table.Count > 1 ? $"{Club(w, table[1].ClubId)} vem em seguida com {table[1].Points}." : "")}", 1, table[0].ClubId);
        }

        public static void Transfer(CareerState s, TransferRecord t, Rng rng, bool rumour = false)
        {
            var w = s.World;
            string p = Name(w, t.PlayerId), to = Club(w, t.ToClubId), from = Club(w, t.FromClubId);
            bool me = t.PlayerId == s.AthleteId;
            if (rumour)
            {
                Add(s, NewsCategory.Rumour, Pick(s, rng, "rum", $"Rumor: {to} monitora {p}", $"{p} na mira do {to}, dizem bastidores", $"Mercado: {to} avalia proposta por {p}"),
                    $"Segundo apuração do Diário NEXT, o {to} acompanha {p}, hoje no {from}.", me ? 4 : 1, t.ToClubId, t.PlayerId);
                return;
            }
            string kind = t.Loan ? "por empréstimo" : t.Fee > 0 ? $"por {FormatMoney(t.Fee)}" : "sem custos";
            Add(s, NewsCategory.Transfer, Pick(s, rng, "tr", $"Oficial: {p} é do {to}", $"{to} anuncia {p}", $"{p} troca o {from} pelo {to}"),
                $"Negócio fechado {kind}. {p} deixa o {from} e reforça o {to}.", me ? 5 : 2, t.ToClubId, t.PlayerId);
        }

        public static void CoachChange(CareerState s, string clubId, string oldCoach, string newCoach, Rng rng)
        {
            var w = s.World;
            Add(s, NewsCategory.Coach, Pick(s, rng, "coach", $"{Club(w, clubId)} demite {oldCoach}", $"Fim da linha: {oldCoach} deixa o comando do {Club(w, clubId)}"),
                $"Após resultados abaixo do esperado, a diretoria do {Club(w, clubId)} anunciou {newCoach} como novo treinador.", clubId == s.ClubId ? 5 : 2, clubId);
        }

        public static void Injury(CareerState s, string playerId, string injury, int days, Rng rng)
        {
            var w = s.World;
            Add(s, NewsCategory.Injury, Pick(s, rng, "inj", $"{Name(w, playerId)} sofre {injury}", $"Desfalque: {Name(w, playerId)} fica fora por {days} dias"),
                $"O departamento médico estima recuperação em cerca de {days} dias.", playerId == s.AthleteId ? 4 : 1, w.Player(playerId)?.ClubId, playerId);
        }

        public static void LostPlace(CareerState s, int benchStreak, Rng rng)
        {
            var w = s.World;
            Add(s, NewsCategory.Player, Pick(s, rng, "bench", $"{Name(w, s.AthleteId)} perde espaço no {Club(w, s.ClubId)}", $"Banco de novo: {Name(w, s.AthleteId)} busca reação"),
                $"{Name(w, s.AthleteId)} não começou jogando nas últimas {benchStreak} partidas.", 3, s.ClubId, s.AthleteId);
        }

        public static void Milestone(CareerState s, string text, Rng rng) =>
            Add(s, NewsCategory.Record, text, $"Marca registrada na temporada {s.World.Season}.", 4, s.ClubId, s.AthleteId);

        public static void NationalTeam(CareerState s, bool called, Rng rng)
        {
            var w = s.World;
            string nat = w.Player(s.AthleteId)?.Nationality ?? "seleção";
            if (called)
                Add(s, NewsCategory.NationalTeam, Pick(s, rng, "nt", $"{Name(w, s.AthleteId)} é convocado pela seleção ({nat})", $"Convocação: {Name(w, s.AthleteId)} está na lista"),
                    "A comissão técnica destacou a boa fase e a regularidade do atleta.", 5, s.ClubId, s.AthleteId);
        }

        public static void AwardNews(CareerState s, string award, string playerId, Rng rng)
        {
            var w = s.World;
            Add(s, NewsCategory.Award, $"{Name(w, playerId)} vence: {award}", $"Prêmio da temporada {w.Season}.", playerId == s.AthleteId ? 5 : 3, w.Player(playerId)?.ClubId, playerId);
        }

        public static void Champion(CareerState s, Competition comp, Rng rng)
        {
            var w = s.World;
            Add(s, NewsCategory.Club, Pick(s, rng, "champ", $"{Club(w, comp.ChampionId)} é campeão da {comp.Name}!", $"Título: {Club(w, comp.ChampionId)} conquista a {comp.Name}"),
                $"Temporada {w.Season} encerrada com festa do {Club(w, comp.ChampionId)}.", comp.ChampionId == s.ClubId ? 5 : 3, comp.ChampionId);
        }

        public static string FormatMoney(long thousands) =>
            thousands >= 1000 ? $"NX$ {thousands / 1000.0:0.#} mi" : $"NX$ {thousands} mil";
    }
}
