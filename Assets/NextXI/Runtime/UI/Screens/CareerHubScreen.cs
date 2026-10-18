using System;
using System.Collections.Generic;
using System.Linq;
using NextXI.Core;
using NextXI.Core.Career;
using NextXI.Core.Data;
using NextXI.Core.Match;
using NextXI.Core.News;
using NextXI.Core.World;
using UnityEngine;

namespace NextXI.Runtime
{
    /// <summary>Career hub: next match, athlete, training, competitions, newspaper, inbox, world.</summary>
    public sealed class CareerHubScreen : IScreen
    {
        public enum Tab { Home, Athlete, Training, Competitions, News, Inbox, World, History }
        private static readonly string[] TabNames = { "INÍCIO", "ATLETA", "TREINO", "TABELAS", "JORNAL", "MENSAGENS", "MUNDO", "HISTÓRICO" };

        private Tab _tab;
        private string _notice;
        private float _noticeUntil;
        private MatchPlan _plan;
        private Fixture _planFixture;
        private string _coachAnswer;
        private int _compIdx;
        private Vector2 _scroll;
        private string _search = "";
        private int _searchPos = -1;
        private string _profileId;
        private bool _newsSocial;
        private bool _confirmRetire;

        public CareerHubScreen(Tab tab, string notice) { _tab = tab; Notice(notice); }

        private CareerState S => CareerSession.Current;

        private void Notice(string text)
        {
            if (string.IsNullOrEmpty(text)) return;
            _notice = text;
            _noticeUntil = Time.unscaledTime + 4f;
        }

        public void OnEnter() { AudioService.Instance?.PlayMusic(true); }
        public void OnExit() { }
        public void Tick(float dt) { }

        public void Draw(float W, float H)
        {
            if (S == null) { GameApp.Instance.SetScreen(CareerScreens.Menu()); return; }
            DrawTopBar(W);
            float tabW = Mathf.Min(150f, (W - 80) / TabNames.Length);
            for (int i = 0; i < TabNames.Length; i++)
            {
                int unread = i == (int)Tab.Inbox ? S.Inbox.Count(m => !m.Read) : 0;
                string label = TabNames[i] + (unread > 0 ? $" ({unread})" : "");
                if (Ui.Chip(new Rect(40 + i * tabW, 86, tabW - 6, 44), label, (int)_tab == i, 16)) { _tab = (Tab)i; _profileId = null; _scroll = Vector2.zero; }
            }
            var area = new Rect(40, 142, W - 80, H - 158);
            switch (_tab)
            {
                case Tab.Home: DrawHome(area); break;
                case Tab.Athlete: DrawAthlete(area); break;
                case Tab.Training: DrawTraining(area); break;
                case Tab.Competitions: DrawCompetitions(area); break;
                case Tab.News: DrawNews(area); break;
                case Tab.Inbox: DrawInbox(area); break;
                case Tab.World: DrawWorld(area); break;
                case Tab.History: DrawHistory(area); break;
            }
            if (_notice != null && Time.unscaledTime < _noticeUntil)
            {
                var r = new Rect(W * 0.5f - 300, H - 70, 600, 50);
                Ui.Round(r, new Color(0.03f, 0.05f, 0.12f, 0.92f));
                Ui.Round(new Rect(r.x, r.y, 6, r.height), Ui.Cyan);
                Ui.Label(r, _notice, 20, Ui.Text, TextAnchor.MiddleCenter, Ui.FontKind.Body);
            }
        }

        private void DrawTopBar(float W)
        {
            var a = S.Athlete;
            var club = S.Club;
            Ui.Round(new Rect(40, 18, 14, 54), club != null ? Conv.Hex(club.HomeKit.Shirt) : Ui.Muted);
            Ui.Label(new Rect(64, 12, 700, 40), a.DisplayName.ToUpperInvariant(), 36, Ui.Text, TextAnchor.MiddleLeft, Ui.FontKind.DisplayItalic);
            Ui.Label(new Rect(66, 48, 900, 26), $"{CareerScreens.PosName(a.Position)} · GER {a.Overall} · {a.AgeOn(S.Date)} anos · {(club != null ? club.Name : "SEM CLUBE")} · {S.Date:dd/MM/yyyy}", 18, Ui.Muted, TextAnchor.MiddleLeft, Ui.FontKind.Body);
            if (Ui.Button(new Rect(W - 190, 20, 150, 50), "MENU", false, 20))
            {
                CareerSession.Close();
                GameApp.Instance.SetScreen(new MainMenuScreen());
            }
        }

        // ------------------------------------------------------------------ home

