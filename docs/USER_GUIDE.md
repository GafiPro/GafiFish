# Guia de utilização do GafiFish

## Interface e switches

O Dashboard concentra a pré-visualização, o estado da captura e os controlos principais. Os separadores ocupam a largura disponível e as opções contínuas usam switches:

- **Dashboard**: liga/desliga a observação contínua e o macro de pesca.
- **Sessions**: liga/desliga a gravação de uma sessão OCR local. O switch fica temporariamente bloqueado enquanto a sessão está a iniciar ou a terminar.
- **Aquarium**: liga/desliga o Auto Aquarium; os switches Feed, Restock e Claim definem as ações permitidas.
- **Fishing**: cada opção automática (Auto-cast, Auto-shake, Sell Anywhere, consumíveis, alvo, etc.) tem o seu próprio switch.
- **Angler** e **Connection watchdog**: cada módulo tem um switch de ativação próprio.

Quando um módulo não pode arrancar por falta de calibração, foco ou conflito com outro módulo, o switch regressa a desligado e o estado explica o motivo. As ações de uma só vez — capturar um frame, exportar dados, marcar uma alimentação — continuam a ser botões.

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

- Liga o switch **Fishing macro · Reel assist** no Dashboard ou prime F8 para armar o controlador hold/release e as opções selecionadas.
- Desliga o mesmo switch ou prime F9 para parar a automação e libertar imediatamente o rato e as teclas.
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

Como fallback, podes capturar um template de cada marcador: escolhe **Capture fish marker template** ou **Capture control marker template** e desenha um retângulo apertado (máximo 128×128) à volta do marcador num frame limpo. Os recortes são convertidos para PNG em escala de cinzentos e guardados no perfil local. Durante a pesca, HSV continua a ser a primeira escolha quando a confiança é suficiente; o template é usado quando a deteção por cor falha ou fica fraca.

Regiões disponíveis:

- **Fishing search region**: área do minijogo onde aparecem o peixe e a barra de controlo.
- **Cast meter region**: área do medidor Perfect Cast.
- **Shake region**: área onde aparecem os prompts.
- **Sell button region** e **inventory items area**: calibração do botão Sell Anywhere e da lista usada na verificação visual.
- **Catch notification region**: área da notificação de peixe capturado que será lida pelo OCR.

Não uses uma captura de outro tamanho de janela/resolução sem voltar a confirmar a calibração. O perfil guarda a resolução de referência, mas a região normalizada é reaplicada proporcionalmente.

## 3.1 Replay offline de vídeo

Na página **Fishing**, na secção **Offline frame replay**, escolhe **Load video** e seleciona um ficheiro MP4, M4V, AVI, MOV, MKV, WMV ou WEBM. Usa **Previous**, **Next**, **Play** e **Pause** para inspecionar a gravação. Para vídeos acima de 30 FPS, o replay pode saltar fotogramas para manter a interface responsiva.

O replay descodifica apenas o fotograma atual. Os detetores visuais e overlays continuam ativos para ajudar a rever a calibração, mas não envia cliques/teclas, não executa Auto Aquarium e não grava OCR nem capturas na base de dados. O suporte de cada codec depende do ficheiro e das bibliotecas OpenCV disponíveis. Se o vídeo não abrir, exporta alguns frames PNG com a ferramenta de gravação e usa **Load image sequence**.

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

O fluxo guarda primeiro a posição do cursor dentro da janela Roblox. Verifica depois se o botão já está visível; se o inventário estiver fechado, abre-o. Espera por várias deteções estáveis do botão, envia um único clique e exige uma alteração da lista em dois fotogramas consecutivos. Após confirmar a venda, fecha sempre o inventário (mesmo que já estivesse aberto antes) e restaura a posição original do cursor antes de retomar a pesca. A macro aguarda brevemente que a interface feche antes de iniciar outro lançamento. Se não conseguir guardar/restaurar o cursor, encontrar o botão ou confirmar a alteração, o GafiFish pára e liberta os inputs; não repete o clique às cegas.

