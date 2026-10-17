using System;
using System.Collections.Generic;
using System.Linq;
using NextXI.Core;
using NextXI.Core.Career;
using NextXI.Core.Data;
using NextXI.Core.Match;
using NextXI.Core.News;
using NextXI.Core.Persistence;
using NextXI.Core.World;
using UnityEngine;

namespace NextXI.Runtime
{
    public static class CareerScreens
    {
        public static IScreen Menu() => new CareerMenuScreen();
        public static IScreen Hub(CareerHubScreen.Tab tab = CareerHubScreen.Tab.Home, string notice = null) => new CareerHubScreen(tab, notice);

        public static string PosName(Position p)
        {
            switch (p)
            {
                case Position.GK: return "GOL"; case Position.CB: return "ZAG"; case Position.LB: return "LE"; case Position.RB: return "LD";
                case Position.LWB: return "ALE"; case Position.RWB: return "ALD"; case Position.CDM: return "VOL"; case Position.CM: return "MC";
                case Position.CAM: return "MEI"; case Position.LM: return "ME"; case Position.RM: return "MD"; case Position.LW: return "PE";
                case Position.RW: return "PD"; case Position.CF: return "SA"; default: return "ATA";
            }
        }

        public static void Header(float W, string title, string subtitle = null)
        {
            Ui.Label(new Rect(40, 18, W - 80, 56), title, 44, Ui.Text, TextAnchor.MiddleLeft, Ui.FontKind.DisplayItalic);
            if (subtitle != null) Ui.Label(new Rect(42, 70, W - 80, 26), subtitle, 18, Ui.Muted, TextAnchor.MiddleLeft, Ui.FontKind.Body);
        }
    }

    // ====================================================================== saves list

    public sealed class CareerMenuScreen : IScreen
    {
        private List<CareerSlotInfo> _slots;
        private string _confirmDelete;
        private string _error;

        public void OnEnter() { Refresh(); }
        public void OnExit() { }
        public void Tick(float dt) { }

        private void Refresh()
        {
            try { _slots = CareerSession.Store.List(); }
            catch (Exception e) { _slots = new List<CareerSlotInfo>(); _error = e.Message; }
        }

        public void Draw(float W, float H)
        {
            CareerScreens.Header(W, "CARREIRA", "Crie seu atleta ou continue uma carreira salva. Cada carreira é independente.");
            if (Ui.Button(new Rect(40, 110, 360, 70), "NOVA CARREIRA", true, 30)) GameApp.Instance.SetScreen(new CareerCreateScreen());
            float y = 200;
            if (_slots.Count == 0) Ui.Label(new Rect(40, y, 700, 40), "Nenhuma carreira salva.", 22, Ui.Muted);
            foreach (var s in _slots.Take(5))
            {
                var r = new Rect(40, y, Mathf.Min(W - 80, 900), 76);
                Ui.PanelBox(r, true);
                if (s.Corrupted)
                {
                    Ui.Label(new Rect(r.x + 18, r.y, r.width - 200, r.height), $"Arquivo danificado ({s.Id}): {s.Error}", 18, Ui.Bad, TextAnchor.MiddleLeft, Ui.FontKind.Body, true);
                }
                else
                {
                    Ui.Label(new Rect(r.x + 18, r.y + 6, r.width - 300, 36), s.AthleteName.ToUpperInvariant(), 28, Ui.Text);
                    Ui.Label(new Rect(r.x + 18, r.y + 40, r.width - 300, 28), $"{s.ClubName} · Temporada {s.Season} · Dia {s.Day} · GER {s.Overall}", 18, Ui.Muted, TextAnchor.MiddleLeft, Ui.FontKind.Body);
                    if (Ui.Button(new Rect(r.xMax - 260, r.y + 12, 130, 52), "JOGAR", true, 22))
                    {
                        var st = CareerSession.Load(s.Id, out var err);
                        if (CareerSession.Current != null && (st == LoadStatus.Ok || st == LoadStatus.RecoveredFromBackup))
                            GameApp.Instance.SetScreen(CareerScreens.Hub(CareerHubScreen.Tab.Home, st == LoadStatus.RecoveredFromBackup ? "Salvamento recuperado da cópia de segurança." : null));
                        else _error = "Não foi possível carregar: " + err;
                    }
                }
                bool confirm = _confirmDelete == s.Id;
                if (Ui.Button(new Rect(r.xMax - 120, r.y + 12, 104, 52), confirm ? "CONFIRMAR" : "APAGAR", false, 18))
                {
                    if (confirm) { CareerSession.Store.Delete(s.Id); _confirmDelete = null; Refresh(); break; }
                    _confirmDelete = s.Id;
                }
                y += 86;
            }
            if (_error != null) Ui.Label(new Rect(40, H - 140, W - 80, 30), _error, 18, Ui.Bad, TextAnchor.MiddleLeft, Ui.FontKind.Body);
            if (Ui.Button(new Rect(40, H - 90, 240, 60), "VOLTAR")) GameApp.Instance.SetScreen(new MainMenuScreen());
        }
    }

