# Gerar o APK pelo GitHub Actions (sem instalar nada)

O workflow `.github/workflows/ci.yml` roda em todo push:

1. **core-tests** — testes do núcleo (.NET), compile-check da camada Unity e simulação headless. Não precisa de licença.
2. **unity-tests** — abre o projeto no Unity 6000.0.32f1 (imagem GameCI) e roda os testes EditMode.
3. **android-build** — gera `NEXT_XI_<versão>_dev.apk` e publica como artefato `NEXT-XI-android-apk`.

Os passos 2 e 3 só rodam quando o repositório tem a licença Unity configurada. Suas credenciais ficam nos
*secrets* do GitHub; ninguém (nem o Claude) as lê.

## Configurar a licença (uma vez)

1. Instale o Unity Hub no seu computador, entre com sua conta Unity e ative uma licença **Personal** (gratuita).
2. Localize o arquivo de licença:
   - Windows: `C:\ProgramData\Unity\Unity_lic.ulf`
   - macOS: `/Library/Application Support/Unity/Unity_lic.ulf`
   - Linux: `~/.local/share/unity3d/Unity/Unity_lic.ulf`
3. No GitHub: repositório › **Settings › Secrets and variables › Actions › New repository secret**, crie:
   - `UNITY_LICENSE` — o conteúdo inteiro do arquivo `.ulf`
   - `UNITY_EMAIL` — e-mail da conta Unity
   - `UNITY_PASSWORD` — senha da conta Unity
4. Em **Actions › CI › Run workflow** (ou faça qualquer push). Ao terminar, baixe o artefato
   `NEXT-XI-android-apk` na página da execução e instale no celular (permitir "fontes desconhecidas")
   ou com `adb install -r NEXT_XI_0.1.0_dev.apk`.

Referência: documentação do GameCI (game.ci/docs/github/activation).
