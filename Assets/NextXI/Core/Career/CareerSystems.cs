using System;
using System.Collections.Generic;
using System.Linq;
using NextXI.Core.Data;
using NextXI.Core.Match;
using NextXI.Core.News;
using NextXI.Core.World;

namespace NextXI.Core.Career
{
    public static partial class CareerManager
    {
        // ================================================================== progression

        public static int XpForLevel(int level) => (int)(160 * Math.Pow(level, 1.35));

        public static void GainXp(CareerState s, int xp)
        {
            s.Xp += xp;
            s.XpThisWeek += xp;
            while (s.Level < 30 && s.Xp >= XpForLevel(s.Level))
            {
                s.Xp -= XpForLevel(s.Level);
                s.Level++;
                OnLevelUp(s);
            }
        }

        private static void OnLevelUp(CareerState s)
        {
            var ladder = s.Archetype.PerkLadder;
            if (s.Level % 2 != 0) return;
            int tier = s.Level / 2 - 1;
            if (tier < 0 || tier >= ladder.Length) return;
            var options = ladder[tier].Where(t => !s.UnlockedPerks.Contains(t)).ToList();
            if (options.Count == 1) UnlockPerk(s, options[0]);
            else if (options.Count > 1) s.PendingPerkChoice = options;
            Message(s, "Comissão técnica", $"Nível {s.Level} alcançado", options.Count > 1 ? "Escolha uma especialização na tela do atleta." : $"Nova habilidade: {PerkName(options.FirstOrDefault())}.");
        }

        public static void UnlockPerk(CareerState s, string trait)
        {
            if (trait == null || s.UnlockedPerks.Contains(trait)) return;
            s.UnlockedPerks.Add(trait);
            var a = s.Athlete;
            if (!a.Traits.Contains(trait)) a.Traits.Add(trait);
            s.PendingPerkChoice.Clear();
        }

        public static string PerkName(string t)
        {
            switch (t)
            {
                case Traits.FinesseShot: return "Chute colocado (curva e precisão)";
                case Traits.PowerShot: return "Chute potente (velocidade de longe)";
                case Traits.CloseControl: return "Condução curta (toques mais rápidos)";
                case Traits.FirstTimePass: return "Primeira (menos erro de primeira)";
                case Traits.ThroughBallVision: return "Visão de enfiada (lançamentos precisos)";
                case Traits.Explosive: return "Explosão (arrancada após drible)";
                case Traits.AerialThreat: return "Domínio aéreo (alcance no cabeceio)";
                case Traits.CleanTackler: return "Desarme limpo (menos faltas)";
                case Traits.Interceptor: return "Interceptador (alcance de antecipação)";
                case Traits.Shielder: return "Proteção (difícil de desarmar)";
                case Traits.ShotStopper: return "Reflexo (alcance no mergulho)";
                case Traits.Sweeper: return "Goleiro-líbero (sai melhor do gol)";
                case Traits.Crosser: return "Cruzador (cruzamentos precisos)";
                case Traits.Engine: return "Pulmão (cansa menos)";
                default: return t ?? "";
            }
        }

        /// <summary>Converts accumulated XP into attribute growth (archetype weights, age, potential).</summary>
        private static void WeeklyProgression(CareerState s)
        {
            var a = s.Athlete;
            if (a == null) return;
            int age = a.AgeOn(s.Date);
            float ageF = age <= 21 ? 1.3f : age <= 25 ? 1.1f : age <= 29 ? 0.8f : age <= 32 ? 0.4f : 0.15f;
            float gap = MathUtil.Clamp01((a.Potential - a.Overall + 3) / 10f);
            float points = s.XpThisWeek / 70f * ageF * (0.25f + 0.75f * gap);
            s.XpThisWeek = 0;
            var growth = s.Archetype.Growth;
            float wsum = growth.Values.Sum();
            foreach (var kv in growth)
            {
                float add = points * kv.Value / wsum;
                s.AttributeProgress.TryGetValue(kv.Key, out var acc);
                acc += add;
                while (acc >= 1f) { a.Attributes.Set(kv.Key, a.Attributes.Get(kv.Key) + 1); acc -= 1f; }
                s.AttributeProgress[kv.Key] = acc;
            }
            // Physical decline after 30.
            if (age >= 31)
            {
                float decline = (age - 30) * 0.06f;
                foreach (var key in new[] { "Pace", "Acceleration", "Stamina", "Agility" })
                {
                    s.AttributeProgress.TryGetValue(key, out var acc);
                    acc -= decline;
                    while (acc <= -1f) { a.Attributes.Set(key, a.Attributes.Get(key) - 1); acc += 1f; }
                    s.AttributeProgress[key] = acc;
                }
            }
        }

        // ================================================================== training

        public sealed class Drill
        {
            public string Id, Name, Description;
            public string[] Attributes;
            public bool KeeperOnly, OutfieldOnly;
        }

