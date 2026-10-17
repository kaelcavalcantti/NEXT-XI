using System;
using System.Collections.Generic;
using System.Linq;
using NextXI.Core;
using NextXI.Core.Match;
using NextXI.Core.World;
using UnityEngine;

namespace NextXI.Runtime
{
    public sealed class MainMenuScreen : IScreen
    {
        private float _t;
        public void OnEnter() { _t = 0; AudioService.Instance?.PlayMusic(true); }
        public void OnExit() { }
        public void Tick(float dt) { _t += dt; }

        public void Draw(float W, float H)
        {
            float k = Mathf.Clamp01(_t / 0.5f);
            Ui.Logo(new Rect(60, 60, 520, 96), k);
            Ui.Label(new Rect(64, 160, 600, 30), "CARREIRA DE JOGADOR  ·  FUTEBOL 3D", 22, new Color(Ui.Cyan.r, Ui.Cyan.g, Ui.Cyan.b, k), TextAnchor.MiddleLeft, Ui.FontKind.DisplayItalic);
            float x = 60, y = 230, w = 400, h = 66, gap = 78;
            if (Ui.Button(new Rect(x, y, w, h), "CARREIRA", true, 30)) GameApp.Instance.SetScreen(CareerScreens.Menu());
            if (Ui.Button(new Rect(x, y + gap, w, h), "PARTIDA RÁPIDA", false, 28)) GameApp.Instance.SetScreen(new QuickMatchScreen());
            if (Ui.Button(new Rect(x, y + gap * 2, w, h), "TREINAMENTO", false, 28)) GameApp.Instance.SetScreen(new TrainingMenuScreen(null));
            if (Ui.Button(new Rect(x, y + gap * 3, w, h), "CONFIGURAÇÕES", false, 28)) GameApp.Instance.SetScreen(new SettingsMenuScreen(() => GameApp.Instance.SetScreen(new MainMenuScreen())));
            if (Ui.Button(new Rect(x, y + gap * 4, w, h), "CRÉDITOS E LICENÇAS", false, 24)) GameApp.Instance.SetScreen(new CreditsScreen());
            Ui.Label(new Rect(W - 420, H - 46, 400, 30), "v" + GameApp.Instance.Version + "  ·  build de desenvolvimento", 16, Ui.Muted, TextAnchor.MiddleRight, Ui.FontKind.Body);
        }
    }

    public sealed class SettingsMenuScreen : IScreen
    {
        private int _tab;
        private readonly Action _back;
        public SettingsMenuScreen(Action back) { _back = back; }
        public void OnEnter() { }
        public void OnExit() { GameApp.Instance.SaveSettings(); }
        public void Tick(float dt) { }
        public void Draw(float W, float H)
        {
            if (SettingsScreen.Draw(W, H, ref _tab, GameApp.Instance.Settings, _back)) GameApp.Instance.ApplySettings();
        }
    }

    public sealed class CreditsScreen : IScreen
    {
        public void OnEnter() { }
        public void OnExit() { }
        public void Tick(float dt) { }
        public void Draw(float W, float H)
        {
            Ui.Label(new Rect(60, 40, W, 60), "CRÉDITOS E LICENÇAS", 48, Ui.Text, TextAnchor.MiddleLeft, Ui.FontKind.DisplayItalic);
            var r = new Rect(60, 120, Mathf.Min(W - 120, 960), H - 230);
            Ui.PanelBox(r);
            Ui.Label(new Rect(r.x + 24, r.y + 18, r.width - 48, r.height - 36),
                "NEXT XI é um projeto original. Nenhum código, marca, modelo, som ou animação de jogos comerciais foi utilizado.\n\n" +
                "• Personagens, estádio, bola, texturas e animações: gerados proceduralmente pelo próprio jogo.\n" +
                "• Sons e música: sintetizados pelo próprio jogo (sem gravações de terceiros).\n" +
                "• Tipografia: Barlow e Barlow Condensed — The Barlow Project Authors, SIL Open Font License 1.1.\n" +
                "• Clubes, jogadores e competições: universo de DEMONSTRAÇÃO FICTÍCIO. Nomes e estatísticas não representam pessoas ou clubes reais.\n\n" +
                "Dados reais só serão incluídos a partir de fontes licenciadas, com procedência e data documentadas.",
                20, Ui.Text, TextAnchor.UpperLeft, Ui.FontKind.Body, true);
            if (Ui.Button(new Rect(60, H - 90, 260, 60), "VOLTAR")) GameApp.Instance.SetScreen(new MainMenuScreen());
        }
    }

    /// <summary>Quick match setup: teams, duration, difficulty, control mode.</summary>
    public sealed class QuickMatchScreen : IScreen
    {
        private readonly List<DemoClubs.Def> _clubs = DemoClubs.All.ToList();
        private int _home, _away = 1, _control;
        private int _side;

        public void OnEnter() { }
        public void OnExit() { }
        public void Tick(float dt) { }

