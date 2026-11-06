# Guia de utilização do GafiFish

## 1. Captura

O GafiFish captura apenas o cliente Roblox visível e em primeiro plano.

- **F6**: captura um único fotograma.
- **F7**: inicia ou pára a observação contínua (intervalo de aproximadamente 120 ms).

As teclas são globais. Podes deixar o Roblox em primeiro plano, usar F6/F7 e voltar ao GafiFish com Alt+Tab para inspecionar a última imagem.

## 2. Reel assistido e paragem de emergência

O Reel assistido é opcional e só deve ser armado depois de calibrares:

1. A **Fishing search region**.
2. A cor do marcador do peixe.
3. A cor da barra de controlo.

Com o Roblox em primeiro plano e a observação F7 ativa:

- **Start macro (F8)** na janela ou F8 arma o controlador hold/release e as opções selecionadas.
- **Stop macro (F9)** na janela ou F9 pára a automação e liberta imediatamente o rato e as teclas.
- Se o Roblox perder o foco ou F7 for parado, o GafiFish também desarma e liberta os inputs.

O controlador só atua quando ambos os marcadores estão presentes e acima do limiar de confiança. Se um desaparecer, o botão esquerdo é libertado. A opção **Invert hold/release mapping** permite inverter o sentido do controlo; testa-a apenas numa situação segura. Antes de iniciar o macro, podes ativar as opções de automação desligadas por defeito:
- **Equipar cana** (opcional): marca a caixa e escolhe o slot de hotbar que contém a cana (1–9 ou 0). Ao pressionar F8, o GafiFish envia essa tecla uma vez através do input normal do Windows. Se não ativares a opção, deixa a cana equipada manualmente.
- **Auto-cast e auto-recast**: exige medidor Cast calibrado. Segura o botão esquerdo e liberta-o quando o Perfect Cast prevê a janela-alvo; cancela se a leitura ficar inválida ou ultrapassar 8 segundos. Por defeito, se não surgirem marcadores de Reel em 30 segundos, tenta novamente até duas vezes; depois pára em segurança.
- **Keep fishing until the first fish is caught**: opção desligada por defeito. Exige Auto-cast, sessão OCR ativa e região da notificação calibrada. Repete os lançamentos sem limite de tentativas em falta de mordida e termina quando uma notificação de captura é reconhecida com confiança igual ou superior a 90% durante o estado Reel.
- **Fish until this exact target is caught**: escreve o nome pretendido (por exemplo, “Wrath Scylla”). Exige os mesmos pré-requisitos, faz tentativas sem limite e só termina quando uma notificação de captura incluir todos os termos do alvo com confiança OCR de pelo menos 90%. Outras capturas não terminam a procura. Se um Reel durar mais de três minutos, liberta o controlo e reinicia o ciclo. F9, perda de foco do Roblox ou paragem de F7 continuam a parar a automação.
- **Auto-shake**: exige região e cor Shake calibradas. Clica só após dois frames estáveis acima do limiar, não repete o mesmo prompt e termina a sequência depois de quatro frames sem candidatos.

A direção e o timing do Reel/Cast/Shake foram testados com inputs simulados e frames sintéticos, não numa sessão real do Fisch. A calibração visual e a resposta da tua cana/resolução ainda precisam de validação prática.



## 3. Calibração

A calibração é guardada em `%LOCALAPPDATA%\GafiFish\vision-profile.json`.

Para selecionar uma região, captura primeiro um fotograma, escolhe a ação na secção **Visual calibration** e arrasta um retângulo sobre a pré-visualização.

Para amostrar uma cor, escolhe a ação correspondente e clica num pixel limpo do marcador. O GafiFish converte o pixel para uma gama HSV com tolerância.

Regiões disponíveis:

- **Fishing search region**: área do minijogo onde aparecem o peixe e a barra de controlo.
- **Cast meter region**: área do medidor Perfect Cast.
- **Shake region**: área onde aparecem os prompts.
- **Sell button region** e **inventory items area**: calibração do botão Sell Anywhere e da lista usada na verificação visual.
- **Catch notification region**: área da notificação de peixe capturado que será lida pelo OCR.