        public static readonly List<Drill> Drills = new List<Drill>
        {
            new Drill { Id = "finishing", Name = "Finalização", Description = "Chutes contra o goleiro de diferentes posições.", Attributes = new[] { "Finishing", "ShotPower", "Composure", "LongShots" }, OutfieldOnly = true },
            new Drill { Id = "passing", Name = "Passe", Description = "Passes rasteiros e em profundidade para companheiros em movimento.", Attributes = new[] { "ShortPassing", "LongPassing", "Vision" }, OutfieldOnly = true },
            new Drill { Id = "dribbling", Name = "Drible", Description = "Conduza entre defensores sem perder a bola.", Attributes = new[] { "Dribbling", "Agility", "Balance" }, OutfieldOnly = true },
            new Drill { Id = "control", Name = "Controle de bola", Description = "Domine bolas altas e rasteiras e mantenha a posse.", Attributes = new[] { "BallControl", "Reactions", "Composure" }, OutfieldOnly = true },
            new Drill { Id = "speed", Name = "Velocidade", Description = "Arrancadas de 30 m contra o relógio.", Attributes = new[] { "Pace", "Acceleration", "Stamina" } },
            new Drill { Id = "agility", Name = "Agilidade", Description = "Mudanças de direção entre marcadores.", Attributes = new[] { "Agility", "Balance", "Acceleration" } },
            new Drill { Id = "positioning", Name = "Posicionamento", Description = "Ataque os espaços certos para receber.", Attributes = new[] { "Positioning", "Vision", "Reactions" }, OutfieldOnly = true },
            new Drill { Id = "defending", Name = "Defesa", Description = "Desarme o atacante sem cometer falta.", Attributes = new[] { "StandingTackle", "SlidingTackle", "Marking", "Interceptions" }, OutfieldOnly = true },
            new Drill { Id = "heading", Name = "Cabeceio", Description = "Cruzamentos para cabeceio no gol.", Attributes = new[] { "Heading", "Jumping", "Strength" }, OutfieldOnly = true },
            new Drill { Id = "goalkeeping", Name = "Goleiro", Description = "Sequência de chutes para defender.", Attributes = new[] { "GkReflexes", "GkDiving", "GkHandling", "GkPositioning" }, KeeperOnly = true },
        };

        public const int MaxTrainingPerWeek = 2;

        public static bool CanTrain(CareerState s) => s.TrainingSessionsThisWeek < MaxTrainingPerWeek && !s.Retired && s.AthleteState.InjuryDays <= 0;

        /// <summary>Applies a training result (score 0..1 from the playable drill).</summary>
        public static TrainingResult ApplyTraining(CareerState s, string drillId, float score)
        {
            score = MathUtil.Clamp01(score);
            var d = Drills.FirstOrDefault(x => x.Id == drillId) ?? Drills[0];
            int xp = (int)(25 + score * 45);
            GainXp(s, xp);
            // Targeted growth for the drill attributes (in addition to archetype growth).
            var a = s.Athlete;
            int age = a.AgeOn(s.Date);
            float ageF = age <= 23 ? 1.2f : age <= 29 ? 0.9f : 0.5f;
            float gap = MathUtil.Clamp01((a.Potential - a.Overall + 3) / 10f);
            foreach (var attr in d.Attributes)
            {
                s.AttributeProgress.TryGetValue(attr, out var acc);
                acc += score * 0.35f * ageF * (0.3f + 0.7f * gap);
                while (acc >= 1f) { a.Attributes.Set(attr, a.Attributes.Get(attr) + 1); acc -= 1f; }
                s.AttributeProgress[attr] = acc;
            }
            s.Trust = MathUtil.Clamp(s.Trust + (score - 0.55f) * 4f, 0, 100);
            s.TrainingSessionsThisWeek++;
            var r = new TrainingResult { Season = s.World.Season, Day = s.Day, DrillId = d.Id, Score = score, Xp = xp };
            s.Training.Insert(0, r);
            if (s.Training.Count > 200) s.Training.RemoveAt(s.Training.Count - 1);
            // Academy players are promoted after convincing training and enough time.
            if (s.InYouthTeam && s.Training.Count(t => t.Season == s.World.Season && t.Score >= 0.6f) >= 4 && s.Athlete.Overall >= 60)
            {
                s.InYouthTeam = false;
                s.AthleteState.Contract.Role = SquadRole.Rotation;
                Message(s, s.World.Coach(s.Club.CoachId)?.Name ?? "Treinador", "Promovido ao profissional!", "Seu desempenho nos treinos convenceu a comissão. A partir de agora você treina e joga com o elenco principal.");
                NewsGenerator.Milestone(s, $"{s.Athlete.DisplayName} é promovido ao time principal do {s.Club.Name}", RngFor(s, 3));
            }
            return r;
        }

        // ================================================================== coach conversations

        public enum CoachRequest { StartingPlace, PositionChange, Loan, Transfer, Feedback }

