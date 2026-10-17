# Arquitetura técnica — NEXT XI

## Decisões

| Tema | Decisão | Motivo |
|---|---|---|
| Motor | Unity 6 (6000.0 LTS), C# 9, URP | Pedido do projeto; nenhuma incompatibilidade encontrada. |
| Simulação | Núcleo em C# puro, sem dependência da Unity (`noEngineReferences`) | Testável fora do Editor, determinístico, reaproveitável (partida 3D, treinos, simulação de mundo). |
| Passo | Fixo de 60 Hz com interpolação na renderização | Física estável e independente do FPS (30/60). |
| Determinismo | `Rng` xorshift com semente | Mesma semente ⇒ mesma partida (testes, replays, depuração). |
| UI | IMGUI com kit visual próprio (`Ui.cs`) | Verificável sem o Editor; escalonamento explícito (texto nítido); sem assets de UI. |
| Arte | Procedural (malhas, texturas, animação, som) | Nenhum recurso licenciado disponível; nada de terceiros. Pontos de troca documentados em `ASSETS.md`. |
| Dados | JSON (Newtonsoft, pacote oficial da Unity) | Pacotes de dados importáveis sem alterar código. |
| Salvamento | Envelope versionado + SHA-256 + escrita atômica + `.bak` | Corrupção detectada e recuperada; migrações por versão. |

## Módulos

```
Assets/NextXI/
  Core/                         NextXI.Core.asmdef (sem UnityEngine)
    Math/        Vec3, MathUtil, Rng
    Match/       PitchSpec, BallPhysics, KickSolver, MatchClock, MatchSettings, MatchPlayer, MatchTeam,
                 Formation, MatchEngine(.Ball/.User), ShotModel, MatchEvents, MatchReport, TeamSheetBuilder
      Ai/        MatchAi (forma tática, sem bola, portador), .Keeper, .Restarts (bolas paradas, comemoração, técnico)
    Data/        PlayerAttributes (31 atributos 0..99), PlayerProfile/Appearance/Traits, PlayerGenerator (fictício)
    World/       WorldModel (clubes, contratos, competições, tabela), WorldBuilder (demo, agenda, importação),
                 StatSimulator (detalhada/leve), DemoClubs
    Career/      Archetype, CareerState, CareerManager (+Systems), TrainingDrills, CareerStore
    News/        NewsGenerator (jornal + redes sociais)
    Persistence/ SaveSystem, IFileStore (Disk/Memory)
    Settings/    GameSettings (áudio, gráficos, câmera, controles, partida)
  Runtime/                      NextXI.Runtime.asmdef (Unity)
    App/         GameApp (bootstrap, telas, configurações, fundo do menu, prévia do atleta, treino), QualityService
    Match/       MatchController (partida 3D), TrainingController, PlayerView/BallView, CameraDirector, ReplaySystem
    Visual/      StadiumBuilder, MeshBuilder/MeshKit, ProcTex (texturas + fonte vetorial), Mat (materiais URP/Standard)
      Athlete/   AthleteBody (malha skinned), AthletePose (animação procedural), AthleteRig (SkinnedMeshRenderer)
    Input/       TouchControls (analógico flutuante, botões contextuais, potência, teclado)
    Audio/       Synth (DSP puro), AudioService (mixagem por categoria, torcida dinâmica)
    UI/          Ui (kit), Screens/ (menu, partida rápida, configurações, carreira, hub, créditos)
    Career/      CareerSession (carreira ativa, autosave, lançamento de partidas)
  Editor/        ProjectSetup (URP, materiais, Player Settings, cena), BuildScript (CLI)
  Tests/EditMode NextXI.Tests.EditMode.asmdef — os mesmos arquivos rodam com `dotnet test`
tools/           Projetos .NET de verificação (testes, simulação headless, compile-check, prévia geométrica/sonora)
```

## Fluxo de uma partida 3D

1. `CareerManager.PrepareMatch` (ou a tela de partida rápida) monta dois `TeamSheet` e decide titular/reserva.
2. `MatchController` cria o `MatchEngine`, o estádio, as `PlayerView` e a câmera.
3. A cada quadro: `TouchControls.Poll` → `PlayerCommand` (relativo à câmera) → `MatchEngine.Advance(dt)`
   (passos fixos) → eventos drenados para áudio/HUD/replay/torcida → `PlayerView.Apply` interpola as posições e
   resolve a pose (`AthletePose`) com o estado de ação do motor (o chute acontece no quadro de contato).
4. Fim de jogo → `MatchReport.FromEngine` → `CareerManager.ApplyUserMatch` (estatísticas, XP, confiança,
   objetivos, notícias) → autosave.

## Simulação do mundo em camadas

| Camada | Onde | Conteúdo |
|---|---|---|
| 3D completa | Partida do atleta | `MatchEngine` com controle do usuário |
| Detalhada | Liga e copa do atleta | `StatSimulator.SimulateDetailed`: escalações, Poisson ataque×defesa + mando, autores de gols/assistências por atributos, notas, cartões, lesões |
| Leve | Outras ligas | `StatSimulator.SimulateLight`: só o placar a partir da força dos times |

## Convenções do motor

- Unidades SI; origem no centro do campo; X = comprimento, Y = altura, Z = largura.
- O time 0 ataca +X no 1º tempo; `AttackDir(team)` dá o sentido atual; `AttackX` converte para o referencial ofensivo.
- Ângulo de orientação (`Facing`) = `atan2(z, x)`; `Conv.FacingToRotation` gera a rotação Unity.
- Eventos (`MatchEventLog`) são a única via de comunicação motor → apresentação.
- Botões são eventos de borda (`PlayerCommand`), consumidos no próximo passo fixo; potência = tempo segurado.

## Desempenho (alvos)

- Simulação: ~11 s de CPU para 10 partidas completas de 5 min/tempo em headless (≈ 0,03 ms por passo fixo).
- Atleta: ~3,5 mil vértices, 1 SkinnedMeshRenderer com 7 submalhas; malhas e materiais compartilhados por cache.
- Torcida: 3 materiais compartilhados (cartões animados por offset de UV), sem luzes extras (1 direcional com sombra).
- Perfis Econômico/Equilibrado/Alto + resolução dinâmica (`QualityService`). Nenhuma medição em aparelho foi feita ainda.
