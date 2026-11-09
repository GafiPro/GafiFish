# Arquitetura técnica atual

## 1. Stack

**C# / .NET 10 / WPF / Windows x64.**

- WPF: interface, calibração, pré-visualização e overlays.
- OpenCvSharp: conversão de cor, contornos e deteção visual.
- P/Invoke Win32: captura GDI, hotkeys globais e eventos de input.
- TesseractOCR: OCR inglês, executado localmente.
- Microsoft.Data.Sqlite: histórico de sessões e capturas.
- xUnit v3: testes automatizados.

O GafiFish é uma aplicação externa. Não lê memória do Roblox, não injeta código e não altera tráfego de rede.

## 2. Estrutura do repositório

```
GafiFish/
  src/
    GafiFish.App/       # WPF, preview, overlay e coordenação
    GafiFish.Core/      # modelos, estados e controladores
    GafiFish.Windows/   # captura visível, foco e input Win32
    GafiFish.Vision/    # calibração, OpenCV e tracking
    GafiFish.Ocr/       # Tesseract local e parser de notificações
    GafiFish.Data/      # SQLite e exportação CSV/JSON
  tests/
    GafiFish.Core.Tests/
    GafiFish.Vision.Tests/
    GafiFish.Data.Tests/
    GafiFish.App.Tests/
    GafiFish.Ocr.Tests/
  docs/
```

## 3. Pipeline visual

```
Janela Roblox em primeiro plano
      |
      v
Captura GDI da área de cliente
      |
      v
Regiões normalizadas / calibração HSV
      |
      v
Detetores OpenCV
  - peixe e barra de Reel
  - indicador e zona-alvo de Perfect Cast
  - candidatos Shake
  - fallback de template em escala de cinzentos para peixe/barra
  - botão Sell Anywhere
  - replay de vídeo offline (um frame de cada vez)
      |
      v
Tracker temporal / estimador de confiança
      |
      v
Pré-visualização, overlay e diagnóstico
```

Cada frame tem timestamp, resolução, origem no ecrã e buffer BGRA. A calibração é guardada em `%LOCALAPPDATA%\GafiFish\vision-profile.json`.

Os detetores trabalham na região calibrada. Quando não há deteção válida, não inventam uma posição: a confiança decai e o controlador liberta o rato se os marcadores de Reel deixarem de estar disponíveis.

## 4. Reel assistido

O Reel usa um controlador PD com look-ahead curto e deadband:

- erro entre a posição prevista do peixe e o centro da barra;
- termo proporcional para corrigir a diferença;
- termo derivativo para considerar a velocidade;
- deadband para manter o último estado quando o erro é pequeno;
- limiar de confiança e deteções recentes.

A coordenação está em `ReelInputCoordinator`. Só atua no estado `Reeling` e precisa de ambos os marcadores calibrados. F8 arma o input; F9 pára e liberta todos os inputs. A opção de inversão do mapeamento hold/release existe porque a direção correta deve ser confirmada no minijogo real.

O ciclo pode opcionalmente equipar a cana a partir de um slot de hotbar, faz Auto-cast, espera pela mordida e tenta recast até duas vezes após timeouts. Depois de detetar os dois marcadores, o Reel usa o controlador PD. A direção, velocidade e limiares ainda precisam de validação com a interface real do Fisch.

## 5. Perfect Cast

O medidor é calibrado por região, indicador e zona-alvo. O detetor calcula a posição normalizada e a velocidade com sinal. O controlador estima a posição no momento esperado do input, considerando a latência configurada, e produz `Wait`, `Release` ou `Cancel`.

O `CastInputCoordinator` pode ser armado pela opção **Auto-cast**, desligada por defeito. Segura o botão esquerdo e liberta quando o controlador indica `Release`; cancela em leitura inválida ou após 8 segundos. A cana deve ser equipada manualmente. O fluxo tem testes sintéticos, mas ainda precisa de validação em jogo real.

## 6. Shake

O detetor procura blobs compactos dentro da região e da cor calibradas. Os candidatos são desenhados no overlay e apresentados com o respetivo número.

A opção **Auto-shake**, desligada por defeito, envia um clique normal na posição de um candidato calibrado apenas depois de dois frames estáveis. Aguarda que o prompt desapareça ou se mova antes de permitir outro clique. Exige timeout global e paragem F9; ainda precisa de validação com a UI real.

