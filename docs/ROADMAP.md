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
- [x] Reel assistido opt-in com botões Start/Stop na janela e atalhos F8/F9; o botão Start aguarda o Roblox voltar a primeiro plano antes de armar.
- [x] Libertar o input ao perder foco, parar F7, falhar captura ou ocorrer erro.
- [x] Deteção visual do indicador e zona-alvo de Perfect Cast; cálculo diagnóstico de timing e latência.
- [x] Auto-cast opt-in: segura o botão até o medidor Perfect Cast indicar a janela calibrada, com timeout e libertação segura.
- [x] Auto-shake opt-in: deteta candidatos de confiança elevada, clica após dois frames estáveis e deteta o fim da sequência após quatro frames sem prompt.
- [x] Auto-recast normal: duas tentativas adicionais após 30 segundos sem mordida.
- [x] Modo opcional de tentativas ilimitadas até à primeira captura confirmada por OCR de confiança elevada (requer sessão e região de notificação calibradas).
- [x] Equipar a cana por seleção opcional do slot de hotbar 1–0.
- [x] Timeout de segurança de três minutos no estado Reel.
- [ ] Validar Auto-cast, Auto-shake, Reel, modo de captura contínua e hotbar numa sessão real do Fisch com diferentes resoluções/canas.
- [ ] Perfis por cana com calibração real.
- [ ] Melhorar a deteção com cascata de fallback (cor, contorno, template e tracking temporário), após reunir frames reais.

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
- [x] Explorador das 20 sessões mais recentes, com detalhes das últimas 15 capturas por sessão.
- [x] Exportação CSV/JSON da sessão concluída selecionada, incluindo sessões anteriores.
- [x] Exportação global de todas as capturas, com metadados da sessão, para CSV/JSON.
- [x] Resumo com até 25 melhores peixes.
- [ ] Afinação do parser com notificações reais do Fisch e nomes especiais.
- [ ] Cópia direta do resumo para a área de transferência.

## Fase 5 — Robustez e melhorias

- [x] Ajuste adaptativo da cadência de captura com perfis Economy, Balanced e Responsive.
- [x] Limites de tempo e recuperação segura nos estados Cast, Shake, espera por mordida e Reel.
- [ ] Reprodução de capturas/gravações reais para diagnóstico sem abrir o jogo.
- [x] Estatísticas acumuladas de todas as sessões: espécies mais capturadas, raridade alta, mutações, peso médio/máximo, valor estimado reconhecido por OCR e capturas por hora de sessões concluídas.
- [ ] Relatórios de diagnóstico exportáveis.
- [x] Alertas sonoros locais para espécies configuradas, raridade Legendary+ e mutações detetadas por OCR.
- [ ] HUD de telemetria com posição, erro, confiança, estado e motivo de cada decisão.
- [ ] Perfis por cana com ajuste do controlador e cores próprios.
- [ ] Auto-totem e auto-poção com horários/teclas configuráveis e bloqueio estrito durante Cast, Shake e Reel.
- [ ] Deteção de desconexão e recuperação/reconexão segura.
- [ ] Melhorias do Perfect Cast com dados medidos em sessões reais.
- [ ] Módulos independentes posteriores: Hunt Detection, Appraise/Treasure Appraise, Auto Enchant, Aquarium, Angler Quest e Harpoon; cada um requer calibração, confirmação do alvo e testes isolados.

## Próximo bloqueio técnico

O Shake e o Perfect Cast já têm deteção visual, overlay, controladores e testes sintéticos. Os inputs automáticos continuam opt-in e exigem calibração manual. A nova opção de tentativas ilimitadas e os botões da janela já estão no código, mas ainda precisam de compilação/testes CI nesta revisão e validação prática com frames reais do Fisch, sobretudo a deteção da notificação de captura e os tempos do medidor/prompt.

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


## Melhorias integradas nesta revisão

