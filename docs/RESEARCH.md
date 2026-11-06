# Pesquisa de referências

Pesquisa consultada em 5 de novembro de 2026. As licenças e os repositórios podem mudar; voltar a verificá-los antes de reutilizar código.

## 1. Bloomfisching — projeto pedido pelo utilizador

- Projeto oficial: [injuriez/Bloomfisching](https://github.com/injuriez/Bloomfisching)
- Última versão observada: 2.1.1, publicada em 7 de fevereiro de 2026.
- Estado: arquivado em 15 de fevereiro de 2026. O autor diz que o macro deixou de funcionar e que não será mantido.
- Conteúdo da branch principal: apenas README e ficheiro de licença. A release publica um executável Windows, não código-fonte.
- Licença: EULA proprietária que proíbe expressamente copiar, redistribuir, modificar, adaptar, fazer reverse engineering ou derivar código.
- Decisão: não copiar o executável nem tentar extrair ou reconstruir o código. O projeto serve como referência histórica, não como base legal ou técnica.

## 2. Repositório separado com "AFK Bloom Fishing V13-4"

- Projeto: [kamikoluna/kk-fisch-ahkv2-v1-macro](https://github.com/kamikoluna/kk-fisch-ahkv2-v1-macro)
- Contém scripts AutoHotkey, incluindo `Fisch.ahk`, e perfis `.ini`.
- O README chama-lhe "OPEN SOURCE", mas o repositório não contém uma licença explícita. Essa expressão, por si só, não concede permissão legal para copiar ou distribuir o código.
- A autoria e a relação deste script com o Bloomfisching oficial não estão demonstradas. Não assumir que é o código original.
- A secção Perfect Cast usa a câmara, procura visualmente zonas verdes e brancas e liberta o botão de lançamento quando os marcadores ficam suficientemente próximos. É uma referência de comportamento, não uma implementação para copiar.
- Inspeção estática limitada do script principal: foram observadas operações locais de ficheiros de configuração e inputs do rato/teclado; não foram encontradas chamadas de rede, leitura de memória do processo ou download remoto nas linhas analisadas. Isto não constitui auditoria completa do repositório, dependências ou executáveis.
- O script sugere executar como administrador para problemas de gravação. O GafiFish não adotará essa exigência.
- Decisão: usar apenas como referência conceptual; não copiar código sem licença ou autorização explícita.

## 3. FemPoof / fisch-macro

- Projeto: [FemPoof/fisch-macro](https://github.com/FemPoof/fisch-macro)
- Documentação: [Detection](https://github.com/FemPoof/fisch-macro/blob/main/docs/detection.md), [Controller](https://github.com/FemPoof/fisch-macro/blob/main/docs/controller.md), [Calibration](https://github.com/FemPoof/fisch-macro/blob/main/docs/calibration.md), [Perfect Cast](https://github.com/FemPoof/fisch-macro/blob/main/docs/perfect-cast.md)
- Ideias úteis:
  - Cascata de deteção: cor da barra, contornos, indicador lateral, template matching e previsão temporária.
  - Controlador PD/PWM, deadband, look-ahead, telemetria e perfis por cana.
  - Perfect Cast através de uma sequência de câmara e deteção visual do medidor de lançamento.
  - Calibração da região e amostragem de cores por cana.
- Licença: proprietária; proíbe cópias, forks e obras derivadas sem autorização.
- Decisão: estudar documentação e conceitos; não copiar código.

## 4. Scarlet Hub

- Projeto: [HovDor/Scarlet-Hub](https://github.com/HovDor/Scarlet-Hub)
- Ideias úteis:
  - Ciclo completo: cast, shake e reel.
  - Shake por deteção visual ou navegação.
  - Seguimento por linha ou cor, com perfis por cana.
  - Perfect Cast com calibração do atraso dos inputs.
  - Editor de regiões, overlay, hotkeys e diagnóstico.
- Licença: não foi encontrada uma licença explícita no repositório consultado.
- Decisão: usar descrição pública como referência; não copiar o código.

## 5. FischCalibrate

- Projeto: [ashvik-cs50/FischCalibrate](https://github.com/ashvik-cs50/FischCalibrate)
- Ideias úteis: captura visual local, deteção do peixe e marcador de controlo, leitura do resultado após uma captura.
- A classificação opcional usa uma API externa e token pessoal, funcionalidade que não será necessária no GafiFish.
- Licença: não foi encontrada uma licença explícita no repositório consultado.
- Decisão: implementar o registo de capturas localmente, sem enviar imagens ou tokens para serviços externos.

## 6. freew-w / Fisch-Macro

- Projeto: [freew-w/Fisch-Macro](https://github.com/freew-w/Fisch-Macro)
- Implementação analisada: C++ e OpenCV.
- Técnicas: captura GDI/BitBlt, deteção por cor/contornos, inputs através de SendInput e controlador PD simples.
- Funcionalidades: auto-shake, minijogo da barra e auto-sell.
- Licença: GPL-3.0. Reutilização e distribuição de derivados exigem cumprir a GPL-3.0.
- Estado: arquivado em 9 de março de 2025.
- Decisão: referência técnica, mas demasiado antiga para ser a fundação do GafiFish. Não copiar nesta fase para não impor copyleft sem decisão explícita.

## 7. OpenMacro / XTernal / Swift

- Website oficial: [macro.fish](https://macro.fish/) e [openmacro.net](https://openmacro.net/).
- XTernal: [termx3/OpenMacro-XTernal](https://github.com/termx3/OpenMacro-XTernal). É a oferta gratuita e open-source para Fisch. O README declara que lê diretamente a memória do processo Roblox em vez de analisar a imagem do ecrã.
- Licença do XTernal: AGPL-3.0-only. Permite estudar, modificar e redistribuir, mas uma versão derivada distribuída tem de cumprir a AGPL e disponibilizar o código-fonte correspondente. Não incorporar partes no GafiFish sem decidir primeiro a licença do projeto e cumprir todas as obrigações da AGPL. A pasta `library/` tem dependências com licenças próprias; não presumir que tudo segue a mesma licença.
- Swift: produto comercial e fechado, distinto do XTernal. O site anuncia suporte a canas e totens, Auto Harpoon, Auto Appraise e recarga Sovereign. Estas são funcionalidades anunciadas pelo fornecedor, não validadas independentemente nesta pesquisa.
- Histórico público do Swift: a versão 2.0.4 refere uma melhoria do método Predict de 64% para 87% de taxa de captura em testes/simulação; a versão 2.0.6, de 24 de outubro de 2026, lista sobretudo correções de estabilidade. Não transportar esses números para o GafiFish: dependem do método e do ambiente de teste do fornecedor.
- Os Termos do OpenMacro dizem que a automação pode violar regras do Roblox ou de experiências individuais e resultar em medidas sobre a conta. Também estabelecem condições etárias e exigem que um encarregado de educação reveja e aceite os termos quando o utilizador tem menos de 18 anos.
- Decisão técnica: não copiar código do Swift nem tentar reproduzir os seus componentes internos. Não adotar leitura de memória, injeção ou executores. O GafiFish mantém a arquitetura independente baseada em captura visual local e eventos normais de rato/teclado do Windows. Podem aproveitar-se apenas ideias gerais documentadas publicamente, com implementação própria e testes.

## 8. Venda através da gamepass Sell Anywhere

- A gamepass **Sell Anywhere** permite vender, através de um botão no inventário, os itens que não estejam marcados como favoritos.
- Referências: [Gamepasses — Fisch Wiki](https://fisch.fandom.com/wiki/Gamepasses), [Gamepasses — Fischepedia](https://fischepedia.com/wiki/gamepasses/).
- O macro não deve presumir que todos os peixes podem ser vendidos: favoritos são a proteção do jogador.
- Decisão: depois do intervalo configurado, aguardar o fim da captura atual, abrir o inventário, acionar a opção visível de venda e confirmar visualmente o resultado. A predefinição será 10 minutos.

## Conclusão

Não há código oficial do Bloomfisching que possamos reutilizar licitamente. O projeto independente V13-4 também não fornece uma licença de reutilização e não está demonstrado que seja o código original. Vamos implementar o GafiFish de raiz em C#/.NET, aplicando os padrões documentados de visão, calibração e controlo, e acrescentando Perfect Cast e venda periódica desde a primeira versão funcional.


## 9. Levantamento adicional de funcionalidades — 6 de novembro de 2026

Esta secção compara funcionalidades publicamente anunciadas ou documentadas; não é uma auditoria exaustiva de todos os programas existentes, nem prova que cada função funciona em todas as versões do Fisch.

### FemPoof / fisch-macro — v2.4.x

- A documentação atual descreve deteção em cascata por cor, contornos, indicador lateral, correspondência de templates e tracking temporário.
- Inclui amostragem da tonalidade dinâmica da barra, controlador PWM-PD com previsão e amortecimento, ajuste adaptativo à taxa de captura de imagem e HUD com posição da barra/peixe e erro.
- Inclui perfis independentes por cana, auto-totem e auto-poção condicionados a estados seguros, deteção visual do slot da cana para a voltar a equipar e recuperação de desconexões.
- O Perfect Cast é marcado como experimental e requer afinação por configuração.
- Decisão: adotar como referências para uma futura melhoria da deteção, telemetria e perfis por cana. Não reutilizar código: o repositório declara licença proprietária.

### OpenMacro / Swift — funcionalidades públicas adicionais

- A página do produto anuncia Auto Totem para os totens suportados, Auto Appraise por mutação/peso, Auto Harpoon, suporte alargado a canas e automação de recarga Sovereign.
- A lista comparativa pública também anuncia Hunt Detection, Auto Treasure Appraise, Auto Enchant, Auto Aquarium e Auto Angler Quest.
- O changelog público acrescenta exemplos de robustez de produto: reconexão após falhas de rede, acompanhamento do jogador após reconectar, definições centralizadas para modo/hotkeys/slot de cana, mensagens de estado e correções para não clicar em itens ocultos ou fora da janela.
- Estas afirmações são do próprio fornecedor; não foram testadas independentemente. O Swift é fechado e comercial. Não copiar código nem tentar reconstruir componentes internos.
- Decisão: estudar os conceitos, mas implementar qualquer ação de inventário, appraisal, encantamento, aquário, quest ou Harpoon apenas com deteção visual específica, pré-visualização do alvo e salvaguardas contra cliques ou consumo de itens não pretendidos.

### Macros antigos e projetos complementares

- [freew-w/Fisch-Macro](https://github.com/freew-w/Fisch-Macro) lista Auto Shake, minijogo da barra e venda automática; o repositório está arquivado desde março de 2025.
- Um projeto macOS independente descreve perfis por cana, recarga de totens, Sovereign Recharge, Angler Quest, Aquarium, Hunt Detect, Appraise e Enchant. Serve como inventário de ideias, não como validação de compatibilidade com Windows ou com a versão atual do jogo.
- Alguns hubs de scripts apresentam teleporte e automação através de executores/injeção. Essas funções não serão incorporadas no GafiFish: o projeto continuará limitado a captura de ecrã local e eventos normais de teclado/rato.

### Prioridades para o GafiFish

1. **Base de pesca e experiência de utilização:** botões Start/Stop na janela, perfis de configuração persistentes, tentativa contínua opcional até à primeira captura confirmada, estado/log em direto e paragem de emergência.
2. **Qualidade de deteção:** perfis por cana, amostragem de cor adaptativa, alternativas de tracking e HUD de telemetria, sempre com testes sintéticos antes de qualquer validação prática.
3. **Acessórios de pesca:** deteção visual de cana equipada, auto-re-equip apenas se o slot errado for confirmado, e ativação de totem/poção apenas entre ciclos e com opt-in explícito.
4. **Módulos independentes:** deteção de hunts e alertas primeiro; reconexão, quests, aquário, appraisal, encantamento e Harpoon ficam para uma fase separada, porque têm fluxos de interface e consequências diferentes.

O GafiFish não promete equivalência completa com Swift ou outros produtos. Cada módulo tem de ser implementado de raiz, ter testes próprios, limites de tempo e um caminho de cancelamento que liberte todos os inputs.
