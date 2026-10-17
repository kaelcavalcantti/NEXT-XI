# NEXT XI

Simulador 3D de carreira de jogador de futebol para celular (Unity 6 · C# · URP · Android primeiro, iOS depois).

> **Estado atual (honesto):** o código do jogo está implementado e testado fora do Unity, mas **ainda não foi aberto,
> executado nem compilado dentro do Unity Editor** — o ambiente em que foi desenvolvido não tinha o Unity instalado
> (download bloqueado pela rede). O motor de futebol, a carreira, o mundo e o salvamento rodam e têm 87 testes
> automatizados passando com `dotnet test`. A camada Unity (cena 3D, atletas, câmeras, toque, HUD, áudio, menus)
> compila contra as assemblies de referência da Unity, mas precisa ser validada no Editor. Veja
> [`Docs/PROGRESS.md`](Docs/PROGRESS.md) para o que está concluído, parcial e pendente.

## O que existe

- **Motor de futebol 11x11 contínuo** (`Assets/NextXI/Core/Match`): física da bola (arrasto, efeito Magnus,
  quique, rolagem, traves, rede), cinemática dos atletas baseada em atributos, posse e condução, chutes com
  janela de contato sincronizada com a animação, desarmes e carrinhos, goleiro (posicionamento, mergulho,
  espalmar, encaixar, saída, reposição), regras (gol, lateral, escanteio, tiro de meta, faltas, cartões, pênalti,
  impedimento configurável, acréscimos), IA tática por função, bolas paradas, substituições e técnico da IA.
- **Relógio virtual**: 3, 4, 5, 6, 8 ou 10 minutos reais por tempo; o jogo sempre tem 45 minutos virtuais por tempo.
- **Carreira** (`Core/Career`): criação do atleta (origem, posição, 13 arquétipos com habilidades que alteram o
  gameplay, aparência, clube), confiança do treinador e escalação, objetivos, treinos jogáveis, progressão,
  contratos, propostas, empréstimos, renovação, transferências da IA, demissões de treinadores, seleção,
  prêmios, títulos, virada de temporada e aposentadoria.
- **Mundo** (`Core/World`): universo de demonstração **fictício** (18 clubes, 2 ligas, 1 copa), simulação em
  camadas (3D / detalhada / leve), tabelas, artilharia, busca de jogadores e importação de pacotes JSON.
- **Jornal e redes sociais** gerados a partir de eventos reais da simulação.
- **Salvamento** versionado com checksum, cópia de segurança, recuperação e migração; várias carreiras.
- **Camada Unity** (`Assets/NextXI/Runtime`): estádio procedural, atleta humanoide com malha única skinned e
  animação procedural, bola, câmeras (transmissão, próxima, jogador, dinâmica, replay), replays de gol,
  controles por toque configuráveis, HUD, menus, áudio sintetizado e perfis de qualidade.

## Abrir no Unity

1. Instale o **Unity 6 (6000.0 LTS)** com o módulo **Android Build Support** (OpenJDK, SDK & NDK).
2. Abra esta pasta pelo Unity Hub (o Unity baixa os pacotes do `Packages/manifest.json`).
3. Na primeira importação, `NextXI/Editor/ProjectSetup.cs` roda sozinho: cria o asset URP, os materiais-modelo,
   a cena `Assets/NextXI/Scenes/Boot.unity` e ajusta o Player Settings. Para repetir: menu **NEXT XI › Configurar projeto**.
4. Abra `Boot.unity` e aperte **Play**. No editor, o mouse funciona como dedo e o teclado também:
   WASD/setas mover · Shift correr · J passe · L enfiada · I lançar/cruzar · K chute (segure para potência) ·
   U drible · E proteger/conter · Q trocar · C câmera · Esc pausa.

## Build Android

- Pelo menu **NEXT XI › Build › Android APK (desenvolvimento)**, ou pela linha de comando:
  `Unity -batchmode -quit -projectPath . -executeMethod NextXI.EditorTools.BuildScript.BuildAndroidApk -logFile build.log`
- Saída em `Builds/Android/`. Instale com `adb install -r Builds/Android/NEXT_XI_0.1.0_dev.apk`.

## Testes fora do Unity

```bash
# .NET 8 SDK
cd tools/NextXI.Core.Tests && dotnet test           # 87 testes do núcleo (mesmas fontes dos testes EditMode)
cd tools/NextXI.Headless && dotnet run -c Release -- 10 5   # simula 10 partidas 11x11 de 5 min/tempo
cd tools/NextXI.UnityCheck && ./fetch_refs.sh && dotnet build  # compile-check da camada Unity
cd tools/NextXI.GeometryPreview && dotnet run -- out           # exporta o atleta em OBJ (render: tools/debug/render_obj.py)
```

Detalhes em [`Docs/TESTING.md`](Docs/TESTING.md). Arquitetura em [`Docs/ARCHITECTURE.md`](Docs/ARCHITECTURE.md).
Recursos e o que falta em [`Docs/ASSETS.md`](Docs/ASSETS.md). Dados importáveis em [`Docs/DATA_PACKS.md`](Docs/DATA_PACKS.md).

## Licenças e conteúdo

Todo o conteúdo é original ou gerado proceduralmente pelo próprio jogo. A única dependência de arte externa é a
fonte **Barlow / Barlow Condensed** (SIL Open Font License 1.1, `Assets/NextXI/Resources/Fonts/OFL.txt`).
Clubes, jogadores, técnicos e competições do universo de demonstração são **fictícios**.