    // ====================================================================== creation wizard

    public sealed class CareerCreateScreen : IScreen
    {
        private int _step;
        private readonly CreationOptions _o = new CreationOptions();
        private readonly List<DemoClubs.Def> _clubs = DemoClubs.Brazil.ToList();
        private int _club;
        private int _archIdx;
        private readonly Position[] _positions = { Position.ST, Position.CF, Position.LW, Position.RW, Position.CAM, Position.CM, Position.CDM, Position.LM, Position.RM, Position.CB, Position.LB, Position.RB, Position.GK };
        private int _posIdx;
        private string[] _nations = { "Brasil (fictício)", "Portugal (fictício)", "Argentina (fictício)", "Uruguai (fictício)", "Espanha (fictício)", "França (fictício)", "Nigéria (fictício)", "Japão (fictício)" };
        private int _nat;
        private int _existing;
        private List<PlayerProfile> _catalogue;
        private static readonly string[] Steps = { "ORIGEM", "POSIÇÃO E ESTILO", "APARÊNCIA", "CLUBE", "IDENTIDADE", "CONFIRMAR" };

        public void OnEnter()
        {
            _o.Seed = (ulong)DateTime.UtcNow.Ticks;
            _o.Appearance = new Appearance { SkinTone = 0.45f, Hair = HairStyle.Short, HairColor = 0.1f, Build = 0.5f };
        }

        public void OnExit() { GameApp.Instance.ClearPreview(); }
        public void Tick(float dt) { }

        private PlayerProfile PreviewProfile() => new PlayerProfile
        {
            Id = "preview", FirstName = _o.FirstName, LastName = _o.LastName, HeightCm = _o.HeightCm, WeightKg = _o.WeightKg, Position = _o.Position,
            Appearance = _o.Appearance, ShirtNumber = _o.ShirtNumber > 0 ? _o.ShirtNumber : 10, PreferredFoot = _o.Foot
        };

        public void Draw(float W, float H)
        {
            CareerScreens.Header(W, "NOVA CARREIRA", $"Passo {_step + 1} de {Steps.Length}: {Steps[_step]}");
            if (_step >= 1 && _step <= 4 && _o.Origin != CareerOrigin.ExistingAthlete)
                GameApp.Instance.SetPreviewAthlete(PreviewProfile(), DemoClubs.Kit(_clubs[_club]));
            else GameApp.Instance.ClearPreview();

            var panel = new Rect(40, 108, Mathf.Min(W * 0.55f, 700), H - 210);
            Ui.PanelBox(panel);
            float x = panel.x + 22, w = panel.width - 44, y = panel.y + 18;
            switch (_step)
            {
                case 0: DrawOrigin(x, y, w); break;
                case 1: DrawPosition(x, y, w); break;
                case 2: DrawAppearance(x, y, w); break;
                case 3: DrawClub(x, y, w); break;
                case 4: DrawIdentity(x, y, w); break;
                default: DrawConfirm(x, y, w); break;
            }
            if (Ui.Button(new Rect(40, H - 90, 220, 60), _step == 0 ? "CANCELAR" : "VOLTAR"))
            {
                if (_step == 0) GameApp.Instance.SetScreen(CareerScreens.Menu());
                else _step--;
            }
            bool last = _step == Steps.Length - 1;
            if (Ui.Button(new Rect(panel.xMax - 260, H - 96, 260, 70), last ? "COMEÇAR" : "PRÓXIMO", true, 30))
            {
                if (_o.Origin == CareerOrigin.ExistingAthlete && _step == 0) _step = Steps.Length - 1;
                else if (!last) _step++;
                else Create();
            }
        }