        private void DrawHome(Rect area)
        {
            var s = S;
            var left = new Rect(area.x, area.y, area.width * 0.58f - 10, area.height);
            var right = new Rect(area.x + area.width * 0.58f + 10, area.y, area.width * 0.42f - 10, area.height);
            Ui.PanelBox(left, true);
            float x = left.x + 20, y = left.y + 16, w = left.width - 40;
            if (s.Retired)
            {
                Ui.Label(new Rect(x, y, w, 40), "CARREIRA ENCERRADA", 34, Ui.Accent, TextAnchor.MiddleLeft, Ui.FontKind.DisplayItalic);
                Ui.Label(new Rect(x, y + 50, w, 120), $"Aposentado na temporada {s.RetiredSeason}. Veja o histórico completo na aba HISTÓRICO.", 20, Ui.Text, TextAnchor.UpperLeft, Ui.FontKind.Body, true);
                return;
            }
            var next = CareerManager.NextUserFixture(s);
            if (next == null)
            {
                bool done = s.World.Competitions.All(c => c.Fixtures.All(f => f.Played));
                Ui.Label(new Rect(x, y, w, 40), s.Club == null ? "SEM CLUBE" : done ? "TEMPORADA ENCERRADA" : "SEM JOGOS AGENDADOS", 32, Ui.Text, TextAnchor.MiddleLeft, Ui.FontKind.DisplayItalic);
                if (s.Club == null)
                {
                    Ui.Label(new Rect(x, y + 50, w, 60), "Você está sem contrato. Avalie as propostas na aba MENSAGENS.", 20, Ui.Muted, TextAnchor.UpperLeft, Ui.FontKind.Body, true);
                    if (!s.Offers.Any(o => o.Status == OfferStatus.Pending) && Ui.Button(new Rect(x, y + 120, 340, 60), "PROCURAR CLUBE", true, 22))
                    {
                        CareerManager.GenerateTransferOffers(s, CareerManager.RngFor(s, 55), true);
                        CareerSession.Save();
                    }
                }
                else if (!done)
                {
                    if (Ui.Button(new Rect(x, y + 60, 360, 64), "AVANÇAR CALENDÁRIO", true, 24))
                    {
                        CareerManager.AdvanceToNextMatch(s);
                        CareerSession.Save();
                    }
                }
                else if (Ui.Button(new Rect(x, y + 60, 360, 64), "ENCERRAR TEMPORADA", true, 24))
                {
                    GameApp.Instance.RunWithLoading("FECHANDO A TEMPORADA", () =>
                    {
                        CareerManager.EndSeason(s);
                        CareerSession.Save();
                        Notice($"Nova temporada: {s.World.Season}");
                    });
                }
                DrawObjectives(new Rect(x, y + 200, w, left.yMax - y - 220));
                return;
            }
            var comp = s.World.Competition(next.CompetitionId);
            bool home = next.HomeId == s.ClubId;
            var opp = s.World.Club(home ? next.AwayId : next.HomeId);
            int daysTo = next.Day - s.Day;
            Ui.Label(new Rect(x, y, w, 26), "PRÓXIMO JOGO" + (daysTo > 0 ? $" · em {daysTo} dia(s)" : " · HOJE"), 18, Ui.Cyan);
            Ui.Label(new Rect(x, y + 26, w, 50), $"{(home ? s.Club.Name : opp.Name)} x {(home ? opp.Name : s.Club.Name)}".ToUpperInvariant(), 34, Ui.Text, TextAnchor.MiddleLeft, Ui.FontKind.DisplayItalic, true);
            Ui.Label(new Rect(x, y + 78, w, 26), $"{comp.Name} · {(comp.Format == CompetitionFormat.League ? "Rodada " + next.Round : RoundName(comp, next.Round))} · {(home ? "em casa" : "fora")} · força adversária {s.World.Strength(opp.Id):0}", 17, Ui.Muted, TextAnchor.MiddleLeft, Ui.FontKind.Body);
            y += 118;
            if (daysTo > 0)
            {
                if (CareerManager.CanTrain(s) && !s.InYouthTeam)
                    Ui.Label(new Rect(x, y, w, 26), $"Você ainda pode treinar {CareerManager.MaxTrainingPerWeek - s.TrainingSessionsThisWeek}x nesta semana.", 17, Ui.Gold, TextAnchor.MiddleLeft, Ui.FontKind.Body);
                if (Ui.Button(new Rect(x, y + 34, 360, 64), "IR PARA O DIA DO JOGO", true, 24))
                {
                    CareerManager.AdvanceToNextMatch(s);
                    _plan = null;
                    CareerSession.Save();
                }
                if (s.InYouthTeam && Ui.Button(new Rect(x + 380, y + 34, 220, 64), "TREINAR", false, 22)) _tab = Tab.Training;
            }
            else
            {
                if (_plan == null || _planFixture != next) { _plan = CareerManager.PrepareMatch(s, next); _planFixture = next; }
                string status = _plan.Unavailable ? "FORA: " + _plan.Reason : _plan.Starter ? "TITULAR — " + _plan.Reason : "RESERVA — " + _plan.Reason;
                Ui.Label(new Rect(x, y, w, 30), status, 20, _plan.Starter ? Ui.Good : _plan.Unavailable ? Ui.Bad : Ui.Gold, TextAnchor.MiddleLeft, Ui.FontKind.Body, true);
                if (_plan.Starter)
                {
                    if (Ui.Button(new Rect(x, y + 40, 300, 70), "JOGAR EM 3D", true, 28)) CareerSession.PlayMatch(_plan, false);
                    if (Ui.Button(new Rect(x + 320, y + 40, 220, 70), "SIMULAR", false, 22)) Simulate();
                }
                else
                {
                    Ui.Label(new Rect(x, y + 36, w, 44), _plan.Unavailable ? "A partida será simulada sem você." : $"Você começa no banco; chance de entrar: {CareerManager.BenchChance(s) * 100:0}%. Simule ou acompanhe em 3D.", 17, Ui.Muted, TextAnchor.UpperLeft, Ui.FontKind.Body, true);
                    if (Ui.Button(new Rect(x, y + 84, 300, 64), "SIMULAR", false, 22)) Simulate();
                    if (_plan.OnBench && Ui.Button(new Rect(x + 320, y + 84, 300, 64), "VER DO BANCO (3D)", true, 22)) CareerSession.PlayMatch(_plan, false, true);
                }
            }
            DrawObjectives(new Rect(x, left.yMax - 210, w, 196));

            // Right column: coach & last match & standing.
            Ui.PanelBox(right);
            float rx = right.x + 18, ry = right.y + 14, rw = right.width - 36;
            var coach = s.World.Coach(s.Club?.CoachId);
            Ui.Label(new Rect(rx, ry, rw, 26), "TREINADOR: " + (coach?.Name ?? "-").ToUpperInvariant(), 18, Ui.Muted);
            Bar(new Rect(rx, ry + 30, rw, 26), "Confiança", s.Trust / 100f, s.Trust >= 60 ? Ui.Good : s.Trust >= 40 ? Ui.Gold : Ui.Bad);
            Bar(new Rect(rx, ry + 60, rw, 26), "Forma", Mathf.InverseLerp(5f, 8.5f, s.AthleteState.Form), Ui.Cyan);
            Bar(new Rect(rx, ry + 90, rw, 26), "Moral", s.AthleteState.Morale, Ui.Accent);
            float by = ry + 130;
            string[] talk = { "PEDIR TITULARIDADE", "PEDIR FEEDBACK", "PEDIR EMPRÉSTIMO", "PEDIR TRANSFERÊNCIA" };
            var reqs = new[] { CareerManager.CoachRequest.StartingPlace, CareerManager.CoachRequest.Feedback, CareerManager.CoachRequest.Loan, CareerManager.CoachRequest.Transfer };
            for (int i = 0; i < talk.Length; i++)
                if (Ui.Button(new Rect(rx + (i % 2) * (rw * 0.5f + 3), by + (i / 2) * 54, rw * 0.5f - 3, 48), talk[i], false, 15))
                {
                    _coachAnswer = CareerManager.TalkToCoach(s, reqs[i]);
                    _plan = null;
                    CareerSession.Save();
                }
            if (_coachAnswer != null) Ui.Label(new Rect(rx, by + 114, rw, 80), _coachAnswer, 17, Ui.Text, TextAnchor.UpperLeft, Ui.FontKind.Body, true);
            float ly = by + 200;
            var last = s.MatchLog.FirstOrDefault();
            if (last != null && ly < right.yMax - 80)
            {
                Ui.Label(new Rect(rx, ly, rw, 24), "ÚLTIMO JOGO", 17, Ui.Muted);
                Ui.Label(new Rect(rx, ly + 24, rw, 30), $"{(last.Home ? "x" : "@")} {s.World.Club(last.OpponentId)?.Name}  {last.GoalsFor}-{last.GoalsAgainst}", 22, Ui.Text);
                Ui.Label(new Rect(rx, ly + 52, rw, 26), last.Minutes > 0 ? $"Nota {last.Rating:0.0} · {last.Minutes}' · G {last.Goals} · A {last.Assists}" : "Não entrou em campo", 17, Ui.Muted, TextAnchor.MiddleLeft, Ui.FontKind.Body);
            }
        }