        /// <summary>Handles a conversation with the coach. Returns the coach's answer.</summary>
        public static string TalkToCoach(CareerState s, CoachRequest req, Position? newPosition = null)
        {
            var rng = RngFor(s, 600 + (int)req);
            var coach = s.World.Coach(s.Club.CoachId);
            string name = coach?.Name ?? "Treinador";
            if (s.Day - s.LastCoachTalkDay < 7 && req != CoachRequest.Feedback)
                return $"{name}: Já conversamos esta semana. Mostre em campo.";
            s.LastCoachTalkDay = s.Day;
            float form = s.AthleteState.Form;
            switch (req)
            {
                case CoachRequest.StartingPlace:
                    if (s.Trust >= 60 || form >= 7.0f)
                    {
                        s.StartRequestPending = true;
                        s.Trust = MathUtil.Clamp(s.Trust + 1, 0, 100);
                        return $"{name}: Você tem merecido. Vou te dar oportunidades.";
                    }
                    s.Trust = MathUtil.Clamp(s.Trust - 3, 0, 100);
                    return $"{name}: Titularidade se conquista. Hoje outros estão à sua frente.";
                case CoachRequest.PositionChange:
                    if (newPosition == null) return $"{name}: Para qual posição?";
                    float fit = TeamSheetBuilder.Fit(s.Athlete, newPosition.Value);
                    if (fit >= s.Athlete.Overall - 4 || s.Trust >= 70)
                    {
                        s.PreferredPosition = newPosition.Value;
                        if (!s.Athlete.SecondaryPositions.Contains(newPosition.Value)) s.Athlete.SecondaryPositions.Add(newPosition.Value);
                        return $"{name}: Vamos testar você como {newPosition}.";
                    }
                    s.Trust = MathUtil.Clamp(s.Trust - 2, 0, 100);
                    return $"{name}: Não vejo você rendendo como {newPosition} agora.";
                case CoachRequest.Loan:
                {
                    int age = s.Athlete.AgeOn(s.Date);
                    if (age <= 23 && MinutesShare(s) < 0.45f)
                    {
                        GenerateLoanOffers(s, rng, 2);
                        return $"{name}: Um empréstimo pode te dar minutos. Seu agente vai receber propostas.";
                    }
                    return $"{name}: Prefiro você aqui. Você faz parte dos planos.";
                }
                case CoachRequest.Transfer:
                    s.Trust = MathUtil.Clamp(s.Trust - 8, 0, 100);
                    s.AthleteState.Morale = MathUtil.Clamp01(s.AthleteState.Morale - 0.05f);
                    GenerateTransferOffers(s, rng, force: true);
                    return $"{name}: Fico decepcionado, mas o clube vai ouvir propostas.";
                default:
                {
                    float avg = s.MatchLog.Take(5).Where(m => m.Minutes > 0).Select(m => m.Rating).DefaultIfEmpty(0).Average();
                    string trustTxt = s.Trust >= 70 ? "confio muito em você" : s.Trust >= 50 ? "você está no caminho certo" : s.Trust >= 35 ? "precisa mostrar mais" : "você está longe da equipe titular";
                    return $"{name}: Sua média recente é {avg:0.0}. Hoje {trustTxt}. Foque em {WeakestArea(s)}.";
                }
            }
        }

        private static string WeakestArea(CareerState s)
        {
            var growth = s.Archetype.Growth.Keys.OrderBy(k => s.Athlete.Attributes.Get(k)).First();
            return AttributeLabel(growth).ToLowerInvariant();
        }

        public static string AttributeLabel(string key)
        {
            switch (key)
            {
                case "Pace": return "Velocidade"; case "Acceleration": return "Aceleração"; case "Agility": return "Agilidade"; case "Balance": return "Equilíbrio";
                case "Stamina": return "Fôlego"; case "Strength": return "Força"; case "Jumping": return "Impulsão"; case "BallControl": return "Controle";
                case "Dribbling": return "Drible"; case "ShortPassing": return "Passe curto"; case "LongPassing": return "Passe longo"; case "Crossing": return "Cruzamento";
                case "Finishing": return "Finalização"; case "ShotPower": return "Força do chute"; case "LongShots": return "Chute de longe"; case "Heading": return "Cabeceio";
                case "Curve": return "Curva"; case "Vision": return "Visão"; case "Positioning": return "Posicionamento"; case "Composure": return "Frieza";
                case "Reactions": return "Reação"; case "Aggression": return "Agressividade"; case "Marking": return "Marcação"; case "StandingTackle": return "Desarme";
                case "SlidingTackle": return "Carrinho"; case "Interceptions": return "Interceptação"; case "GkDiving": return "Mergulho"; case "GkHandling": return "Pegada";
                case "GkKicking": return "Reposição"; case "GkPositioning": return "Colocação"; case "GkReflexes": return "Reflexos";
                default: return key;
            }
        }

        public static float MinutesShare(CareerState s)
        {
            var recent = s.MatchLog.Where(m => m.Season == s.World.Season).Take(8).ToList();
            if (recent.Count == 0) return 1f;
            return recent.Sum(m => m.Minutes) / (90f * recent.Count);
        }

        // ================================================================== objectives