Não uses uma captura de outro tamanho de janela/resolução sem voltar a confirmar a calibração. O perfil guarda a resolução de referência, mas a região normalizada é reaplicada proporcionalmente.

## 4. OCR e sessões

Antes de iniciares uma sessão, calibra **Catch notification region**.

Ao pressionar **Start session + local OCR**:

1. O histórico SQLite é inicializado.
2. O modelo inglês oficial é verificado; se não existir, é descarregado uma vez.
3. O motor Tesseract é inicializado localmente.
4. É criada uma sessão no histórico.

Quando F7 está ativo, o OCR analisa no máximo cerca de um fotograma por segundo e apenas a região calibrada. O mesmo texto de notificação é temporariamente deduplicado para não registar a mesma captura em todos os fotogramas.

O parser atual reconhece uma notificação inglesa que contenha `You caught [species]`. O OCR é genérico e pode confundir nomes estilizados ou texto pequeno; o parser deixa os campos não reconhecidos vazios em vez de inventar dados.

A secção **Recent sessions** mostra as 20 sessões mais recentes. Seleciona uma sessão para consultar a duração, o estado e as últimas 15 capturas reconhecidas. A lista é atualizada no arranque, ao iniciar/terminar uma sessão, após uma captura e através de **Refresh session history**. **Export CSV** e **Export JSON** exportam a sessão concluída que estiver selecionada, incluindo sessões antigas.

Ao terminar a sessão, o GafiFish guarda a hora de fim e mostra o total, a taxa de capturas por hora e até 25 peixes ordenados pela raridade, mutação, peso e valor reconhecidos. Os botões **Export CSV** e **Export JSON** guardam o histórico completo dessa sessão, incluindo campos desconhecidos como valores vazios/nulos.

## 5. Sell Anywhere e limites da versão atual

O agendamento Sell Anywhere só é criado quando, antes de pressionares F8, marcas a caixa **Enable Sell Anywhere every 10 minutes**, existe uma sessão OCR ativa e estão calibradas a região/cor do botão e a área da lista de itens. A primeira venda só é tentada depois de 10 minutos e de uma notificação de captura OCR com confiança elevada durante o estado Reel.

O fluxo verifica primeiro se o botão já está visível. Se o inventário estiver fechado, abre-o; se já estiver aberto, preserva-o. Depois espera por várias deteções estáveis do botão, envia um único clique e exige uma alteração da lista em dois fotogramas consecutivos. Só fecha o inventário quando foi o próprio GafiFish a abri-lo. Se o botão não for encontrado ou a lista não mudar de forma estável, o GafiFish pára e liberta os inputs; não repete o clique às cegas.

Sell Anywhere vende o que não estiver marcado como favorito. A opção está desligada por defeito e exige confirmação explícita na caixa da interface. Marca como favorito tudo o que pretendes guardar antes de a ativares. O fluxo ainda precisa de validação prática com a tua interface do Fisch e a gamepass ativa.

O Auto-cast usa o indicador e a zona verde do medidor calibrado; o Auto-shake usa os candidatos de cor/forma na região calibrada. Ambos são opções na interface, desligadas por defeito, e ainda precisam de ser validados com uma captura real. O GafiFish pode equipar a cana no slot escolhido e fazer recast automático. O modo normal limita-se a duas tentativas adicionais sem mordida; o modo “Keep fishing until the first fish is caught” remove esse limite e pára após uma captura confirmada por OCR. A deteção visual ainda requer calibração real. Não alteres o perfil para forçar cliques com coordenadas ou cores adivinhadas.


## 6. Perfis, consumíveis e diagnóstico