        public void Draw(float W, float H)
        {
            var s = GameApp.Instance.Settings;
            Ui.Label(new Rect(60, 30, W, 60), "PARTIDA RÁPIDA", 48, Ui.Text, TextAnchor.MiddleLeft, Ui.FontKind.DisplayItalic);
            Ui.Label(new Rect(60, 84, W, 26), "Universo de demonstração — clubes e atletas fictícios", 18, Ui.Muted, TextAnchor.MiddleLeft, Ui.FontKind.Body);
            DrawClubPicker(new Rect(60, 120, 420, 220), "MANDANTE", ref _home);
            DrawClubPicker(new Rect(W - 480, 120, 420, 220), "VISITANTE", ref _away);
            Ui.Label(new Rect(W * 0.5f - 60, 190, 120, 80), "VS", 64, Ui.Accent, TextAnchor.MiddleCenter, Ui.FontKind.DisplayItalic);

            float y = 360;
            Ui.Label(new Rect(60, y, 300, 30), "VOCÊ CONTROLA", 20, Ui.Muted);
            _side = Ui.Segmented(new Rect(60, y + 32, 420, 50), _side, new[] { "MANDANTE", "VISITANTE", "ASSISTIR" }, 18);
            Ui.Label(new Rect(W - 480, y, 300, 30), "MODO DE CONTROLE", 20, Ui.Muted);
            _control = Ui.Segmented(new Rect(W - 480, y + 32, 420, 50), _control, new[] { "TIME (TROCA)", "UM ATLETA" }, 18);

            y += 100;
            Ui.Label(new Rect(60, y, 300, 30), "DURAÇÃO POR TEMPO", 20, Ui.Muted);
            var opts = MatchSettings.HalfLengthOptions;
            int idx = Math.Max(0, Array.IndexOf(opts, s.Match.MinutesPerHalf));
            idx = Ui.Segmented(new Rect(60, y + 32, 560, 50), idx, opts.Select(o => o + " MIN").ToArray(), 18);
            s.Match.MinutesPerHalf = opts[idx];
            Ui.Label(new Rect(W - 480, y, 300, 30), "DIFICULDADE", 20, Ui.Muted);
            s.Match.Difficulty = (Difficulty)Ui.Segmented(new Rect(W - 480, y + 32, 420, 50), (int)s.Match.Difficulty, new[] { "AMA", "SEMI", "PRO", "CM", "LEN" }, 18);

            if (Ui.Button(new Rect(60, H - 90, 240, 60), "VOLTAR")) GameApp.Instance.SetScreen(new MainMenuScreen());
            if (Ui.Button(new Rect(W - 380, H - 96, 320, 70), "JOGAR", true, 34)) Start();
        }

        private void DrawClubPicker(Rect r, string label, ref int index)
        {
            Ui.PanelBox(r, true);
            var c = _clubs[index];
            Ui.Label(new Rect(r.x + 20, r.y + 10, r.width, 28), label, 18, Ui.Muted);
            Ui.Round(new Rect(r.x + 20, r.y + 50, 70, 90), Conv.Hex(c.Shirt));
            Ui.Round(new Rect(r.x + 20, r.y + 110, 70, 30), Conv.Hex(c.Shorts));
            Ui.Label(new Rect(r.x + 106, r.y + 46, r.width - 120, 44), c.Name.ToUpperInvariant(), 30, Ui.Text, TextAnchor.MiddleLeft, Ui.FontKind.Display, true);
            Ui.Label(new Rect(r.x + 106, r.y + 92, r.width - 120, 26), $"{c.City} · {c.Country}", 16, Ui.Muted, TextAnchor.MiddleLeft, Ui.FontKind.Body);
            Ui.Label(new Rect(r.x + 106, r.y + 118, r.width - 120, 26), $"FORÇA {c.Strength}  ·  {c.Formation}", 18, Ui.Cyan);
            if (Ui.Button(new Rect(r.x + 20, r.yMax - 64, 80, 50), "<", false, 26)) index = (index + _clubs.Count - 1) % _clubs.Count;
            if (Ui.Button(new Rect(r.xMax - 100, r.yMax - 64, 80, 50), ">", false, 26)) index = (index + 1) % _clubs.Count;
        }

        private void Start()
        {
            if (_home == _away) _away = (_away + 1) % _clubs.Count;
            var hd = _clubs[_home]; var ad = _clubs[_away];
            var homeKit = DemoClubs.Kit(hd);
            var awayKit = DemoClubs.Clash(hd.Shirt, ad.Shirt) ? DemoClubs.AwayKit(ad) : DemoClubs.Kit(ad);
            var home = TeamSheetBuilder.Build(hd.Id, hd.Name, hd.Short, DemoClubs.Squad(hd), hd.Formation, homeKit);
            var away = TeamSheetBuilder.Build(ad.Id, ad.Name, ad.Short, DemoClubs.Squad(ad), ad.Formation, awayKit);
            var settings = GameApp.Instance.Settings.ToMatchSettings((ulong)DateTime.UtcNow.Ticks);
            settings.ControlMode = _control == 0 ? ControlMode.TeamSwitching : ControlMode.PlayerLock;
            int userTeam = _side == 2 ? -1 : _side;
            string locked = null;
            if (settings.ControlMode == ControlMode.PlayerLock && userTeam >= 0)
            {
                var sheet = userTeam == 0 ? home : away;
                locked = sheet.StartingXI[sheet.StartingXI.Count - 1].Id; // the centre-forward
            }
            GameApp.Instance.SaveSettings();
            GameApp.Instance.StartMatch(new MatchSetup
            {
                Home = home, Away = away, Settings = settings, UserTeam = userTeam, LockedPlayerProfileId = locked,
                Competition = "AMISTOSO", StadiumName = hd.Stadium.ToUpperInvariant(),
                OnFinished = r => GameApp.Instance.SetScreen(new QuickMatchScreen()),
                OnQuit = () => GameApp.Instance.SetScreen(new QuickMatchScreen())
            });
        }
    }
}
