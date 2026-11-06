# A CASA QUE RESPIRA — Parte 1: A Herança

Curta de animação de terror psicológico (≈ 6 min, 16:9, 1920×1080, 24 fps, PT‑BR), produzido
**inteiramente por código**: cenários, personagem, câmera, luz, efeitos atmosféricos, vozes,
efeitos sonoros, trilha e mixagem. Nenhum asset de imagem ou som foi baixado de bancos prontos:
tudo é gerado por algoritmos neste diretório. As únicas peças externas são a biblioteca 3D, as
fontes tipográficas e o modelo neural de voz (licenças abaixo).

**Vídeo final:** `output/A_Casa_que_Respira_Parte1.mp4` (H.264 + AAC 48 kHz)

## Como produzir de novo

```bash
cd Films/a-casa-que-respira
./build.sh            # tudo: dependências, vozes, áudio, quadros e MP4
WORKERS=3 ./build.sh  # mais navegadores renderizando em paralelo
```

Etapas individuais (úteis para iterar):

| Etapa | Comando | Saída |
|---|---|---|
| Linha do tempo (fonte única de tempos) | `node tools/export_timeline.mjs` | `build/timeline.json` |
| Vozes neurais + sincronia labial | `python3 audio/voices.py [ID…]` | `build/voices/*.wav`, `build/lipsync.json` |
| Sonoplastia, trilha e mixagem | `python3 audio/mix.py` | `build/audio/mix.wav`, `build/audio/stems/` |
| Quadros da animação | `node tools/render.mjs --workers 2` | `build/frames/f00000.jpg…` |
| Trecho específico | `node tools/render.mjs --from 5280 --to 5400 --force` | (quadros = segundos × 24) |
| Quadros de conferência | `node tools/render.mjs --stills 12,48.5,200 --scale 0.5` | `build/stills/` |
| Relatório de áudio | `python3 tools/audio_report.py 0 368 out.png` | níveis por camada + espectrograma |
| MP4 final | `bash tools/mux.sh` | `output/A_Casa_que_Respira_Parte1.mp4` |

Requisitos: Node 18+, Python 3.10+, `ffmpeg` com `libx264` e `rubberband`, e o Chromium do
Playwright (`npx playwright install chromium` se ainda não houver). O render usa WebGL por software
(SwiftShader), então roda em qualquer máquina sem GPU: ~1,3 s por quadro com 2 workers em 4 núcleos
(≈ 3–4 h para os 8 832 quadros).

## Tecnologia escolhida (e por quê)

- **Imagem — Three.js renderizado quadro a quadro em Chromium headless.** Cada quadro é
  determinístico (`window.renderFrame(f)`), então áudio e imagem compartilham exatamente os mesmos
  tempos. Pós‑produção própria: profundidade de campo por profundidade (mudanças de foco), bloom,
  gradação de cor fria (azul/cinza/verde), ACES, grão de filme, vinheta, aberração cromática e
  letterbox 2,2:1 dentro do quadro 16:9.
- **Atmosfera:** chuva em GPU (40 mil gotas, iluminadas por relâmpagos e pelos faróis), respingos,
  névoa rasteira com ruído, feixes volumétricos (postes, luar, faróis, lanterna), poeira suspensa
  que só brilha dentro do cone da lanterna, céu com nuvens e relâmpagos, para‑brisa com gotas e
  limpadores sincronizados.
- **Personagem:** Gabriel é um único modelo procedural (`src/char/human.js`) usado em todas as cenas —
  mesmo rosto, cabelo, jaqueta verde‑oliva, jeans e botas. Cabeça esculpida por perfis anatômicos,
  *morph targets* (mandíbula, sorriso, sobrancelhas, lábios arredondados e o sorriso anormal do duplo),
  olhos com íris e pálpebras que piscam, olhar que segue alvos, esqueleto com caminhada/corrida
  procedurais e IK de braço (lanterna, celular no ouvido, chave, carta, fotografia). A boca é animada
  a partir da energia e do espectro do áudio de cada fala (`build/lipsync.json`).
- **Vozes — Kokoro‑82M (ONNX, local).** Vozes PT‑BR `pm_alex` (Gabriel), `pm_santa` (Augusto),
  `pf_dora` (a voz feminina) e uma mistura de estilos para o advogado. Cadeias de efeito por tipo:
  telefone/telefone com falhas/grito distorcido, rádio com estática, carta lida em voz íntima,
  voz abafada atrás da porta (com sussurro por LPC e reverb de porão) e o sussurro do duplo
  (voz de Gabriel transformada em sussurro + oitava abaixo).