        private void Simulate()
        {
            var plan = _plan;
            GameApp.Instance.RunWithLoading("SIMULANDO A PARTIDA", () =>
            {
                CareerSession.PlayMatch(plan, true);
            });
            _plan = null;
        }

        private static string RoundName(Competition c, int round)
        {
            int clubs = c.ClubIds.Count;
            int left = clubs >> (round - 1);
            return left == 2 ? "Final" : left == 4 ? "Semifinal" : left == 8 ? "Quartas de final" : "Fase " + round;
        }

        private void DrawObjectives(Rect r)
        {
            Ui.Label(new Rect(r.x, r.y, r.width, 26), "OBJETIVOS", 18, Ui.Muted);
            float y = r.y + 30;
            foreach (var o in S.Objectives.Where(o => !o.Failed).OrderBy(o => o.Done).Take(4))
            {
                Ui.Label(new Rect(r.x, y, r.width * 0.7f, 30), (o.Done ? "[OK] " : "") + o.Text, 17, o.Done ? Ui.Good : Ui.Text, TextAnchor.MiddleLeft, Ui.FontKind.Body);
                string prog = o.Metric == "rating" || o.Metric == "bestrating" ? $"{o.Progress:0.0}/{o.Target:0.0}" : $"{o.Progress:0}/{o.Target:0}";
                Ui.Label(new Rect(r.x + r.width * 0.72f, y, r.width * 0.28f, 30), prog, 18, Ui.Cyan, TextAnchor.MiddleRight);
                y += 34;
            }
        }

        private static void Bar(Rect r, string label, float v, Color c)
        {
            Ui.Label(new Rect(r.x, r.y, r.width * 0.32f, r.height), label, 17, Ui.Muted, TextAnchor.MiddleLeft, Ui.FontKind.Body);
            var track = new Rect(r.x + r.width * 0.34f, r.y + r.height * 0.5f - 5, r.width * 0.56f, 10);
            Ui.Round(track, new Color(1, 1, 1, 0.1f));
            Ui.Round(new Rect(track.x, track.y, track.width * Mathf.Clamp01(v), track.height), c);
            Ui.Label(new Rect(track.xMax + 6, r.y, r.width * 0.1f, r.height), Mathf.RoundToInt(Mathf.Clamp01(v) * 100).ToString(), 17, Ui.Text, TextAnchor.MiddleRight);
        }

        // ------------------------------------------------------------------ athlete

