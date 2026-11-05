# GafiFish

Aplicação Windows para análise visual e automatização do ciclo de pesca no Roblox Fisch.

> **Estado atual:** planeamento técnico e pesquisa. Ainda não existe uma versão funcional do macro.

## Objetivos

- Detetar visualmente as fases de pesca (cast, shake e reel).
- Acompanhar a posição e a velocidade do peixe em tempo real.
- Controlar os inputs com um controlador adaptativo.
- Registar as capturas de cada sessão.
- Ao parar, apresentar um resumo com o total de peixes e, quando existirem mais de 25, mostrar apenas os 25 melhores.
- Permitir calibração por resolução e por cana.

## Stack proposta

- **C# / .NET 10 LTS** — aplicação principal.
- **WPF** — interface Windows.
- **OpenCvSharp / OpenCV** — visão computacional.
- **Windows Graphics Capture / DXGI** — captura de imagem.
- **Win32 SendInput** — simulação de inputs normais do rato e teclado.
- **SQLite + JSON** — histórico e configuração local.
- **xUnit** — testes automatizados.

A escolha evita depender de Python em execução e aproveita a integração nativa com Windows. O processamento visual será local.

## Princípios de segurança

- Não ler nem alterar a memória do Roblox.
- Não injetar código, DLLs, scripts ou executores no jogo.
- Não alterar ficheiros do Roblox nem tráfego de rede.
- Não pedir nem guardar credenciais, cookies ou tokens da conta.
- Não exigir privilégios de administrador.
- Parar e libertar todos os botões quando a janela do Roblox perde o foco, a deteção falha ou o utilizador aciona a tecla de emergência.
- Não enviar capturas de ecrã para serviços externos.

A utilização de automação pode estar sujeita às regras do Roblox e do Fisch e pode implicar penalizações da conta. O GafiFish não tentará contornar sistemas anti-cheat.

## Funcionalidades previstas

### MVP
1. Captura e calibração da área do minijogo.
2. Deteção da barra, do peixe e do estado da pesca.
3. Rastreamento da direção e velocidade do peixe.
4. Auto-shake, auto-reel e auto-cast.
5. Tecla global de iniciar/parar e paragem de emergência.
6. Sobreposição de diagnóstico e registos locais.

### Registo e estatísticas
1. Leitura local da notificação de captura.
2. Histórico de espécies, raridade, mutações e peso — apenas quando os dados estiverem visíveis e forem reconhecidos com confiança.
3. Resumo apresentado ao parar.
4. Top 25 capturas, com ordenação configurável.
5. Exportação CSV/JSON.

### Evoluções
- Perfis individuais por cana.
- Perfect Cast com calibração do atraso de input.
- Alertas para capturas prioritárias.
- Estatísticas por hora e taxa de sucesso.
- Venda automática e recuperação de posição, depois de o ciclo principal estar estável.

## Documentação

- [Pesquisa de projetos e licenças](docs/RESEARCH.md)
- [Arquitetura técnica](docs/ARCHITECTURE.md)
- [Plano de desenvolvimento](docs/ROADMAP.md)
- [Segurança e tolerância a falhas](docs/SECURITY.md)

## Aviso

Projeto independente, sem afiliação ao Roblox ou aos criadores de Fisch.
