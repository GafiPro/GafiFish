# Segurança, privacidade e tolerância a falhas

## Limites de integração

O GafiFish será uma aplicação externa de desktop:
- lê apenas imagens do ecrã;
- envia inputs normais do Windows;
- identifica a janela do Roblox para confirmar que é o destino correto.

Não será implementado:
- leitura ou escrita da memória do processo Roblox;
- injeção de DLLs, scripts, executores ou hooks no jogo;
- alteração dos ficheiros do jogo;
- interceção ou alteração do tráfego de rede;
- recolha de cookies, tokens, passwords ou dados da conta;
- técnicas destinadas a esconder a automação ou a contornar anti-cheat.

A automação pode estar sujeita às regras do Roblox e às regras próprias de Fisch. As Community Standards do Roblox incluem uma proibição de bots ou automação programada para executar tarefas disruptivas. Não é possível garantir que qualquer macro seja aceite pelos criadores do jogo ou que não resulte em penalizações.

Referência: https://about.roblox.com/community-standards

## Controlos de jogo opcionais

O Reel assistido, Auto-cast e Auto-shake estão desligados até o utilizador armar o assistente com F8 e ativar as opções pretendidas. Auto-cast e Auto-shake exigem calibração própria; as opções são bloqueadas enquanto a automação está armada.

- Auto-cast mantém o botão esquerdo premido por um máximo de 8 segundos e liberta-o apenas quando o medidor calibrado indica a zona-alvo. Se o medidor desaparecer, ficar desatualizado ou a previsão for inválida, o cast é cancelado e o input é libertado.
- Auto-shake exige deteção acima do limiar e estabilidade em dois frames. Depois de um clique, não volta a clicar no mesmo prompt; considera a sequência terminada após quatro frames sem candidato válido.
- Os testes destes controlos usam inputs simulados e frames sintéticos; ainda é necessária validação no Fisch real. Não se deve armar estas opções numa sessão importante sem confirmar a calibração e a direção do Reel.

## Venda com Sell Anywhere

A gamepass vende itens não favoritos. Para evitar perdas:
- O GafiFish não altera o estado de favorito de qualquer peixe.
- A venda só pode começar depois do fim do ciclo de pesca e com o inventário visualmente reconhecido.
- O botão tem de ser identificado de forma estável na região calibrada; não usar coordenadas fixas cegas.
- A ação é executada uma vez e o resultado tem de ser confirmado por uma alteração estável em dois fotogramas consecutivos.
- Se aparecer um diálogo inesperado, o botão não for reconhecido ou não houver confirmação, a automação pára e pede intervenção.
- O utilizador deve favoritar previamente todos os peixes/itens que pretende guardar.

## Modelo de ameaças

| Risco | Mitigação |
|---|---|
| Input enviado para a janela errada | Confirmar processo e janela ativa antes de cada ação; parar se perder foco |
| Botão fica premido após erro | Libertar todos os inputs em finally, cancelamento e encerramento |
| Deteção visual errada | Limite de confiança, confirmação em vários frames e estado neutro; Auto-shake exige estabilidade e mudança do prompt |
| Botão de cast fica premido | Limite máximo de 8 segundos e libertação em Cancel/Timeout/F9 |
| Clique repetido no mesmo Shake prompt | Requer desaparecimento ou deslocamento do candidato após cada clique |
| Frame congelado/desatualizado | Timestamp por frame e timeout rigoroso |
| Mudança de resolução/UI | Invalidar calibração e pedir nova calibração |
| Falso positivo no fim da pesca | Confirmar mais do que um sinal visual antes de gravar uma captura |
| Dependência maliciosa ou comprometida | Fixar versões, rever dependências e licenças, não executar binários desconhecidos |
| Atualização adulterada | Não executar atualizações silenciosas; verificar origem e hash antes de instalar |
| Exposição de dados | Histórico local; sem contas externas ou upload de imagens |

## Regras de paragem imediata

O motor de input deve libertar o rato e o teclado e parar quando:
- o utilizador carrega em F9 (hotkey global de emergência);
- a janela do Roblox deixa de estar em primeiro plano;
- deixa de ser possível identificar a janela correta;
- a captura falha ou excede a idade máxima permitida;
- o detetor não encontra um alvo com confiança suficiente;
- o medidor de Cast fica invisível ou desatualizado durante o carregamento;
- o Auto-shake excede o limite de espera de 20 segundos;
- ocorre uma exceção não recuperável;
- o utilizador fecha a aplicação.

F9 (ou a tecla Stop configurada) é registado num ciclo de mensagens Win32 dedicado, separado do dispatcher WPF. A primeira ação do callback é bloquear novos inputs e libertar os botões/teclas rastreados de forma síncrona; a limpeza visual dos coordenadores é colocada na fila do WPF e termina quando a interface voltar a responder. Se o Windows não permitir reservar a tecla no thread independente, a aplicação tenta o registo WPF normal e identifica explicitamente esse fallback no estado das hotkeys. A paragem também é acionada quando F7 pára, a janela Roblox perde o foco ou uma captura falha. Os cliques de ponto são associados ao PID que originou o frame e usam estados de rato rastreados para tentar libertar o botão mesmo após falha parcial do Windows. A libertação continua dependente de o Windows aceitar os eventos de input.

## Privacidade

- Configuração e histórico guardados localmente em AppData.
- Sem telemetria externa por predefinição.
- Sem serviços cloud para OCR ou classificação.
- Capturas de diagnóstico desligadas por predefinição e limitadas à região relevante.
- O resumo pode ser copiado ou exportado localmente pelo utilizador.

## Desenvolvimento seguro

- Não exigir execução como administrador.
- Fixar versões NuGet e rever licenças.
- Ativar análise estática e avisos de compilação.
- Testar parsing de configuração e OCR com entradas inválidas.
- Não executar comandos obtidos de ficheiros de configuração.
- Não guardar segredos em JSON, logs ou no repositório.