Sell Anywhere vende o que não estiver marcado como favorito. A opção está desligada por defeito e exige confirmação explícita na caixa da interface. Marca como favorito tudo o que pretendes guardar antes de a ativares. Durante a automação, o estado do Sell Anywhere e a hora prevista da próxima venda aparecem junto à deteção do botão. O cronómetro é reiniciado apenas depois de uma venda visualmente confirmada.

O fluxo ainda precisa de validação prática com a tua interface do Fisch e a gamepass ativa.

O Auto-cast usa o indicador e a zona verde do medidor calibrado; o Auto-shake usa os candidatos de cor/forma na região calibrada. Ambos são opções na interface, desligadas por defeito, e ainda precisam de ser validados com uma captura real. O GafiFish pode equipar a cana no slot escolhido e fazer recast automático. O modo normal limita-se a duas tentativas adicionais sem mordida; o modo “Keep fishing until the first fish is caught” remove esse limite e pára após uma captura confirmada por OCR. A deteção visual ainda requer calibração real. Não alteres o perfil para forçar cliques com coordenadas ou cores adivinhadas.


## 6. Perfis, consumíveis e diagnóstico

- **Perfis por cana**: escreve um nome no campo Rod calibration profile e usa Load profile ou Save profile. Para seleção por slot, ativa **Equip my rod**, escolhe o slot de hotbar e prime **Bind active profile**.
- **Reconhecimento da cana**: com o nome visível no HUD, captura F6, seleciona **Select name area** e delimita apenas o texto. Usa **Recognize now** para confirmar a leitura e associa o nome ao perfil ativo. A opção **Recognize rod name while observing** inicializa o OCR local, exige duas leituras estáveis antes de trocar de perfil e só muda de perfil com o macro parado. As associações de nome e slot ficam guardadas no ficheiro local de bindings.
- **Afinação Reel por cana**: cada perfil guarda uma opção Balanced, Precision, Heavy fish ou Stable. Escolhe a afinação enquanto o macro está parado.
- **Minijogo Charge Meter Bonus**: seleciona esse modo no perfil da cana. Em Calibration, delimita o percurso completo do medidor (0–100%) e amostra uma cor no interior da parte preenchida. Define a direção do enchimento e escolhe o limiar do perfil (80%, 90% ou 100%). O macro liberta uma vez por ciclo apenas quando o marcador do peixe está dentro da barra; em medidores cíclicos, só volta a libertar depois de observar uma leitura nova de 20% ou menos. Mede o limiar numa gravação real da cana antes de usar a automação.
- **Minijogo Dual Fish**: seleciona esse modo para canas cujo minijogo mostra dois peixes em simultâneo. Na Calibration, amostra o primeiro peixe, o segundo peixe e a barra de controlo; podes repetir a mesma amostra se os marcadores tiverem a mesma cor. O GafiFish usa o centro entre os dois alvos, apresenta quantos dos dois peixes estão dentro da barra e liberta o input se a distância entre alvos exceder a largura da barra. Se perder um dos peixes, o controlo é suspenso. Valida este modo com a interface real antes de o usares numa sessão normal.
- **Minijogo Tranquility Rhythm**: seleciona esse modo no perfil da Tranquility Rod. Primeiro delimita as quatro pistas com **Select search region**. Depois, na secção Tranquility Rhythm calibration, amostra uma nota em cada pista D, F, J e K, e clica na linha dos recetores com **Select rhythm hit line**. O modo não exige as cores dos marcadores do Standard Reel. O controlador espera por duas capturas novas para estimar a velocidade vertical, prevê a posição da nota usando o avanço temporal configurado (0–250 ms) e toca a tecla correspondente apenas no estado Reeling. Revê gravações com o replay offline e começa com uma sessão de teste; ainda falta validar a deteção e o timing no Fisch real.
- **Barras rotativas/deslocáveis**: o detetor preserva a geometria orientada da barra e estima a interseção horizontal à altura do peixe. Um segundo tracker mede também a deslocação horizontal da barra e o controlador usa a velocidade relativa peixe/barra. Frames repetidos ou fora de ordem não atualizam o tracking. Continua a exigir cores/região calibradas e validação com frames reais antes de usar a automação.
- **MiguRod:** a aplicação inclui deteção visual calibrável e alertas locais para o aviso/zona de corte. Em Calibration, seleciona a região do aviso, amostra a sua cor numa captura real e ativa os alertas. É um módulo de observação: não executa movimentos de evasão. Valida o reconhecimento com várias capturas antes de confiares no alerta.
- **Jinglestar:** seleciona este modo no perfil da cana. Introduz os tempos de pressão e libertação (20–500 ms) medidos numa gravação e confirma-os. O handler executa dez pulsos, aguarda o cooldown de dois segundos e pode repetir a sequência enquanto o peixe continua em Reeling. Liberta o botão quando perde deteções frescas ou quando o estado muda. Os valores predefinidos não são uma calibração do jogo.
- **D.R.E.D.:** usa o modo D.R.E.D. Dual Fish + passive. Partilha a deteção dos dois peixes e apresenta a estimativa do ciclo de cargas/OVERDRIVE. Em Calibration, delimita a zona onde os objetos bónus caem e amostra a cor de um objeto real; ativa os alertas para receber uma notificação apenas depois de associar um candidato ao longo de fotogramas distintos e confirmar movimento descendente. O painel distingue candidatos de objetos com trajetória descendente; isto não prova que foram recolhidos. O GafiFish não tenta recolhê-los automaticamente nem ativa Overdrive sem confirmar a interação real.
- As barras rotativas/deslocáveis já têm geometria e tracking de movimento partilhados, mas ainda precisam de calibração/validação com gravações reais.
- **Capture performance**: Economy reduz a frequência de análise para diminuir o uso esperado de CPU; Balanced é a opção recomendada; Responsive tenta analisar mais frequentemente e pode aumentar o uso de CPU. A cadência continua adaptativa ao tempo de processamento de cada fotograma.
- **Perfis transferíveis**: usa Import profile JSON / Export profile JSON para mover calibrações entre PCs. O ficheiro contém apenas parâmetros de calibração, não imagens nem credenciais.
- **Arquivo completo**: Export all CSV / Export all JSON exporta todas as capturas locais, incluindo o identificador e as horas de início/fim da sessão associada. Export session continua limitado à sessão selecionada.
- **Auto-totem / Auto-potion**: desligados por defeito. Exigem Auto-cast e Equip my rod. Podes configurar até dois slots de totem temporizado e um de poção, cada um com intervalo independente entre 1 e 720 minutos. As ações temporizadas só correm em Preparing, entre ciclos completos: selecionam o slot, enviam um clique normal único e repõem o slot da cana. Em alternativa, a rotação dia/noite usa a região HUD e as cores calibradas para escolher o slot correspondente à fase, sem gastar um item na primeira fase reconhecida. Cada uso temporizado pode consumir um item e ainda não existe confirmação visual de ativação; verifica todos os slots antes de ativar e valida com frames reais.
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
- **Activity & engine log**: a página Logs mostra os 500 eventos mais recentes e mantém um ficheiro local com rotação automática em `%LOCALAPPDATA%\GafiFish\logs`. Podes pesquisar, filtrar, copiar, exportar para TXT ou limpar o registo.
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


