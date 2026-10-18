# Progresso do NEXT XI

Arquivo de retomada: o que realmente existe, o que falta e qual é o próximo passo.
Última atualização: 2026-10-18.

## Ambiente de desenvolvimento usado

- Container Linux sem Unity (o download do Unity Hub/Editor é bloqueado pela política de rede) e sem GPU/tela.
- Instalado: .NET 8 SDK (apt). Disponível: Python 3 + Pillow + NumPy (ferramentas de inspeção).
- Consequência: **nada foi executado dentro da Unity**. Toda verificação foi feita com testes .NET, simulação
  headless, compile-check contra assemblies de referência (UnityEngine 2021.3 / UnityEditor 2018.1 do NuGet) e
  renderização por software dos modelos procedurais.

## Concluído (com evidência)

| Item | Evidência |
|---|---|
| Motor 11x11 contínuo (física, regras, IA, goleiro, bolas paradas, substituições, cartões, acréscimos) | 43 testes de motor/regras/física/relógio; simulações headless de 12 jogos (média 2,1–2,9 gols, 10–20 finalizações, ~75% de passes certos) |
| Relógio virtual configurável (3–10 min/tempo) | Testes de relógio e de duração |
| Controle do usuário contextual (passe, enfiada, cruzamento, chute com potência, drible, proteção, desarme, carrinho, pressão, troca, primeira) | Teste de chute do usuário; compile-check da camada de toque |
| Carreira completa no núcleo (criação, arquétipos/habilidades, escalação, confiança, objetivos, contratos, propostas, empréstimos, IA de transferências, demissões, seleção, prêmios, temporadas, aposentadoria) | 17 testes de carreira, incluindo temporada inteira e virada |
| Treinos jogáveis (9 exercícios + goleiro assistido) | 12 testes; notas medidas com o atleta em piloto automático |
| Mundo fictício expansível, tabelas, artilharia, busca, importação JSON com procedência | 10 testes; pacote de exemplo importado sem erros |
| Jornal e redes sociais ligados a eventos | Gerados nos testes de carreira |
| Salvamento versionado/checksum/backup/migração, múltiplas carreiras, configurações | 6 testes |
| Atleta procedural skinned com proporções coerentes e 13 poses de animação | Renders offline em `Docs/images/`; validação de pesos (0 erros) |
| Áudio sintetizado (16 sons + música) | Geração offline: sem NaN, picos 0,4–0,95 |
| Camada Unity (estádio, atleta, animação, câmeras, replays, toque, HUD, menus, carreira, treino, áudio, qualidade, bootstrap, editor/build) | Compila sem erros contra as referências da Unity |

## Parcial / não verificado

- **Toda a camada Unity está não verificada em execução**: renderização URP, sombras, shaders encontrados em build,
  entrada por toque em aparelho, mixagem real de áudio, layout da UI em telas reais, desempenho.
- Compile-check usa APIs da Unity 2021.3/2018.1; APIs exclusivas do Unity 6 não foram exercitadas
  (só `EditorUserBuildSettings.buildAppBundle` está protegida por `#if`).
- Goleiro controlado pelo usuário: posicionamento manual, reposição (mão/chute) e carrinho no atacante — coberto por
  testes do núcleo, mas a sensação de controle no toque não foi avaliada em aparelho.
- Atleta reserva: pode assistir do banco em 3D (avanço 6x) e entrar no minuto que o técnico decidir; também pode simular.
- Seleção nacional é simulada (sem partida 3D da seleção).
- Personalização: sem editor de rosto detalhado (blendshapes), apenas parâmetros globais.
- Narração: categoria de volume existe, sem conteúdo.

## Problemas conhecidos

1. Precisão de passe da IA ~74–76% após incluir bloqueio de corpo na avaliação de risco (antes ~68%); ainda abaixo do futebol real em jogos de alto nível.
2. Faltas ainda abaixo do real (~1–2 por partida de 5 min/tempo) — desarmes da IA raramente cometem falta.
3. Calção: rasgo na flexão da coxa corrigido (vértices inferiores pesados para a coxa); restam pequenos pontos na barra da camisa na pose de chute (render offline).
4. Glyphs especiais (✓ ★ ♥ setas) podem não existir na fonte; foram trocados por texto, mas outros podem aparecer.
5. Jogadores do time que ataca às vezes ficam muito longe da linha defensiva (bloco alongado) após recuperações.
6. Treinos com o atleta em piloto automático pontuam baixo em drible/finalização (a IA chuta cedo) — com jogador humano o esperado é maior, mas não foi medido.
7. Placares de "tempo real": um jogo de 5 min/tempo gera ~220 passes (comprimido em relação a 90' reais); as estatísticas por jogo da carreira simulada seguem números de 90'.

## Recursos ausentes (precisam de produção/licença)

Modelos humanoides profissionais, captura de movimento, estádio modelado, áudio de estádio gravado, narração,
identidade visual por designer, dados reais licenciados. Ver `Docs/ASSETS.md`.

## CI

`.github/workflows/ci.yml`: testes .NET + compile-check sempre; testes Unity e APK Android via GameCI apenas quando os
segredos `UNITY_LICENSE`/`UNITY_EMAIL`/`UNITY_PASSWORD` existirem (ver `Docs/BUILD_CI.md`). Nenhum APK foi gerado ainda.

## Testes executados nesta sessão

- `dotnet test` (tools/NextXI.Core.Tests): **90/90 passando**.
- Simulações headless (tools/NextXI.Headless): 10 partidas por rodada de calibração.
- Compile-check (tools/NextXI.UnityCheck): **sem erros** (Core + Runtime + Editor).
- Prévia geométrica/sonora (tools/NextXI.GeometryPreview): pesos válidos, áudio finito.

## Próxima etapa

1. Abrir no Unity 6, rodar `NEXT XI › Configurar projeto`, corrigir qualquer erro de importação/compilação.
2. Rodar os testes EditMode na Unity e jogar a primeira partida no Editor; corrigir problemas visuais reais
   (escala de UI, materiais, sombras, números, rede, iluminação noturna).
3. Gerar o APK, medir FPS/memória em 2 aparelhos e ajustar perfis de qualidade.
4. Calibrar faltas e variedade tática.
5. Iniciar a substituição de recursos provisórios (atleta e animações) por recursos profissionais.