        private void DrawOrigin(float x, float y, float w)
        {
            string[] names = { "CATEGORIA DE BASE", "JOVEM PROMOVIDO", "PROFISSIONAL", "ATLETA CADASTRADO" };
            string[] desc =
            {
                "17 anos, começa na base. Precisa convencer nos treinos para subir ao profissional. Maior potencial.",
                "18 anos, já no elenco principal como opção de rotação. Equilíbrio entre potencial e minutos.",
                "22 anos, nível de titular. Menos margem de evolução, cobrança desde o início.",
                "Assuma um atleta já cadastrado no universo de demonstração (fictício)."
            };
            for (int i = 0; i < 4; i++)
            {
                var r = new Rect(x, y + i * 100, w, 90);
                bool sel = (int)_o.Origin == i;
                Ui.Round(r, sel ? new Color(Ui.Accent.r, Ui.Accent.g, Ui.Accent.b, 0.25f) : Ui.Panel);
                Ui.Round(r, sel ? Ui.Accent : Ui.Line, true);
                Ui.Label(new Rect(r.x + 16, r.y + 6, r.width - 32, 34), names[i], 26, Ui.Text);
                Ui.Label(new Rect(r.x + 16, r.y + 38, r.width - 32, 48), desc[i], 17, Ui.Muted, TextAnchor.UpperLeft, Ui.FontKind.Body, true);
                if (GUI.Button(Ui.P(r), GUIContent.none, GUIStyle.none)) { _o.Origin = (CareerOrigin)i; AudioService.Instance?.PlayUi(UiSound.Click); }
            }
            if (_o.Origin == CareerOrigin.ExistingAthlete)
            {
                if (_catalogue == null)
                    _catalogue = DemoClubs.Brazil.SelectMany(d => DemoClubs.Squad(d)).Where(p => p.Position != Position.GK).OrderByDescending(p => p.Potential - p.Overall).Take(12).ToList();
                var p = _catalogue[_existing % _catalogue.Count];
                var r = new Rect(x, y + 410, w, 56);
                Ui.PanelBox(r, true);
                Ui.Label(new Rect(r.x + 70, r.y, r.width - 140, r.height), $"{p.DisplayName} · {CareerScreens.PosName(p.Position)} · GER {p.Overall} · {DemoClubs.Find(p.ClubId)?.Name}", 18, Ui.Text, TextAnchor.MiddleCenter, Ui.FontKind.Body);
                if (Ui.Button(new Rect(r.x + 6, r.y + 6, 56, 44), "<", false, 22)) _existing = (_existing + _catalogue.Count - 1) % _catalogue.Count;
                if (Ui.Button(new Rect(r.xMax - 62, r.y + 6, 56, 44), ">", false, 22)) _existing = (_existing + 1) % _catalogue.Count;
                _o.ExistingPlayerId = p.Id;
            }
        }

        private void DrawPosition(float x, float y, float w)
        {
            Ui.Label(new Rect(x, y, w, 28), "POSIÇÃO", 20, Ui.Muted);
            float bw = w / 7f;
            for (int i = 0; i < _positions.Length; i++)
            {
                var r = new Rect(x + (i % 7) * bw + 2, y + 32 + (i / 7) * 54, bw - 4, 48);
                if (Ui.Chip(r, CareerScreens.PosName(_positions[i]), i == _posIdx, 18)) { _posIdx = i; _archIdx = 0; }
            }
            _o.Position = _positions[_posIdx];
            var archs = Archetype.ForPosition(_o.Position).ToList();
            if (archs.Count == 0) archs = Archetype.All;
            _archIdx = Mathf.Clamp(_archIdx, 0, archs.Count - 1);
            y += 150;
            Ui.Label(new Rect(x, y, w, 28), "ESTILO DE JOGO (ARQUÉTIPO)", 20, Ui.Muted);
            for (int i = 0; i < archs.Count; i++)
            {
                var a = archs[i];
                var r = new Rect(x, y + 32 + i * 86, w, 78);
                bool sel = i == _archIdx;
                Ui.Round(r, sel ? new Color(Ui.Accent.r, Ui.Accent.g, Ui.Accent.b, 0.22f) : Ui.Panel);
                Ui.Round(r, sel ? Ui.Accent : Ui.Line, true);
                Ui.Label(new Rect(r.x + 14, r.y + 4, r.width - 28, 32), a.Name.ToUpperInvariant(), 24, Ui.Text);
                Ui.Label(new Rect(r.x + 14, r.y + 34, r.width - 28, 40), a.Description + "  Primeira habilidade: " + CareerManager.PerkName(a.PerkLadder[0][0]), 16, Ui.Muted, TextAnchor.UpperLeft, Ui.FontKind.Body, true);
                if (GUI.Button(Ui.P(r), GUIContent.none, GUIStyle.none)) { _archIdx = i; AudioService.Instance?.PlayUi(UiSound.Click); }
            }
            _o.ArchetypeId = archs[_archIdx].Id;
            float fy = y + 40 + archs.Count * 86;
            _o.Foot = Ui.Segmented(new Rect(x, fy, w * 0.6f, 48), _o.Foot == Foot.Right ? 0 : 1, new[] { "DESTRO", "CANHOTO" }, 18) == 0 ? Foot.Right : Foot.Left;
        }