## 7. Máquina de estados e inputs

Estados usados pelo fluxo:
- Idle / Preparing
- Casting / WaitingForBite
- Shaking / Reeling
- CatchDetected
- SellPending / OpeningInventory / SellingInventory
- VerifyingSale / ClosingInventory
- Recovering / Stopping / Stopped / Error

O adaptador `RobloxInputService` exige `Arm()`, confirma que o processo Roblox está em primeiro plano e liberta o rato/teclas ao desarmar. A observação F7 monitoriza o foco; se o Roblox deixa de estar em primeiro plano, a automação pára.

Hotkeys:
- F6: capturar um fotograma.
- F7: iniciar/parar observação contínua.
- F8: armar Reel assistido e, se a sessão OCR e calibração do inventário estiverem ativas, iniciar Sell Anywhere.
- F9: paragem de emergência.

## 8. Sell Anywhere

O coordenador `SellAnywhereCoordinator` usa `PeriodicSellSchedule` com intervalo de 10 minutos.

O temporizador só é criado se o utilizador marcar a opção Sell Anywhere na interface, existir uma sessão OCR ativa e o botão/lista estiverem calibrados. Depois, transforma a venda em pendente. A venda só começa depois de uma notificação OCR de confiança elevada marcar uma captura concluída.

Fluxo:
1. Detetar primeiro se o botão Sell Anywhere já está visível; se não estiver, abrir o inventário com o Roblox em primeiro plano.
2. Aguardar várias deteções estáveis do botão calibrado.
3. Enviar um único clique por tentativa.
4. Comparar a região dos itens antes/depois.
5. Exigir alteração suficiente em dois fotogramas consecutivos.
6. Fechar o inventário apenas se foi o GafiFish a abri-lo; depois retomar a preparação.

Se o botão não aparecer, a captura falhar ou a alteração não for confirmada antes do timeout, o coordenador pára e liberta os inputs. Não há repetição cega do clique. O teste automatizado usa frames sintéticos; a validação na interface real continua pendente.

A gamepass vende itens não favoritos. O GafiFish não altera favoritos.

## 9. OCR, histórico e exportação

O modelo inglês Tesseract é descarregado de uma versão fixa de `tessdata_fast`, validado por tamanho e Git blob SHA-1 e guardado em AppData. As imagens são convertidas em memória e não são enviadas para serviços externos.

O parser é conservador: só regista uma captura quando encontra uma notificação que corresponda ao padrão inglês `You caught [species]`. Raridade, mutação, peso e valor ficam nulos quando não são reconhecidos.

O `FishSessionStore` guarda sessões e capturas em SQLite. No fim da sessão, o resumo apresenta no máximo 25 peixes. O histórico completo pode ser exportado em CSV ou JSON.

## 10. Testes e limites

A CI Windows:
- restaura e compila a solução;
- executa as cinco suites de testes;
- publica um ZIP Windows x64 self-contained.

Os testes do controlador usam inputs simulados. Os testes de visão usam frames sintéticos. Ainda falta testar a deteção e o controlo com capturas reais do jogo, especialmente Perfect Cast e Shake.


## 11. Replay de vídeo offline

A UI usa `OpenCvVideoFrameReader` para abrir ficheiros de vídeo suportados pelo backend OpenCV. `VideoReplaySession` no Core mantém o índice e valida cada buffer BGRA sem carregar o vídeo inteiro. O leitor procura fotogramas apenas quando necessário; a reprodução limita o trabalho visual a 30 atualizações por segundo e salta fotogramas de fontes mais rápidas.

O replay usa `DisplayFrame(..., allowAutomation: false)`: os detetores e overlays são apresentados, mas a automação, o input do Roblox e a escrita OCR/sessão são ignorados. O descodificador é fechado quando uma nova fonte substitui a anterior ou quando a janela termina. A disponibilidade de codecs depende da build/runtime OpenCV e do ficheiro selecionado.


## 12. Watchdog de reconexão