        private void DrawAthlete(Rect area)
        {
            var s = S;
            var a = s.Athlete;
            var st = s.AthleteState;
            var left = new Rect(area.x, area.y, area.width * 0.62f - 10, area.height);
            Ui.PanelBox(left);
            string[][] groups =
            {
                new[] { "Pace", "Acceleration", "Agility", "Balance", "Stamina", "Strength", "Jumping" },
                new[] { "BallControl", "Dribbling", "ShortPassing", "LongPassing", "Crossing", "Finishing", "ShotPower", "LongShots", "Heading", "Curve" },
                new[] { "Vision", "Positioning", "Composure", "Reactions", "Aggression", "Marking", "StandingTackle", "SlidingTackle", "Interceptions" },
            };
            if (a.Position == Position.GK) groups[2] = new[] { "GkDiving", "GkHandling", "GkKicking", "GkPositioning", "GkReflexes", "Reactions", "Composure" };
            string[] titles = { "FÍSICO", "TÉCNICA", a.Position == Position.GK ? "GOLEIRO" : "MENTAL / DEFESA" };
            float colW = (left.width - 40) / 3f;
            for (int g = 0; g < 3; g++)
            {
                float x = left.x + 20 + g * colW, y = left.y + 14;
                Ui.Label(new Rect(x, y, colW, 26), titles[g], 18, Ui.Cyan);
                y += 30;
                foreach (var key in groups[g])
                {
                    int v = a.Attributes.Get(key);
                    s.AttributeProgress.TryGetValue(key, out var prog);
                    Ui.Label(new Rect(x, y, colW - 60, 28), CareerManager.AttributeLabel(key), 17, Ui.Text, TextAnchor.MiddleLeft, Ui.FontKind.Body);
                    Ui.Label(new Rect(x + colW - 64, y, 40, 28), v.ToString(), 20, v >= 80 ? Ui.Good : v >= 65 ? Ui.Text : Ui.Muted, TextAnchor.MiddleRight);
                    if (prog > 0.05f) Ui.Round(new Rect(x + colW - 18, y + 22, 12 * Mathf.Clamp01(prog), 3), Ui.Accent);
                    y += 30;
                }
            }
            var right = new Rect(area.x + area.width * 0.62f + 10, area.y, area.width * 0.38f - 10, area.height);
            Ui.PanelBox(right, true);
            float rx = right.x + 18, ry = right.y + 14, rw = right.width - 36;
            Ui.Label(new Rect(rx, ry, rw, 44), $"GERAL {a.Overall}", 40, Ui.Accent, TextAnchor.MiddleLeft, Ui.FontKind.DisplayItalic);
            Ui.Label(new Rect(rx, ry + 44, rw, 26), $"{s.Archetype.Name} · Nível {s.Level} · XP {s.Xp}/{CareerManager.XpForLevel(s.Level)}", 17, Ui.Text, TextAnchor.MiddleLeft, Ui.FontKind.Body);
            Ui.Label(new Rect(rx, ry + 70, rw, 26), $"Valor de mercado: {NewsGenerator.FormatMoney(st.MarketValue)} · Reputação {a.Reputation}", 17, Ui.Muted, TextAnchor.MiddleLeft, Ui.FontKind.Body);
            var c = st.Contract;
            Ui.Label(new Rect(rx, ry + 96, rw, 26), c.ClubId == null ? "Sem contrato" : $"Contrato até {c.UntilSeason} · NX$ {c.Wage} mil/sem · {CareerManager.RoleName(c.Role)}{(c.LoanFromClubId != null ? " · emprestado" : "")}", 17, Ui.Muted, TextAnchor.MiddleLeft, Ui.FontKind.Body, true);
            float y2 = ry + 136;
            Ui.Label(new Rect(rx, y2, rw, 26), "HABILIDADES", 18, Ui.Cyan);
            y2 += 28;
            foreach (var t in a.Traits.Take(6)) { Ui.Label(new Rect(rx, y2, rw, 26), "• " + CareerManager.PerkName(t), 16, Ui.Text, TextAnchor.MiddleLeft, Ui.FontKind.Body); y2 += 26; }
            if (a.Traits.Count == 0) { Ui.Label(new Rect(rx, y2, rw, 26), "Suba de nível para desbloquear.", 16, Ui.Muted, TextAnchor.MiddleLeft, Ui.FontKind.Body); y2 += 26; }
            if (s.PendingPerkChoice.Count > 0)
            {
                Ui.Label(new Rect(rx, y2 + 6, rw, 26), "ESCOLHA SUA ESPECIALIZAÇÃO:", 17, Ui.Gold);
                y2 += 34;
                foreach (var opt in s.PendingPerkChoice.ToList())
                {
                    if (Ui.Button(new Rect(rx, y2, rw, 46), CareerManager.PerkName(opt), true, 15)) { CareerManager.UnlockPerk(s, opt); CareerSession.Save(); }
                    y2 += 52;
                }
            }
            var injury = st.InjuryDays > 0 ? $"Lesão: {st.InjuryName} ({st.InjuryDays} dias)" : st.SuspensionMatches > 0 ? $"Suspenso por {st.SuspensionMatches} jogo(s)" : "Condição: disponível";
            Ui.Label(new Rect(rx, right.yMax - 120, rw, 26), injury, 17, st.InjuryDays > 0 || st.SuspensionMatches > 0 ? Ui.Bad : Ui.Good, TextAnchor.MiddleLeft, Ui.FontKind.Body);
            int age = a.AgeOn(s.Date);
            if (age >= 33 && !s.Retired)
            {
                if (Ui.Button(new Rect(rx, right.yMax - 80, rw, 56), _confirmRetire ? "CONFIRMAR APOSENTADORIA" : "ANUNCIAR APOSENTADORIA", false, 18))
                {
                    if (_confirmRetire) { CareerManager.Retire(s); CareerSession.Save(); }
                    _confirmRetire = !_confirmRetire;
                }
            }
        }

        // ------------------------------------------------------------------ training

