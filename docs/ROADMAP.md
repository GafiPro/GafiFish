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
- [x] Replay offline de sequências de imagens PNG/JPG/BMP, com navegação/reprodução e execução apenas dos detetores visuais (sem inputs nem registos OCR).
- [x] Importação e reprodução offline de gravações de vídeo (MP4/AVI/MOV/MKV/WMV/WEBM), descodificadas um fotograma de cada vez; sem inputs nem escrita OCR.
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
- [x] Se marcadores de Reel frescos e de confiança elevada surgirem durante Casting, o macro interrompe o cast, liberta o rato e transita diretamente para Reeling.
- [x] Auto-shake opt-in: deteta candidatos de confiança elevada, clica após dois frames estáveis e deteta o fim da sequência após quatro frames sem prompt.
- [x] Auto-recast normal: duas tentativas adicionais após 30 segundos sem mordida.
- [x] Modo opcional de tentativas ilimitadas até à primeira captura confirmada por OCR de confiança elevada (requer sessão e região de notificação calibradas).
- [x] Equipar a cana por seleção opcional do slot de hotbar 1–0.
- [x] Timeout de segurança de três minutos no estado Reel.
- [ ] Validar Auto-cast, Auto-shake, Reel, modo de captura contínua e hotbar numa sessão real do Fisch com diferentes resoluções/canas.
- [x] Perfis nomeados associados pelo utilizador aos slots de hotbar da cana, com carregamento automático ao escolher o slot.
- [ ] Calibração e afinação de cada perfil com dados reais.
- [x] Fallback de template local em escala de cinzentos para peixe/barra quando a deteção por cor falha ou fica abaixo da confiança mínima.
- [ ] Afinar template e tracking temporal com gravações reais do Fisch; a captura de templates é manual e limitada a recortes de 128×128.

Critério de saída: o Reel e o Perfect Cast têm de funcionar com confiança em capturas/gravações reais; nenhum estado pode deixar um botão premido após timeout.

## Fase 3 — Sell Anywhere a cada 10 minutos

**Estado: implementada em simulação; por validar no Fisch real**

- [x] Temporizador de 10 minutos, protegido por opção explícita desligada por defeito.
- [x] Venda pendente até existir uma captura OCR de confiança elevada e o ciclo chegar a um estado seguro.
- [x] O cursor é guardado antes da venda e restaurado após fechar o inventário; uma posição fora da janela Roblox impede a venda.
- [x] Após a venda, o ciclo de pesca aguarda a transição visual do inventário antes de iniciar outro lançamento/consumível.
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
- [x] Cópia direta do resumo para a área de transferência.

## Fase 5 — Robustez e melhorias

- [x] Ajuste adaptativo da cadência de captura com perfis Economy, Balanced e Responsive.
- [x] Limites de tempo e recuperação segura nos estados Cast, Shake, espera por mordida e Reel.
- [x] Reprodução offline de sequências de imagens reais para diagnóstico sem abrir o jogo.
- [x] Reprodução direta de gravações de vídeo para diagnóstico, em streaming e com limite de 30 atualizações visuais/s.
- [x] Estatísticas acumuladas de todas as sessões: espécies mais capturadas, raridade alta, mutações, peso médio/máximo, valor estimado reconhecido por OCR e capturas por hora de sessões concluídas.
- [x] Relatórios de diagnóstico exportáveis (JSON e pacote ZIP de suporte, sem screenshots nem base de dados).
- [x] Alertas sonoros locais para espécies configuradas, raridade Legendary+ e mutações detetadas por OCR.
- [x] HUD de telemetria com posição, erro, confiança, estado, FPS de captura e motivo das decisões.
- [x] Perfis nomeados manualmente por cana, com calibração visual exportável/importável em JSON.
- [x] Seleção automática do perfil pelo slot de hotbar ou pelo nome da cana reconhecido em OCR, após duas leituras estáveis e associação explícita.
- [x] Presets Balanced, Precision, Heavy fish e Stable do controlador Reel, guardados individualmente em cada perfil de cana.
- [x] Minijogo Charge Meter Bonus calibrável: leitura percentual do medidor e libertação aos 80% apenas quando o peixe está dentro da barra.
- [x] Minijogo Dual Fish: deteção de dois marcadores por cores calibradas, controlo pelo centro entre ambos, indicador de quantos peixes estão efetivamente dentro da barra e pausa segura quando falta um alvo ou os dois não cabem na barra.
- [ ] Afinar o controlador e os perfis com dados medidos em sessões reais.
- [x] Auto-totem (dois slots independentes) e auto-poção com slots/intervalos configuráveis, executados apenas no estado Preparing e com reposição do slot da cana.
- [ ] Confirmar visualmente a ativação de cada consumível e validar slots com capturas reais.
- [x] Watchdog visual de reconexão com região/cor calibradas, três frames estáveis, clique único e máximo de três tentativas por ativação.
- [x] A confirmação de regresso exige um segundo indicador HUD calibrado, visível em três frames distintos; o desaparecimento do botão, por si só, nunca é tratado como reconexão.
- [ ] Validar o indicador HUD e o regresso efetivo à sessão em cada tipo de desconexão, com frames reais do Fisch.
- [ ] Melhorias do Perfect Cast com dados medidos em sessões reais.
- [x] Hunt Detection: alerta visual calibrável, apenas de leitura, com debounce e proteção contra frames repetidos.
- [x] Angler Quest Assist: acompanha um peixe pedido por OCR, pára após a captura confirmada, aceita o pedido e entrega o peixe através de botões visuais calibrados com clique único e confirmação de desaparecimento.
- Appraise/Treasure Appraiser e Auto Enchant estão explicitamente fora do âmbito de automação: não existe OCR de painel, tentativa automática ou clique de gasto; a interação permanece manual.
- [x] Reconhecimento OCR do nome da cana com região calibrada, duas leituras estáveis e mapeamento explícito para um perfil.
- [ ] Implementar handlers dos restantes minijogos visuais únicos depois de recolher frames reais e validar cada mecânica. Standard Reel, Charge Meter Bonus e Dual Fish têm handlers próprios/testes sintéticos; outros exemplos públicos incluem eventos de barra rotativa ou deslocação e desafios de timing, cuja implementação depende de capturas reais e confirmação da interface atual.