## Proteção de OCR contra a notificação falsa da Silly Fun Happy Rod

A Silly Fun Happy Rod pode apresentar uma notificação deliberadamente falsa de 10x Shiny Sparkling Giant Atlantean Nessies. O parser ignora esse padrão conhecido para não o guardar no histórico nem o tratar como conclusão de um objetivo de peixe. Esta exceção não substitui a validação do OCR com notificações reais.

## Alertas de cooldown do Aquarium

Na página **Aquarium**, ativa **Play a sound when an aquarium action becomes ready** para receber um som local quando um dos cooldowns registados terminar. Cada alimentação/reposição é alertada independentemente; o claim só gera alerta quando o respetivo lembrete horário está ativo. Cada conclusão só gera um alerta por timestamp, sem repetir o som a cada segundo. Os alertas funcionam enquanto o GafiFish estiver aberto e não executam ações dentro do jogo.


## Auto Aquarium (calibração visual)

A automação do Aquarium é independente da pesca e tem de ser armada separadamente na página **Aquarium**. Primeiro abre o painel do Aquarium em Fisch e captura um frame com F6. Seleciona uma pequena área uniforme exclusiva do painel (longe dos botões) e amostra a sua cor usando **Select panel anchor area** e **Sample open panel colour**. Depois, para cada ação, escolhe-a na lista, seleciona uma região apertada à volta do respetivo botão ativo e amostra uma cor no interior desse botão. Repete para todas as ações que ativares. O GafiFish verifica a âncora antes de cada ação; se o painel não estiver visível, não clica. Se desaparecer enquanto uma ação aguarda confirmação, o módulo pára sem registar o cooldown nem repetir o clique.