        private void DrawTraining(Rect area)
        {
            var s = S;
            Ui.Label(new Rect(area.x, area.y, area.width, 30), $"Sessões nesta semana: {s.TrainingSessionsThisWeek}/{CareerManager.MaxTrainingPerWeek}. Treinos são jogáveis em 3D; o desempenho vira XP e evolução dos atributos do exercício.", 17, Ui.Muted, TextAnchor.MiddleLeft, Ui.FontKind.Body, true);
            bool keeper = s.Athlete.Position == Position.GK;
            var drills = CareerManager.Drills.Where(d => keeper ? !d.OutfieldOnly : !d.KeeperOnly).ToList();
            float cw = (area.width - 20) / 3f;
            for (int i = 0; i < drills.Count; i++)
            {
                var d = drills[i];
                var r = new Rect(area.x + (i % 3) * (cw + 10), area.y + 44 + (i / 3) * 132, cw, 122);
                Ui.PanelBox(r, true);
                Ui.Label(new Rect(r.x + 14, r.y + 6, r.width - 28, 32), d.Name.ToUpperInvariant(), 24, Ui.Text);
                Ui.Label(new Rect(r.x + 14, r.y + 36, r.width - 28, 40), d.Description, 15, Ui.Muted, TextAnchor.UpperLeft, Ui.FontKind.Body, true);
                var best = s.Training.Where(t => t.DrillId == d.Id).Select(t => t.Score).DefaultIfEmpty(0).Max();
                Ui.Label(new Rect(r.x + 14, r.yMax - 40, r.width * 0.5f, 30), best > 0 ? $"Melhor: {best * 100:0}" : "Novo", 16, Ui.Cyan);
                if (CareerManager.CanTrain(s) && Ui.Button(new Rect(r.xMax - 130, r.yMax - 52, 116, 44), "TREINAR", true, 18))
                    GameApp.Instance.StartTraining(d.Id, s);
            }
            if (!CareerManager.CanTrain(s))
                Ui.Label(new Rect(area.x, area.yMax - 40, area.width, 30), s.AthleteState.InjuryDays > 0 ? "Lesionado: treinos suspensos." : "Limite semanal atingido. Avance o calendário.", 18, Ui.Gold);
        }

        // ------------------------------------------------------------------ competitions

        private void DrawCompetitions(Rect area)
        {
            var w = S.World;
            var comps = w.Competitions;
            _compIdx = Mathf.Clamp(_compIdx, 0, comps.Count - 1);
            _compIdx = Ui.Segmented(new Rect(area.x, area.y, Mathf.Min(area.width, 240 * comps.Count), 46), _compIdx, comps.Select(c => c.Name.ToUpperInvariant()).ToArray(), 16);
            var comp = comps[_compIdx];
            var left = new Rect(area.x, area.y + 56, area.width * 0.58f - 10, area.height - 56);
            Ui.PanelBox(left);
            if (comp.Format == CompetitionFormat.League)
            {
                var table = comp.Table();
                float y = left.y + 10;
                Ui.Label(new Rect(left.x + 14, y, left.width, 24), "#   CLUBE                                         J    V    E    D    SG   PTS", 15, Ui.Muted, TextAnchor.MiddleLeft, Ui.FontKind.Body);
                y += 26;
                for (int i = 0; i < table.Count; i++)
                {
                    var row = table[i];
                    bool me = row.ClubId == S.ClubId;
                    if (me) Ui.Round(new Rect(left.x + 6, y, left.width - 12, 30), new Color(Ui.Accent.r, Ui.Accent.g, Ui.Accent.b, 0.22f));
                    var c = w.Club(row.ClubId);
                    Ui.Label(new Rect(left.x + 14, y, 30, 30), (i + 1).ToString(), 18, Ui.Text);
                    Ui.Round(new Rect(left.x + 44, y + 8, 8, 14), Conv.Hex(c.HomeKit.Shirt));
                    Ui.Label(new Rect(left.x + 58, y, left.width * 0.45f, 30), c.Name, 18, me ? Ui.Accent : Ui.Text, TextAnchor.MiddleLeft, Ui.FontKind.Body);
                    string nums = $"{row.Played,2}  {row.Won,3}  {row.Drawn,3}  {row.Lost,3}  {row.GoalDiff,4}  {row.Points,4}";
                    Ui.Label(new Rect(left.x + left.width * 0.5f, y, left.width * 0.5f - 14, 30), nums, 18, Ui.Text, TextAnchor.MiddleRight);
                    y += 32;
                    if (y > left.yMax - 30) break;
                }
            }
            else
            {
                float y = left.y + 10;
                foreach (var f in comp.Fixtures.OrderBy(f => f.Round))
                {
                    string res = f.Played ? $"{f.HomeGoals}-{f.AwayGoals}{(string.IsNullOrEmpty(f.PenaltyWinnerId) ? "" : " (pên.)")}" : "x";
                    Ui.Label(new Rect(left.x + 14, y, left.width - 28, 30), $"{RoundName(comp, f.Round)}: {w.Club(f.HomeId).Name} {res} {w.Club(f.AwayId).Name}", 17, f.Involves(S.ClubId) ? Ui.Accent : Ui.Text, TextAnchor.MiddleLeft, Ui.FontKind.Body);
                    y += 32;
                }
                if (comp.ChampionId != null) Ui.Label(new Rect(left.x + 14, y + 10, left.width, 34), "CAMPEÃO: " + w.Club(comp.ChampionId).Name.ToUpperInvariant(), 24, Ui.Gold);
            }
            // Top scorers & assists of this competition.
            var right = new Rect(area.x + area.width * 0.58f + 10, area.y + 56, area.width * 0.42f - 10, area.height - 56);
            Ui.PanelBox(right);
            var lines = w.PlayerStates.Values.SelectMany(st => st.Stats.Where(l => l.Season == w.Season && l.CompetitionId == comp.Id).Select(l => (st.PlayerId, l))).ToList();
            var scorers = lines.GroupBy(x => x.PlayerId).Select(g => (id: g.Key, goals: g.Sum(x => x.l.Goals), assists: g.Sum(x => x.l.Assists))).ToList();
            float ry = right.y + 10;
            Ui.Label(new Rect(right.x + 14, ry, right.width, 26), "ARTILHARIA", 18, Ui.Cyan);
            ry += 28;
            foreach (var t in scorers.Where(t => t.goals > 0).OrderByDescending(t => t.goals).Take(6))
            {
                var p = w.Player(t.id);
                Ui.Label(new Rect(right.x + 14, ry, right.width - 70, 26), $"{p?.ShortName} ({w.Club(p?.ClubId)?.ShortName})", 17, t.id == S.AthleteId ? Ui.Accent : Ui.Text, TextAnchor.MiddleLeft, Ui.FontKind.Body);
                Ui.Label(new Rect(right.xMax - 60, ry, 46, 26), t.goals.ToString(), 18, Ui.Text, TextAnchor.MiddleRight);
                ry += 27;
            }
            ry += 8;
            Ui.Label(new Rect(right.x + 14, ry, right.width, 26), "ASSISTÊNCIAS", 18, Ui.Cyan);
            ry += 28;
            foreach (var t in scorers.Where(t => t.assists > 0).OrderByDescending(t => t.assists).Take(5))
            {
                var p = w.Player(t.id);
                Ui.Label(new Rect(right.x + 14, ry, right.width - 70, 26), $"{p?.ShortName} ({w.Club(p?.ClubId)?.ShortName})", 17, t.id == S.AthleteId ? Ui.Accent : Ui.Text, TextAnchor.MiddleLeft, Ui.FontKind.Body);
                Ui.Label(new Rect(right.xMax - 60, ry, 46, 26), t.assists.ToString(), 18, Ui.Text, TextAnchor.MiddleRight);
                ry += 27;
            }
        }

