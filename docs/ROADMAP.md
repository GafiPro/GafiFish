# Plano de desenvolvimento

## Fase 0 — Pesquisa e desenho

- [x] Identificar macros relevantes para Fisch e referências de Perfect Cast.
- [x] Encontrar o Bloomfisching correto e verificar estado, conteúdo e licença.
- [x] Rever licenças dos projetos que disponibilizam código.
- [x] Escolher arquitetura sem leitura da memória nem injeção.
- [x] Integrar os novos requisitos: Perfect Cast no MVP e Sell Anywhere de 10 em 10 minutos.
- [x] Documentar a arquitetura e os riscos.

## Fase 1 — Fundação e visão

Objetivo: provar que o GafiFish reconhece os elementos visuais antes de controlar inputs.

Entregáveis:
1. Solução .NET 10 e projetos App/Core/Windows/Vision/Tests.
2. Janela WPF inicial.
3. Deteção da janela Roblox e captura da região selecionada.
4. Assistente de calibração para a barra, marcadores e medidor de cast.
5. Overlay que desenha deteções, direção e confiança.
6. Modo de reprodução de imagens/gravações para testar sem abrir o jogo.

Critério de saída: os detetores acompanham elementos relevantes e identificam frames perdidos sem inventar posições.

## Fase 2 — Pesca completa e Perfect Cast

Entregáveis:
1. Máquina de estados com timeouts.
2. Auto-shake e deteção da mordida.
3. Tracking da direção e velocidade do peixe.
4. Controlador PD/PWM com deadband e limites.
5. **Perfect Cast incluído no MVP**, com calibração de câmara/medidor e ajuste do timing.
6. Auto-reel e auto-cast.
7. Hotkeys configuráveis e paragem de emergência.
8. Libertação garantida de inputs ao parar ou perante erro.

Critério de saída: o controlador funciona com confiança em gravações e o Perfect Cast mede, calibra e valida o resultado; nenhum estado pode deixar um botão premido após timeout.

## Fase 3 — Sell Anywhere a cada 10 minutos

Entregáveis:
1. Temporizador de 10 minutos configurável.
2. Pedido de venda fica pendente até a captura atual terminar.
3. Abrir inventário com a janela Roblox em foco.
4. Reconhecer o botão da gamepass Sell Anywhere.
5. Acionar uma vez e verificar visualmente o resultado.
6. Fechar o inventário e retomar a pesca.
7. Erro seguro se a UI não corresponder ao esperado.

Critério de saída: testes com frames gravados demonstram que o ciclo não vende no meio de uma captura, não faz cliques repetidos cegos e não prossegue perante uma confirmação incerta.

Nota: a gamepass vende itens não favoritos. O utilizador é responsável por marcar como favoritos os peixes que pretende guardar.

## Fase 4 — Capturas e resumo

Entregáveis:
1. Deteção do resultado de uma pesca.
2. OCR local da notificação.
3. Registo de espécie, raridade, mutação e peso quando reconhecíveis.
4. Histórico local por sessão em SQLite.
5. Resumo automático quando o utilizador parar.
6. Top 25 no máximo, ordenado pela pontuação configurada.
7. Exportação CSV/JSON e cópia do resumo.

Critério de saída: os totais são coerentes e a interface nunca apresenta mais de 25 peixes no resumo.

## Fase 5 — Robustez e melhorias

- Perfis por cana e resolução.
- Ajuste adaptativo à taxa de frames.
- Deteção de ciclos bloqueados e recuperação segura.
- Estatísticas por hora, taxa de sucesso e vendas.
- Relatórios de diagnóstico.
- Alertas para capturas prioritárias e preferências de mutação/espécie.
- Melhorias do Perfect Cast com dados medidos em sessões de teste.

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