As opções **Auto feed**, **Auto restock** e **Auto claim** são independentes e começam desligadas. A reposição pode gastar moeda do jogo e, por isso, exige ativação explícita. As ações só ficam elegíveis quando o respetivo horário inicial já foi registado e o cooldown horário terminou. Mantém o painel do Aquarium aberto e o Roblox em primeiro plano: o GafiFish não tenta navegar até ao Aquarium.

O motor espera por três frames estáveis antes de enviar um clique único. Só atualiza o horário depois de o botão na cor de estado ativo desaparecer em dois frames consecutivos. Se a confirmação não acontecer no prazo de cinco segundos, desarma sem repetir o clique. A calibração depende da resolução, tema e estado da interface; testa primeiro cada ação e não uses coordenadas ou cores aproximadas.

### Mecânicas especiais — calibração de alertas

Os detetores MiguRod e D.R.E.D. usam regiões e cores HSV escolhidas pelo utilizador. Captura um frame com F6 quando o aviso/objeto real estiver visível, seleciona uma região apertada e amostra um pixel limpo do marcador. O overlay mostra os candidatos que correspondem à calibração. O MiguRod usa debounce de frames distintos; o D.R.E.D. associa candidatos entre frames e exige deslocação descendente antes do debounce do alerta. Os alertas só rearmam após vários frames sem o sinal. A reprodução offline mostra os overlays, mas não toca alertas nem envia inputs. Estes módulos são deliberadamente apenas de observação: não assumem que um objeto foi recolhido e não enviam movimentos, cliques ou teclas para mecânicas ainda não validadas.


## Watchdog de reconexão

1. Abre o separador **Calibration** e captura um frame F6 quando o botão de reconexão estiver visível.
2. Escolhe **Select reconnect button region** e desenha um retângulo apertado à volta do botão.
3. Escolhe **Sample reconnect button colour** e clica num pixel limpo dentro do botão.
4. Ainda em **Calibration**, entra numa sessão Fisch carregada e captura outro frame F6.
5. Escolhe **Select in-game HUD indicator region** e desenha um retângulo pequeno à volta de um elemento HUD persistente que só apareça dentro do mundo carregado. Evita elementos que também sejam visíveis no ecrã de reconexão.
6. Escolhe **Sample in-game HUD indicator colour** e clica num pixel limpo desse elemento.
7. Guarda o perfil. No separador **Fishing**, inicia F7 e usa **Arm reconnect watchdog**.
8. Mantém Roblox em primeiro plano. O módulo exige três frames estáveis do botão antes do clique. Depois de o botão desaparecer, espera por três frames estáveis do indicador HUD calibrado para confirmar que a sessão voltou.
9. Se o indicador HUD não surgir no prazo, a tentativa não é considerada bem-sucedida. Só pode haver outra tentativa depois de aparecer novamente o botão calibrado; são permitidos no máximo três cliques por ativação.

O watchdog não funciona ao mesmo tempo que Reel assist ou Auto Aquarium. A calibração do botão e do indicador HUD depende da resolução, escala e aparência da interface; repete-a quando a UI mudar. Escolhe um indicador pequeno, distintivo e estável para reduzir falsos positivos.


## Rotação de totens dia/noite

