# GafiFish

Aplicação Windows para observação visual, calibração, Reel assistido e registo de sessões de pesca no Roblox Fisch.

**Estado: Reel assistido, Auto-cast e Auto-shake opcionais, Sell Anywhere temporizado e histórico local; validação prática no Fisch ainda pendente.**

## O que já funciona

- Captura da área visível do cliente Roblox através de F6 e observação contínua com F7.
- Calibração visual por região e amostragem HSV dos marcadores.
- Deteção dos marcadores do peixe e da barra, com estimativa curta de movimento.
- Deteção do indicador e da zona-alvo do Perfect Cast, com decisão diagnóstica considerando a latência.
- Deteção de prompts Shake e Auto-shake opcional: clique único após estabilidade, espera por movimento/desaparecimento antes de repetir.
- Auto-cast opcional: segura o rato e liberta-o quando o medidor Perfect Cast calibrado entra na janela prevista, com timeout.
- Deteção visual do botão Sell Anywhere e verificação de alterações estáveis na lista do inventário.
- Reel assistido opt-in com F8/F9, controlador preditivo e libertação automática de input quando os marcadores desaparecem ou o Roblox perde o foco.
- Sell Anywhere opcional a cada 10 minutos quando existe uma sessão OCR ativa e as regiões de botão/lista estão calibradas; requer captura OCR de confiança elevada e confirma a alteração em dois fotogramas antes de fechar o inventário.
- OCR local com Tesseract, incluindo verificação de integridade do modelo de idioma.
- Histórico local SQLite, resumo de sessão com os 25 melhores peixes e exportação CSV/JSON.
- Testes automatizados para geometria, tracking, controladores, deteção visual, venda simulada, OCR e persistência.

## O que falta validar no jogo real

- Auto-cast e Auto-shake estão disponíveis, mas desligados por defeito. Foram testados com frames simulados; ainda precisam de validação prática na tua interface do Fisch e com a tua cana/resolução.
- O Reel assistido não faz recast nem inicia uma sessão de pesca sozinho; só controla o hold/release quando o minijogo e ambos os marcadores calibrados são detetados.
- A venda real depende de calibração feita no teu ecrã, da gamepass Sell Anywhere e de uma notificação OCR de confiança elevada. O fluxo foi testado com frames simulados, mas ainda precisa de validação numa sessão real do Fisch.

Não se assumem coordenadas, cores ou teclas para os prompts de Shake/lançamento.

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
7. Para Auto-cast, calibra a região do medidor e as cores do indicador e da zona-alvo; para Auto-shake, calibra a região e a cor dos prompts. Marca as respetivas caixas na interface antes de ativar o assistente. Ambas estão desligadas por defeito.
8. Regressa ao Roblox e pressiona **F8** para armar o Reel assistido e as opções que escolheste. Se a sessão OCR e as regiões Sell Anywhere estiverem calibradas, o temporizador de venda fica ativo. **F9** é a paragem de emergência e liberta o input.
9. Pressiona **End session and show Top 25** para parar a automação, guardar a sessão e ver o resumo.
10. Usa **Export CSV** ou **Export JSON** para guardar todas as capturas reconhecidas.

Sell Anywhere vende os peixes que não estejam marcados como favoritos. Marca previamente como favoritos todos os peixes que pretendes guardar. A venda só é armada por F8 quando existe uma sessão OCR e as regiões do botão/inventário estão calibradas.

O parser de capturas é deliberadamente conservador: só regista uma espécie quando reconhece uma notificação compatível com o padrão inglês `You caught [species]`. Raridade, mutação, peso e valor ficam vazios quando não são reconhecidos com confiança. A notificação real do jogo poderá exigir afinação adicional.

## Dados locais

- Perfil visual: `%LOCALAPPDATA%\\GafiFish\\vision-profile.json`
- Histórico: `%LOCALAPPDATA%\\GafiFish\\sessions.db`
- Modelo OCR: `%LOCALAPPDATA%\\GafiFish\\tessdata\\eng.traineddata`

As capturas e o texto OCR são processados localmente. O GafiFish não envia imagens para serviços externos.

O Reel assistido usa apenas eventos normais de rato/teclado do Windows. F8 exige a janela Roblox em primeiro plano e F7 ativo. Se o Roblox perder o foco, ou se F7 parar, o input é libertado. O mapeamento hold/release pode ser invertido na caixa de seleção da interface.

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
- O adaptador de input liberta os botões e desarma quando perde o foco, quando F7 pára ou quando F9 é pressionado.
- A venda envia um único clique por tentativa; não repete cliques às cegas quando a confirmação visual falha.

A utilização de automação pode estar sujeita às regras do Roblox e do Fisch e pode implicar penalizações da conta. O GafiFish não tenta contornar sistemas anti-cheat.

## Documentação

- [Pesquisa de projetos e licenças](docs/RESEARCH.md)
- [Arquitetura técnica](docs/ARCHITECTURE.md)
- [Plano de desenvolvimento](docs/ROADMAP.md)
- [Segurança e tolerância a falhas](docs/SECURITY.md)

Projeto independente, sem afiliação ao Roblox ou aos criadores de Fisch.