        private void DrawAppearance(float x, float y, float w)
        {
            var a = _o.Appearance;
            float row = 52;
            _o.HeightCm = Mathf.RoundToInt(Mathf.Lerp(162, 200, Ui.Slider(new Rect(x, y, w, 46), Mathf.InverseLerp(162, 200, _o.HeightCm), "Altura", _o.HeightCm + " cm"))); y += row;
            // Weight constrained to a healthy athletic BMI range for the chosen height.
            float hM = _o.HeightCm / 100f;
            int minW = Mathf.RoundToInt(20.5f * hM * hM), maxW = Mathf.RoundToInt(27f * hM * hM);
            _o.WeightKg = Mathf.Clamp(_o.WeightKg, minW, maxW);
            _o.WeightKg = Mathf.RoundToInt(Mathf.Lerp(minW, maxW, Ui.Slider(new Rect(x, y, w, 46), Mathf.InverseLerp(minW, maxW, _o.WeightKg), "Peso", _o.WeightKg + " kg"))); y += row;
            a.Build = Ui.Slider(new Rect(x, y, w, 46), a.Build, "Constituição"); y += row;
            a.SkinTone = Ui.Slider(new Rect(x, y, w, 46), a.SkinTone, "Tom de pele"); y += row;
            a.FaceWidth = Ui.Slider(new Rect(x, y, w, 46), a.FaceWidth, "Largura do rosto"); y += row;
            a.FaceLength = Ui.Slider(new Rect(x, y, w, 46), a.FaceLength, "Comprimento do rosto"); y += row;
            a.HairColor = Ui.Slider(new Rect(x, y, w, 46), a.HairColor, "Cor do cabelo"); y += row;
            string[] hair = { "MÁQUINA", "CURTO", "TOPETE", "MÉDIO", "CACHEADO", "BLACK", "COQUE", "CARECA" };
            a.Hair = (HairStyle)Ui.Segmented(new Rect(x, y, w, 44), (int)a.Hair, hair, 14); y += row;
            string[] beard = { "SEM", "RALA", "CURTA", "CHEIA", "CAVANHAQUE" };
            a.Beard = (BeardStyle)Ui.Segmented(new Rect(x, y, w, 44), (int)a.Beard, beard, 15); y += row;
            string[] boots = { "PRETA", "BRANCA", "LARANJA", "AZUL", "AMARELA", "ROSA" };
            a.BootStyle = Ui.Segmented(new Rect(x, y, w, 44), a.BootStyle, boots, 15); y += row;
            a.LongSleeves = Ui.Toggle(new Rect(x, y, w, 44), a.LongSleeves, "Manga longa");
            a.Clamp();
        }

        private void DrawClub(float x, float y, float w)
        {
            Ui.Label(new Rect(x, y, w, 28), "ESCOLHA SEU CLUBE (LIGA NACIONAL — FICTÍCIA)", 20, Ui.Muted);
            for (int i = 0; i < _clubs.Count; i++)
            {
                var c = _clubs[i];
                var r = new Rect(x + (i % 2) * (w * 0.5f + 4), y + 34 + (i / 2) * 64, w * 0.5f - 4, 58);
                bool sel = i == _club;
                Ui.Round(r, sel ? new Color(Ui.Accent.r, Ui.Accent.g, Ui.Accent.b, 0.22f) : Ui.Panel);
                Ui.Round(r, sel ? Ui.Accent : Ui.Line, true);
                Ui.Round(new Rect(r.x + 10, r.y + 12, 14, 34), Conv.Hex(c.Shirt));
                Ui.Label(new Rect(r.x + 32, r.y + 2, r.width - 40, 30), c.Name.ToUpperInvariant(), 20, Ui.Text);
                Ui.Label(new Rect(r.x + 32, r.y + 30, r.width - 40, 24), $"Força {c.Strength} · {c.Stadium}", 15, Ui.Muted, TextAnchor.MiddleLeft, Ui.FontKind.Body);
                if (GUI.Button(Ui.P(r), GUIContent.none, GUIStyle.none)) { _club = i; AudioService.Instance?.PlayUi(UiSound.Click); }
            }
            _o.ClubId = _clubs[_club].Id;
            Ui.Label(new Rect(x, y + 34 + 5 * 64 + 6, w, 46), "Clubes mais fortes oferecem menos minutos no começo; clubes menores dão espaço, mas menos visibilidade.", 16, Ui.Muted, TextAnchor.UpperLeft, Ui.FontKind.Body, true);
        }