        private static void NewObjectives(CareerState s, Rng rng, bool seasonStart)
        {
            s.Objectives.RemoveAll(o => o.SeasonLong && seasonStart);
            var g = PositionInfo.Group(s.Athlete.Position);
            var role = s.AthleteState.Contract.Role;
            int seasonEnd = 140;
            if (seasonStart)
            {
                int apps = role == SquadRole.KeyPlayer ? 20 : role == SquadRole.Starter ? 16 : role == SquadRole.Rotation ? 10 : 4;
                s.Objectives.Add(new Objective { Id = s.NewId("o"), Text = $"Disputar {apps} partidas na temporada", Metric = "apps", Target = apps, Deadline = seasonEnd, SeasonLong = true, RewardTrust = 6, RewardXp = 120 });
                if (g == PositionGroup.Forward)
                    s.Objectives.Add(new Objective { Id = s.NewId("o"), Text = $"Marcar {(role >= SquadRole.Starter ? 10 : 5)} gols", Metric = "goals", Target = role >= SquadRole.Starter ? 10 : 5, Deadline = seasonEnd, SeasonLong = true, RewardTrust = 8, RewardXp = 150 });
                else if (g == PositionGroup.Midfielder)
                    s.Objectives.Add(new Objective { Id = s.NewId("o"), Text = "Dar 5 assistências", Metric = "assists", Target = 5, Deadline = seasonEnd, SeasonLong = true, RewardTrust = 8, RewardXp = 150 });
                else if (g == PositionGroup.Goalkeeper)
                    s.Objectives.Add(new Objective { Id = s.NewId("o"), Text = "Não sofrer gols em 5 jogos", Metric = "cleansheets", Target = 5, Deadline = seasonEnd, SeasonLong = true, RewardTrust = 8, RewardXp = 150 });
                else
                    s.Objectives.Add(new Objective { Id = s.NewId("o"), Text = "Nota média acima de 6.8", Metric = "rating", Target = 6.8f, Deadline = seasonEnd, SeasonLong = true, RewardTrust = 8, RewardXp = 150 });
            }
            // Short objective for the next few weeks.
            if (!s.Objectives.Any(o => !o.SeasonLong && !o.Done && !o.Failed))
            {
                if (g == PositionGroup.Forward || rng.Chance(0.4f))
                    s.Objectives.Add(new Objective { Id = s.NewId("o"), Text = "Participar de um gol (gol ou assistência) nos próximos 21 dias", Metric = "contrib", Target = 1, Deadline = s.Day + 21, RewardTrust = 4, RewardXp = 60 });
                else
                    s.Objectives.Add(new Objective { Id = s.NewId("o"), Text = "Ter nota 7.0+ em uma partida nos próximos 21 dias", Metric = "bestrating", Target = 7f, Deadline = s.Day + 21, RewardTrust = 4, RewardXp = 60 });
            }
        }

        private static void UpdateObjectives(CareerState s)
        {
            var season = s.MatchLog.Where(m => m.Season == s.World.Season).ToList();
            foreach (var o in s.Objectives.Where(o => !o.Done && !o.Failed))
            {
                int start = o.SeasonLong ? 0 : o.Deadline - 21;
                var window = season.Where(m => m.Day >= start && m.Minutes > 0).ToList();
                switch (o.Metric)
                {
                    case "apps": o.Progress = window.Count; break;
                    case "goals": o.Progress = window.Sum(m => m.Goals); break;
                    case "assists": o.Progress = window.Sum(m => m.Assists); break;
                    case "cleansheets": o.Progress = window.Count(m => m.GoalsAgainst == 0 && m.Minutes >= 60); break;
                    case "rating": o.Progress = window.Count >= 5 ? window.Average(m => m.Rating) : 0; break;
                    case "contrib": o.Progress = window.Sum(m => m.Goals + m.Assists); break;
                    case "bestrating": o.Progress = window.Select(m => m.Rating).DefaultIfEmpty(0).Max(); break;
                }
                if (o.Progress >= o.Target && (o.Metric != "rating" || s.Day >= o.Deadline - 7))
                {
                    o.Done = true;
                    s.Trust = MathUtil.Clamp(s.Trust + o.RewardTrust, 0, 100);
                    GainXp(s, o.RewardXp);
                    Message(s, "Comissão técnica", "Objetivo cumprido", o.Text);
                }
            }
        }

        private static void CheckObjectiveDeadlines(CareerState s)
        {
            foreach (var o in s.Objectives.Where(o => !o.Done && !o.Failed && s.Day > o.Deadline))
            {
                o.Failed = true;
                s.Trust = MathUtil.Clamp(s.Trust - o.RewardTrust * 0.6f, 0, 100);
            }
        }

        // ================================================================== weekly / windows

        private static void WeeklyTick(CareerState s)
        {
            WeeklyProgression(s);
            s.TrainingSessionsThisWeek = 0;
            CheckObjectiveDeadlines(s);
            var rng = RngFor(s, 1);
            NewObjectives(s, rng, false);
            // Trust drifts slowly towards 50 when nothing happens.
            s.Trust += (50f - s.Trust) * 0.01f;
            var a = s.Athlete;
            a.Reputation = MathUtil.Clamp((int)(a.Overall - 35 + s.AthleteState.Form * 2 - 13 + s.Titles.Count * 2), 1, 99);
            WorldBuilder.UpdateMarketValue(s.World, a);
            InTransferWindowTick(s, rng);
        }

        public static bool InTransferWindow(CareerState s) => s.Day <= TransferWindow1End || (s.Day >= TransferWindow2Start && s.Day <= TransferWindow2End);

        private static void TransferWindowOpens(CareerState s)
        {
            var rng = RngFor(s, 2);
            Message(s, s.AgentName, "Janela de transferências aberta", "Vou acompanhar o mercado e te aviso de qualquer proposta.");
            GenerateTransferOffers(s, rng, force: false);
            // Renewal: own club when the contract is ending and the athlete performs.
            var c = s.AthleteState.Contract;
            if (c.LoanFromClubId == null && c.UntilSeason <= s.World.Season && (s.Trust >= 50 || s.AthleteState.Form >= 6.8f))
                AddOffer(s, OfferType.Renewal, s.ClubId, (int)(c.Wage * rng.Range(1.1f, 1.5f)) + 1, rng.Range(2, 5), RoleFromTrust(s.Trust), 0, rng);
        }

