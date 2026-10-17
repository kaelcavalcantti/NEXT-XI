# Testes

## Automatizados (executados neste repositório)

Os testes ficam em `Assets/NextXI/Tests/EditMode` (assembly EditMode da Unity) e são compilados também pelo
projeto `tools/NextXI.Core.Tests`, permitindo rodá-los sem a Unity:

```bash
cd tools/NextXI.Core.Tests
dotnet test            # 87 testes, ~75 s
```

| Área | Testes | O que garantem |
|---|---|---|
| Física da bola | 6 | Bola parada estável; quiques decrescentes sem atravessar o chão; rolagem desacelera e para (bate com a fórmula analítica); trave rebate; rede contém; efeito curva a bola |
| Solver de chute | 6 | Passe rasteiro chega ao alvo com a velocidade pedida; lançamento cai perto do alvo no tempo de voo e com velocidade realista; chute passa na altura pedida |
| Relógio | 8 | 45' virtuais para 3/4/5/6/8/10 min reais; acréscimos anunciados e exibidos; 2º tempo começa em 45:00 |
| Regras | 11 | Gol entre as traves; chute para fora ⇒ tiro de meta; por cima ⇒ não é gol; desvio do defensor ⇒ escanteio; lateral e sua cobrança; impedimento marcado e passe em condição completado; falta na área ⇒ pênalti; fora ⇒ falta; 2º amarelo ⇒ expulsão |
| Partida completa | 13 | Partida termina; estado sempre finito; bola nunca abaixo do gramado nem fora do estádio; placar = eventos de gol; nenhum atleta parado o jogo todo; atletas não se sobrepõem; gols/finalizações/precisão de passe realistas em 8 sementes; time mais forte vence mais; determinismo por semente; duração só muda o ritmo do relógio; substituições e limite; atributos alteram velocidade/aceleração; aceleração e giro graduais; chute do usuário marca; goleiro defende alguns e não todos |
| Mundo | 10 | Turno e returno completos sem conflito de rodada; folgas com nº ímpar; tabela (pontos, saldo, desempate); mundo demo consistente; busca por nome/clube/posição/competição; mata-mata gera campeão; simulação detalhada coerente com mando de campo; importação de pacotes, rejeição de dados reais sem procedência, referências quebradas |
| Carreira | 17 | Criação registra atleta no clube; arquétipo molda atributos; avanço e escalação; partida simulada atualiza tudo; relatório da partida 3D (motor headless) alimenta a carreira; temporada completa + virada; treino dá XP e evolui atributos; limite semanal; níveis liberam habilidades de gameplay; transferência; empréstimo e retorno; conversa com treinador; salvar/carregar idêntico; recuperação por backup; adulteração detectada; carreiras independentes; atleta jovem evolui |
| Salvamento | 4 | Migração de versão; save de versão mais nova recusado; configurações persistidas e validadas; escrita atômica com backup em disco |
| Treinos | 12 | Todos os 9 exercícios jogáveis terminam com nota válida; finalização gera gols; velocidade premia atletas rápidos; só participantes em campo |

## Ferramentas de verificação

- `tools/NextXI.Headless`: simula partidas completas e imprime estatísticas e eventos (`dotnet run -c Release -- 10 5 trace.csv`).
- `tools/debug/render_trace.py`: desenha quadros vistos de cima a partir do trace (formação, intenções da IA).
- `tools/NextXI.GeometryPreview`: gera o atleta e suas poses em OBJ, valida pesos de skinning/orientação dos
  triângulos e sintetiza o áudio em WAV com análise de pico/RMS (`dotnet run -- out audio`).
- `tools/debug/render_obj.py`: rasterizador por software para inspecionar os OBJ (frente, perfil, 3/4).
- `tools/NextXI.UnityCheck`: compila Core + Runtime + Editor contra assemblies de referência da Unity.

## Pendente: testes na Unity e em aparelho

Ainda **não executados** (sem Unity no ambiente):

1. Abrir o projeto no Unity 6, rodar `ProjectSetup` e verificar ausência de erros de compilação/importação.
2. `Unity -batchmode -projectPath . -runTests -testPlatform EditMode -testResults results.xml`.
3. Play Mode: menu → partida rápida → jogar um tempo inteiro; verificar câmera, toque, sons, replays, pausa.
4. Carreira: criar atleta (prévia 3D), jogar partida 3D, treino 3D, simular semanas, salvar, fechar e reabrir.
5. Visual: proporções do atleta na engine real, sombras, materiais URP, números nas camisas, rede, torcida.
6. Build Android (APK) e teste em aparelho intermediário e de entrada: FPS, temperatura, memória, carregamento.
