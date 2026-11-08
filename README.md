# ByteTrainer

Primeira versão funcional para Windows. O nome é provisório.

O catálogo tem centenas de jogos reais importados da Steam. **My Friend Pedro e Sol Cesto** têm trainers. Os demais jogos podem ser pesquisados, favoritados e adicionados à lista local de pedidos de suporte. A inclusão no catálogo não indica suporte a cheats.

## Usar

Abra `Trainer Hub.exe` na pasta `release/win-unpacked`. Na página de My Friend Pedro, clique em **Conectar trainer**. O painel antigo é fechado normalmente para evitar dois aplicativos disputando as mesmas opções. Os atalhos F1–F8 ficam ativos enquanto o app controla o trainer. Ao fechar ou desconectar, as opções são desligadas; em encerramento inesperado, expiram em até cinco segundos.

O jogo sempre é iniciado pela Steam. A integração preserva a alteração já validada no trainer anterior: os cheats atuam diretamente sobre os valores do jogo e não ligam as flags de modificadores nativos. O suporte continua restrito à versão do jogo já analisada; arquivos incompatíveis não são sobrescritos.

Favoritos, pedidos e histórico ficam em `%APPDATA%/trainer-hub`. Pedidos não são enviados a uma equipe ou servidor. Capas e atualizações do catálogo precisam de internet; a lista embarcada, a detecção Steam e o trainer funcionam sem atualizar o catálogo.

## Desenvolvimento

`npm install`, `npm run build`, `npm start`.

`npm test`: testes de importação Steam, isolamento de suporte, catálogo e comunicação do adaptador.

`npm run catalog` atualiza a lista embarcada a partir de 12 páginas públicas da loja Steam. `npm run pack` gera o aplicativo Windows. `npm run dist` gera o instalador NSIS e o manifesto de atualização `release/latest.yml`.

## Organização

- `src`: interface React e estilos.
- `electron/main.cjs` e `preload.cjs`: aplicativo desktop e operações permitidas à interface.
- `electron/steam.mjs`: descoberta das bibliotecas e jogos instalados.
- `electron/catalog.mjs`: importação e normalização do catálogo.
- `electron/pedro.cjs`: adaptador de My Friend Pedro, controle de sessão, opções e atalhos.
- `resources/pedro`: instalador e runtime do trainer existente, com a licença Mono.Cecil.
- `assets/catalog.json`: catálogo inicial; suporte é definido pelo código, não por metadados importados.

Para adicionar outro jogo, implemente um adaptador independente e seu manifesto de opções, registre o jogo como suportado e conecte suas operações à ponte desktop. Catálogo, descoberta, favoritos, pedidos, sons e a seleção do trainer ativo são compartilhados. Os comandos validam o jogo conectado para impedir que uma página controle outro trainer.