- **Perfis por cana**: escreve um nome no campo Rod calibration profile e usa Load profile ou Save profile. O perfil Default mantém a calibração antiga; os outros são guardados em `%LOCALAPPDATA%\GafiFish\rod-profiles`. A seleção é manual: o programa não identifica automaticamente a cana equipada.
- **Capture performance**: Economy reduz a frequência de análise para diminuir o uso esperado de CPU; Balanced é a opção recomendada; Responsive tenta analisar mais frequentemente e pode aumentar o uso de CPU. A cadência continua adaptativa ao tempo de processamento de cada fotograma.
- **Perfis transferíveis**: usa Import profile JSON / Export profile JSON para mover calibrações entre PCs. O ficheiro contém apenas parâmetros de calibração, não imagens nem credenciais.
- **Arquivo completo**: Export all CSV / Export all JSON exporta todas as capturas locais, incluindo o identificador e as horas de início/fim da sessão associada. Export session continua limitado à sessão selecionada.
- **Auto-totem / Auto-potion**: desligados por defeito. Exigem Auto-cast e Equip my rod. Podes configurar até dois slots de totem e um de poção, cada um com intervalo independente entre 1 e 720 minutos. As ações só correm em Preparing, entre ciclos completos: selecionam o slot, enviam um clique normal único e repõem o slot da cana. Se várias ações ficarem pendentes, são separadas por um intervalo de segurança. Cada uso pode consumir um item e não existe confirmação visual de sucesso; verifica todos os slots antes de ativar. A rotação é por temporizador, não deteta ainda o ciclo dia/noite nem o estado do servidor.
- **Telemetria**: mostra estado, posição do peixe e barra, erro estimado, confiança e FPS aproximado da captura.
- **Diagnóstico**: Export diagnostics guarda um JSON com sistema, perfil, resolução, estado e calibrações, sem imagens nem credenciais. Copy session summary copia o resumo concluído.


## 7. Alertas de capturas e estatísticas acumuladas

- **Espécies prioritárias**: na secção Catch alerts, escreve fragmentos de nomes separados por vírgulas. Exemplo: `Scylla, Megalodon`. O alerta compara estes termos com a espécie reconhecida por OCR, sem distinguir maiúsculas/minúsculas.
- **Raridade alta**: a opção Legendary+ está ligada por defeito e alerta para Legendary, Mythical, Exotic, Secret e Apex.
- **Mutações**: ativa a caixa correspondente para receber um som quando o OCR reconhecer uma mutação.
- **Lifetime statistics**: agrega o histórico SQLite local: sessões, capturas, espécies mais capturadas, mutações, raridades Legendary+, pesos médio/máximo, valor estimado reconhecido no texto e capturas por hora de sessões concluídas. O valor é uma estimativa lida pelo OCR, não o saldo real da conta.


## 8. Objetivo de capturas do peixe-alvo

No modo **Fish until this exact target is caught**, o campo de quantidade aceita de 1 a 100 capturas. O contador avança apenas quando o nome do peixe corresponde à notificação OCR, a confiança é pelo menos 90% e a deteção passa a deduplicação. A automação termina quando o número pedido é atingido. O modo **Keep fishing until the first fish is caught** continua a ser uma opção independente.


## 9. Aquarium Planner

A página **Aquarium** acompanha os ciclos horários das três comidas do Personal Aquarium: Guppies' Best, Fish Chow e Splash Snacks.

Depois de alimentares ou comprares uma comida no jogo, seleciona **Mark fed** ou **Mark restocked** na respetiva secção. O GafiFish guarda o momento localmente e mostra quanto falta até a próxima janela horária. Os registos de cada comida e de cada ação são independentes e continuam disponíveis depois de fechares a aplicação.

A opção **Enable hourly reward-claim reminder** mostra o tempo desde a última recolha de recompensas. Depois de recolheres no jogo, seleciona **Mark reward claimed** para iniciar o próximo contador. A opção controla apenas o lembrete; não recolhe recompensas automaticamente.

O Aquarium Planner não abre o painel, não compra comida, não alimenta peixes nem clica no jogo. É um contador persistente para acompanhar os tempos sem fazer ações não confirmadas.

## 10. Hotkeys, janela e logs