        private static void InTransferWindowTick(CareerState s, Rng rng)
        {
            if (!InTransferWindow(s)) return;
            if (rng.Chance(0.35f)) GenerateTransferOffers(s, rng, false);
            AiTransfers(s, rng);
        }

        private static SquadRole RoleFromTrust(float trust) => trust >= 75 ? SquadRole.KeyPlayer : trust >= 55 ? SquadRole.Starter : trust >= 35 ? SquadRole.Rotation : SquadRole.Prospect;

        /// <summary>Clubs whose need, budget and ambition match the athlete make offers.</summary>
        public static void GenerateTransferOffers(CareerState s, Rng rng, bool force)
        {
            var w = s.World;
            var a = s.Athlete;
            var st = s.AthleteState;
            if (s.Retired) return;
            var group = PositionInfo.Group(a.Position);
            int made = 0;
            foreach (var club in w.Clubs.Values.Where(c => c.Id != s.ClubId).OrderBy(c => rng.Next()))
            {
                if (made >= (force ? 3 : 2)) break;
                if (s.Offers.Any(o => o.ClubId == club.Id && o.Status == OfferStatus.Pending)) continue;
                var bestAtPos = w.SquadOf(club.Id).Where(p => PositionInfo.Group(p.Position) == group).Select(p => p.Overall).DefaultIfEmpty(0).Max();
                bool need = a.Overall + 3 >= bestAtPos;
                bool ambition = a.Reputation + 30 >= club.Reputation;
                int wage = Math.Max(st.Contract.Wage + 1, WorldBuilder.WageFor(a.Overall, a.AgeOn(s.Date)));
                bool affordable = wage <= club.WageBudget / 8 && st.MarketValue <= club.Budget;
                float interest = (need ? 0.4f : 0.05f) + (st.Form - 6.5f) * 0.25f + (force ? 0.3f : 0f);
                if (!ambition || !affordable || !rng.Chance(MathUtil.Clamp01(interest))) continue;
                var role = a.Overall >= bestAtPos + 2 ? SquadRole.Starter : SquadRole.Rotation;
                long fee = st.Contract.UntilSeason <= w.Season ? 0 : (long)(st.MarketValue * rng.Range(0.9f, 1.3f));
                AddOffer(s, fee == 0 && st.Contract.UntilSeason < w.Season ? OfferType.FreeAgent : OfferType.Transfer, club.Id, (int)(wage * rng.Range(1.0f, 1.35f)), rng.Range(2, 5), role, fee, rng);
                NewsGenerator.Transfer(s, new TransferRecord { PlayerId = a.Id, FromClubId = s.ClubId, ToClubId = club.Id }, rng, rumour: true);
                made++;
            }
        }

        public static void GenerateLoanOffers(CareerState s, Rng rng, int count)
        {
            var w = s.World;
            var mine = w.Strength(s.ClubId);
            foreach (var club in w.Clubs.Values.Where(c => c.Id != s.ClubId && w.Strength(c.Id) < mine).OrderBy(c => rng.Next()).Take(count))
                AddOffer(s, OfferType.Loan, club.Id, s.AthleteState.Contract.Wage, 1, SquadRole.Starter, 0, rng);
        }

        private static void AddOffer(CareerState s, OfferType type, string clubId, int wage, int years, SquadRole role, long fee, Rng rng)
        {
            var o = new TransferOffer { Id = s.NewId("of"), Type = type, ClubId = clubId, Wage = Math.Max(1, wage), Years = years, Role = role, Fee = fee, ExpiresDay = s.Day + 10 };
            s.Offers.Insert(0, o);
            string kind = type == OfferType.Loan ? "empréstimo" : type == OfferType.Renewal ? "renovação" : "transferência";
            Message(s, s.AgentName, $"Proposta de {kind}: {s.World.Club(clubId)?.Name}",
                $"Salário semanal NX$ {o.Wage} mil por {years} temporada(s), papel: {RoleName(role)}{(fee > 0 ? $". Valor ao clube: {NewsGenerator.FormatMoney(fee)}" : "")}.", o.Id);
        }

        public static string RoleName(SquadRole r) =>
            r == SquadRole.KeyPlayer ? "peça-chave" : r == SquadRole.Starter ? "titular" : r == SquadRole.Rotation ? "rotação" : "promessa";

