# Pacotes de dados (países, ligas, clubes, jogadores)

O mundo do jogo é expansível sem alterar código: um pacote JSON (`WorldPack`) é importado com
`WorldBuilder.Import(world, WorldBuilder.ParsePack(json))`. A importação valida ids e referências e devolve a lista
de problemas encontrados.

## Regras de procedência

- `Fictional: true` — conteúdo inventado; pode ser importado livremente.
- `Fictional: false` — dados reais. **Obrigatório** informar `Source` (fonte autorizada), `License` (termos que
  permitem o uso) e `UpdatedAt` (data da última atualização). Sem isso o pacote é **rejeitado**.
- Não use raspagem de sites nem materiais de terceiros sem licença. Nomes, imagens, escudos, uniformes e
  semelhanças de atletas reais exigem direitos específicos.
- Cada clube e jogador importado guarda `DataSource`, exibido no perfil do jogador dentro do jogo; o mundo guarda
  a lista `DataSources` com pacote, fonte, licença e data.

## Formato

Veja `Docs/examples/demo_pack.json`. Campos principais:

```json
{
  "PackId": "exemplo-ficticio",
  "Description": "Liga de exemplo",
  "Source": "Criado pela equipe NEXT XI",
  "License": "Uso interno",
  "UpdatedAt": "2026-10-17",
  "Fictional": true,
  "Clubs": [ { "Id": "...", "Name": "...", "ShortName": "...", "Country": "...", "Formation": "4-3-3", "HomeKit": { "Shirt": "#RRGGBB" } } ],
  "Players": [ { "Id": "...", "FirstName": "...", "LastName": "...", "Position": "ST", "ClubId": "...", "Attributes": { "Finishing": 70 } } ],
  "Coaches": [ { "Id": "...", "Name": "...", "ClubId": "..." } ],
  "Leagues": [ { "Id": "...", "Name": "...", "Country": "...", "ClubIds": [ "..." ] } ]
}
```

Atributos ausentes assumem o valor padrão (60; goleiro 20). Posições: `GK CB LB RB LWB RWB CDM CM CAM LM RM LW RW CF ST`.