O [catálogo oficial de integrações](https://bytetrainer.github.io/trainer-integrations/) serve pacotes assinados. O instalador do aplicativo não tem assinatura Authenticode.

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

Correções podem ser distribuídas como pacotes `.thub` assinados. A importação manual continua em **Configurações → Atualizações das integrações → Importar pacote assinado**.

Veja [INTEGRATIONS.md](INTEGRATIONS.md) para instalação transacional, backups por versão, publicação dos pacotes e localização da chave privada fora do projeto.

## Versão 0.7.0 — Distribuição e atualizações do aplicativo

Instale a versão inicial com atualizador pelos [Releases do Trainer Hub](https://github.com/ByteTrainer/trainer-hub/releases/latest). O Hub verifica novas versões estáveis ao abrir e a cada seis horas. Quando estiver em foco, avisa se houver uma atualização. O download e a instalação exigem confirmação, e o Hub reinicia depois de instalar. A versão 0.6.0 e anteriores precisam dessa instalação manual uma vez.

Para publicar uma versão estável, aumente a versão em `package.json` e `package-lock.json`, rode `npm test` e `npm run dist` e envie o commit. Crie um release em rascunho com a tag `vX.Y.Z`. Anexe `latest.yml` e o instalador gerado na mesma compilação, usando o nome exato indicado em `files[0].url` no manifesto, como `Trainer-Hub-Setup-X.Y.Z.exe`. Publique o release como estável e marque-o como o mais recente. O atualizador verifica o SHA-512 do instalador declarado no manifesto distribuído pelo GitHub.

## Pumpkin Jack

No Hub, abra **Pumpkin Jack**, instale ou atualize a integração com o jogo fechado e inicie pela Steam. Entre em uma fase com Jack no corpo e conecte o trainer. Os atalhos padrão são **F1** para vida ilimitada, **F2** para pulo ilimitado, **F3** para velocidade, **F4** para mortes com um golpe, **F5** para recarga do corvo e **F6** para o rastreador. A velocidade pode ser ajustada de 50% a 250% e o valor fica salvo no Hub.

A proteção cobre o dano normal. A velocidade atua no solo. O controle de dano reforça golpes diretos contra inimigos comuns, incluindo os morcegos. Chefes e sequências roteirizadas podem ter regras próprias. O rastreador mostra a direção em relação à câmera e a distância em linha reta até a caveira e o vinil mais próximos em modo janela ou sem bordas. Ao coletar um item, o indicador muda para o próximo do mesmo tipo, se houver. Para remover a integração, feche o jogo e use **Restaurar original**.

## Moventure

Inicie o jogo pela Steam e conecte o trainer. Use **F1** para bloquear dano, empurrões e quedas causadas por ataques e **F2** para pulo infinito. Pressione o botão de pulo novamente no ar para dar outro impulso. O reinício por queda fora do mapa continua disponível. Para remover a integração, feche o jogo e use **Restaurar original**.

## Publicação a partir da versão 1.0

Mantenha o changelog em `assets/changelog.json`. Ele alimenta a janela do Hub e as notas do GitHub Release. Após validar e registrar o código no Git, compile o instalador e execute `./scripts/publish-release.ps1 -BuildDirectory release` para enviar o commit, verificar os arquivos anexados ao rascunho e publicar a versão estável.

Para distribuir alterações dos jogos, siga o [fluxo de pacotes assinados](INTEGRATIONS.md). O comando de publicação atualiza o catálogo e a página do site juntos.

## Versão 1.1.0

Na página do jogo, **Atualizar integração** consulta o catálogo, baixa e verifica o pacote assinado e aplica a revisão com o jogo fechado. A mesma página permite configurar a pasta de instalação e voltar à detecção da Steam.

## My Little Universe

Disponível por pacote assinado com 18 controles. A integração 1.1.0 inclui refinamento instantâneo das filas já pagas das bancadas e uma lista de recursos em ordem alfabética, com Madeira de Lei, Óleo de Peixe e Rubi Corrompido identificados em português. O ajuste de recursos adiciona valores positivos e remove valores negativos uma vez por aplicação, com confirmação do jogo. O seletor identifica itens especiais limitados a 10 unidades pela validação nativa do save. Os demais recursos têm estoque mínimo zero.

O alcance aceita até 100×, os multiplicadores de personagem aceitam de 0,1× a 20× e a velocidade do jogo aceita de 0,1× a 5×. As escolhas ficam salvas por jogo. A pesca fácil conclui a captura na primeira interação do minijogo, e o bloqueio de respawn afeta geradores de inimigos já derrotados.

Os rastreadores de Energy Crystals (F8) e expansão do mapa (F9) são independentes. O primeiro consulta o estado de coleta da fase atual e o segundo usa apenas construções de terreno liberadas e incompletas, com materiais restantes. Ambos mostram um marcador ou uma seta na borda da tela e a distância em linha reta, sem calcular rotas. As referências são atualizadas ao trocar de fase, incluindo dungeons; os alvos concluídos são descartados. São controles de leitura, sem conceder itens, concluir construções ou salvar progresso.

## Shake it Co!

Integração 1.1.0 (revisão 2), build Steam 26324813. Os 13 controles são distribuídos pelo pacote assinado, sem exigir nova versão do Hub. Inclui definição de moedas, almas e fichas, pesca fácil, pesca AFK com relançamento máximo, ciclo dia/noite congelado, baús perfeitos, movimento de 0,1× a 10×, Blackjack em 21, jackpot em todo giro, árvore automática, bônus temporários congelados e venda automática. Os três saldos são definidos uma vez por aplicação, entre zero e 999.999.999.999, com valores salvos separadamente.

A árvore automática exige que o personagem esteja segurando a árvore e respeita os bloqueios de interface. A pesca AFK complementa a pesca fácil e usa o relançamento nativo, com suas verificações de posição, movimento e cancelamento. Os bônus de livros e os frenesis conservam o tempo restante, que volta a correr ao desligar. O Blackjack altera apenas as cartas da mão local antes da avaliação; o crupiê pode empatar seguindo as regras normais. O jackpot mantém o acumulado e as contribuições de cada giro, sem alterar as tabelas de probabilidade: ao desligar, a geração normal volta imediatamente. A venda automática usa as rotinas nativas para novas frutas e peixes; não esvazia retroativamente o inventário.

## Idiomas e contribuições

O aplicativo e o site oferecem português brasileiro, inglês e espanhol. A escolha do app fica em Configurações e também é usada na sobreposição. O site tem um seletor próprio que mantém a página atual ao trocar de idioma.

As traduções compartilham os arquivos JSON de `locales/`. Para corrigir uma tradução ou adicionar outro idioma, consulte [o guia de contribuição](locales/README.md) e execute `npm run translations:check`.

## Out of Space

Integração 1.1.0 (10 controles) para a build Steam 6616527, distribuída por pacote assinado sem exigir atualização do Hub. Inclui estamina ilimitada (F1), sem fome (F2), não ficar sujo (F3) e definição de moedas da partida. Os três controles de personagem removem a necessidade atual pelas rotinas nativas e impedem aumentos somente nos personagens controlados neste computador. Ao desligar, o comportamento normal retorna; o desafio Zen nativo não é ativado.

As moedas usam o saldo nativo, com limite de 0 a 9.999 e confirmação de aplicação uma única vez. Em uma sala online, a ação exige o computador que hospeda a partida. O pacote verifica a biblioteca principal e as três dependências Photon antes da instalação, mantém cópia original e permite restauração.

Os controles adicionais do Out of Space incluem balde infinito (F4), cachorro sem fome (F5), recuperação de nocaute congelada (F6), eclosão bloqueada (F7), movimento de 0,1× a 10× (F8) e simulação de 0,1× a 5× (F9). O balde e o cachorro respeitam a propriedade local dos objetos; outros pets mantêm seu consumo normal. O bloqueio de recuperação não derruba inimigos ativos e não impede coletar os já nocauteados. O controle de eclosão cobre os tipos derivados de ovo e os pedidos de eclosão forçada da sala, preservando os temporizadores. Movimento escala velocidade e aceleração sem alterar seus campos de origem. A simulação preserva a escala anterior, usa a velocidade normal nos menus de pausa e restaura ao desativar, perder conexão ou sair da partida. Os controles de simulação compartilhada exigem a partida solo ou o anfitrião.

## Aparência e perfis de cheats

O app segue o tema do Windows por padrão. Em Configurações, escolha Claro, Escuro ou Seguir o sistema. A preferência também vale para a sobreposição.

Na página de cada jogo, abra Perfis de cheats para salvar os controles ativos e seus multiplicadores com um nome próprio. Escolha um perfil padrão para ativá-lo quando uma nova sessão do jogo estiver pronta. Aplicar um perfil substitui os cheats ativos; ações de moedas e recursos nunca são repetidas. Desconectar manualmente impede a reconexão automática naquela sessão.

As traduções usam inglês como base: mensagens compartilhadas em `locales/common/` e textos exclusivos em `locales/games/<game>/`. O guia em `locales/README.md` explica como contribuir.