        /// <summary>User response to an offer. Returns a message describing the outcome.</summary>
        public static string RespondToOffer(CareerState s, string offerId, bool accept, bool counter = false)
        {
            var o = s.Offers.FirstOrDefault(x => x.Id == offerId);
            if (o == null || o.Status != OfferStatus.Pending) return "Proposta indisponível.";
            var rng = RngFor(s, offerId.GetHashCode());
            var w = s.World;
            if (counter)
            {
                if (o.Counters >= 2) { o.Status = OfferStatus.Withdrawn; return "O clube encerrou as negociações."; }
                o.Counters++;
                int asked = (int)(o.Wage * 1.2f) + 1;
                float accept_p = MathUtil.Clamp01(0.75f - o.Counters * 0.25f + (s.AthleteState.Form - 6.5f) * 0.15f);
                if (rng.Chance(accept_p)) { o.Wage = asked; o.Status = OfferStatus.Pending; return $"Contraproposta aceita: NX$ {asked} mil por semana."; }
                if (rng.Chance(0.4f)) { o.Status = OfferStatus.Withdrawn; return "O clube retirou a proposta."; }
                return "O clube manteve a proposta original.";
            }
            if (!accept) { o.Status = OfferStatus.Rejected; return "Proposta recusada."; }
            o.Status = OfferStatus.Accepted;
            var st = s.AthleteState;
            string from = s.ClubId;
            if (o.Type == OfferType.Renewal)
            {
                st.Contract.Wage = o.Wage;
                st.Contract.UntilSeason = w.Season + o.Years;
                st.Contract.Role = o.Role;
                s.Trust = MathUtil.Clamp(s.Trust + 4, 0, 100);
                NewsGenerator.Milestone(s, $"{s.Athlete.DisplayName} renova com o {s.Club.Name} até {st.Contract.UntilSeason}", rng);
                return "Contrato renovado!";
            }
            MovePlayer(w, s.AthleteId, from, o.ClubId, o.Fee, o.Type == OfferType.Loan);
            if (o.Type == OfferType.Loan)
            {
                st.Contract.LoanFromClubId = from;
                st.Contract.LoanUntilSeason = w.Season;
                st.Contract.ClubId = o.ClubId;
                st.Contract.Role = o.Role;
            }
            else st.Contract = new Contract { ClubId = o.ClubId, Wage = o.Wage, UntilSeason = w.Season + o.Years, Role = o.Role };
            var rec = w.Transfers.Last();
            NewsGenerator.Transfer(s, rec, rng);
            s.CoachId = w.Club(o.ClubId).CoachId;
            s.Trust = o.Role >= SquadRole.Starter ? 55 : 45;
            s.Athlete.ShirtNumber = PickShirtNumber(w, o.ClubId, s.Athlete.ShirtNumber, s.Athlete.Position);
            foreach (var other in s.Offers.Where(x => x.Status == OfferStatus.Pending)) other.Status = OfferStatus.Withdrawn;
            s.Objectives.RemoveAll(x => !x.Done);
            NewObjectives(s, rng, true);
            Message(s, w.Coach(s.CoachId)?.Name ?? "Treinador", "Bem-vindo!", $"Seja bem-vindo ao {w.Club(o.ClubId).Name}. Conquiste seu espaço.");
            return $"Negócio fechado com o {w.Club(o.ClubId).Name}!";
        }

        public static void MovePlayer(WorldState w, string playerId, string fromId, string toId, long fee, bool loan)
        {
            var p = w.Player(playerId);
            w.Club(fromId)?.Squad.Remove(playerId);
            var to = w.Club(toId);
            if (to != null && !to.Squad.Contains(playerId)) to.Squad.Add(playerId);
            p.ClubId = toId;
            if (to != null) to.Budget -= fee;
            var from = w.Club(fromId);
            if (from != null) from.Budget += fee;
            w.State(playerId).Contract.ClubId = toId;
            w.State(playerId).TransferHistory.Add($"{w.Season}: {from?.Name ?? "Livre"} -> {to?.Name}{(loan ? " (empréstimo)" : "")}");
            w.Transfers.Add(new TransferRecord { Season = w.Season, PlayerId = playerId, FromClubId = fromId, ToClubId = toId, Fee = fee, Loan = loan });
        }

        /// <summary>AI clubs buy needed players from weaker clubs, keeping squads coherent.</summary>
        private static void AiTransfers(CareerState s, Rng rng)
        {
            var w = s.World;
            if (!rng.Chance(0.5f)) return;
            var buyer = w.Clubs.Values.OrderBy(c => rng.Next()).First();
            if (buyer.Id == s.ClubId && rng.Chance(0.5f)) return;
            var groups = new[] { PositionGroup.Defender, PositionGroup.Midfielder, PositionGroup.Forward };
            var g = groups[rng.Range(0, groups.Length)];
            var mine = w.SquadOf(buyer.Id).Where(p => PositionInfo.Group(p.Position) == g).ToList();
            float myBest = mine.Select(p => (float)p.Overall).DefaultIfEmpty(50).Average();
            var target = w.Players.Values
                .Where(p => p.ClubId != null && p.ClubId != buyer.Id && p.Id != s.AthleteId && PositionInfo.Group(p.Position) == g && p.Overall > myBest + 2)
                .Where(p => w.State(p.Id).MarketValue <= buyer.Budget && w.Club(p.ClubId) != null && w.Club(p.ClubId).Squad.Count > 18)
                .Where(p => w.Club(p.ClubId).Reputation <= buyer.Reputation + 5)
                .OrderByDescending(p => p.Overall).FirstOrDefault();
            if (target == null) return;
            string from = target.ClubId;
            long fee = w.State(target.Id).MarketValue;
            MovePlayer(w, target.Id, from, buyer.Id, fee, false);
            w.State(target.Id).Contract = new Contract { ClubId = buyer.Id, Wage = WorldBuilder.WageFor(target.Overall, target.AgeOn(s.Date)), UntilSeason = w.Season + rng.Range(2, 5), Role = SquadRole.Starter };
            if (buyer.Id == s.ClubId || from == s.ClubId || rng.Chance(0.4f)) NewsGenerator.Transfer(s, w.Transfers.Last(), rng);
            // Keep the selling club's squad playable: sign a generated free agent if needed.
            var fromClub = w.Club(from);
            if (fromClub.Squad.Count < 19)
            {
                var youth = PlayerGenerator.Generate(rng, target.Position, Math.Max(50, target.Overall - 8), rng.Range(18, 22), from + "-y" + w.Season, rng.Range(100, 999), w.Season);
                youth.ClubId = from;
                youth.ShirtNumber = PickShirtNumber(w, from, 0, youth.Position);
                w.Players[youth.Id] = youth;
                fromClub.Squad.Add(youth.Id);
                w.State(youth.Id).Contract = new Contract { ClubId = from, Wage = WorldBuilder.WageFor(youth.Overall, 19), UntilSeason = w.Season + 3, Role = SquadRole.Prospect };
            }
        }