        private void DrawIdentity(float x, float y, float w)
        {
            Ui.Label(new Rect(x, y, w, 26), "NOME", 18, Ui.Muted);
            _o.FirstName = TextField(new Rect(x, y + 28, w * 0.48f, 50), _o.FirstName, 16);
            _o.LastName = TextField(new Rect(x + w * 0.52f, y + 28, w * 0.48f, 50), _o.LastName, 18);
            Ui.Label(new Rect(x, y + 90, w, 26), "NOME NA CAMISA (OPCIONAL)", 18, Ui.Muted);
            _o.KnownAs = TextField(new Rect(x, y + 118, w * 0.48f, 50), _o.KnownAs ?? "", 16);
            Ui.Label(new Rect(x + w * 0.52f, y + 90, w * 0.48f, 26), "NÚMERO", 18, Ui.Muted);
            string num = TextField(new Rect(x + w * 0.52f, y + 118, 120, 50), _o.ShirtNumber > 0 ? _o.ShirtNumber.ToString() : "", 2);
            _o.ShirtNumber = int.TryParse(num, out var n) ? Mathf.Clamp(n, 1, 99) : 0;
            Ui.Label(new Rect(x, y + 190, w, 26), "NACIONALIDADE (UNIVERSO FICTÍCIO)", 18, Ui.Muted);
            float bw = w / 4f;
            for (int i = 0; i < _nations.Length; i++)
                if (Ui.Chip(new Rect(x + (i % 4) * bw + 2, y + 220 + (i / 4) * 52, bw - 4, 46), _nations[i].Replace(" (fictício)", "").ToUpperInvariant(), _nat == i, 15)) _nat = i;
            _o.Nationality = _nations[_nat];
        }

        private static string TextField(Rect r, string value, int maxLen)
        {
            Ui.Round(r, Ui.PanelHi);
            Ui.Round(r, Ui.Line, true);
            var st = Ui.Style(24, Ui.Text, TextAnchor.MiddleLeft);
            st.padding = new RectOffset(Mathf.RoundToInt(12 * Ui.S), 8, 0, 0);
            st.normal.background = null;
            var v = GUI.TextField(Ui.P(r), value ?? "", maxLen, st);
            return v;
        }

        private void DrawConfirm(float x, float y, float w)
        {
            if (_o.Origin == CareerOrigin.ExistingAthlete)
            {
                Ui.Label(new Rect(x, y, w, 300), "Você assumirá o atleta cadastrado selecionado, mantendo clube, contrato e atributos (dados fictícios de demonstração).", 20, Ui.Text, TextAnchor.UpperLeft, Ui.FontKind.Body, true);
                return;
            }
            var arch = Archetype.Get(_o.ArchetypeId);
            string[] lines =
            {
                $"{_o.FirstName} {_o.LastName}".ToUpperInvariant(),
                $"{CareerScreens.PosName(_o.Position)} · {arch.Name} · {(_o.Foot == Foot.Right ? "destro" : "canhoto")}",
                $"{_o.HeightCm} cm · {_o.WeightKg} kg · {_o.Nationality}",
                $"Clube: {_clubs[_club].Name}",
                $"Origem: {(_o.Origin == CareerOrigin.Academy ? "categoria de base" : _o.Origin == CareerOrigin.PromotedYouth ? "jovem promovido" : "profissional")}",
            };
            for (int i = 0; i < lines.Length; i++) Ui.Label(new Rect(x, y + i * 40, w, 38), lines[i], i == 0 ? 34 : 22, i == 0 ? Ui.Accent : Ui.Text);
            Ui.Label(new Rect(x, y + 230, w, 90), "O jogo salva automaticamente após cada partida, treino e decisão. Você pode ter várias carreiras independentes.", 17, Ui.Muted, TextAnchor.UpperLeft, Ui.FontKind.Body, true);
        }

        private void Create()
        {
            if (string.IsNullOrWhiteSpace(_o.FirstName)) _o.FirstName = "Novo";
            if (string.IsNullOrWhiteSpace(_o.LastName)) _o.LastName = "Atleta";
            GameApp.Instance.RunWithLoading("CRIANDO O MUNDO DO FUTEBOL", () =>
            {
                var s = CareerManager.Create(_o);
                CareerSession.Begin(s);
                GameApp.Instance.SetScreen(CareerScreens.Hub(CareerHubScreen.Tab.Home, "Carreira criada. Boa sorte!"));
            });
        }
    }
}
