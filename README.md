# GafiFish

Aplicação Windows para observação visual, calibração, Reel assistido e registo de sessões de pesca no Roblox Fisch.

**Estado: aplicação gráfica Windows com iniciar/parar, Reel assistido, Auto-cast/Auto-shake opcionais, modo de recast até à primeira captura confirmada, Sell Anywhere temporizado e histórico local; validação prática no Fisch ainda pendente.**

## O que já funciona

- Captura da área visível do cliente Roblox através de F6 e observação contínua com F7.
- Calibração visual por região e amostragem HSV dos marcadores.
- Deteção dos marcadores do peixe e da barra, com estimativa curta de movimento.
- Deteção do indicador e da zona-alvo do Perfect Cast, com decisão diagnóstica considerando a latência.
- Deteção de prompts Shake e Auto-shake opcional: só clica após dois frames estáveis, evita repetir o mesmo prompt e reconhece o fim da sequência após quatro frames sem prompt.
- Auto-cast opcional: segura e liberta o rato na janela Perfect Cast calibrada; por defeito, após 30 segundos sem mordida faz até dois recasts adicionais. O modo opcional “Keep fishing until the first fish is caught” remove esse limite e termina após uma notificação OCR de captura com confiança elevada. O modo “Fish until this exact target is caught” permite escrever um nome como “Wrath Scylla”; continua a tentar até todos os termos do alvo aparecerem numa notificação de captura reconhecida com confiança elevada.
- Deteção visual do botão Sell Anywhere e verificação de alterações estáveis na lista do inventário.
- Botões visíveis Start macro / Stop macro, além dos atalhos F8/F9. O modo de recast contínuo exige Auto-cast, sessão OCR ativa e região de notificação calibrada; o timeout de Reel liberta o controlo e reinicia o ciclo, sem eliminar os stops de emergência.
- Sell Anywhere opcional a cada 10 minutos quando existe uma sessão OCR ativa e as regiões de botão/lista estão calibradas; requer captura OCR de confiança elevada e confirma a alteração em dois fotogramas antes de fechar o inventário.
- OCR local com Tesseract, incluindo verificação de integridade do modelo de idioma.
- Histórico local SQLite, explorador das 20 sessões mais recentes, detalhes das últimas 15 capturas por sessão e novo Catchbook pesquisável para consultar o arquivo completo, filtrar por raridade/mutação/espécies e exportar resultados CSV.
- Ritmos de captura Economy/Balanced/Responsive, com cadência adaptativa e controlo do compromisso entre CPU e atualização visual.
- Objetivos de peixe-alvo de 1 a 100 capturas confirmadas, em vez de parar obrigatoriamente após o primeiro exemplar.
- Importação/exportação de perfis de calibração JSON e arquivo completo de capturas em CSV/JSON com metadados da sessão.
- Interface WPF reorganizada em Dashboard, Fishing, Calibration, Sessions e Settings, com botões arredondados e animações subtis de hover.
- Testes automatizados para geometria, tracking, controladores, deteção visual, venda simulada, OCR, persistência, objetivos e cadência.

## O que falta validar no jogo real

- Toda a lógica de controlo tem testes automatizados e frames sintéticos, mas ainda falta calibrar cores/regiões com capturas reais do teu Fisch e validar o timing numa sessão real. Não afirmamos que a visão esteja pronta para a tua resolução/cana sem esse passo.
- Equipar a cana por hotbar é opcional: podes escolher um slot de 1 a 0; o GafiFish envia a tecla normal do Windows uma vez ao armar, mas não consegue garantir visualmente que a cana correta está nesse slot. Também podes deixar a cana equipada manualmente.
- A venda real depende da gamepass Sell Anywhere, da lista/botão calibrados e de notificações OCR reconhecidas. O fluxo é testado por frames simulados; a calibração e o resultado real ainda precisam de confirmação no teu jogo.

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
8. Para iniciar, clica em **Start macro (F8)** e muda para a janela Roblox; o GafiFish arma-se assim que captura o primeiro fotograma do Roblox. Também podes usar F8 diretamente com o Roblox em primeiro plano. Para parar, clica em **Stop macro (F9)** ou usa F9. Para o modo ilimitado até à primeira captura, ativa **Keep fishing until the first fish is caught** — exige Auto-cast, sessão OCR e região de notificação calibrada. Para vender automaticamente, marca **Enable Sell Anywhere every 10 minutes**; a gamepass vende os itens não favoritos, por isso marca como favoritos os que pretendes guardar.
9. Para procurar um peixe específico, ativa **Fish until this exact target is caught**, escreve o nome (por exemplo, **Wrath Scylla**) e inicia o macro. O modo exige Auto-cast, sessão OCR ativa e região de notificação calibrada. Só termina quando o texto da notificação incluir todos os termos do alvo e a confiança OCR for pelo menos 90%; capturas de outros peixes não terminam a procura. Pressiona **End session and show Top 25** para parar a automação, guardar a sessão e ver o resumo.
10. Usa **Export CSV** ou **Export JSON** para guardar todas as capturas reconhecidas.

Sell Anywhere vende os peixes que não estejam marcados como favoritos. Marca previamente como favoritos todos os peixes que pretendes guardar. A venda só é armada por F8 quando a caixa Sell Anywhere está marcada, existe uma sessão OCR e as regiões do botão/inventário estão calibradas.

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


### Melhorias adicionais e limites

A aplicação inclui perfis de calibração nomeados manualmente por cana, telemetria, intervalo de captura adaptativo, ações temporizadas opcionais para totem/poção (slot escolhido pelo utilizador, clique único entre ciclos e reposição do slot da cana), cópia do resumo e exportação de diagnóstico JSON. A ativação dos consumíveis não é confirmada visualmente, pelo que um slot mal configurado pode gastar um item. Appraise, Treasure Appraise, Auto Enchant, Aquarium, Angler Quest, Harpoon, deteção de hunts e reconexão automática continuam não implementados.


### Alertas e estatísticas

Inclui alertas sonoros locais para espécies prioritárias, raridades Legendary+ e mutações reconhecidas por OCR. O painel de estatísticas acumuladas agrega o histórico SQLite local (sessões, capturas, espécies mais comuns, mutações, raridade, pesos, valor estimado e capturas por hora de sessões concluídas). O valor não representa nem confirma o saldo real do jogo.


- Logs de atividade persistentes (últimos 500 eventos visíveis), com rotação automática de ficheiros, pesquisa, filtro por nível, cópia/exportação TXT e registo de avisos/erros.
- Hotkeys globais F2–F12 configuráveis, validadas para serem únicas e guardadas localmente; tecla dedicada para mostrar/ocultar a janela.
- Pasta de dados local acessível diretamente a partir do painel de logs.
- Cópias de segurança portáteis ZIP dos dados locais, com snapshot SQLite online, manifesto e verificação de integridade SHA-256.
- Catchbook com pesquisa global do histórico completo, filtros combinados por raridade/identificação/mutação, ordenação por colunas e exportação CSV/JSON dos resultados filtrados.
- Ocultação opcional da janela enquanto o macro está armado, sem desativar a hotkey de emergência.
- Dashboard com estatísticas de sessões/capturas/Legendary+/capturas por hora e Quick Info que distingue módulos prontos de módulos ainda sem calibração.
