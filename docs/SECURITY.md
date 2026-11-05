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

## Venda com Sell Anywhere

A gamepass vende itens não favoritos. Para evitar perdas:
- O GafiFish não altera o estado de favorito de qualquer peixe.
- A venda só pode começar depois do fim do ciclo de pesca e com o inventário visualmente reconhecido.
- O botão tem de ser identificado na região calibrada; não usar coordenadas fixas cegas.
- A ação é executada uma vez e o resultado tem de ser confirmado visualmente.
- Se aparecer um diálogo inesperado, o botão não for reconhecido ou não houver confirmação, a automação pára e pede intervenção.
- O utilizador deve favoritar previamente todos os peixes/itens que pretende guardar.

## Modelo de ameaças

| Risco | Mitigação |
|---|---|
| Input enviado para a janela errada | Confirmar processo e janela ativa antes de cada ação; parar se perder foco |
| Botão fica premido após erro | Libertar todos os inputs em finally, cancelamento e encerramento |
| Deteção visual errada | Limite de confiança, confirmação em vários frames e estado neutro |
| Frame congelado/desatualizado | Timestamp por frame e timeout rigoroso |
| Mudança de resolução/UI | Invalidar calibração e pedir nova calibração |
| Falso positivo no fim da pesca | Confirmar mais do que um sinal visual antes de gravar uma captura |
| Dependência maliciosa ou comprometida | Fixar versões, rever dependências e licenças, não executar binários desconhecidos |
| Atualização adulterada | Não executar atualizações silenciosas; verificar origem e hash antes de instalar |
| Exposição de dados | Histórico local; sem contas externas ou upload de imagens |

## Regras de paragem imediata

O motor de input deve libertar o rato e o teclado e parar quando:
- o utilizador carrega na hotkey de emergência;
- a janela do Roblox deixa de estar em primeiro plano;
- deixa de ser possível identificar a janela correta;
- a captura falha ou excede a idade máxima permitida;
- o detetor não encontra um alvo com confiança suficiente;
- ocorre uma exceção não recuperável;
- o utilizador fecha a aplicação.

A paragem de emergência não depende da interface gráfica estar responsiva. Deve ser tratada por um caminho curto e independente do loop de visão.

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
