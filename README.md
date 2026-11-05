# GafiFish

Aplicação Windows para análise visual e automatização do ciclo de pesca no Roblox Fisch.

> **Estado atual:** pesquisa e planeamento publicados; o motor executável ainda está em desenvolvimento.

## Objetivos principais

- Pesca automática: cast, shake, reel e recast.
- Perfect Cast integrado desde a primeira versão funcional, com calibração da câmara e do medidor.
- Tracking visual do peixe, com estimativa de direção, velocidade e antecipação.
- Venda automática a cada **10 minutos**, usando a gamepass **Sell Anywhere** do utilizador.
- Registo de capturas e resumo ao parar.
- Apresentar no máximo os **25 melhores peixes**, mesmo que a sessão tenha mais capturas.
- Histórico completo local e exportação CSV/JSON.

## Stack proposta

- **C# / .NET 10 LTS** — aplicação principal.
- **WPF** — interface Windows.
- **OpenCvSharp / OpenCV** — visão computacional.
- **Windows Graphics Capture / DXGI** — captura de imagem.
- **Win32 SendInput** — inputs normais do rato e teclado.
- **SQLite + JSON** — histórico e configuração local.
- **xUnit** — testes automatizados.

O processamento visual será local. O GafiFish não depende de Python em execução nem de serviços de IA na cloud.

## Funcionalidades da primeira versão

### Pesca e controlo
1. Captura e calibração do minijogo.
2. Deteção da barra, do peixe e do estado da pesca.
3. Tracking da direção e velocidade do peixe.
4. Auto-shake, auto-reel e auto-cast.
5. **Perfect Cast incluído desde o início**, com calibração e ajuste de timing.
6. Perfis de calibração por cana.

### Venda automática
1. Intervalo predefinido de **10 minutos**.
2. Não interromper uma captura: a venda fica pendente até o ciclo atual terminar.
3. Abrir o inventário.
4. Usar o botão da gamepass **Sell Anywhere**.
5. Confirmar visualmente a venda, fechar o inventário e retomar a pesca.
6. Parar e pedir intervenção se a interface não corresponder ao esperado.

**Importante:** Sell Anywhere vende itens não favoritos. Marcar como favorito tudo o que pretendes guardar. A aplicação nunca tentará remover favoritos nem usar APIs internas do jogo.

### Registo e estatísticas
1. Leitura local da notificação de captura.
2. Histórico de espécies, raridade, mutações e peso quando os dados forem reconhecidos com confiança.
3. Resumo apresentado ao parar.
4. Top 25 capturas, com ordenação configurável.
5. Exportação CSV/JSON.

## Princípios de segurança

- Não ler nem alterar a memória do Roblox.
- Não injetar código, DLLs, scripts ou executores no jogo.
- Não alterar ficheiros do Roblox nem tráfego de rede.
- Não pedir nem guardar credenciais, cookies ou tokens da conta.
- Não exigir privilégios de administrador.
- Parar e libertar todos os botões quando a janela do Roblox perde o foco, a deteção falha ou o utilizador aciona a tecla de emergência.
- Não enviar capturas de ecrã para serviços externos.

A utilização de automação pode estar sujeita às regras do Roblox e do Fisch e pode implicar penalizações da conta. O GafiFish não tentará contornar sistemas anti-cheat.

## Documentação

- [Pesquisa de projetos e licenças](docs/RESEARCH.md)
- [Arquitetura técnica](docs/ARCHITECTURE.md)
- [Plano de desenvolvimento](docs/ROADMAP.md)
- [Segurança e tolerância a falhas](docs/SECURITY.md)

## Aviso

Projeto independente, sem afiliação ao Roblox ou aos criadores de Fisch.
