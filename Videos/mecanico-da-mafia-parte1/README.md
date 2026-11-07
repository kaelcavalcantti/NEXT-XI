# E se você fosse o mecânico da máfia? — Parte 1: O Acordo

Vídeo vertical 9:16 (1080×1920, 30 fps, ~62 s) no estilo "Technicolor 3D Stage Set":
céu pintado laranja-avermelhado (#E0481E) com faixas de nuvem douradas, sol baixo e quente com sombras longas,
preenchimento frio, bonecos-manequim, e Alejandro como único herói em laranja (#D97757) com contorno preto.

**Arquivo final:** `mecanico-da-mafia-parte1.mp4`

## Cenas
| Tempo | Cena | O que aparece |
|---|---|---|
| 0–12 s | 1 · A máfia | Abertura em contra-plongée contra o céu laranja, descida contínua até o nível da rua em Guadalajara (catedral com torres, papel picado, palmeiras), SUV preta passando; travelling lateral com os dois homens dentro; SUV passando por cima da câmera. |
| 12–22 s | 2 · Sua vida | Oficina: Alejandro no motor de um carro antigo, lata de "AHORROS" com uma moeda, foto da mãe + conta da clínica ($480,000), reação cômica. |
| 22–35 s | 3 · A visita | SUV freia em frente ao "TALLER ALEJANDRO", sapato pisando na rua, capanga inspecionando carro consertado, chefe em contra-plongée contra o céu: "Precisamos de um mecânico como você…" (ka-ching em "dez vezes"). |
| 35–42.5 s | 4 · O acordo | Dinheiro caindo na bancada, olhar desconfiado, foto da mãe por cima do ombro, aperto de mão, sorriso ingênuo com a chave inglesa. |
| 42.5–49.7 s | 5 · O primeiro dia | Garagem gigante cheia de SUVs, chave jogada, pilhas de dinheiro brotando (queixo caído), capô fechado com força, chefe se aproximando. |
| 49.7–60.3 s | 6 · O mistério | Celular deslizando e vibrando ("DESCONOCIDO"), voz no telefone, medo crescente, luz vermelha e *dolly zoom* no rosto. |
| 60.3–62.5 s | Tela final | "CONTINUA NA PARTE 2…" |

## Como foi feito
- Cenários, personagens e câmera: three.js, renderizado quadro a quadro no Chromium headless (`source/`).
- Narração: TTS Piper (voz pt-BR "edresson", sintética); a fala do HOMEM e a VOZ NO TELEFONE são a mesma voz com
  tom e filtro diferentes. **É uma voz provisória** — para publicar, vale regravar com locutor ou um TTS melhor
  usando `source/timeline.json` (horários de cada fala).
- Trilha de suspense e efeitos sonoros sintetizados em Python (`source/audio_mix.py`), com ducking sob a narração.
- Legendas estilo TikTok (palavra ativa destacada) desenhadas no próprio quadro.

## Re-renderizar
```bash
cd source && npm i three@0.170.0 @fontsource/anton@5.0.18 @fontsource/bebas-neue@5.0.18
python3 audio_prep.py   # precisa dos .wav gerados pelo Piper em audio/
python3 -m http.server 8765 &
node render.mjs frames 0 1875 part0.mjpeg   # (ou dividir em 3 partes, como em assemble.sh)
python3 audio_mix.py && bash assemble.sh
```
