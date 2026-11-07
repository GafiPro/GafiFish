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
- [ ] Perfis por cana com calibração real.
- [x] Fallback de template local em escala de cinzentos para peixe/barra quando a deteção por cor falha ou fica abaixo da confiança mínima.
- [ ] Afinar template e tracking temporal com gravações reais do Fisch; a captura de templates é manual e limitada a recortes de 128×128.

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
- [ ] Seleção automática de perfil por cana e afinação do controlador com dados reais.
- [x] Auto-totem (dois slots independentes) e auto-poção com slots/intervalos configuráveis, executados apenas no estado Preparing e com reposição do slot da cana.
- [ ] Confirmar visualmente a ativação de cada consumível e validar slots com capturas reais.
- [x] Watchdog visual de reconexão com região/cor calibradas, três frames estáveis, clique único e máximo de três tentativas por ativação.
- [ ] Confirmar o regresso efetivo à sessão em cada tipo de desconexão; o desaparecimento do botão não é prova de reconexão.
- [ ] Melhorias do Perfect Cast com dados medidos em sessões reais.
- [ ] Módulos posteriores: Hunt Detection, Appraise/Treasure Appraise, Auto Enchant, Angler Quest e outros minijogos por cana; dependem de frames reais e de validação isolada. Auto Aquarium já tem motor visual calibrável, mas continua por validar no Fisch real.

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

Appraise/Treasure Appraise, Auto Enchant, Angler Quest e deteção de hunts continuam por implementar; o watchdog de reconexão visual está implementado, mas a recuperação efetiva ainda exige validação no Fisch. O Auto Aquarium já tem automação visual calibrável para alimentação/reposição/claim, mas exige calibração das sete ações e validação com capturas reais do Fisch. Os perfis por cana são selecionados manualmente. Os consumíveis temporizados ainda não confirmam visualmente a ativação e podem gastar um item se o slot estiver errado.


## Inspiração ExoPanda — próximos módulos

- [x] Logs locais de atividade/engine persistentes, com rotação automática, pesquisa, filtro por nível, copy/export e limite de 500 entradas visíveis.
- [x] Hotkeys globais configuráveis e persistentes, incluindo show/hide e auto-hide opcional.
- [x] Quick Info para estado dos módulos.
- [x] Aquarium Planner: registar alimentação e reposição independentes para as três comidas, acompanhar cooldowns horários e guardar horários localmente.
- [x] Auto Aquarium: motor visual calibrável para sete botões; ações independentes de alimentação/reposição/claim, cooldown registado, deteção estável, clique único e falha fechada.
- [ ] Detetar automaticamente o painel e validar os botões/calibrações em frames reais do Fisch.
- [ ] Auto Appraisal: calibrar as zonas de diálogo, selecionar um peixe pela hotbar, ler peso/mutação e limitar tentativas/custo antes de repetir.
- [ ] Custom Rod handlers: implementar cada minijogo específico apenas após recolher e validar frames reais.
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
- [x] Exigir três frames estáveis e enviar um clique por aparição.
- [x] Aguardar dois frames sem o botão antes de permitir novo ciclo.
- [x] Intervalo de segurança de 20 segundos e máximo de três cliques por ativação.
- [x] Desarmar sem retry quando o botão não desaparece no timeout.
- [ ] Validar com diferentes ecrãs reais de desconexão e confirmar visualmente a entrada na sessão.


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
