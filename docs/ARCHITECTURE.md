# Arquitetura técnica proposta

## 1. Decisão tecnológica

**C# / .NET 10 LTS / WPF / Windows x64.**

Motivos:
- Integração madura com Windows para janelas, hotkeys globais, captura de ecrã e inputs.
- WPF fornece uma interface nativa e extensível.
- OpenCvSharp permite usar o motor nativo OpenCV.
- O processamento será local e os pacotes serão fixados e revistos por segurança.

Bibliotecas propostas:
- OpenCvSharp para processamento de imagem.
- Vortice.Windows ou integração WinRT controlada para captura DXGI/Windows Graphics Capture.
- P/Invoke para SendInput, RegisterHotKey e validação da janela ativa.
- SQLite para sessões e capturas.
- Serilog para registos.
- xUnit para testes.

## 2. Estrutura prevista

```
GafiFish/
  src/
    GafiFish.App/             # WPF: interface, overlay e calibração
    GafiFish.Core/            # modelos, estados e controlo
    GafiFish.Windows/         # captura, foco e inputs Win32
    GafiFish.Vision/          # OpenCV, deteção e tracking
    GafiFish.Data/            # SQLite, JSON e resumo
  tests/
    GafiFish.Core.Tests/
    GafiFish.Vision.Tests/
  docs/
```

## 3. Pipeline visual e controlo

```
Janela do Roblox
      |
      v
Captura de frames
      |
      v
Detetores OpenCV
  - medidor Perfect Cast
  - barra de pesca
  - peixe / marcador-alvo
  - prompts de shake
  - botão Sell Anywhere
      |
      v
Tracker temporal / estimador de confiança
      |
      v
Máquina de estados
      |
      v
Controladores de Cast / Reel / Inventory
      |
      v
Input Win32 (SendInput)
```

O motor visual não deve depender de um único pixel. A ordem de deteção será configurável e poderá combinar:
1. Correspondência de cor na região calibrada.
2. Deteção de contornos/limites.
3. Marcadores de cor ou forma específicos da cana.
4. Template matching como fallback.
5. Predição curta da última posição válida, sempre limitada no tempo.

A confiança cai com a idade da última deteção. O controlador entra em estado neutro quando a informação fica desatualizada.

## 4. Tracking e controlo do reel

Cada frame válido fornece uma posição do alvo e uma timestamp monotónica. O tracker calcula posição suavizada, velocidade horizontal, direção, aceleração aproximada e confiança.

O controlador terá:
- componente proporcional para corrigir o erro;
- componente derivativa para reduzir oscilações;
- look-ahead curto para antecipar o movimento;
- deadband para evitar inputs desnecessários;
- PWM para converter o valor de controlo em intervalos de botão premido/libertado;
- limites de slew e modo de recuperação para mudanças bruscas.

Os valores serão calibráveis por cana. Não serão copiados valores de outros macros como se fossem universais.

## 5. Perfect Cast — incluído no MVP

O Perfect Cast faz parte da primeira versão funcional e estará disponível na interface de Cast.

Pipeline:
1. Calibrar a região do medidor e a posição de referência da câmara.
2. Preparar a câmara de forma repetível com ações normais de zoom e movimento.
3. Iniciar a carga do lançamento.
4. Detetar visualmente o indicador de progresso e a zona-alvo.
5. Estimar a proximidade/tempo até à zona ideal.
6. Libertar no momento calculado e validar o resultado visual.
7. Registar o timing e permitir ajuste por resolução, sensibilidade e cana.

O timing não será um valor fixo copiado. A calibração é específica da configuração e um Perfect Cast não pode ser garantido em todos os lançamentos. Se o medidor desaparecer ou a deteção perder confiança, o sistema deve cancelar de forma segura, em vez de manter um botão premido indefinidamente.

## 6. Máquina de estados

Estados:
- Idle
- Preparing
- Casting
- WaitingForBite
- Shaking
- Reeling
- CatchDetected
- SellPending
- OpeningInventory
- SellingInventory
- VerifyingSale
- ClosingInventory
- Recovering
- Stopping
- Stopped
- Error

