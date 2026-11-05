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
  - botão Sell Anywhere
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

O Reel não faz cast nem recast. A direção, a velocidade e os limiares precisam de validação com a interface real do Fisch.

## 5. Perfect Cast

O medidor é calibrado por região, indicador e zona-alvo. O detetor calcula a posição normalizada e a velocidade com sinal. O controlador estima a posição no momento esperado do input, considerando a latência configurada, e produz `Wait`, `Release` ou `Cancel`.

Nesta versão a decisão é diagnóstica: o programa desenha o indicador e a zona-alvo, mas ainda não executa o clique de libertação.

## 6. Shake

O detetor procura blobs compactos dentro da região e da cor calibradas. Os candidatos são desenhados no overlay e apresentados com o respetivo número.

Ainda não classifica de forma fiável a direção de cada prompt nem escolhe a tecla de resposta. Por isso, não envia inputs para Shake nesta versão.

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

O temporizador só transforma a venda em pendente. A venda só começa depois de uma notificação OCR de confiança elevada marcar uma captura concluída.

Fluxo:
1. Abrir o inventário com o Roblox em primeiro plano.
2. Aguardar várias deteções estáveis do botão calibrado.
3. Enviar um único clique por tentativa.
4. Comparar a região dos itens antes/depois.
5. Exigir alteração suficiente em dois fotogramas consecutivos.
6. Fechar o inventário e retomar a preparação.

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