        // ------------------------------------------------------------------ news

        private void DrawNews(Rect area)
        {
            int sel = Ui.Segmented(new Rect(area.x, area.y, 420, 44), _newsSocial ? 1 : 0, new[] { "DIÁRIO NEXT", "REDES SOCIAIS" }, 18);
            _newsSocial = sel == 1;
            var r = new Rect(area.x, area.y + 54, area.width, area.height - 54);
            Ui.PanelBox(r);
            float y = r.y + 12;
            if (!_newsSocial)
            {
                var items = S.News.Take(12).ToList();
                for (int i = 0; i < items.Count && y < r.yMax - 60; i++)
                {
                    var n = items[i];
                    bool big = i == 0 || n.Importance >= 5;
                    Ui.Label(new Rect(r.x + 18, y, 200, 22), $"T{n.Season} · DIA {n.Day} · {CategoryName(n.Category)}", 14, Ui.Accent);
                    Ui.Label(new Rect(r.x + 18, y + 20, r.width - 36, big ? 40 : 30), n.Headline, big ? 30 : 22, Ui.Text, TextAnchor.MiddleLeft, Ui.FontKind.Display, true);
                    y += big ? 62 : 52;
                    if (big)
                    {
                        Ui.Label(new Rect(r.x + 18, y, r.width - 36, 44), n.Body, 16, Ui.Muted, TextAnchor.UpperLeft, Ui.FontKind.Body, true);
                        y += 48;
                    }
                }
            }
            else
            {
                foreach (var p in S.Social.Take(10))
                {
                    if (y > r.yMax - 70) break;
                    Ui.Label(new Rect(r.x + 18, y, 400, 24), p.Handle, 17, p.Sentiment > 0 ? Ui.Good : p.Sentiment < 0 ? Ui.Bad : Ui.Cyan);
                    Ui.Label(new Rect(r.xMax - 220, y, 200, 24), $"{p.Likes} curtidas", 16, Ui.Muted, TextAnchor.MiddleRight, Ui.FontKind.Body);
                    Ui.Label(new Rect(r.x + 18, y + 24, r.width - 36, 40), p.Text, 18, Ui.Text, TextAnchor.UpperLeft, Ui.FontKind.Body, true);
                    y += 72;
                }
                if (S.Social.Count == 0) Ui.Label(new Rect(r.x + 18, y, r.width, 30), "Jogue partidas para gerar reações da torcida.", 18, Ui.Muted);
            }
        }

        private static string CategoryName(NewsCategory c)
        {
            switch (c)
            {
                case NewsCategory.Result: return "RESULTADO"; case NewsCategory.Player: return "ATLETA"; case NewsCategory.Transfer: return "MERCADO";
                case NewsCategory.Rumour: return "RUMOR"; case NewsCategory.Injury: return "DEPARTAMENTO MÉDICO"; case NewsCategory.Coach: return "TÉCNICOS";
                case NewsCategory.Record: return "MARCAS"; case NewsCategory.NationalTeam: return "SELEÇÃO"; case NewsCategory.Award: return "PRÊMIOS";
                case NewsCategory.Interview: return "ENTREVISTA"; default: return "CLUBES";
            }
        }

        // ------------------------------------------------------------------ inbox & offers

