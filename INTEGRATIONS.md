# Integrações do Trainer Hub

O Hub 0.6 separa três camadas: interface e atalhos; pacote de integração por jogo; código executado pelo próprio jogo. A Steam continua sendo usada para iniciar os jogos.

## Compatibilidade

O Hub lê o identificador de build no manifesto local da Steam e verifica os arquivos relevantes com SHA-256. Os hashes são reutilizados apenas enquanto tamanho, datas e identidade do arquivo permanecem iguais. Ao conectar, a leitura é forçada novamente. Enquanto aberto, o Hub consulta o estado a cada dois segundos; arquivos grandes só são recalculados quando mudam.

Uma mudança de build com os mesmos arquivos relevantes continua compatível. Arquivo original reconhecido com marcador antigo indica integração removida. Arquivo diferente e não validado bloqueia instalação e conexão: isso não afirma que os cheats quebraram. Falhas no acesso, atualização da Steam e transações interrompidas possuem mensagens próprias. Uma alteração incompatível durante uma sessão desconecta o trainer e desliga as opções.

Cada receita declara quais comandos suporta. No Sol Cesto, os campos são resolvidos por nome e tipo; a posição antiga só existe como caminho legado para estruturas previamente verificadas. Rotinas de inventário também são verificadas. No Pedro, campos nomeados e seus tipos são conferidos por grupo; uma falha de foco ou munição não impede os controles independentes. Os jogos publicam capacidades e uma confirmação periódica. Sem confirmação recente, o Hub não declara uma conexão ativa.

## Instalação, restauração e recuperação

A instalação é preparada em uma pasta temporária no diretório do jogo. O instalador específico trabalha nessa cópia; somente depois o Hub aplica os arquivos, registra um diário e verifica os resultados. Uma falha durante a aplicação tenta restaurar os arquivos anteriores. Ao encontrar um diário pendente, a interface oferece **Recuperar instalação**, com o jogo fechado.

A recuperação não substitui arquivos que a Steam ou outro programa alterou depois da operação. Nesse caso, o usuário recebe uma explicação e pode verificar o jogo pela Steam. Para uma nova versão original reconhecida, o backup anterior é preservado em `.trainerhub-originals/<jogo>/<hash>/`, antes de preparar o backup da nova base. A restauração usa o original verificado e não executa um instalador destinado a outra versão do jogo.

## Pacotes independentes

Arquivos `.thub` contêm um envelope JSON assinado com Ed25519. A assinatura cobre os dados exatos do pacote, incluindo jogo, revisão, versão mínima do Hub, receita, nomes e hashes dos arquivos. A importação recusa assinatura desconhecida, conteúdo alterado, arquivos fora da lista permitida, revisão antiga e recursos que exijam um Hub mais recente. Uma reimportação verificada pode reparar o cache corrompido.

Os pacotes ficam no diretório de dados do usuário e são verificados antes da ativação e ao serem carregados. A chave privada de publicação não está no projeto, no instalador, nos pacotes ou no app; fica em `%LOCALAPPDATA%\TrainerHubPublisher\integration-signing-private.pem`. Faça uma cópia segura dessa chave: versões já distribuídas do Hub confiam na chave pública correspondente.

O Hub vem configurado para consultar o catálogo oficial assinado no GitHub Pages na abertura e a cada seis horas. Nas configurações, é possível trocar o endereço HTTPS ou deixar o campo vazio para desativar as consultas. Uma consulta com erro preserva os pacotes instalados. A importação local continua disponível. O pacote é baixado para o Hub e a alteração no jogo é uma etapa separada, executada somente com ele fechado. [Catálogo oficial](https://bytetrainer.github.io/trainer-integrations/catalog.json)

## Publicar uma correção

1. Investigue e valide a versão real do jogo. Não troque apenas um hash para liberar uma versão desconhecida.
2. Atualize o código do jogo e `resources/<jogo>/recipe.json`. Aumente `release` sempre que o conteúdo mudar. Mantenha os hashes suportados alinhados ao instalador e a lista de cheats alinhada aos testes. Novos comandos que exijam interface ou protocolo diferente também exigem uma versão mínima mais recente do Hub.
3. Para o Pedro, o código-fonte está em `native/pedro/`. O runtime é compilado contra o `mscorlib.dll` do jogo. Para o Sol Cesto, o código está em `resources/solcesto/`.
4. Execute os testes e confirme instalação, restauração, conexão e comandos na versão suportada. Os testes de mudanças de versão, rollback e assinaturas usam diretórios temporários, sem editar os jogos reais.
5. Gere os pacotes a partir da raiz do projeto:

```powershell
node scripts/integrations.mjs init-keys
node scripts/integrations.mjs pack 557340 release/Pedro-0.6.0.thub
node scripts/integrations.mjs pack 2738490 release/SolCesto-0.6.0.thub
```

O comando de inicialização preserva uma chave existente; ele não troca silenciosamente a identidade do publicador.

Para hospedar atualizações, publique os pacotes em HTTPS e prepare um catálogo com `kind: "catalog"` e `packages`. Cada entrada contém `gameId`, `release`, `version`, `url` HTTPS e `sha256` do arquivo `.thub` completo. Assine esse catálogo:

```powershell
node scripts/integrations.mjs catalog catalogo.json catalogo-assinado.json
```

No [repositório de integrações](https://github.com/ByteTrainer/trainer-integrations), publique os novos arquivos em `packages/` e substitua `catalog.json` pelo catálogo **assinado** no mesmo commit. Use um nome novo para cada revisão e preserve os arquivos já publicados. O GitHub Pages publica a partir da branch `main`. Confira o catálogo e os arquivos por HTTPS após a publicação. Mantenha a chave privada fora dos repositórios. Uma nova versão do jogo ainda exige investigação e testes do mantenedor.
