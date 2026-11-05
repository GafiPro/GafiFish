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

- **F8** arma o controlador preditivo hold/release.
- **F9** pára o controlador e liberta imediatamente o rato e as teclas.
- Se o Roblox perder o foco ou F7 for parado, o GafiFish também desarma e liberta os inputs.

O controlador só atua quando ambos os marcadores estão presentes e acima do limiar de confiança. Se um desaparecer, o botão esquerdo é libertado. A opção **Invert hold/release mapping** permite inverter o sentido do controlo; testa-a apenas numa situação segura. Antes de pressionar F8, podes ativar duas opções desligadas por defeito:
- **Auto-cast**: exige medidor Cast calibrado. Segura o botão esquerdo e liberta-o quando o Perfect Cast indica a janela-alvo; cancela se a leitura ficar inválida ou ultrapassar 8 segundos.
- **Auto-shake**: exige região e cor Shake calibradas. Clica apenas num candidato acima do limiar, estável em dois frames; espera que desapareça ou se mova antes de aceitar outro clique.

Equipar a cana continua a ser uma ação manual. Estas opções foram testadas em frames simulados, não numa sessão real do Fisch. A direção e o timing devem ser verificados com cuidado antes de as usares numa sessão importante.



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

Ao terminar a sessão, o GafiFish guarda a hora de fim e mostra o total, a taxa de capturas por hora e até 25 peixes ordenados pela raridade, mutação, peso e valor reconhecidos. Os botões **Export CSV** e **Export JSON** guardam o histórico completo dessa sessão, incluindo campos desconhecidos como valores vazios/nulos.

## 5. Sell Anywhere e limites da versão atual

O agendamento Sell Anywhere só é criado quando, antes de pressionares F8, marcas a caixa **Enable Sell Anywhere every 10 minutes**, existe uma sessão OCR ativa e estão calibradas a região/cor do botão e a área da lista de itens. A primeira venda só é tentada depois de 10 minutos e de uma notificação de captura OCR com confiança elevada durante o estado Reel.

O fluxo abre o inventário, espera por várias deteções estáveis do botão, envia um único clique e exige uma alteração da lista em dois fotogramas consecutivos antes de fechar o inventário. Se o botão não for encontrado ou a lista não mudar de forma estável, o GafiFish pára e liberta os inputs; não repete o clique às cegas.

Sell Anywhere vende o que não estiver marcado como favorito. A opção está desligada por defeito e exige confirmação explícita na caixa da interface. Marca como favorito tudo o que pretendes guardar antes de a ativares. O fluxo ainda precisa de validação prática com a tua interface do Fisch e a gamepass ativa.

O Auto-cast usa o indicador e a zona verde do medidor calibrado; o Auto-shake usa os candidatos de cor/forma na região calibrada. Ambos são opções na interface, desligadas por defeito, e ainda precisam de ser validados com uma captura real. O GafiFish não equipa a cana nem faz recast depois de uma captura. Não alteres o perfil para forçar cliques com coordenadas ou cores adivinhadas.
