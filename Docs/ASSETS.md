# Inventário de recursos e pontos de integração

Nenhum recurso 3D, animação, som, textura ou dado licenciado estava disponível no ambiente de desenvolvimento.
Tudo abaixo é **original e gerado pelo próprio código**. A qualidade é a de uma implementação provisória
tecnicamente correta — não é qualidade de produto comercial, e os itens marcados como "substituir" precisam de
recursos profissionais (próprios ou licenciados) antes de um lançamento.

| Recurso | Implementação atual | Avaliação honesta | Ação recomendada |
|---|---|---|---|
| Atletas | `AthleteBody`: malha única skinned (~3,5k vértices, 20 ossos, 7 materiais), proporções antropométricas, perfis de seção por altura/constituição, rosto com nariz/orelhas/olhos/sobrancelhas, 8 penteados, 5 barbas, chuteiras, meiões, números | Proporções corretas e sem emendas nas articulações (ver `Docs/images/athlete_*_offline_render.png`, renderização por software fora da Unity). Aparência estilizada/low-poly: **não** é fotorrealista. Pequeno artefato na abertura do calção quando a coxa flexiona muito. | **Substituir** por modelo humanoide rigado profissional (corpo + cabeças + cabelos modulares) com blendshapes de rosto. |
| Animação | `AthletePose`: solver procedural (marcha/corrida/arrancada com fase por distância, lateral, de costas, inclinação em curva, idle, contenção, proteção, chute com quadro de contato, voleio, cabeceio, desarme, carrinho, mergulho, defesa, arremesso lateral, queda, comemoração, frustração, drible) com blending | Funcional e sincronizado com o motor, mas sem a riqueza de captura de movimento. | **Substituir/complementar** por clipes mocap licenciados em Mecanim (Humanoid). O esqueleto já segue a hierarquia humanoide; mapear `PlayerAction` → parâmetros/triggers do Animator e manter o quadro de contato do motor como evento. |
| Estádio | `StadiumBuilder`: gramado com listras, marcações oficiais, gols com rede, bandeiras, placas de LED com patrocinadores fictícios, 4 arquibancadas de 2 anéis, cobertura, torres de iluminação, céu | Coerente, leve para celular, mas geométrico. | Substituir por estádio modelado (LODs, lightmaps). |
| Torcida | Cartões animados (2 quadros, 3 grupos de fase) | Funciona como massa à distância; não resiste a close. | Torcida por impostores/instancing com variação. |
| Bola | Esfera com textura de painéis original e rotação física | Adequada. | Modelo de bola com normal map. |
| Texturas | `ProcTex`: grama, rede, pele, tecido, assentos, LED, números, gradientes | Simples. | Texturas PBR licenciadas. |
| Fonte | Barlow / Barlow Condensed (SIL OFL 1.1) | Licença permite uso comercial e embarque. | Manter ou trocar por tipografia proprietária da marca. |
| Áudio | `Synth`: torcida (ambiente, gol, "uhh", vaia, aplausos, cântico original), chute, apito, rede, trave, contato, UI, música de menu original | Validado offline (sem NaN, níveis sãos). Sintético: não soa como gravação real de estádio. | **Substituir** por gravações próprias/licenciadas de estádio e foley; narração exige locutor e roteiro licenciados. |
| Narração | Não há | Canal de volume existe; nenhum conteúdo. | Gravar/licenciar narração. |
| Logotipo | Marca tipográfica + emblema desenhados em código (`Ui.Logo`) | Provisório. | Designer de marca. |
| Dados | Universo fictício gerado (`DemoClubs`, `PlayerGenerator`) | Claramente identificado como fictício em toda a UI e nos dados (`IsFictional`, `DataSource`). | Importar pacotes reais **somente** de fontes licenciadas (ver `DATA_PACKS.md`). |

## Pontos de integração

- **Modelo de atleta**: `AthleteRig.Create` é o único ponto que constrói o visual. Um `IAthleteVisual` alternativo
  pode instanciar um prefab Humanoid e receber `AnimInput` (mesma estrutura usada hoje) para alimentar um Animator.
- **Áudio**: `AudioService.BuildClips` cria os `AudioClip`; trocar por clipes de `Resources`/Addressables mantendo
  as chaves `Sfx`/`UiSound` não exige mudanças no resto do jogo.
- **Estádio**: `StadiumBuilder.Build` pode ser trocado por um prefab; `CrowdMaterials` e `AnimateCrowd` são opcionais.
- **Dados**: `WorldBuilder.Import(world, pack)` aceita pacotes JSON com procedência obrigatória para dados reais.