        /// <summary>Clubs far below expectations may sack their coach (affects the athlete's trust).</summary>
        private static void CoachReview(CareerState s)
        {
            var w = s.World;
            var rng = RngFor(s, 3);
            foreach (var comp in w.Competitions.Where(c => c.Format == CompetitionFormat.League))
            {
                var table = comp.Table();
                if (table.Count == 0 || table[0].Played < 6) continue;
                var expected = comp.ClubIds.OrderByDescending(id => w.Strength(id)).ToList();
                for (int i = 0; i < table.Count; i++)
                {
                    string clubId = table[i].ClubId;
                    int exp = expected.IndexOf(clubId);
                    if (i - exp < 4 || !rng.Chance(0.35f)) continue;
                    var club = w.Club(clubId);
                    var old = w.Coach(club.CoachId);
                    var coach = new CoachRecord
                    {
                        Id = $"coach-{clubId}-{w.Season}-{s.Day}", Name = PlayerGenerator.RandomName(rng, out _, out _), Nationality = club.Country,
                        Style = rng.Next(), YouthTrust = rng.Range(0.2f, 0.85f), Reputation = MathUtil.Clamp((old?.Reputation ?? 50) + rng.Range(-5, 10), 20, 95), ClubId = clubId
                    };
                    w.Coaches[coach.Id] = coach;
                    if (old != null) old.ClubId = null;
                    club.CoachId = coach.Id;
                    NewsGenerator.CoachChange(s, clubId, old?.Name ?? "o treinador", coach.Name, rng);
                    if (clubId == s.ClubId)
                    {
                        s.CoachId = coach.Id;
                        s.Trust = 45f;
                        Message(s, coach.Name, "Novo treinador", "Todos começam do zero comigo. Mostre nos treinos e nos jogos por que merece jogar.");
                    }
                    break; // at most one sacking per league per review
                }
            }
        }

        private static void NationalTeamWindow(CareerState s)
        {
            var rng = RngFor(s, 4);
            var a = s.Athlete;
            float form = s.AthleteState.Form;
            bool called = !s.Retired && s.AthleteState.InjuryDays <= 0 && (a.Overall >= 76 || (a.Overall >= 71 && form >= 7.1f));
            if (!called) return;
            NewsGenerator.NationalTeam(s, true, rng);
            Message(s, "Seleção", "Convocação", "Você foi convocado para os próximos jogos da seleção.");
            // Two international matches simulated with the athlete's quality.
            for (int i = 0; i < 2; i++)
            {
                s.NationalCaps++;
                float rating = 6.4f + rng.Gaussian() * 0.6f + (a.Overall - 75) * 0.03f;
                int goals = PositionInfo.Group(a.Position) == PositionGroup.Forward ? rng.Poisson(0.35f) : rng.Poisson(0.08f);
                s.NationalGoals += goals;
                GainXp(s, (int)(30 + (rating - 6) * 20 + goals * 15));
                if (goals > 0) NewsGenerator.Milestone(s, $"{a.DisplayName} marca pela seleção", rng);
            }
            if (s.NationalCaps == 2) NewsGenerator.Milestone(s, $"Estreia de {a.DisplayName} pela seleção", rng);
        }

        // ================================================================== season end

