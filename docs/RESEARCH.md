# Pesquisa de referências

Pesquisa consultada em 5 de novembro de 2026. Os repositórios e os respetivos termos podem mudar; as licenças devem ser novamente verificadas antes de qualquer reutilização de código.

## 1. Blossom

- Projeto: [MaxxWasHere/Blossom](https://github.com/MaxxWasHere/Blossom)
- Documentação: [Fishing mode](https://maxxwashere.github.io/blossom.github.io/fishing.html)
- Âmbito: macro para **Sol's RNG**, não um macro específico para Fisch.
- Ideias úteis: ciclo de pesca, deteção visual da mordida, calibração das zonas, caminho até ao cais, venda opcional e recuperação de ciclos bloqueados.
- Decisão: usar como referência de desenho de fluxo e calibração; não usar como base do motor de pesca do Fisch.

## 2. FemPoof / fisch-macro

- Projeto: [FemPoof/fisch-macro](https://github.com/FemPoof/fisch-macro)
- Documentação: [Detection](https://github.com/FemPoof/fisch-macro/blob/main/docs/detection.md), [Controller](https://github.com/FemPoof/fisch-macro/blob/main/docs/controller.md), [Calibration](https://github.com/FemPoof/fisch-macro/blob/main/docs/calibration.md)
- Ideias úteis:
  - Cascata de deteção: cor da barra, contornos, indicador lateral, template matching e previsão temporária da última posição.
  - Calibração da área da barra e amostragem de cores por cana.
  - Controlador PD com antecipação, deadband, limites de velocidade e impulsos PWM.
  - Métricas por ciclo e diagnóstico dos métodos de deteção.
- Licença: a licença publicada reserva os direitos de redistribuição e proíbe cópias, forks e obras derivadas sem autorização.
- Decisão: estudar a documentação e os conceitos; **não copiar código**.

## 3. Scarlet Hub

- Projeto: [HovDor/Scarlet-Hub](https://github.com/HovDor/Scarlet-Hub)
- Ideias úteis:
  - Três etapas completas: cast, shake e reel.
  - Shake por deteção de pixel, círculo ou navegação.
  - Seguimento por linha ou cor, com perfis por cana.
  - Perfect Cast que estima o atraso dos inputs.
  - Editor de regiões, overlay, hotkeys e registo de diagnóstico.
- Licença: não foi encontrada uma licença explícita no repositório consultado.
- Decisão: usar apenas a descrição pública das funcionalidades como referência; não copiar o código.

## 4. FischCalibrate

- Projeto: [ashvik-cs50/FischCalibrate](https://github.com/ashvik-cs50/FischCalibrate)
- Ideias úteis:
  - Captura visual local para cast, shake e reel.
  - Deteção de peixe e marcador de controlo.
  - Captura da notificação após uma pesca para avaliar o resultado.
- Aspeto a evitar: a classificação opcional de capturas usa uma API externa e um token pessoal.
- Licença: não foi encontrada uma licença explícita no repositório consultado.
- Decisão: implementar o registo de capturas localmente, sem enviar imagens ou tokens para serviços externos.

## 5. freew-w / Fisch-Macro

- Projeto: [freew-w/Fisch-Macro](https://github.com/freew-w/Fisch-Macro)
- Implementação analisada: C++ e OpenCV.
- Técnicas observadas: captura por GDI/BitBlt, processamento de imagem com OpenCV, deteção por cor/contornos, inputs através de SendInput e controlo PD simples.
- Funcionalidades documentadas: auto-shake, minijogo da barra e venda automática.
- Licença: GPL-3.0. A reutilização de código exige preservar avisos e cumprir as obrigações da GPL; uma versão derivada distribuída tem de ser disponibilizada sob GPL-3.0.
- Estado: o GitHub indica que o repositório foi arquivado em 9 de março de 2025.
- Decisão: boa referência de implementação nativa, mas demasiado antiga e básica para ser a fundação do GafiFish. Não copiar código nesta fase, evitando impor uma licença copyleft ao projeto sem decisão explícita.

## 6. OpenMacro / XTernal / Swift

- Website: [macro.fish](https://macro.fish/)
- XTernal: [termx3/OpenMacro-XTernal](https://github.com/termx3/OpenMacro-XTernal)
- Swift é comercial e apresenta funções como pesca automática, Auto Harpoon, Auto Appraise e gestão de totems.
- XTernal é publicado sob AGPL-3.0, mas declara usar leitura da memória do Roblox para obter o estado do jogo.
- Decisão: não usar leitura de memória, injeção, executores nem técnicas de contorno. O GafiFish terá de funcionar apenas com imagem do ecrã e inputs normais do Windows.

## Conclusão

Não é necessário copiar código para chegar a uma implementação completa. Os projetos dão-nos uma lista clara de padrões úteis, mas várias implementações recentes não permitem reutilização e o exemplo GPL mais simples está arquivado. A abordagem escolhida é uma implementação própria em C# com OpenCV, captura de ecrã e inputs normais do Windows, mantendo o projeto sem dependência de código proprietário ou de leitura da memória do jogo.