## Próximo bloqueio técnico

O Shake e o Perfect Cast já têm deteção visual, overlay, controladores e testes sintéticos. Os inputs automáticos continuam opt-in e exigem calibração manual. A nova opção de tentativas ilimitadas e os botões da janela já estão no código. O watchdog de reconexão e o motor visual dia/noite também têm testes sintéticos e build CI; todos continuam a precisar de validação prática com frames reais do Fisch, sobretudo a deteção da notificação de captura, o indicador dia/noite e os tempos do medidor/prompt.

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

Ainda falta validar a aplicação com capturas reais e várias resoluções: Reel/Perfect Cast/Shake, OCR, Sell Anywhere, Auto Aquarium, indicador dia/noite, reconexão e os minijogos exclusivos. Appraise/Treasure Appraiser e Auto Enchant estão fora do âmbito: não há leitura OCR desses painéis nem automatização de gastos. Os consumíveis temporizados ainda não confirmam visualmente a ativação e podem gastar um item se o slot estiver errado. Os módulos de visão têm testes sintéticos, mas esses testes não substituem uma sessão real.


## Inspiração ExoPanda — próximos módulos

- [x] Logs locais de atividade/engine persistentes, com rotação automática, pesquisa, filtro por nível, copy/export e limite de 500 entradas visíveis.
- [x] Hotkeys globais configuráveis e persistentes, incluindo show/hide e auto-hide opcional.
- [x] Quick Info para estado dos módulos.
- [x] Aquarium Planner: registar alimentação e reposição independentes para as três comidas, acompanhar cooldowns horários e guardar horários localmente.
- [x] Auto Aquarium: motor visual calibrável para sete botões; ações independentes de alimentação/reposição/claim, cooldown registado, deteção estável, clique único e falha fechada.
- [x] Detetar a presença do painel através de uma âncora de cor/região calibrada pelo utilizador; bloquear ações quando não está visível.
- [ ] Validar a âncora e os botões/calibrações em frames reais do Fisch.
Appraise/Treasure Appraiser e Enchant automáticos estão excluídos: a interação e os gastos permanecem manuais.
- [x] OCR do nome da cana, com região calibrada e associação persistente a perfis.
- [x] Presets de Reel por perfil de cana, sem assumir que substituem minijogos diferentes.
- [x] Custom Rod handlers para Standard Reel, Charge Meter Bonus, Dual Fish e Tranquility Rhythm, com calibração por perfil e testes sintéticos.
- [ ] Custom Rod handlers adicionais para eventos com barra rotativa/deslocável, zonas de bónus ou timing diferente, após recolha de frames reais e validação no jogo.
- [x] Dois slots de totem com temporizadores independentes, separados das poções e executados apenas entre ciclos de pesca.
- [x] Motor de rotação dia/noite por indicador HUD calibrado, três frames estáveis, baseline inicial sem consumo e execução em Preparing.
- [ ] Validar com capturas reais do indicador HUD do Fisch, em dia e noite, antes de usar os slots reais.


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


