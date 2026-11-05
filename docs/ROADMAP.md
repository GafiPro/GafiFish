# Plano de desenvolvimento

## Fase 0 — Pesquisa e desenho

- [x] Identificar macros relevantes para Fisch e referências de Sol's RNG.
- [x] Ler documentação de deteção, calibração, controlo e funcionalidades.
- [x] Rever licenças dos projetos que disponibilizam código.
- [x] Escolher uma arquitetura sem leitura da memória nem injeção no Roblox.
- [x] Documentar o plano no repositório.
- [ ] Rever a lista de funcionalidades com o utilizador.

## Fase 1 — Fundação e visão (primeiro código)

Objetivo: provar que o GafiFish consegue observar o minijogo de forma consistente, antes de enviar inputs.

Entregáveis:
1. Solução .NET 10 e projetos App/Core/Windows/Vision/Tests.
2. Janela WPF inicial com estado do jogo.
3. Deteção da janela do Roblox e captura da região selecionada.
4. Assistente de calibração para a barra e marcadores.
5. Overlay que desenha a barra, o alvo, a direção e a confiança.
6. Modo de reprodução de imagens/gravações para testar o detetor sem abrir o jogo.

Critério de saída: o overlay acompanha corretamente os elementos relevantes numa sequência de imagens e identifica frames perdidos sem inventar posições.

## Fase 2 — Controlador e ciclo de pesca

Entregáveis:
1. Máquina de estados com timeouts.
2. Deteção de prompts de shake.
3. Estimativa de movimento do peixe.
4. Controlador PD/PWM com deadband e limites.
5. Input Win32 apenas depois de confirmar que a janela certa está ativa.
6. Hotkeys configuráveis e paragem de emergência.
7. Libertação garantida de todos os inputs ao parar ou perante erro.

Critério de saída: o controlador funciona com confiança em testes gravados e pode ser interrompido em qualquer estado.

## Fase 3 — Capturas e resumo

Entregáveis:
1. Deteção do resultado de uma pesca.
2. OCR local da notificação de captura.
3. Registo com espécie, raridade, mutação e peso quando reconhecíveis.
4. Histórico local por sessão em SQLite.
5. Resumo automático quando o utilizador para.
6. Top 25 no máximo, ordenado pela pontuação configurada.
7. Exportação CSV/JSON e cópia do resumo.

Critério de saída: uma sessão de teste apresenta totais corretos e não mostra mais de 25 entradas, mesmo que o histórico tenha mais capturas.

## Fase 4 — Robustez e perfis

Entregáveis:
- Perfis por cana.
- Calibração de cores por perfil.
- Ajuste adaptativo à taxa de frames.
- Deteção de ciclos bloqueados e recuperação segura.
- Estatísticas de capturas por hora, duração média e taxa de sucesso.
- Relatórios de diagnóstico exportáveis.

## Fase 5 — Funcionalidades avançadas opcionais

Estas funcionalidades só devem entrar depois de o ciclo principal ser fiável:
- Perfect Cast com calibração do atraso de input.
- Alertas quando aparece uma captura prioritária.
- Preferências de espécies/mutações.
- Auto-equip da cana com validação visual.
- Venda automática de inventário e regresso à zona de pesca.
- Perfis por local e resolução.

## Funcionalidades que não fazem parte do plano

- Leitura ou alteração de memória do Roblox.
- Injeção de código, executores ou scripts dentro do jogo.
- Contorno de anti-cheat.
- Serviços de IA externos que exijam enviar capturas ou tokens.
- Atualizadores que executem binários sem validação explícita.

## Regras de qualidade

- Cada módulo novo tem testes unitários ou testes com frames gravados.
- Toda a alteração ao controlador inclui medições antes/depois.
- As dependências e licenças são revistas.
- Bugs de input (botão preso, foco errado, hotkey ignorada) têm prioridade sobre novas funcionalidades.
- Uma melhoria só é aceite se não reduzir a segurança da paragem nem a qualidade da deteção.