O detetor `ConnectionRecoveryVisionDetector` procura apenas a região e a cor HSV calibradas pelo utilizador. O `ConnectionRecoveryCoordinator` exige três frames estáveis, envia um clique normal do Windows e aguarda dois frames sem o botão. Aplica um intervalo de segurança e um máximo de três tentativas por ativação. Se o botão não desaparecer dentro do timeout, desarma sem repetir. Não infere que a sessão foi recuperada e não pode correr em simultâneo com a pesca ou o Aquarium.


## 13. Rotação de totens dia/noite

O `WorldPhaseVisionDetector` compara duas gamas HSV calibradas pelo utilizador numa região fixa do HUD. Se ambas surgirem ao mesmo tempo, se nenhuma surgir ou se a confiança for baixa, o estado é `Unknown`. O `DayNightTotemRotationCoordinator` usa a primeira fase estável apenas como baseline; uma transição posterior precisa de três frames consistentes e só executa em `Preparing`. Um clique normal seleciona o slot do totem correspondente e restaura o slot da cana. A opção não usa relógios ou durações presumidas do ciclo do jogo.


## 14. Templates locais para marcadores de pesca

O perfil pode guardar pequenos recortes PNG em escala de cinzentos para o peixe e para a barra de controlo. A seleção é feita pelo utilizador na pré-visualização e limitada a 128×128 pixels. O detetor tenta primeiro HSV; quando a deteção está ausente ou abaixo da confiança mínima, executa `matchTemplate` com correlação normalizada e limiar de 0,82. O template é redimensionado proporcionalmente à resolução de referência. Não há templates genéricos embutidos nem coordenadas presumidas.


## 15. Tranquility Rhythm — quatro pistas

O `RhythmNoteVisionDetector` deteta, numa região selecionada pelo utilizador, a nota mais próxima da linha de receção em cada uma das quatro pistas. Cada pista tem uma amostra HSV independente (ordem D/F/J/K) e a linha horizontal é guardada como posição normalizada no perfil. O detetor ignora os recetores estáticos colocados sobre a linha.

O `RhythmNoteInputCoordinator` só envia uma tecla normal do Windows depois de obter duas observações monotónicas da mesma nota, estimar a velocidade vertical e prever a passagem pela janela de timing calibrada. As teclas são D, F, J e K, por pista. O envio só é permitido quando a máquina de estados está em `Reeling`; observações duplicadas/fora de ordem não geram input. Os toques têm libertação imediata e qualquer falha de input desarma o coordenador.

O avanço temporal (0–250 ms) é configurável por perfil porque a latência de captura e o FPS variam entre computadores. Os testes usam frames sintéticos; a validação da geometria, velocidade e timing com gravações reais do Fisch continua pendente.


## 16. Auto Appraise e Auto Enchant

Os dois módulos são independentes do ciclo de pesca e começam desligados. Exigem ativação explícita, uma captura ao vivo de Roblox em primeiro plano e calibração manual das regiões.

**Auto Appraise** seleciona o fish slot configurado e abre o diálogo do Appraiser com a tecla de interação. Só escolhe a opção de appraisal quando o texto está estável em dois frames; depois exige preço C$ legível e o botão Yes reconhecido em dois frames. O valor é debitado do orçamento local no instante em que o clique Yes é enviado. Após uma captura ser appraised, volta a selecionar o fish slot e reabre o diálogo. Se o resultado não mudar e ficar estável em dois frames, o coordenador pára sem repetir. O limite predefinido é C$ 100.000 por execução e pode ser alterado nas Settings.

**Auto Enchant** seleciona o slot de relíquias configurado e aguarda o prompt Enchant da Keeper's Altar. Só pressiona a tecla de interação configurada (E por defeito) depois de o prompt estar estável em dois frames. O utilizador equipa manualmente a cana correta e mantém as relíquias no slot escolhido. Cada utilização enviada conta imediatamente; só depois de o OCR confirmar um novo resultado estável é permitido selecionar o slot e avançar. O limite predefinido é 10 utilizações por execução.

Ambos param quando o texto-alvo aparece, quando o limite é atingido, quando o OCR não confirma uma alteração dentro do timeout, quando o Roblox perde o foco, quando a observação pára ou quando F9 é acionado. Não fazem retry cego. A validação com gravações reais ainda é necessária: os testes automatizados e a calibração manual não substituem confirmação na interface atual do Fisch.