Regras:
- Só um estado pode controlar os inputs de cada vez.
- Cada estado tem timeout e condições de saída explícitas.
- A venda nunca interrompe uma captura; fica pendente até o ciclo terminar.
- Perda de foco, falha de captura, deteção incerta ou exceção têm prioridade sobre qualquer ação.
- A finalização liberta todos os inputs antes de guardar o resumo.

## 7. Venda periódica — Sell Anywhere

A gamepass **Sell Anywhere** permite vender os itens não favoritos através de um botão no inventário. O intervalo predefinido será de 10 minutos.

Fluxo:
1. Iniciar um temporizador monotónico de 10 minutos.
2. Quando expirar, marcar a venda como pendente.
3. Esperar pelo fim da captura/reel atual.
4. Confirmar que a janela Roblox está ativa e a interface está num estado reconhecido.
5. Abrir o inventário através do input/controlo visual calibrado.
6. Detetar e acionar o botão Sell Inventory/Sell Anywhere.
7. Confirmar visualmente que a operação terminou.
8. Fechar o inventário e reiniciar o temporizador.
9. Se o botão, o inventário ou a confirmação não forem reconhecidos, não repetir cliques às cegas: parar a automação e avisar o utilizador.

A venda aplica-se a itens que não estejam favoritos. O GafiFish não altera favoritos; o utilizador deve marcar previamente o que pretende guardar. Não será usada qualquer API interna do jogo.

## 8. Capturas e resumo Top 25

Quando a pesca termina:
1. Detetar o sinal visual de captura.
2. Capturar apenas a região da notificação.
3. Aplicar OCR local.
4. Extrair nome, raridade, mutação, peso e valor quando visíveis.
5. Associar confiança a cada campo.
6. Guardar os dados; leituras incertas ficam desconhecidas, nunca inventadas.

Modelo:

```text
FishCatch
  Id
  SessionId
  Timestamp
  Species
  Rarity
  Mutation
  Weight
  EstimatedValue
  RecognitionConfidence
```

Ao parar:
- mostrar total de capturas;
- duração e capturas por hora;
- mostrar no máximo 25 peixes;
- indicar quando a lista foi truncada;
- manter o histórico completo em SQLite e permitir CSV/JSON.

A ordenação será configurável. A proposta inicial pondera raridade, mutação, peso e valor quando este for reconhecido com confiança.

## 9. Interface

Separadores:
- **Dashboard:** estado, iniciar/parar, duração e capturas/hora.
- **Fishing:** Shake, Reel, Cast e Perfect Cast.
- **Sell Anywhere:** intervalo, estado do temporizador e verificação visual.
- **Calibration:** janela, regiões e amostragem de cores.
- **Rod Profiles:** calibração por cana.
- **Session:** histórico e Top 25.
- **Diagnostics:** overlay, FPS, confiança e motivo de cada input.

Hotkeys implementadas:
- F6: captura de um fotograma.
- F7: iniciar/parar observação contínua.
- F8: armar o Reel assistido; também arma Sell Anywhere se a sessão OCR e as regiões do inventário estiverem calibradas.
- F9: paragem de emergência, com libertação de todos os inputs.

A implementação atual mantém o Perfect Cast em diagnóstico e o Shake em deteção de candidatos. O Reel é opt-in e usa a máquina de estados/calibração. A venda é iniciada apenas depois de uma notificação OCR de confiança elevada e confirmada com duas imagens consecutivas da lista do inventário.

## 10. Testabilidade

O motor visual aceitará imagens gravadas como entrada. Testar:
- posições e tamanhos detetados;
- mudança de direção;
- medidor de Perfect Cast;
- falsos positivos e frames perdidos;
- temporizador de venda e adiamento para estado seguro;
- reconhecimento do botão e confirmação visual;
- transições de estado;
- ordenação e limite de 25 capturas.

Os testes do controlador não enviam inputs reais.