1. Confirma que tens um indicador fixo na HUD do Fisch que muda visivelmente entre dia e noite. Não uses o céu, sombras ou elementos animados.
2. Em **Calibration**, captura F6 durante o dia, seleciona uma pequena região à volta do indicador e amostra a cor diurna.
3. Faz o mesmo durante a noite, na mesma região, e amostra a cor noturna. Guarda o perfil.
4. Na página **Fishing**, ativa Auto-cast e Equip my rod. Ativa **Use day/night totems on phase change** e escolhe dois slots diferentes para os totens de dia e de noite. Os slots têm de ser diferentes do slot da cana e de qualquer poção.
5. A rotação não usa item na primeira fase reconhecida. Exige três frames estáveis para confirmar uma mudança e só usa o totem quando a pesca estiver em **Preparing**. O slot da cana é reposto após o clique.
6. Não atives simultaneamente os dois temporizadores de totem: escolhe a rotação dia/noite ou os temporizadores normais. A ativação do item não é confirmada visualmente pelo jogo; verifica os slots e testa primeiro com atenção.

A função depende de um indicador HUD real e estável. Se não existir um indicador com cores claramente distintas, não inventes uma região/cor: deixa a opção desligada.


## Angler Quest Assist

1. Vai junto do Angler e abre o diálogo do pedido. O GafiFish não movimenta a personagem até ao NPC nem abre o diálogo.
2. Captura um frame com F6 enquanto o botão **Accept Request** está visível. Usa **Select Accept area** para delimitar o botão e **Sample Accept colour** para amostrar uma cor dentro dele.
3. Ativa **Angler Quest assist**, escreve o nome exato do peixe pedido e prime **Arm Auto Accept / Submit**. Volta ao Fisch; o módulo só arma quando o Roblox estiver em primeiro plano.
4. A aceitação exige três frames novos e estáveis. O GafiFish clica uma vez e espera que o botão desapareça em dois frames posteriores distintos. Depois liberta os inputs; inicia o macro de pesca normalmente.
5. Confirma que tens uma sessão OCR ativa e as regiões de pesca/Perfect Cast calibradas. Quando o OCR reconhecer o peixe pedido, o macro pára.
6. Abre o diálogo de entrega junto do Angler. Captura um frame com F6 enquanto o botão **Submit Fish** está visível; usa **Select Submit area** e **Sample Submit colour** para calibrá-lo.
7. Prime novamente **Arm Auto Accept / Submit**. O módulo só tenta a entrega quando o tracker confirma que o peixe-alvo foi capturado. O clique é único e a entrega só é registada depois de o botão calibrado desaparecer em frames posteriores.
8. O cooldown de 120 segundos começa depois da confirmação visual da entrega. A opção manual **I handed the fish to the Angler · start cooldown (manual)** continua disponível como alternativa. Faz a calibração com o botão ativo e na mesma resolução que vais usar.

O coordenador nunca repete automaticamente um clique que não consiga confirmar: pára e pede intervenção manual. A confirmação é visual — o GafiFish observa o desaparecimento do botão calibrado; não consegue consultar o estado interno da quest no servidor. A captura depende da notificação de captura e da confiança do OCR local. O pedido ativo, a captura pendente de entrega e o cooldown ficam guardados em `%LOCALAPPDATA%\\GafiFish\\angler-quest-state.json`; ao reiniciar a aplicação, o GafiFish recupera o estado. Se o ficheiro estiver danificado, o assistente bloqueia novas ações para evitar perder um cooldown.

## Ações aleatórias que consomem recursos

O GafiFish não automatiza nem repete ações de resultado aleatório que consumam moeda ou itens do jogo.


## Roblox-only input guard

O GafiFish só arma o macro quando o cliente Roblox está em primeiro plano. No momento de armar, guarda o PID exato dessa instância. Todas as novas teclas, cliques e movimentos do cursor voltam a verificar que a mesma instância continua em primeiro plano. Se mudares para outra aplicação, fechares o Roblox ou trocares para outra instância, os novos inputs são recusados, os controlos já pressionados são libertados e a automação é desarmada. Volta a focar o Roblox e arma o macro novamente com F8. A captura de ecrã também rejeita qualquer janela que não pertença ao processo Roblox.