        /// <summary>Closes the season: awards, history, contracts, ageing/development, new schedule.</summary>
        public static void EndSeason(CareerState s)
        {
            var w = s.World;
            var rng = RngFor(s, 99);
            var summary = new SeasonSummary { Season = w.Season };
            foreach (var c in w.Competitions)
            {
                if (c.Format == CompetitionFormat.League && c.ChampionId == null && c.Fixtures.Count > 0) c.ChampionId = c.Table()[0].ClubId;
                if (c.ChampionId != null) summary.Champions[c.Id] = c.ChampionId;
            }
            // Awards for the athlete's league.
            string leagueId = s.Club.LeagueId;
            var lines = w.PlayerStates.Values.SelectMany(st => st.Stats.Where(l => l.Season == w.Season && l.CompetitionId == leagueId).Select(l => (st.PlayerId, l))).ToList();
            var top = lines.GroupBy(x => x.PlayerId).Select(gp => (id: gp.Key, goals: gp.Sum(x => x.l.Goals), apps: gp.Sum(x => x.l.Apps), rating: gp.Sum(x => x.l.RatingSum) / Math.Max(1, gp.Sum(x => x.l.Apps)))).ToList();
            var scorer = top.OrderByDescending(t => t.goals).ThenBy(t => t.apps).FirstOrDefault();
            var best = top.Where(t => t.apps >= 8).OrderByDescending(t => t.rating).FirstOrDefault();
            if (scorer.id != null)
            {
                summary.TopScorerId = scorer.id; summary.TopScorerGoals = scorer.goals;
                NewsGenerator.AwardNews(s, $"Artilheiro da temporada ({scorer.goals} gols)", scorer.id, rng);
                if (scorer.id == s.AthleteId) s.Awards.Add(new Award { Season = w.Season, Name = "Artilheiro", Detail = $"{scorer.goals} gols" });
            }
            if (best.id != null)
            {
                summary.BestPlayerId = best.id;
                NewsGenerator.AwardNews(s, $"Melhor jogador da temporada (média {best.rating:0.00})", best.id, rng);
                if (best.id == s.AthleteId) s.Awards.Add(new Award { Season = w.Season, Name = "Melhor jogador", Detail = $"média {best.rating:0.00}" });
            }
            var young = top.Where(t => t.apps >= 8 && w.Player(t.id) != null && w.Player(t.id).AgeOn(s.Date) <= 21).OrderByDescending(t => t.rating).FirstOrDefault();
            if (young.id == s.AthleteId && young.id != null) s.Awards.Add(new Award { Season = w.Season, Name = "Revelação", Detail = $"média {young.rating:0.00}" });
            w.History.Add(summary);

            // Loans end; contracts expire.
            var st = s.AthleteState;
            if (st.Contract.LoanFromClubId != null && st.Contract.LoanUntilSeason <= w.Season)
            {
                string parent = st.Contract.LoanFromClubId;
                MovePlayer(w, s.AthleteId, s.ClubId, parent, 0, false);
                st.Contract.LoanFromClubId = null;
                st.Contract.ClubId = parent;
                s.CoachId = w.Club(parent).CoachId;
                s.Trust = 50;
                Message(s, s.AgentName, "Fim do empréstimo", $"Você retorna ao {w.Club(parent).Name}.");
            }
            // AI development, ageing, retirements and replacements.
            foreach (var p in w.Players.Values.ToList())
            {
                if (p.Id == s.AthleteId) continue;
                int age = p.AgeOn(s.Date.AddDays(200));
                var pst = w.State(p.Id);
                if (age >= 34 && rng.Chance((age - 33) * 0.25f) && p.ClubId != null)
                {
                    pst.Retired = true;
                    var club = w.Club(p.ClubId);
                    club?.Squad.Remove(p.Id);
                    if (club != null)
                    {
                        var youth = PlayerGenerator.Generate(rng, p.Position, Math.Max(50, (int)w.Strength(club.Id) - 10), rng.Range(17, 20), club.Id + "-y" + (w.Season + 1), rng.Range(100, 999), w.Season + 1);
                        youth.ClubId = club.Id;
                        youth.ShirtNumber = PickShirtNumber(w, club.Id, 0, youth.Position);
                        w.Players[youth.Id] = youth;
                        club.Squad.Add(youth.Id);
                        w.State(youth.Id).Contract = new Contract { ClubId = club.Id, Wage = 2, UntilSeason = w.Season + 4, Role = SquadRole.Prospect };
                    }
                    p.ClubId = null;
                    continue;
                }
                int delta = age <= 22 ? rng.Range(1, 5) : age <= 27 ? rng.Range(0, 3) : age <= 30 ? rng.Range(-1, 2) : rng.Range(-4, 0);
                if (p.Overall + delta > p.Potential) delta = Math.Min(delta, 0);
                if (delta != 0)
                    foreach (var kv in PlayerAttributes.OverallWeights(p.Position)) p.Attributes.Set(kv.Key, p.Attributes.Get(kv.Key) + delta);
                if (pst.Contract.UntilSeason < w.Season + 1) pst.Contract.UntilSeason = w.Season + rng.Range(1, 4);
                pst.Stats.RemoveAll(l => l.Season < w.Season - 6);
            }

            // Next season.
            w.Season++;
            w.SeasonStartDate = $"{w.Season}-02-01";
            s.Day = 0;
            foreach (var c in w.Competitions) { c.Fixtures.Clear(); c.ChampionId = null; }
            WorldBuilder.ScheduleSeason(w, rng);
            NewObjectives(s, rng, true);
            foreach (var p in w.Players.Values) WorldBuilder.UpdateMarketValue(w, p);

            // The athlete's contract.
            if (st.Contract.UntilSeason < w.Season)
            {
                Message(s, s.AgentName, "Contrato encerrado", "Seu contrato terminou. Você é agente livre: avalie as propostas.");
                MovePlayer(w, s.AthleteId, s.ClubId, null, 0, false);
                st.Contract = new Contract { ClubId = null, Wage = 0, UntilSeason = w.Season };
                GenerateTransferOffers(s, rng, force: true);
            }
            int myAge = s.Athlete.AgeOn(s.Date);
            if (myAge >= 40) Retire(s);
            else if (myAge >= 34) Message(s, s.AgentName, "Futuro", "Você pode anunciar a aposentadoria quando quiser na tela do atleta.");
        }

        public static void Retire(CareerState s)
        {
            if (s.Retired) return;
            s.Retired = true;
            s.RetiredSeason = s.World.Season;
            s.AthleteState.Retired = true;
            var stats = s.AthleteState.Stats;
            NewsGenerator.Milestone(s, $"{s.Athlete.DisplayName} anuncia aposentadoria após {stats.Sum(l => l.Apps)} jogos e {stats.Sum(l => l.Goals)} gols", RngFor(s, 8));
        }
    }
}
