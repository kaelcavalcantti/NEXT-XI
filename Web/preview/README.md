# NEXT XI — prévia jogável no navegador

Versão reduzida da partida do NEXT XI escrita em JavaScript, para jogar sem a Unity enquanto o APK não é gerado.

- `src/engine.js` — motor da partida. Física da bola, solver de chute, geometria do campo e modelo de xG são
  portados 1:1 de `Assets/NextXI/Core/Match`. IA e regras são simplificadas: sem faltas, cartões, impedimento,
  substituições ou fadiga.
- `src/game.js` — renderização three.js (r128, via cdnjs), estádio e atletas procedurais, câmera, HUD, controles
  de toque/teclado e áudio sintetizado (Web Audio).
- `src/page.html` — layout, estilos e menus.
- `build.js` — gera `dist/next-xi-preview.html` (corpo da página publicada) e `dist/standalone.html` (documento
  completo, abre direto no navegador com internet para baixar o three.js).

## Testes

```bash
node test/sim.js 8 3          # 8 partidas IA x IA com 3 min por tempo (gols, chutes, passes, NaN, bola parada)
node test/sim.js 4 2 user     # idem com entradas aleatórias de "usuário"
node test/keeper.js           # galeria de chutes contra o goleiro
THREE_JS=/caminho/three.min.js node test/browser.js <pasta>   # Chromium headless: menu, partida, fim de jogo, erros
THREE_JS=/caminho/three.min.js node test/touch.js             # joystick e botões por eventos de ponteiro
```

Resultados medidos (seeds fixas): média de 1,8–3,7 gols por partida de 2×3 min (varia por calibração e times),
passes certos ~60–75%, nenhum NaN, nenhuma bola parada em jogo. Página sem erros de console no Chromium headless
(renderização por software, ~30 fps em 844×390). Desempenho em celulares reais não foi medido.