## Cópias de segurança portáteis — novembro de 2026

- [x] Exportar a base de dados SQLite através da API de backup online, sem cópia insegura do ficheiro ativo.
- [x] Incluir perfis/calibrações, preferências, logs e modelo OCR local.
- [x] Excluir ficheiros temporários, sidecars SQLite e backups ZIP antigos para evitar cópias recursivas.
- [x] Gerar manifesto com versão do formato, data, tamanho e SHA-256 de cada ficheiro.
- [x] Verificar backups sem extrair ficheiros, rejeitando caminhos inseguros, duplicados, ficheiros em falta ou conteúdo alterado.
- [x] Testes para snapshot SQLite, conteúdo incluído/excluído, manifesto e integridade.

- [x] Pré-visualização pós-verificação com data, tamanho e lista dos conteúdos do backup, sem extrair nem restaurar ficheiros.
- [x] Ignorar pastas/referências reparse-point ao percorrer dados locais, evitando seguir junctions/symlinks para fora da pasta de dados.


## Replay offline de frames — novembro de 2026

- [x] Guardar a captura atual como PNG para construir um conjunto de frames reais.
- [x] Carregar sequências locais PNG/JPG/JPEG/BMP, com validação de formato, existência e limite de tamanho.
- [x] Navegar frame a frame ou reproduzir a cerca de 8 FPS.
- [x] O modo replay não envia inputs, não arma o macro e não grava resultados OCR na sessão.
- [x] Importação direta de gravações MP4, M4V, AVI, MOV, MKV, WMV e WEBM com descodificação frame a frame; codecs e sincronização continuam dependentes dos ficheiros/runtime do computador.


## Alertas de cooldown do Aquarium — novembro de 2026

- [x] Alertas sonoros locais quando cada cooldown de alimentação/reposição passa a READY.
- [x] Alerta do claim condicionado ao lembrete horário.
- [x] Deduplicação por ação e timestamp para evitar spam a cada atualização do temporizador.
- [x] Preferência persistente e testes de ações independentes, novas conclusões e estados não registados.


## Auto Aquarium — motor visual calibrável — novembro de 2026

- [x] Configurações de botão independentes por ação: região normalizada e cor do estado pronto, inicialmente vazias.
- [x] Deteção OpenCV restrita à região que o utilizador selecionou para cada botão.
- [x] Coordenador de ações que aguarda cooldown registado, exige três frames estáveis e envia um único clique.
- [x] Confirmação por desaparecimento do botão ativo em dois frames consecutivos; falha fechada após timeout sem repetir.
- [x] Opções independentes de alimentação, reposição e claim, todas desligadas por defeito.
- [x] Reposição exige ativação explícita e a UI informa que pode gastar moeda do jogo.
- [ ] Validar as sete calibrações com frames reais do Fisch e testar o fluxo completo no jogo.


## Estado da versão — 7 de novembro de 2026

