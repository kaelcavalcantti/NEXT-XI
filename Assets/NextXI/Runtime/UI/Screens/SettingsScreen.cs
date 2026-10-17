using System;
using NextXI.Core.Match;
using NextXI.Core.Settings;
using UnityEngine;

namespace NextXI.Runtime
{
    /// <summary>Settings UI (graphics, audio, controls, camera, gameplay). Used from the menu and pause.</summary>
    public static class SettingsScreen
    {
        private static readonly string[] Tabs = { "GRÁFICOS", "ÁUDIO", "CONTROLES", "CÂMERA", "PARTIDA" };

        /// <summary>Draws the settings; returns true when something changed.</summary>
        public static bool Draw(float W, float H, ref int tab, GameSettings s, Action onBack)
        {
            bool changed = false;
            Ui.Label(new Rect(40, 24, W, 56), "CONFIGURAÇÕES", 48, Ui.Text, TextAnchor.MiddleLeft, Ui.FontKind.DisplayItalic);
            tab = Ui.Segmented(new Rect(40, 92, Mathf.Min(W - 80, 860), 52), tab, Tabs, 20);
            var panel = new Rect(40, 160, Mathf.Min(W - 80, 860), H - 260);
            Ui.PanelBox(panel);
            float x = panel.x + 24, w = panel.width - 48, y = panel.y + 18, row = 56;
            bool before = GUI.changed;
            GUI.changed = false;
            switch (tab)
            {
                case 0:
                {
                    var g = s.Graphics;
                    int prof = Ui.Segmented(new Rect(x, y, w, 50), (int)g.Profile, new[] { "ECONÔMICO", "EQUILIBRADO", "ALTO", "PERSONALIZADO" }, 18);
                    if (prof != (int)g.Profile) { if (prof < 3) g.ApplyProfile((QualityProfile)prof); else g.Profile = QualityProfile.Custom; changed = true; }
                    y += row + 10;
                    int fps = Ui.Segmented(new Rect(x, y, w * 0.5f, 46), g.TargetFps == 60 ? 1 : 0, new[] { "30 FPS", "60 FPS" }, 18);
                    if ((fps == 1 ? 60 : 30) != g.TargetFps) { g.TargetFps = fps == 1 ? 60 : 30; g.Profile = QualityProfile.Custom; changed = true; }
                    y += row;
                    float rs = Ui.Slider(new Rect(x, y, w, 48), Mathf.InverseLerp(0.5f, 1f, g.RenderScale), "Resolução de renderização", $"{g.RenderScale * 100:0}%");
                    if (GUI.changed) { g.RenderScale = Mathf.Lerp(0.5f, 1f, rs); g.Profile = QualityProfile.Custom; }
                    y += row;
                    bool sh = Ui.Toggle(new Rect(x, y, w, 48), g.Shadows, "Sombras");
                    if (sh != g.Shadows) { g.Shadows = sh; g.ShadowDistance = sh ? Math.Max(40, g.ShadowDistance) : 0; g.Profile = QualityProfile.Custom; changed = true; }
                    y += row;
                    int crowd = Ui.Segmented(new Rect(x, y, w, 46), g.CrowdDensity, new[] { "SEM PÚBLICO", "BAIXO", "MÉDIO", "LOTADO" }, 18);
                    if (crowd != g.CrowdDensity) { g.CrowdDensity = crowd; g.Profile = QualityProfile.Custom; changed = true; }
                    y += row;
                    bool dyn = Ui.Toggle(new Rect(x, y, w, 48), g.DynamicResolution, "Resolução dinâmica");
                    if (dyn != g.DynamicResolution) { g.DynamicResolution = dyn; changed = true; }
                    y += row;
                    Ui.Label(new Rect(x, y, w, 40), "60 FPS depende do aparelho; o perfil Equilibrado mira 30 FPS estáveis.", 18, Ui.Muted, TextAnchor.MiddleLeft, Ui.FontKind.Body);
                    break;
                }
                case 1:
                {
                    var a = s.Audio;
                    a.Master = Ui.Slider(new Rect(x, y, w, 48), a.Master, "Volume geral"); y += row;
                    a.Music = Ui.Slider(new Rect(x, y, w, 48), a.Music, "Música"); y += row;
                    a.Ambience = Ui.Slider(new Rect(x, y, w, 48), a.Ambience, "Ambiente"); y += row;
                    a.Crowd = Ui.Slider(new Rect(x, y, w, 48), a.Crowd, "Torcida"); y += row;
                    a.Effects = Ui.Slider(new Rect(x, y, w, 48), a.Effects, "Efeitos sonoros"); y += row;
                    a.Commentary = Ui.Slider(new Rect(x, y, w, 48), a.Commentary, "Narração (sem recursos licenciados ainda)"); y += row;
                    a.Interface = Ui.Slider(new Rect(x, y, w, 48), a.Interface, "Interface"); y += row;
                    a.Muted = Ui.Toggle(new Rect(x, y, w, 48), a.Muted, "Silenciar tudo");
                    break;
                }
                case 2:
                {
                    var c = s.Controls;
                    c.ButtonScale = Mathf.Lerp(0.7f, 1.5f, Ui.Slider(new Rect(x, y, w, 48), Mathf.InverseLerp(0.7f, 1.5f, c.ButtonScale), "Tamanho dos botões", $"{c.ButtonScale * 100:0}%")); y += row;
                    c.StickScale = Mathf.Lerp(0.7f, 1.5f, Ui.Slider(new Rect(x, y, w, 48), Mathf.InverseLerp(0.7f, 1.5f, c.StickScale), "Tamanho do analógico", $"{c.StickScale * 100:0}%")); y += row;
                    c.Sensitivity = Ui.Slider(new Rect(x, y, w, 48), c.Sensitivity, "Sensibilidade"); y += row;
                    c.PassAssist = Ui.Slider(new Rect(x, y, w, 48), c.PassAssist, "Assistência de passe"); y += row;
                    c.ShotAssist = Ui.Slider(new Rect(x, y, w, 48), c.ShotAssist, "Assistência de chute"); y += row;
                    c.Vibration = Ui.Toggle(new Rect(x, y, w * 0.48f, 48), c.Vibration, "Vibração");
                    c.FloatingStick = Ui.Toggle(new Rect(x + w * 0.52f, y, w * 0.48f, 48), c.FloatingStick, "Analógico flutuante"); y += row;
                    c.LeftHanded = Ui.Toggle(new Rect(x, y, w * 0.48f, 48), c.LeftHanded, "Modo canhoto (espelhar)");
                    c.HighContrastButtons = Ui.Toggle(new Rect(x + w * 0.52f, y, w * 0.48f, 48), c.HighContrastButtons, "Alto contraste"); y += row;
                    c.ShowButtonLabels = Ui.Toggle(new Rect(x, y, w * 0.48f, 48), c.ShowButtonLabels, "Rótulos nos botões");
                    if (Ui.Button(new Rect(x + w * 0.52f, y + 2, w * 0.48f, 44), "RESTAURAR POSIÇÕES", false, 18))
                    {
                        c.ButtonOffsetsX = new float[8]; c.ButtonOffsetsY = new float[8]; changed = true;
                    }
                    break;
                }
                case 3:
                {
                    var c = s.Camera;
                    int m = Ui.Segmented(new Rect(x, y, w, 50), (int)c.Mode, new[] { "TRANSMISSÃO", "PRÓXIMA", "JOGADOR", "DINÂMICA" }, 18);
                    if (m != (int)c.Mode) { c.Mode = (CameraMode)m; changed = true; }
                    y += row + 6;
                    c.Height = Ui.Slider(new Rect(x, y, w, 48), c.Height, "Altura"); y += row;
                    c.Distance = Ui.Slider(new Rect(x, y, w, 48), c.Distance, "Distância"); y += row;
                    c.Zoom = Ui.Slider(new Rect(x, y, w, 48), c.Zoom, "Zoom (campo de visão)"); y += row;
                    c.Angle = Ui.Slider(new Rect(x, y, w, 48), c.Angle, "Ângulo"); y += row;
                    c.Smoothing = Ui.Slider(new Rect(x, y, w, 48), c.Smoothing, "Sensibilidade do acompanhamento"); y += row;
                    c.AutoReplays = Ui.Toggle(new Rect(x, y, w, 48), c.AutoReplays, "Replays automáticos de gol");
                    break;
                }
                default:
                {
                    var md = s.Match;
                    Ui.Label(new Rect(x, y, w, 30), "Duração por tempo (minutos reais)", 20, Ui.Muted); y += 32;
                    var opts = MatchSettings.HalfLengthOptions;
                    int idx = Array.IndexOf(opts, md.MinutesPerHalf);
                    var labels = Array.ConvertAll(opts, o => o + " MIN");
                    int ni = Ui.Segmented(new Rect(x, y, w, 48), Math.Max(0, idx), labels, 18);
                    if (ni != idx) { md.MinutesPerHalf = opts[ni]; changed = true; }
                    y += row + 6;
                    Ui.Label(new Rect(x, y, w, 30), "Dificuldade", 20, Ui.Muted); y += 32;
                    int d = Ui.Segmented(new Rect(x, y, w, 48), (int)md.Difficulty, new[] { "AMADOR", "SEMIPRO", "PRO", "CLASSE MUNDIAL", "LENDÁRIO" }, 16);
                    if (d != (int)md.Difficulty) { md.Difficulty = (Difficulty)d; changed = true; }
                    y += row + 6;
                    md.GameSpeed = Mathf.Lerp(0.85f, 1.15f, Ui.Slider(new Rect(x, y, w, 48), Mathf.InverseLerp(0.85f, 1.15f, md.GameSpeed), "Velocidade de jogo", $"{md.GameSpeed:0.00}x")); y += row;
                    md.Offside = Ui.Toggle(new Rect(x, y, w * 0.48f, 48), md.Offside, "Impedimento");
                    md.Fouls = Ui.Toggle(new Rect(x + w * 0.52f, y, w * 0.48f, 48), md.Fouls, "Faltas"); y += row;
                    md.Injuries = Ui.Toggle(new Rect(x, y, w * 0.48f, 48), md.Injuries, "Lesões");
                    md.Fatigue = Ui.Toggle(new Rect(x + w * 0.52f, y, w * 0.48f, 48), md.Fatigue, "Fadiga"); y += row;
                    md.SkipCutscenes = Ui.Toggle(new Rect(x, y, w, 48), md.SkipCutscenes, "Pular apresentações");
                    break;
                }
            }
            changed |= GUI.changed;
            GUI.changed = before || changed;
            if (Ui.Button(new Rect(40, H - 84, 260, 60), "VOLTAR", false, 24)) { s.Validate(); onBack?.Invoke(); changed = true; }
            return changed;
        }
    }
}