- [x] Perfis de calibração com nomes manuais por cana e transferência por ficheiros JSON.
- [x] Objetivo configurável de 1–100 capturas do peixe-alvo, com contador OCR e paragem ao completar o objetivo.
- [x] Telemetria ao vivo (posição do peixe/barra, erro, confiança, estado e FPS aproximado).
- [x] Intervalo de captura adaptativo ao tempo de processamento.
- [x] Auto-totem e auto-poção opcionais, com slot/intervalo e execução só no estado Preparing; o slot da cana é reposto após o clique.
- [x] Copiar resumo e exportar diagnóstico JSON sem capturas de ecrã nem credenciais.

## Ainda por implementar

Appraise/Treasure Appraise, Auto Enchant, Aquarium, Angler Quest, Harpoon, deteção de hunts e reconexão automática continuam fora da versão funcional. Exigem fluxos de interface e deteção visual próprios e validação real. Os perfis por cana são selecionados manualmente. Os consumíveis temporizados não confirmam a ativação pelo jogo e podem gastar um item se o slot selecionado estiver errado.


## Inspiração ExoPanda — próximos módulos

- [x] Logs locais de atividade/engine persistentes, com rotação automática, pesquisa, filtro por nível, copy/export e limite de 500 entradas visíveis.
- [x] Hotkeys globais configuráveis e persistentes, incluindo show/hide e auto-hide opcional.
- [x] Quick Info para estado dos módulos.
- [ ] Auto Aquarium: detetar o painel/temporizadores, alimentar apenas quando o cooldown permitir, confirmar a ação e distinguir auto-claim.
- [ ] Auto Appraisal: calibrar as zonas de diálogo, selecionar um peixe pela hotbar, ler peso/mutação e limitar tentativas/custo antes de repetir.
- [ ] Custom Rod handlers: implementar cada minijogo específico apenas após recolher e validar frames reais.
- [ ] Rotação de Totems por fase do dia/noite, com deteção visual do ciclo e confirmação do uso.


## Melhorias de manutenção e interface — novembro de 2026

- [x] Persistência local dos logs entre reinícios, com ficheiro ativo e um arquivo rotativo para limitar o espaço ocupado.
- [x] Pesquisa instantânea no texto dos eventos e filtro por INFO/WARN/ERROR/DEBUG.
- [x] Cópia e exportação respeitam os filtros atualmente visíveis.
- [x] Atalho visual para abrir a pasta de dados local do GafiFish.
- [x] Testes para retenção dos eventos mais recentes, rotação e limpeza do histórico.


## Catchbook — arquivo pesquisável

- [x] Nova página para consultar todas as capturas guardadas em SQLite, independentemente da sessão.
- [x] Pesquisa por espécie, raridade, mutação, peso, valor estimado e ID da sessão.
- [x] Filtros combináveis para raridade, capturas identificadas e capturas com mutação.
- [x] Ordenação por colunas, virtualização de linhas e cópia dos detalhes da captura selecionada.
- [x] Exportação CSV apenas dos resultados atualmente filtrados.
- [x] Testes para filtros combinados, valores OCR desconhecidos, pesquisa e ordenação.


## Preferências de alertas persistentes

- [x] Guardar espécies prioritárias e opções de alertas Legendary+/mutação em JSON local.
- [x] Carregar preferências automaticamente no arranque.
- [x] Gravação atómica e validação do tamanho da lista de espécies.
- [x] Testes de valores por defeito, round-trip, JSON inválido e validação.


## Alertas quantitativos e exportação — novembro de 2026

- [x] Alertas de captura opcionais por peso mínimo reconhecido pelo OCR.
- [x] Alertas de captura opcionais por valor estimado mínimo reconhecido pelo OCR.
- [x] Preferências dos novos limites guardadas no ficheiro local de alertas.
- [x] Os campos não reconhecidos pelo OCR nunca disparam alertas quantitativos.
- [x] Exportação JSON dos resultados atualmente filtrados no Catchbook, em paralelo com CSV.
- [x] Testes para limites atingidos/não atingidos, campos OCR desconhecidos e validação das preferências.
