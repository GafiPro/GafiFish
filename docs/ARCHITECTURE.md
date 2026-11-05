# Arquitetura técnica proposta

## 1. Decisão tecnológica

**C# / .NET 10 LTS / WPF / Windows x64.**

Motivos:
- Integração madura com Windows para janelas, hotkeys globais, captura de ecrã e inputs.
- WPF fornece uma interface nativa e extensível.
- OpenCvSharp permite usar o motor nativo OpenCV sem transformar o projeto numa aplicação Python.
- .NET 10 é LTS; a versão e os pacotes usados serão fixados e atualizados com revisão de segurança.

Bibliotecas propostas (a confirmar durante o bootstrap):
- OpenCvSharp para processamento de imagem.
- Vortice.Windows ou uma integração WinRT controlada para captura DXGI/Windows Graphics Capture.
- P/Invoke para SendInput, RegisterHotKey e validação da janela ativa.
- SQLite para sessões e capturas.
- Serilog para registos.
- xUnit para testes.

## 2. Estrutura prevista

```
GafiFish/
  src/
    GafiFish.App/             # WPF: interface, overlay e calibração
    GafiFish.Core/            # modelos, estado da pesca e lógica de controlo
    GafiFish.Windows/         # captura, foco da janela e inputs Win32
    GafiFish.Vision/          # OpenCV, deteção e tracking
    GafiFish.Data/            # SQLite, JSON e resumo de sessões
  tests/
    GafiFish.Core.Tests/
    GafiFish.Vision.Tests/
  docs/
```

A divisão permite testar o algoritmo sem abrir o Roblox e trocar o backend de captura sem reescrever o controlador.

## 3. Pipeline visual e controlo

```
Janela do Roblox
      |
      v
Captura de frames (região do jogo)
      |
      v
Detetores OpenCV
  - barra de pesca
  - peixe / marcador-alvo
  - prompts de shake
  - estado de captura
      |
      v
Tracker temporal
  - suavização
  - direção e velocidade
  - previsão de curto prazo
      |
      v
Controlador
  - PD/PWM
  - deadband
  - limite de alteração
  - confiança mínima
      |
      v
Input Win32 (SendInput)
      |
      v
Jogo
```

O motor visual não deve depender de um único pixel. A ordem de deteção será configurável e poderá combinar:
1. Correspondência de cor na região calibrada.
2. Deteção de contornos/limites da barra.
3. Marcadores de cor ou forma específicos da cana.
4. Template matching como fallback.
5. Predição curta da última posição válida, sempre limitada no tempo.

A previsão nunca deve continuar indefinidamente quando o peixe desaparece. A confiança cai com a idade da última deteção e o controlador entra em estado neutro quando a informação fica desatualizada.

## 4. Estimativa do movimento

Cada frame válido fornece uma posição do alvo e uma timestamp monotónica. O tracker calcula:
- posição suavizada;
- velocidade horizontal em píxeis/segundo;
- direção provável;
- aceleração aproximada, se a qualidade da deteção permitir;
- confiança da estimativa.

A versão inicial usará suavização temporal e estimativa de velocidade. Um filtro de Kalman pode ser introduzido depois, caso os testes com gravações mostrem benefício.

O controlador terá:
- componente proporcional para corrigir o erro de posição;
- componente derivativa para reduzir oscilações;
- look-ahead pequeno para reagir antes de o alvo atravessar a barra;
- deadband para evitar cliques desnecessários;
- PWM para converter o valor de controlo em intervalos de botão premido/libertado;
- limites de slew e modo de recuperação para mudanças bruscas.

Os valores serão calibráveis por cana. Não serão copiados valores de outros macros como se fossem universais.

## 5. Máquina de estados

Estados previstos:
- Idle
- Preparing
- Casting
- WaitingForBite
- Shaking
- Reeling
- CatchDetected
- Recovering
- Stopping
- Stopped
- Error

Regras:
- Só um estado pode controlar o input de cada vez.
- Cada estado tem timeout e condições de saída explícitas.
- Uma transição inválida não pode manter o botão premido.
- Paragem manual, perda de foco, falha de captura ou exceção têm prioridade sobre qualquer ação.
- A finalização liberta todos os inputs antes de guardar o resumo.

## 6. Identificação e registo de capturas

Quando a pesca termina:
1. Detetar o sinal visual de captura.
2. Capturar apenas a região da notificação.
3. Aplicar OCR local.
4. Extrair os campos reconhecíveis: nome, raridade, mutação, peso e valor, quando visíveis.
5. Associar uma confiança à leitura.
6. Guardar a captura e, em caso de falha, marcar os campos como desconhecidos em vez de inventar dados.

Modelo conceptual:

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
  SourceImageHash
```

Não se enviam imagens para serviços de IA. As capturas de diagnóstico são opcionais e limitadas à região necessária.

## 7. Resumo e Top 25

Ao parar:
- mostrar número total de capturas;
- mostrar capturas identificadas e não identificadas;
- mostrar taxa de captura por hora e duração da sessão;
- mostrar no máximo 25 peixes;
- indicar claramente quando a lista foi truncada (por exemplo: "47 capturas — a mostrar as 25 melhores").

A ordenação será configurável. A proposta inicial é usar uma pontuação que considere raridade, mutação e peso; o valor de venda pode ser usado como critério principal quando for reconhecido com confiança. Empates são resolvidos por valor e depois timestamp.

O histórico completo fica guardado localmente; a interface do resumo mostra apenas os 25 melhores. Também será possível exportar CSV/JSON.

## 8. Interface

Separadores previstos:
- **Dashboard:** estado, iniciar/parar, duração e capturas por hora.
- **Fishing:** opções de cast, shake e reel.
- **Calibration:** escolher janela, marcar regiões e amostrar cores.
- **Rod Profiles:** calibração e parâmetros por cana.
- **Session:** histórico, capturas e Top 25.
- **Diagnostics:** overlay, FPS de captura, confiança e razão de cada input.

Hotkeys iniciais sugeridas:
- F8: iniciar/pausar.
- F9: parar e gerar o resumo.
- F12: paragem de emergência imediata.

As teclas serão configuráveis e registadas globalmente apenas enquanto a aplicação estiver aberta.

## 9. Testabilidade

O motor visual deve aceitar imagens gravadas como entrada. Isto permite testar:
- posições e tamanhos detetados;
- mudança de direção do peixe;
- frames perdidos e falsos positivos;
- resposta do controlador;
- transições de estado;
- ordenação e limite de 25 capturas.

O controlador será testado com simulações e não enviará inputs reais nos testes automatizados.