- **Som — síntese própria em Python/NumPy/SciPy (48 kHz, estéreo):** chuva (exterior, teto do carro,
  vidro), trovões com estalo e ribombo, vento e árvores, motor com Doppler, pneus no molhado,
  limpadores, portas, rangidos por modelo físico *stick‑slip* (porta, assoalho, escada, maçaneta),
  passos por superfície (cascalho, varanda, madeira, escada, tapete, corrida) gerados a partir da
  coreografia, chaves, fechadura, relógio (tique‑taque e 11 badaladas), páginas, papel, caixa,
  batidas sob o assoalho, respiração da casa, respiração atrás da fechadura, ofegar, coração,
  vibração e toque de celular, interferência GSM, falha elétrica, impactos e *risers*.
- **Trilha original** (`audio/music.py`): um tema de cinco notas em ré menor (Ré–Fá–Mi–Si♭–Lá) que
  atravessa o filme — piano na abertura e nas fotografias, caixinha de música desafinando no título
  e no quarto, cordas graves no porão, cluster de metais, coro e tímpanos no clímax do duplo — e
  para de uma vez no congelamento da decisão.
- **Mixagem:** camadas separadas (ambiente, efeitos, sobrenatural, diálogos, trilha, cartelas),
  reverbs por espaço (casa, exterior, carro, porão), *ducking* da trilha e do ambiente sob qualquer
  voz, silêncios intencionais (corte seco da janela, a chuva que para na chegada, a casa que para de
  respirar, o congelamento), compressão leve, loudness integrada −16 LUFS e limitador em −1 dBFS.

## Estrutura

```
src/timeline.js        planos, falas e eventos (fonte única de tempo para imagem e som)
src/choreo.js          trajetos de Gabriel e do duplo -> animação e passos sonoros
src/main.js            renderizador e captura de quadros
src/engine/            pós-produção, efeitos atmosféricos, céu, texturas procedurais
src/sets/              cidade, campo e estrada, casarão (exterior e interior), carro, móveis e objetos
src/char/human.js      Gabriel (e o duplo)
src/shots/             os 22 planos: opening, drive, house_a, house_b, cards
audio/voices.py        vozes neurais + dados de sincronia labial
audio/dsp.py, sfx.py   DSP e efeitos sonoros sintetizados
audio/music.py         trilha original
audio/mix.py           mixagem e master
tools/                 render, mux, modelos de voz, relatório de áudio
```

## Roteiro da Parte 1 (como foi filmado)

Sobrevoo de Pedra Funda sob chuva → estrada entre a mata → o casarão; uma silhueta na janela do
segundo andar → título. A ligação do cartório. A viagem: o rádio sussurra “Você demorou para voltar.”
A chuva para quando ele chega — silêncio. A chave antiga, a porta. A casa respira (e para quando ele
para). O álbum: o menino na janela com o rosto de Gabriel; no verso, “Gabriel — 1994”. O relógio
volta a funcionar: 23h. A pancada no andar de cima, o quarto infantil, a carta de Augusto. Três
batidas sob o assoalho; “Gabriel…”. A porta do porão: a maçaneta gira sozinha, a voz de mulher,
“Você não deveria ter nascido.”, algo respira do outro lado. O telefone: Augusto, “Porque eu estou no
andar de cima.” O duplo no alto da escada inclina a cabeça e sorri; desce. O porão entreaberto com
luz fraca, o duplo descendo — a imagem congela, a música para. Tela preta, três batidas, “Agora
podemos começar.” Cartela interativa (🅰️ porão / 🅱️ enfrentar o duplo), “FIM DA PARTE 1”, som final.

Nota de continuidade: no texto original a foto seria de “32 anos antes do nascimento” de Gabriel,
o que conflita com “Gabriel — 1994” e com seus 24 anos (nascido em 2002). Mantivemos a inscrição
“Gabriel — 1994” — o ano em que Augusto desapareceu — e Gabriel diz: “Mil novecentos e noventa e
quatro… Eu nem tinha nascido.” O mistério continua intacto e coerente.

## Limitações honestas

- Personagem e cenários são procedurais: o visual é estilizado (iluminação e enquadramento
  cinematográficos fazem o trabalho do realismo); não há captura de movimento nem modelos esculpidos à mão.
- A sincronia labial é por energia/espectro do áudio (abertura e arredondamento da boca), não por fonemas.
- As vozes são sintéticas (Kokoro), processadas para soar naturais; não são atores.
- A mixagem foi validada por medições (níveis por camada, loudness, picos, espectrograma) — vale uma
  escuta final em fones antes de publicar.

## Licenças de terceiros

- three.js — MIT · fontes Cinzel, Cormorant Garamond, La Belle Aurore, Special Elite — SIL OFL 1.1 (via @fontsource)
- Kokoro‑82M (pesos ONNX e vozes) — Apache‑2.0 (obtidos dos pacotes npm `kokoro-fp32*-shards` e `kokoro-js`)
- espeak‑ng (fonemização, via `espeakng-loader`) — GPL‑3.0 (usado como ferramenta, não incorporado ao vídeo)