- **Hotkeys globais**: em Settings, escolhe uma tecla de função diferente (F2–F12) para capturar um frame, observar, iniciar, parar e mostrar/ocultar a janela. As teclas são guardadas em `%LOCALAPPDATA%\GafiFish\app-settings.json`.
- **Ocultar a janela**: ativa Hide the GafiFish window while the fishing macro is running. Usa a tecla Show / hide configurada (F10 por defeito) para recuperar a janela. A tecla de emergência continua registada com a janela oculta.
- **Activity & engine log**: a página Logs guarda os 500 eventos mais recentes: ações, sessões, capturas, perfis, hotkeys e erros. Podes copiar, exportar para TXT ou limpar o registo. Os eventos ficam apenas em memória até exportares.
- **Dashboard Quick Info**: apresenta o estado dos módulos e requisitos do Sell Anywhere num único painel.


## Catchbook

A página **Catchbook** permite consultar o arquivo completo de capturas reconhecidas em todas as sessões guardadas localmente. Pesquisa por nome, raridade, mutação, peso, valor estimado ou ID da sessão. Também podes combinar os filtros **Identified only**, **Has mutation** e uma raridade específica. As colunas podem ser ordenadas clicando nos cabeçalhos.

- **Copy selected** copia os detalhes da captura selecionada.
- **Export filtered CSV** exporta apenas as linhas atualmente visíveis após aplicar os filtros.
- **Refresh** volta a consultar a base SQLite local.

Os campos que o OCR não conseguiu reconhecer aparecem como desconhecidos ou vazios; o Catchbook não tenta inferir esses valores.


## Preferências de alertas de capturas

Na página **Sessions**, define as espécies prioritárias (separadas por vírgulas ou ponto e vírgula) e escolhe se queres alertas para raridades Legendary+ e/ou mutações. Usa **Save alert preferences** para guardar as opções em `%LOCALAPPDATA%\\GafiFish\\catch-alert-settings.json`. As preferências são carregadas automaticamente quando a aplicação inicia.


## Replay offline de frames

Na página **Calibration**, usa **Save current frame** para guardar uma captura PNG feita com F6. Junta várias imagens numa sequência com nomes ordenados e preenchidos com zeros (por exemplo, `frame_001.png`, `frame_002.png`, `frame_003.png`) e carrega-as com **Load image sequence**. Podes avançar, recuar ou reproduzir a sequência a cerca de 8 frames por segundo.

O replay executa apenas a apresentação e os detetores visuais para inspeção/calibração. Não envia cliques nem teclas para o Roblox, não arma a automação e não escreve capturas OCR no histórico da sessão. Formatos aceites: PNG, JPG/JPEG e BMP; máximo de 2500 imagens por sequência. Os ficheiros são lidos localmente e não são enviados para lado nenhum.


## Alertas de cooldown do Aquarium

Na página **Aquarium**, ativa **Play a sound when an aquarium action becomes ready** para receber um som local quando um dos cooldowns registados terminar. Cada alimentação/reposição é alertada independentemente; o claim só gera alerta quando o respetivo lembrete horário está ativo. Cada conclusão só gera um alerta por timestamp, sem repetir o som a cada segundo. Os alertas funcionam enquanto o GafiFish estiver aberto e não executam ações dentro do jogo.


## Auto Aquarium (calibração visual)

A automação do Aquarium é independente da pesca e tem de ser armada separadamente na página **Aquarium**. Primeiro abre o painel do Aquarium em Fisch e captura um frame com F6. Para cada ação que pretendes automatizar, escolhe-a na lista, seleciona uma região apertada à volta do respetivo botão ativo e amostra uma cor no interior desse botão. Repete para todas as ações que ativares.

As opções **Auto feed**, **Auto restock** e **Auto claim** são independentes e começam desligadas. A reposição pode gastar moeda do jogo e, por isso, exige ativação explícita. As ações só ficam elegíveis quando o respetivo horário inicial já foi registado e o cooldown horário terminou. Mantém o painel do Aquarium aberto e o Roblox em primeiro plano: o GafiFish não tenta navegar até ao Aquarium.

O motor espera por três frames estáveis antes de enviar um clique único. Só atualiza o horário depois de o botão na cor de estado ativo desaparecer em dois frames consecutivos. Se a confirmação não acontecer no prazo de cinco segundos, desarma sem repetir o clique. A calibração depende da resolução, tema e estado da interface; testa primeiro cada ação e não uses coordenadas ou cores aproximadas.
