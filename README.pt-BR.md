# ByteTrainer

<p align="center">
  <img src="assets/icon.png" width="84" height="84" alt="">
</p>

<p align="center"><strong>Um hub de trainers para jogos single-player da Steam no Windows</strong></p>

<p align="center">
  <a href="README.md">English</a> · <strong>Português (Brasil)</strong> · <a href="README.es.md">Español</a>
</p>

<p align="center">
  <a href="https://github.com/ByteTrainer/trainer-hub/releases/latest"><strong>Baixar para Windows</strong></a> ·
  <a href="https://bytetrainer.github.io/trainer-integrations/">Site oficial</a> ·
  <a href="https://discord.gg/pMSJayXFmj">Discord</a>
</p>

## Recursos

- **Detecção da biblioteca Steam**, com busca no catálogo, favoritos e pasta de instalação configurável por jogo.
- **Sobreposição no jogo** com os mesmos controles do aplicativo. Ative por jogo e abra com <kbd>Alt</kbd> + <kbd>W</kbd> por padrão, no modo janela ou sem bordas.
- **Atalhos personalizados e perfis de cheats** salvos por jogo. Um perfil padrão pode ativar os controles e aplicar os multiplicadores salvos quando uma nova sessão estiver pronta. Ações pontuais, como definir moedas, são aplicadas manualmente.
- **Feedback personalizável** com quatro estilos de som, volume ajustável e opção de silenciar. O app e a sobreposição podem seguir o tema do Windows ou usar o tema claro ou escuro.
- **Três idiomas** no aplicativo, na sobreposição e no site. Escolha entre inglês, português brasileiro e espanhol.
- **Atualizações separadas** do aplicativo e das integrações. O ByteTrainer consulta o GitHub Releases para atualizações do app e um catálogo assinado no GitHub Pages para encontrar os pacotes dos jogos.

## Jogos compatíveis

A página de cada jogo lista os controles disponíveis e oferece o pacote assinado mais recente. A compatibilidade depende da versão instalada pela Steam. [Ver catálogo atualizado](https://bytetrainer.github.io/trainer-integrations/catalogo.html)

| Jogo | Exemplos de controles |
| --- | --- |
| [Dungeon of the Endless](https://bytetrainer.github.io/trainer-integrations/jogos/249050.html) | Vida ilimitada, ajuste de recursos, pesquisa instantânea e habilidades ativas |
| [Kawaii Deathu Desu](https://bytetrainer.github.io/trainer-integrations/jogos/1102880.html) | Vida ilimitada, definir almas, especial ilimitado |
| [Last Hope Bunker: Zombie Survival](https://bytetrainer.github.io/trainer-integrations/jogos/2475440.html) | Filtro ilimitado, munição infinita, sem recarga, modo furtivo |
| [Moventure](https://bytetrainer.github.io/trainer-integrations/jogos/3826060.html) | Invencibilidade, pulo infinito, oxigênio infinito debaixo d’água |
| [My Friend Pedro](https://bytetrainer.github.io/trainer-integrations/jogos/557340.html) | Foco ilimitado, combo travado, nota S, congelar cronômetro da fase |
| [My Little Universe](https://bytetrainer.github.io/trainer-integrations/jogos/2328750.html) | Ajuste de recursos, refinamento instantâneo, rastreadores de cristais e expansão do mapa |
| [Out of Space](https://bytetrainer.github.io/trainer-integrations/jogos/400080.html) | Estamina ilimitada, definir moedas, balde infinito, impedir eclosão |
| [Pigeons Attack](https://bytetrainer.github.io/trainer-integrations/jogos/814300.html) | vida, munição e especial ilimitados, velocidade do jogo, morte com um golpe, multiplicadores de dano e recompensas |
| [Pumpkin Jack](https://bytetrainer.github.io/trainer-integrations/jogos/1186640.html) | Pulo ilimitado, velocidade de movimento, recarga instantânea do corvo, rastreador de colecionáveis |
| [Shake it Co!](https://bytetrainer.github.io/trainer-integrations/jogos/3578070.html) | Pesca fácil, balanço automático da árvore, baús perfeitos, venda automática |
| [Sol Cesto](https://bytetrainer.github.io/trainer-integrations/jogos/2738490.html) | Definir ouro, itens sem consumo, imunidade a veneno, expansão de inventário |
| [Tinykin](https://bytetrainer.github.io/trainer-integrations/jogos/1599020.html) | Planar infinitamente, rastreadores de missões, colecionáveis e corridas |

## Como começar

1. Baixe e execute o instalador para Windows. [Versão mais recente do aplicativo](https://github.com/ByteTrainer/trainer-hub/releases/latest)
2. Abra a página do jogo no ByteTrainer. Se ele ainda não aparecer, vá a **Configurações → Atualizações das integrações → Verificar online** e selecione **Baixar pacote**.
3. Com o jogo fechado, use **Instalar integração** ou **Atualizar integração** na página do jogo. Nessa mesma página você pode indicar a pasta de instalação.
4. Inicie o jogo pela Steam, entre em uma partida solo e selecione **Conectar trainer**. Ative os controles pelo app, pelos atalhos ou pela sobreposição.

**Uso exclusivo em partidas single-player.** Em jogos que também oferecem multiplayer, use os trainers no modo solo.

## Compatibilidade e atualizações

O ByteTrainer verifica os arquivos relevantes antes de instalar. Uma atualização da Steam pode exigir uma revisão compatível da integração. Com o jogo fechado, use **Restaurar original** na página dele para recuperar os arquivos preservados na instalação.

O aplicativo verifica a assinatura de cada pacote `.thub` antes de aceitá-lo. Os pacotes podem adicionar ou atualizar controles independentemente do aplicativo para Windows. Para consultar atualizações do app manualmente, abra **Configurações → Atualizações do ByteTrainer**. As notas de versão estão no número da versão no app e no GitHub. [Versões do aplicativo](https://github.com/ByteTrainer/trainer-hub/releases) · [Repositório de integrações](https://github.com/ByteTrainer/trainer-integrations)

## Como contribuir

- **Solicite um jogo ou relate um problema com um trainer** no Discord. A aba **Jogos confirmados** do app lista as integrações planejadas. [Entre no Discord](https://discord.gg/pMSJayXFmj)
- **Melhore ou adicione uma tradução** usando os catálogos compartilhados e os arquivos por jogo em `locales/`. [Guia de tradução](locales/README.md)
- **Trabalhe nas integrações** seguindo o fluxo de verificação da build, instalação e publicação. [Guia de integrações](INTEGRATIONS.md)

<details>
<summary>Compilar a partir do código-fonte</summary>

No Windows, use Node.js 22.12 ou posterior e npm.

```powershell
npm ci
npm run build
npm start
```

Execute `npm test` e `npm run translations:check` antes de enviar alterações. Use `npm run dist` para criar o instalador para Windows.

| Pasta | Finalidade |
| --- | --- |
| `src/` | Interface do aplicativo e da sobreposição |
| `electron/` | Detecção da Steam, serviços do aplicativo e conexão dos trainers |
| `native/` e `resources/` | Código das integrações e arquivos dos pacotes |
| `locales/` | Traduções compartilhadas e específicas por jogo |
| `scripts/` | Ferramentas de compilação, validação e publicação |

</details>
