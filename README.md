# Trainer Hub

Primeira versão funcional para Windows. O nome é provisório.

O catálogo tem centenas de jogos reais importados da Steam. **My Friend Pedro e Sol Cesto** têm trainers. Os demais jogos podem ser pesquisados, favoritados e adicionados à lista local de pedidos de suporte. A inclusão no catálogo não indica suporte a cheats.

## Usar

Abra `Trainer Hub.exe` na pasta `release/win-unpacked`. Na página de My Friend Pedro, clique em **Conectar trainer**. O painel antigo é fechado normalmente para evitar dois aplicativos disputando as mesmas opções. Os atalhos F1–F8 ficam ativos enquanto o app controla o trainer. Ao fechar ou desconectar, as opções são desligadas; em encerramento inesperado, expiram em até cinco segundos.

O jogo sempre é iniciado pela Steam. A integração preserva a alteração já validada no trainer anterior: os cheats atuam diretamente sobre os valores do jogo e não ligam as flags de modificadores nativos. O suporte continua restrito à versão do jogo já analisada; arquivos incompatíveis não são sobrescritos.

Favoritos, pedidos e histórico ficam em `%APPDATA%/trainer-hub`. Pedidos não são enviados a uma equipe ou servidor. Capas e atualizações do catálogo precisam de internet; a lista embarcada, a detecção Steam e o trainer funcionam sem atualizar o catálogo.

## Desenvolvimento

`npm install`, `npm run build`, `npm start`.

`npm test`: testes de importação Steam, isolamento de suporte, catálogo e comunicação do adaptador.

`npm run catalog`: atualiza a lista embarcada a partir de 12 páginas públicas da loja Steam. `npm run pack`: gera o aplicativo Windows. `npm run dist`: gera o instalador NSIS.

## Organização

- `src`: interface React e estilos.
- `electron/main.cjs` e `preload.cjs`: aplicativo desktop e operações permitidas à interface.
- `electron/steam.mjs`: descoberta das bibliotecas e jogos instalados.
- `electron/catalog.mjs`: importação e normalização do catálogo.
- `electron/pedro.cjs`: adaptador de My Friend Pedro, controle de sessão, opções e atalhos.
- `resources/pedro`: instalador e runtime do trainer existente, com a licença Mono.Cecil.
- `assets/catalog.json`: catálogo inicial; suporte é definido pelo código, não por metadados importados.

Para adicionar outro jogo, implemente um adaptador independente e seu manifesto de opções, registre o jogo como suportado e conecte suas operações à ponte desktop. Catálogo, descoberta, favoritos, pedidos, sons e a seleção do trainer ativo são compartilhados. Os comandos validam o jogo conectado para impedir que uma página controle outro trainer.

Não inclui contas, pagamentos ou serviço online de pedidos. As integrações aceitam pacotes assinados; ainda não há servidor de atualizações configurado. O instalador do aplicativo não tem assinatura Authenticode.

## Sol Cesto · versão 0.4.0

Compatível com a instalação Steam 1.01.4 analisada. Abra Sol Cesto no catálogo, instale a integração com o jogo fechado e conecte o trainer. Inicie sempre pela Steam. Entre em uma partida para aplicar ouro.

- F1: vida ilimitada (não revive personagem já morto).
- F2: habilidade sempre carregada (preserva animações e condições normais de uso).
- F3: itens sem consumo. É necessário já possuir o item. Desative antes de descartar ou entregar itens.
- Definir ouro: valor inteiro de 0 a 999999, aplicado uma vez à bolsa da partida. Compras continuam descontando normalmente. O aplicativo só confirma após resposta do jogo.

Apenas um trainer fica conectado por vez. O som configurado no app também atende às opções do Sol Cesto. Desconectar ou fechar desliga as opções; em falha do app elas expiram em até 3,5 segundos.

A integração preserva `package.nw.trainerhub-original` e verifica os arquivos antes de instalar/restaurar. Se a Steam atualizar o jogo, uma versão diferente é recusada; será necessário analisar a atualização. Não apague o backup original.

Também estão disponíveis imunidade a veneno, força e magia ajustáveis e expansão do inventário até seis espaços. Controle das probabilidades de baús/curas/monstros continua em investigação, sem opção disponível no aplicativo.


Versão 0.2.0: F7 — Sempre nota S (ative antes de concluir); F8 — Congelar cronômetro da fase (mantém o tempo atual e retoma ao desligar). Atualize a integração com o jogo fechado. O cronômetro é filtrado na interface e nos caminhos de resultado e salvamento; a escala global de tempo não é alterada. A nota S mantém o total de pontos calculado pelo jogo.


Versão 0.3.0 — Feedback sonoro

Em Configurações → Feedback sonoro, é possível ativar/desativar os avisos, ajustar o volume de 0 a 100% e escolher Digital, Suave, Arcade ou Minimalista. Cada estilo distingue ativação e desativação. Os botões de prévia não alteram cheats. As preferências são salvas automaticamente e usadas também com o aplicativo minimizado. Os sons são sintetizados localmente, sem downloads ou arquivos de áudio externos.

Versão 0.6.0 — Compatibilidade e atualizações das integrações

O Hub verifica os arquivos dos dois jogos automaticamente, mostra os motivos de incompatibilidade e oferece recuperação de instalações interrompidas. As capacidades são verificadas por cheat. Atalhos personalizados, sons e sobreposição continuam disponíveis.

Correções podem ser distribuídas como pacotes `.thub` assinados, sem reinstalar o aplicativo. Ainda não há servidor de atualizações; use **Configurações → Atualizações das integrações → Importar pacote assinado**. Um endereço HTTPS de catálogo assinado pode ser configurado quando houver hospedagem.

Veja [INTEGRATIONS.md](INTEGRATIONS.md) para instalação transacional, backups por versão, publicação dos pacotes e localização da chave privada fora do projeto.