        private void DrawInbox(Rect area)
        {
            var s = S;
            var r = new Rect(area.x, area.y, area.width, area.height);
            Ui.PanelBox(r);
            float y = r.y + 10;
            foreach (var m in s.Inbox.Take(8).ToList())
            {
                if (y > r.yMax - 90) break;
                m.Read = true;
                var offer = m.OfferId != null ? s.Offers.FirstOrDefault(o => o.Id == m.OfferId) : null;
                float h = offer != null && offer.Status == OfferStatus.Pending ? 118 : 74;
                var box = new Rect(r.x + 10, y, r.width - 20, h);
                Ui.Round(box, Ui.PanelHi);
                Ui.Label(new Rect(box.x + 14, box.y + 4, box.width - 28, 26), $"{m.From} · T{m.Season} dia {m.Day}", 15, Ui.Muted, TextAnchor.MiddleLeft, Ui.FontKind.Body);
                Ui.Label(new Rect(box.x + 14, box.y + 26, box.width - 28, 26), m.Title, 20, Ui.Text);
                Ui.Label(new Rect(box.x + 14, box.y + 50, box.width - 28, 24), m.Body, 15, Ui.Muted, TextAnchor.MiddleLeft, Ui.FontKind.Body, true);
                if (offer != null)
                {
                    if (offer.Status == OfferStatus.Pending)
                    {
                        float bx = box.x + 14, byy = box.y + 76;
                        if (Ui.Button(new Rect(bx, byy, 150, 36), "ACEITAR", true, 16)) { Notice(CareerManager.RespondToOffer(s, offer.Id, true)); CareerSession.Save(); }
                        if (Ui.Button(new Rect(bx + 160, byy, 190, 36), "CONTRAPROPOSTA", false, 16)) { Notice(CareerManager.RespondToOffer(s, offer.Id, false, true)); CareerSession.Save(); }
                        if (Ui.Button(new Rect(bx + 360, byy, 150, 36), "RECUSAR", false, 16)) { Notice(CareerManager.RespondToOffer(s, offer.Id, false)); CareerSession.Save(); }
                        Ui.Label(new Rect(bx + 530, byy, 300, 36), $"Expira no dia {offer.ExpiresDay}", 15, Ui.Gold, TextAnchor.MiddleLeft, Ui.FontKind.Body);
                    }
                    else Ui.Label(new Rect(box.xMax - 220, box.y + 4, 206, 26), StatusName(offer.Status), 16, Ui.Cyan, TextAnchor.MiddleRight);
                }
                y += h + 8;
            }
            if (s.Inbox.Count == 0) Ui.Label(new Rect(r.x + 18, y, r.width, 30), "Sem mensagens.", 18, Ui.Muted);
        }

        private static string StatusName(OfferStatus s)
        {
            switch (s)
            {
                case OfferStatus.Accepted: return "ACEITA"; case OfferStatus.Rejected: return "RECUSADA"; case OfferStatus.Withdrawn: return "RETIRADA";
                case OfferStatus.Expired: return "EXPIRADA"; default: return "PENDENTE";
            }
        }

        // ------------------------------------------------------------------ world search

        private void DrawWorld(Rect area)
        {
            var w = S.World;
            if (_profileId != null) { DrawProfile(area, w.Player(_profileId)); return; }
            Ui.Label(new Rect(area.x, area.y, 120, 46), "BUSCAR", 18, Ui.Muted);
            var st = Ui.Style(22, Ui.Text);
            Ui.Round(new Rect(area.x + 100, area.y, 320, 46), Ui.PanelHi);
            _search = GUI.TextField(Ui.P(new Rect(area.x + 110, area.y, 300, 46)), _search, 24, st);
            string[] pos = { "TODAS", "GOL", "DEF", "MEI", "ATA" };
            _searchPos = Ui.Segmented(new Rect(area.x + 440, area.y, 420, 46), _searchPos + 1, pos, 16) - 1;
            var results = w.Search(string.IsNullOrWhiteSpace(_search) ? null : _search).Where(p => p.ClubId != null)
                .Where(p => _searchPos < 0 || (int)PositionInfo.Group(p.Position) == _searchPos)
                .OrderByDescending(p => p.Overall).Take(14).ToList();
            var r = new Rect(area.x, area.y + 56, area.width, area.height - 56);
            Ui.PanelBox(r);
            float y = r.y + 8;
            foreach (var p in results)
            {
                var row = new Rect(r.x + 10, y, r.width - 20, 32);
                if (p.Id == S.AthleteId) Ui.Round(row, new Color(Ui.Accent.r, Ui.Accent.g, Ui.Accent.b, 0.2f));
                Ui.Label(new Rect(row.x + 8, y, 60, 32), p.Overall.ToString(), 20, Ui.Accent);
                Ui.Label(new Rect(row.x + 60, y, 60, 32), CareerScreens.PosName(p.Position), 17, Ui.Cyan);
                Ui.Label(new Rect(row.x + 120, y, row.width * 0.38f, 32), p.DisplayName, 18, Ui.Text, TextAnchor.MiddleLeft, Ui.FontKind.Body);
                Ui.Label(new Rect(row.x + row.width * 0.5f, y, row.width * 0.3f, 32), w.Club(p.ClubId)?.Name ?? "-", 16, Ui.Muted, TextAnchor.MiddleLeft, Ui.FontKind.Body);
                Ui.Label(new Rect(row.x + row.width * 0.8f, y, row.width * 0.2f - 10, 32), $"{p.AgeOn(S.Date)} anos", 16, Ui.Muted, TextAnchor.MiddleRight, Ui.FontKind.Body);
                if (GUI.Button(Ui.P(row), GUIContent.none, GUIStyle.none)) _profileId = p.Id;
                y += 34;
                if (y > r.yMax - 34) break;
            }
        }

