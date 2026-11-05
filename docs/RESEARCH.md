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

- Website: [macro.fish](https://macro.fish/)
- XTernal: [termx3/OpenMacro-XTernal](https://github.com/termx3/OpenMacro-XTernal)
- Swift é comercial e apresenta funções avançadas para Fisch.
- XTernal é publicado sob AGPL-3.0, mas declara usar leitura da memória do Roblox.
- Decisão: não usar leitura de memória, injeção, executores nem técnicas de contorno. O GafiFish funciona apenas com imagem do ecrã e inputs normais do Windows.

## 8. Venda através da gamepass Sell Anywhere

- A gamepass **Sell Anywhere** permite vender, através de um botão no inventário, os itens que não estejam marcados como favoritos.
- Referências: [Gamepasses — Fisch Wiki](https://fisch.fandom.com/wiki/Gamepasses), [Gamepasses — Fischepedia](https://fischepedia.com/wiki/gamepasses/).
- O macro não deve presumir que todos os peixes podem ser vendidos: favoritos são a proteção do jogador.
- Decisão: depois do intervalo configurado, aguardar o fim da captura atual, abrir o inventário, acionar a opção visível de venda e confirmar visualmente o resultado. A predefinição será 10 minutos.

## Conclusão

Não há código oficial do Bloomfisching que possamos reutilizar licitamente. O projeto independente V13-4 também não fornece uma licença de reutilização e não está demonstrado que seja o código original. Vamos implementar o GafiFish de raiz em C#/.NET, aplicando os padrões documentados de visão, calibração e controlo, e acrescentando Perfect Cast e venda periódica desde a primeira versão funcional.
