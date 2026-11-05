# GafiFish

Aplicação Windows para observação visual, calibração e registo de sessões de pesca no Roblox Fisch.

**Estado: observação e registo locais.**

## O que já funciona

- Captura da área visível do cliente Roblox através de F6 e observação contínua com F7.
- Calibração visual por região e amostragem HSV dos marcadores.
- Deteção dos marcadores do peixe e da barra, com estimativa curta de movimento.
- Deteção do indicador e da zona-alvo do Perfect Cast, com decisão diagnóstica considerando a latência.
- Deteção de candidatos a prompts de Shake.
- Deteção visual do botão Sell Anywhere e medição de alterações na lista do inventário.
- OCR local com Tesseract, incluindo verificação de integridade do modelo de idioma.
- Histórico local SQLite e resumo de sessão com os 25 melhores peixes.
- Testes automatizados para geometria, tracking, controladores, deteção visual, venda simulada, OCR e persistência.

## O que ainda não está ativado

A interface atual está em **OBSERVATION MODE**. Não envia cliques nem teclas para o jogo.

O adaptador de input Win32, o controlador do Reel e o coordenador de Sell Anywhere existem como módulos separados, mas ainda não estão ligados a um fluxo automático de pesca na interface. O Shake deteta candidatos, mas ainda não interpreta a direção/tecla de cada prompt. Estas partes dependem de validar a disposição real dos prompts e dos controlos com capturas do jogo; não se devem assumir coordenadas ou cores fixas.

O coordenador de venda foi testado com cenários simulados, incluindo confirmação visual e falha segura, mas não executa vendas na versão de observação.

## Instalação

1. Abre **Actions** no repositório e entra na execução mais recente com sucesso.
2. Transfere o artefacto `GafiFish-win-x64` e extrai o ZIP.
3. Executa `GafiFish.exe`.

A publicação Windows inclui o runtime .NET. O motor Tesseract usa bibliotecas nativas e pode exigir o **Microsoft Visual C++ Redistributable 2015–2022 (x64)**: https://aka.ms/vs/17/release/vc_redist.x64.exe

Na primeira sessão com OCR, o GafiFish descarrega o modelo inglês `eng.traineddata` (cerca de 4 MB) do repositório oficial `tesseract-ocr/tessdata_fast`. O ficheiro é guardado localmente e a integridade é verificada antes de ser usado. Depois disso, o modelo fica disponível sem novo download.

## Utilização

1. Abre o Fisch e deixa a janela Roblox visível.
2. Pressiona **F6** para capturar um fotograma ou **F7** para iniciar/parar a observação contínua.
3. Volta à janela GafiFish com Alt+Tab. A última captura mantém-se no painel de pré-visualização.
4. Em **Visual calibration**, seleciona a região do minijogo, do medidor de Perfect Cast, do prompt Shake e do botão/inventário Sell Anywhere. Para os detetores por cor, amostra um pixel limpo de cada marcador.
5. Seleciona também a região da notificação de captura. Esta região é usada pelo OCR.
6. Pressiona **Start session + local OCR**, regressa ao Roblox e usa F7. O OCR só analisa fotogramas enquanto existir uma sessão ativa.
7. Pressiona **End session and show Top 25** para guardar a sessão e ver o resumo.

O parser de capturas é deliberadamente conservador: só regista uma espécie quando reconhece uma notificação compatível com o padrão inglês `You caught [species]`. Raridade, mutação, peso e valor ficam vazios quando não são reconhecidos com confiança. A notificação real do jogo poderá exigir afinação adicional.

## Dados locais

- Perfil visual: `%LOCALAPPDATA%\\GafiFish\\vision-profile.json`
- Histórico: `%LOCALAPPDATA%\\GafiFish\\sessions.db`
- Modelo OCR: `%LOCALAPPDATA%\\GafiFish\\tessdata\\eng.traineddata`

As capturas e o texto OCR são processados localmente. O GafiFish não envia imagens para serviços externos.

## Desenvolvimento

Requisitos: Windows, .NET 10 SDK e Visual Studio Build Tools para WPF.

```powershell
dotnet restore GafiFish.sln
dotnet build GafiFish.sln --configuration Release
```

Testes:

```powershell
dotnet run --project tests/GafiFish.Core.Tests/GafiFish.Core.Tests.csproj --configuration Release
dotnet run --project tests/GafiFish.Vision.Tests/GafiFish.Vision.Tests.csproj --configuration Release
dotnet run --project tests/GafiFish.Data.Tests/GafiFish.Data.Tests.csproj --configuration Release
dotnet run --project tests/GafiFish.App.Tests/GafiFish.App.Tests.csproj --configuration Release
dotnet run --project tests/GafiFish.Ocr.Tests/GafiFish.Ocr.Tests.csproj --configuration Release
```

A CI Windows compila a solução, executa as cinco suites de testes e publica um ZIP `win-x64`.

## Princípios de segurança

- Não lê nem altera a memória do Roblox.
- Não injeta código, DLLs, scripts ou executores no jogo.
- Não altera ficheiros do Roblox nem tráfego de rede.
- Não pede nem guarda credenciais, cookies ou tokens.
- Não exige privilégios de administrador.
- O adaptador de input liberta os botões e desarma quando perde o foco ou é parado.
- A aplicação atual não envia inputs para o jogo.

A utilização de automação pode estar sujeita às regras do Roblox e do Fisch e pode implicar penalizações da conta. O GafiFish não tenta contornar sistemas anti-cheat.

## Documentação

- [Pesquisa de projetos e licenças](docs/RESEARCH.md)
- [Arquitetura técnica](docs/ARCHITECTURE.md)
- [Plano de desenvolvimento](docs/ROADMAP.md)
- [Segurança e tolerância a falhas](docs/SECURITY.md)

Projeto independente, sem afiliação ao Roblox ou aos criadores de Fisch.