A revisão de código `d0a7c17` passou Restore, Build, todas as suites de testes, Publish Windows x64, Package e Upload do artefacto na [execução CI 42375885718](https://github.com/GafiPro/GafiFish/actions/runs/42375885718). O pacote Windows x64 `GafiFish-win-x64` foi gerado com sucesso.

A CI confirma compilação e testes com frames sintéticos; não substitui validação no jogo. Continuam pendentes capturas reais para afinar Reel/Perfect Cast/Shake, OCR, Sell Anywhere, Auto Aquarium, o indicador dia/noite, a recuperação efetiva da sessão e a compatibilidade de codecs no PC.

A revisão seguinte, `b0cec0b`, endureceu o Sell Anywhere: rejeita fotogramas repetidos/fora de ordem e impede que a macro seja armada quando a opção de venda está ativa mas falta sessão OCR ou calibração. A interface mostra agora o estado do coordenador e a hora prevista para a próxima venda. Foi acrescentado um teste para fotogramas duplicados e outro para fotogramas fora de ordem; a validação CI correspondente está documentada na execução 42377816941.


## Replay offline de gravações de vídeo — novembro de 2026

- [x] Importar ficheiros MP4, M4V, AVI, MOV, MKV, WMV e WEBM através do descodificador OpenCV.
- [x] Descodificar e manter apenas o fotograma atual em memória.
- [x] Navegar fotograma a fotograma e reproduzir com cadência baseada no FPS, limitando atualizações a 30 FPS e saltando frames quando necessário.
- [x] Executar os detetores visuais durante o replay, sem enviar inputs, executar automação do Aquarium ou gravar capturas OCR/sessões.
- [x] Fechar o descodificador ao terminar a aplicação ou substituir a fonte de replay.
- [ ] Validar codecs e sincronização com gravações reais captadas no computador do utilizador; a compatibilidade depende dos codecs disponíveis no runtime OpenCV.


## Watchdog de reconexão — novembro de 2026

- [x] Calibração manual da região e cor HSV do botão de reconexão.
- [x] Calibração independente de um indicador HUD que só está visível dentro da sessão carregada.
- [x] Exigir três frames estáveis do botão antes de clicar uma vez.
- [x] Após o desaparecimento do botão, exigir três frames do indicador HUD antes de declarar a reconexão confirmada.
- [x] Se não houver indicador antes do timeout, esperar por um novo botão antes de qualquer retry; máximo de três cliques por ativação.
- [x] Desarmar e pedir verificação manual se não for possível confirmar a sessão após o limite de tentativas.
- [ ] Validar as calibrações e a entrada efetiva na sessão com diferentes ecrãs reais de desconexão e várias resoluções.


## Integridade das capturas de automação — novembro de 2026

- [x] Sell Anywhere ignora fotogramas duplicados e fora de ordem; apenas capturas monotónicas contam para estabilização e confirmação.
- [x] Auto Aquarium aplica a mesma proteção antes de contar frames estáveis ou confirmar o desaparecimento de um botão.
- [x] Testes garantem que um único fotograma repetido não consegue provocar um clique nem confirmar uma ação.
- [x] Watchdog de reconexão e rotação dia/noite também só contam observações com timestamps novos; o detetor dia/noite transporta o timestamp do fotograma até ao coordenador.
- [x] Testes impedem que capturas repetidas estabeleçam um baseline ou consumam um totem.
- [x] Perfect Cast rejeita leituras duplicadas/fora de ordem e liberta o botão do rato em vez de decidir com base numa observação antiga.
- [x] O CastMeterVisionDetector ignora capturas duplicadas/fora de ordem sem alterar a posição temporal usada para calcular a velocidade do indicador.
- [x] Alterações e resets de perfis reiniciam o histórico do FishMotionTracker e do CastMeterVisionDetector, para não misturar posições/velocidades entre calibrações.
- [x] Fish motion tracking rejeita timestamps duplicados/fora de ordem e confidence não finita sem substituir a última medição válida; o Reel assist recebe uma observação neutra e liberta o input durante o frame inválido.
- [x] VisionProfileStore recupera de ficheiros com templates PNG inválidos (não apenas JSON malformado), carregando um perfil vazio em vez de interromper o arranque.

## Rotação de totens dia/noite — novembro de 2026

- [x] Calibrar região fixa e cores HSV separadas para dia e noite.
- [x] Exigir três frames consistentes e tratar leituras ambíguas/ausentes como desconhecidas.
- [x] Usar a primeira fase estável apenas como baseline, sem gastar um totem no arranque.
- [x] Guardar uma mudança confirmada até o estado de pesca ser Preparing.
- [x] Selecionar o slot correspondente, clicar uma vez e repor o slot da cana.
- [x] Impedir conflito com temporizadores de totem e validar slots distintos.
- [ ] Validar com capturas reais do indicador HUD do Fisch, em dia e noite, antes de usar os slots reais.


## Hunt Detection — implementação inicial (7 de novembro de 2026)

- [x] Detetor visual local baseado numa região e cor HSV escolhidas pelo utilizador; sem posições ou cores pré-definidas.
- [x] Alertas apenas informativos: som e registo local, sem cliques, seleção de itens ou inputs no jogo.
- [x] Debounce de três frames de deteção e rearme após três frames limpos, evitando alertas duplicados.
- [x] Rejeição de frames repetidos ou fora de ordem.
- [x] Calibração incluída nos perfis de cana e testes sintéticos do detetor/coordenador.
- [ ] Calibrar com uma imagem real da mensagem/indicador de Hunt na versão atual do Fisch e verificar falsos positivos.
Ações aleatórias que consumam moeda ou itens do jogo mantêm-se manuais e não são repetidas pelo GafiFish.
- [x] Angler Quest Assist tem tracker OCR e aceitação/entrega visual por botões calibrados; deslocação ao NPC e abertura do diálogo permanecem manuais.


## Angler Quest Assist — novembro de 2026

- [x] Integra o pedido manual do Angler com o target-fish OCR existente.
- [x] Define automaticamente o objetivo para uma captura confirmada do peixe pedido.
- [x] Pára o macro quando o alvo é reconhecido com confiança suficiente.
- [x] A entrega ao NPC pode ser confirmada visualmente através do botão Submit Fish calibrado, ou manualmente; o cooldown de 120 segundos só começa após a confirmação.
- [x] Mostra o tempo restante e bloqueia o pedido seguinte antes do fim do cooldown.
- [x] Testes para alvo, captura, entrega, cooldown e pedido seguinte.
- [x] Persistir o pedido ativo, a captura por entregar e o cooldown de 120 segundos entre reinícios.
- [x] Validar snapshots inconsistentes e bloquear novos pedidos quando o estado local não pode ser recuperado.
- [x] Testes para restauro de pedido ativo, entrega pendente e cooldown após reinício.
- [ ] Validar o OCR dos nomes dos peixes em diálogos reais do Angler e diferentes resoluções.
- [ ] Validar os botões Accept Request e Submit Fish em frames reais do Fisch e diferentes resoluções.
- [x] Aceitação e entrega do NPC com botões calibrados; deslocação até ao NPC e abertura do diálogo permanecem manuais.

## Proteção de foco e destino de input — 7 de novembro de 2026

- [x] O arranque do macro exige Roblox em primeiro plano e um frame recente da mesma instância.
- [x] O processo Roblox/PID fica fixo durante a ativação; mudar para outra janela ou outra instância invalida o macro.
- [x] Novos cliques, teclas e movimentos são recusados quando a janela ou o PID deixam de corresponder.
- [x] Ao perder o foco, o macro desarma e liberta os controlos que já estavam premidos.
- [x] Testes cobrem foco noutra aplicação, mudança de instância, cliques e movimento do cursor.
- [ ] Validar a experiência completa numa sessão real do Fisch e confirmar a recuperação manual após perda de foco.


## Paragem de emergência independente — novembro de 2026

- [x] Registar a tecla Stop num thread Win32 dedicado, sem depender do dispatcher WPF.
- [x] Bloquear novos inputs e libertar imediatamente os botões/teclas rastreados antes de agendar a limpeza visual da interface.
- [x] Sincronizar o serviço de input entre o thread de automação e o thread de emergência; a paragem fica bloqueada até um novo Arm explícito.
- [x] Manter fallback WPF quando o Windows não permite reservar a tecla no thread independente e apresentar esse estado no painel de hotkeys.
- [ ] Validar manualmente em Windows com a janela WPF deliberadamente sem responder e confirmar a libertação real do input.


## Aquarium panel guard — 8 de novembro de 2026

- [x] Calibração de uma pequena âncora visual exclusiva do painel, independente das regiões dos botões.
- [x] Verificação da âncora antes de cada ação do Auto Aquarium.
- [x] Bloquear cliques quando o painel não é detetado; se desaparecer durante a confirmação, parar sem registar cooldown ou repetir a ação.
- [x] Testes sintéticos de âncora visível/ausente, calibração incompleta, threshold inválido e bloqueio/aborto do coordenador.
- [ ] Validar a âncora com o painel real em várias resoluções e estados da interface.

## Tranquility Rhythm — implementação inicial (7 de novembro de 2026)

- [x] Adicionar modo de cana próprio sem depender das cores de fish/control do Standard Reel.
- [x] Detetar notas descendentes em quatro pistas, com uma amostra HSV independente por pista.
- [x] Calibrar a linha horizontal de receção por clique na pré-visualização.
- [x] Ignorar os recetores estáticos sobre a linha de timing.
- [x] Estimar velocidade apenas com frames monotónicos e prever a aproximação com avanço temporal configurável (0–250 ms).
- [x] Enviar apenas toques D/F/J/K no estado Reeling, com libertação imediata e desarme em caso de falha.
- [x] Testes sintéticos para quatro pistas, receptores estáticos, calibração incompleta, timing, frames repetidos, notas atrasadas e paragem.
- [ ] Validar pistas, cores, velocidade das notas e janela de timing com capturas/replay reais da Tranquility Rod em várias resoluções.
- [ ] Ajustar o avanço temporal recomendado a partir de medições reais; o valor predefinido é apenas um ponto inicial.