        private void DrawProfile(Rect area, PlayerProfile p)
        {
            var w = S.World;
            if (p == null) { _profileId = null; return; }
            var st = w.State(p.Id);
            Ui.PanelBox(area, true);
            float x = area.x + 20, y = area.y + 14;
            Ui.Label(new Rect(x, y, area.width, 44), p.DisplayName.ToUpperInvariant(), 38, Ui.Text, TextAnchor.MiddleLeft, Ui.FontKind.DisplayItalic);
            Ui.Label(new Rect(x, y + 44, area.width, 26), $"{CareerScreens.PosName(p.Position)} · GER {p.Overall} · POT ~{Math.Min(99, p.Potential + 2)} · {p.AgeOn(S.Date)} anos · {p.HeightCm} cm · {p.Nationality} · {(p.PreferredFoot == Foot.Right ? "destro" : "canhoto")}", 18, Ui.Muted, TextAnchor.MiddleLeft, Ui.FontKind.Body);
            Ui.Label(new Rect(x, y + 72, area.width, 26), $"{w.Club(p.ClubId)?.Name ?? "Sem clube"} · camisa {p.ShirtNumber} · contrato até {st.Contract.UntilSeason} · valor {NewsGenerator.FormatMoney(st.MarketValue)}", 18, Ui.Text, TextAnchor.MiddleLeft, Ui.FontKind.Body);
            Ui.Label(new Rect(x, y + 98, area.width, 26), "Fonte dos dados: " + p.DataSource, 15, Ui.Muted, TextAnchor.MiddleLeft, Ui.FontKind.Body);
            y += 134;
            Ui.Label(new Rect(x, y, area.width, 26), "ESTATÍSTICAS", 18, Ui.Cyan);
            y += 28;
            foreach (var l in st.Stats.OrderByDescending(l => l.Season).Take(6))
            {
                Ui.Label(new Rect(x, y, area.width - 40, 26), $"{l.Season} · {w.Club(l.ClubId)?.Name} · {w.Competition(l.CompetitionId)?.Name}: {l.Apps} jogos, {l.Goals} gols, {l.Assists} assist., nota {l.AvgRating:0.00}", 16, Ui.Text, TextAnchor.MiddleLeft, Ui.FontKind.Body);
                y += 26;
            }
            foreach (var h in st.TransferHistory.TakeLast(3)) { Ui.Label(new Rect(x, y, area.width - 40, 24), h, 15, Ui.Muted, TextAnchor.MiddleLeft, Ui.FontKind.Body); y += 24; }
            if (Ui.Button(new Rect(area.xMax - 200, area.yMax - 70, 180, 54), "VOLTAR", false, 20)) _profileId = null;
        }

        // ------------------------------------------------------------------ history

        private void DrawHistory(Rect area)
        {
            var s = S;
            var st = s.AthleteState;
            var left = new Rect(area.x, area.y, area.width * 0.6f - 10, area.height);
            Ui.PanelBox(left);
            float y = left.y + 10;
            Ui.Label(new Rect(left.x + 16, y, left.width, 26), "TEMPORADA · CLUBE · COMPETIÇÃO              J    G    A   NOTA", 15, Ui.Muted, TextAnchor.MiddleLeft, Ui.FontKind.Body);
            y += 28;
            foreach (var l in st.Stats.OrderByDescending(l => l.Season).ThenBy(l => l.CompetitionId))
            {
                if (y > left.yMax - 40) break;
                Ui.Label(new Rect(left.x + 16, y, left.width * 0.62f, 26), $"{l.Season} · {s.World.Club(l.ClubId)?.ShortName} · {s.World.Competition(l.CompetitionId)?.Name}", 16, Ui.Text, TextAnchor.MiddleLeft, Ui.FontKind.Body);
                Ui.Label(new Rect(left.x + left.width * 0.62f, y, left.width * 0.38f - 16, 26), $"{l.Apps,3} {l.Goals,4} {l.Assists,4} {l.AvgRating,6:0.00}", 17, Ui.Text, TextAnchor.MiddleRight);
                y += 27;
            }
            int apps = st.Stats.Sum(l => l.Apps), goals = st.Stats.Sum(l => l.Goals), assists = st.Stats.Sum(l => l.Assists);
            Ui.Label(new Rect(left.x + 16, left.yMax - 36, left.width - 32, 30), $"TOTAL: {apps} jogos · {goals} gols · {assists} assistências · Seleção: {s.NationalCaps} jogos, {s.NationalGoals} gols", 18, Ui.Accent);
            var right = new Rect(area.x + area.width * 0.6f + 10, area.y, area.width * 0.4f - 10, area.height);
            Ui.PanelBox(right, true);
            float ry = right.y + 12;
            Ui.Label(new Rect(right.x + 16, ry, right.width, 26), "TÍTULOS", 18, Ui.Gold);
            ry += 28;
            foreach (var t in s.Titles) { Ui.Label(new Rect(right.x + 16, ry, right.width - 32, 26), "* " + t, 17, Ui.Text, TextAnchor.MiddleLeft, Ui.FontKind.Body); ry += 26; }
            if (s.Titles.Count == 0) { Ui.Label(new Rect(right.x + 16, ry, right.width, 26), "Nenhum ainda.", 16, Ui.Muted, TextAnchor.MiddleLeft, Ui.FontKind.Body); ry += 26; }
            ry += 10;
            Ui.Label(new Rect(right.x + 16, ry, right.width, 26), "PRÊMIOS", 18, Ui.Gold);
            ry += 28;
            foreach (var a in s.Awards) { Ui.Label(new Rect(right.x + 16, ry, right.width - 32, 26), $"{a.Season} · {a.Name} ({a.Detail})", 17, Ui.Text, TextAnchor.MiddleLeft, Ui.FontKind.Body); ry += 26; }
            if (s.Awards.Count == 0) Ui.Label(new Rect(right.x + 16, ry, right.width, 26), "Nenhum ainda.", 16, Ui.Muted, TextAnchor.MiddleLeft, Ui.FontKind.Body);
            ry += 40;
            Ui.Label(new Rect(right.x + 16, ry, right.width - 32, 26), "TRANSFERÊNCIAS", 18, Ui.Cyan);
            ry += 28;
            foreach (var h in st.TransferHistory.TakeLast(5)) { Ui.Label(new Rect(right.x + 16, ry, right.width - 32, 26), h, 16, Ui.Text, TextAnchor.MiddleLeft, Ui.FontKind.Body); ry += 26; }
        }
    }
}
