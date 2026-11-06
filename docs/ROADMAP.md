# Plano de desenvolvimento e estado atual

## Fase 0 — Pesquisa e desenho

**Estado: concluída**

- [x] Identificar macros relevantes para Fisch e referências de Perfect Cast.
- [x] Verificar o Bloomfisching, conteúdo e licença.
- [x] Rever licenças e dependências.
- [x] Definir arquitetura sem leitura de memória nem injeção.
- [x] Definir Perfect Cast no MVP e Sell Anywhere de 10 em 10 minutos.
- [x] Documentar arquitetura, privacidade e riscos.

## Fase 1 — Fundação e visão

**Estado: implementada; falta validação com frames reais**

- [x] Solução .NET 10 e projetos App/Core/Windows/Vision/Data/Ocr.
- [x] Janela WPF e captura do cliente Roblox visível.
- [x] Hotkeys F6/F7 e calibração por região/cor.
- [x] Overlay de peixe/barra, Perfect Cast, Shake e Sell Anywhere.
- [x] Tracking horizontal com confiança e expiração de deteções antigas.
- [x] Testes com frames sintéticos.
- [x] Testes sintéticos de frames para cast e Shake coordinators.
- [ ] Modo de reprodução de imagens/gravações para testar sem abrir o jogo.
- [ ] Validar a calibração com capturas reais do Fisch em diferentes resoluções.

## Fase 2 — Pesca e Perfect Cast

**Estado: parcial**

- [x] Máquina de estados e decisões com timeouts no fluxo de venda.
- [x] Tracking da direção/velocidade do peixe.
- [x] Controlador PD com deadband e antecipação curta.
- [x] Reel assistido opt-in com F8 e paragem F9.
- [x] Libertar o input ao perder foco, parar F7, falhar captura ou ocorrer erro.
- [x] Deteção visual do indicador e zona-alvo de Perfect Cast; cálculo diagnóstico de timing e latência.
- [x] Auto-cast opt-in: segura o botão até o medidor Perfect Cast indicar a janela calibrada, com timeout e libertação segura.
- [x] Auto-shake opt-in: deteta candidatos de confiança elevada, clica após dois frames estáveis e deteta o fim da sequência após quatro frames sem prompt.
- [x] Auto-recast limitado: duas tentativas adicionais após 30 segundos sem mordida; pára depois do limite.
- [x] Equipar a cana por seleção opcional do slot de hotbar 1–0.
- [x] Timeout de segurança de três minutos no estado Reel.
- [ ] Validar Auto-cast, Auto-shake, Reel e hotbar numa captura/sessão real do Fisch com diferentes resoluções/canas.
- [ ] Perfis por cana com calibração real.

Critério de saída: o Reel e o Perfect Cast têm de funcionar com confiança em capturas/gravações reais; nenhum estado pode deixar um botão premido após timeout.

## Fase 3 — Sell Anywhere a cada 10 minutos

**Estado: implementada em simulação; por validar no Fisch real**

- [x] Temporizador de 10 minutos, protegido por opção explícita desligada por defeito.
- [x] Venda pendente até existir uma captura OCR de confiança elevada e o ciclo chegar a um estado seguro.
- [x] Abrir/fechar o inventário com a janela Roblox em primeiro plano.
- [x] Reconhecer um botão calibrado e estável.
- [x] Enviar um único clique por tentativa.
- [x] Confirmar uma alteração de inventário em dois fotogramas consecutivos.
- [x] Parar e libertar inputs perante botão ausente, confirmação incerta ou timeout.
- [ ] Testar com a gamepass ativa e a interface real do Fisch.

Nota: Sell Anywhere vende itens não favoritos. O utilizador tem de marcar como favoritos os peixes que pretende guardar. Não se usam APIs internas do jogo.

## Fase 4 — Capturas e resumo

**Estado: implementada**

- [x] OCR local da notificação de captura.
- [x] Parser conservador de espécie, raridade, mutação, peso e valor quando reconhecidos.
- [x] Deduplicação temporal de notificações.
- [x] Histórico de sessões e capturas em SQLite.
- [x] Resumo com até 25 melhores peixes.
- [x] Exportação CSV/JSON.
- [ ] Afinação do parser com notificações reais do Fisch e nomes especiais.
- [ ] Cópia direta do resumo para a área de transferência.

## Fase 5 — Robustez e melhorias

- [ ] Ajuste adaptativo à taxa de frames.
- [x] Limites de tempo e recuperação segura nos estados Cast, Shake, espera por mordida e Reel.
- [ ] Reprodução de capturas/gravações reais para diagnóstico sem abrir o jogo.
- [ ] Estatísticas de vendas e rendimento por hora.
- [ ] Relatórios de diagnóstico exportáveis.
- [ ] Alertas para capturas prioritárias e preferências de espécie/mutação.
- [ ] Melhorias do Perfect Cast com dados medidos em sessões reais.

## Próximo bloqueio técnico

O Shake e o Perfect Cast já têm deteção visual, overlay, controladores e testes sintéticos. Os inputs automáticos continuam opt-in e exigem calibração manual. O último bloqueio para fechar a validação do produto é testar com frames reais do teu Fisch, confirmar o sentido do Reel e os tempos do medidor/prompt.

## Funcionalidades que não fazem parte do plano

- Leitura ou alteração da memória do Roblox.
- Injeção de código, executores ou scripts dentro do jogo.
- Contorno de anti-cheat.
- Serviços de IA externos que exijam enviar capturas ou tokens.
- Atualizadores que executem binários sem validação explícita.

## Regras de qualidade

- Cada módulo tem testes unitários ou testes com frames gravados.
- Cada alteração ao controlador inclui medições antes/depois.
- Dependências e licenças são revistas.
- Bugs de input, foco errado e vendas sem confirmação têm prioridade sobre novas funcionalidades.
- Uma melhoria nunca pode reduzir a segurança da paragem.
