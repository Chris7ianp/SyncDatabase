# SyncDatabase

Ferramenta para comparar a estrutura e os dados de dois bancos PostgreSQL. A origem é Produção e o destino é Homologação. A aplicação permite atualizar um registro existente em Homologação após prévia e confirmação explícita.

## Arquitetura

- `SyncDatabase.Api`: API ASP.NET Core, controllers, Swagger e composição das dependências.
- `SyncDatabase.Application`: interfaces, DTOs e comparadores sem dependência de PostgreSQL.
- `SyncDatabase.Infrastructure`: leitores de catálogos PostgreSQL usando Npgsql e Dapper.
- `SyncDatabase.Tests`: testes unitários dos comparadores.
- `SyncDatabase.Worker`: projeto template do .NET Worker. Ele não participa do fluxo atual e não é iniciado pela API; comparação de catálogo é síncrona e não justifica uma fila em sua escala atual.
- `frontend/syncdatabase-web`: dashboard Angular.

## Requisitos

- .NET SDK 10.0.
- Node.js e npm compatíveis com a versão do Angular no `package.json`.
- PostgreSQL acessível para cada ambiente, com conexão configurada na API.

## Configuração

A API lê as variáveis de ambiente `ConnectionStrings__Producao` e `ConnectionStrings__Homologacao`, correspondentes às chaves `ConnectionStrings:Producao` e `ConnectionStrings:Homologacao`. Configure-as localmente, por User Secrets ou pelo ambiente de execução. Não versione valores com credenciais e não os inclua em logs ou chamadas de suporte.

O perfil local do Visual Studio também é uma fonte de configuração. Revise-o antes de commit para garantir que não contenha credenciais reais. Se alguma senha foi exposta em arquivo compartilhado ou versionado, troque-a no servidor e remova-a do histórico conforme a política do repositório.

## Executar localmente

Na raiz da solução:

```powershell
dotnet restore SyncDatabase.slnx
dotnet run --project SyncDatabase.Api
```

No ambiente Development, o Swagger fica disponível na raiz `/swagger` da API.

Para iniciar o Angular:

```powershell
cd frontend/syncdatabase-web
npm ci
npm start
```

O servidor Angular encaminha `/api` para a API local em `https://localhost:7133`. Inicie a API e confie no certificado de desenvolvimento HTTPS do .NET para o proxy conseguir conectar.

## Endpoints

| Método | Endpoint | Descrição |
|---|---|---|
| GET | `/api/conexoes/testar-producao` | Abre e testa a conexão de Produção. |
| GET | `/api/conexoes/testar-homologacao` | Abre e testa a conexão de Homologação. |
| GET | `/api/comparacao/estrutura` | Compara tabelas e metadados estruturais compartilhados. |
| GET | `/api/comparacao/dados?schema=public&tabela=nome_da_tabela&tamanhoLote=200` | Compara os dados de uma tabela compartilhada pela chave primária. Lote aceito: 1 a 500. |
| POST | `/api/sincronizacao/registro/previa` | Compara uma linha selecionada pela chave primária e prepara uma prévia de atualização. |
| POST | `/api/sincronizacao/registro/aplicar` | Aplica a prévia confirmada em Homologação. |

Os endpoints de conexão retornam somente estado e mensagem. Não retornam host, usuário, senha ou connection string.

## Escopo da comparação estrutural

O resultado compara tabelas, colunas, chaves primárias, chaves estrangeiras, índices, constraints `UNIQUE`/`CHECK`, views, materialized views, sequences, funções/procedures e triggers. Relacionamentos, índices e constraints são comparados nas tabelas que existem nos dois bancos; tabelas exclusivas são listadas separadamente. A equivalência de índices ignora o nome quando a estrutura das chaves é igual e compara método, unicidade, predicado, colunas incluídas e propriedades de validade. Os outros objetos são identificados por schema, nome e assinatura/pai quando aplicável, e suas definições são comparadas com espaço em branco normalizado.

`comparacaoEstruturalCompleta` é `false`, pois o endpoint estrutural não varre automaticamente os dados das tabelas. As consultas de estrutura usam os catálogos PostgreSQL; quando uma consulta de catálogo não puder ser executada, a API retorna erro em vez de interpretar silenciosamente metadados incompletos como equivalentes. A ausência de um objeto deve ser interpretada dentro das permissões e visibilidade disponíveis ao usuário PostgreSQL.

## Dados e sincronização

O endpoint `/api/comparacao/dados` compara uma tabela sob demanda. Exige chave primária com as mesmas colunas, na mesma ordem e com os mesmos tipos nos dois ambientes. Faz paginação por chave (lotes de até 500), usa transações `REPEATABLE READ` e `READ ONLY` em cada ambiente, aceita cancelamento HTTP e não carrega a tabela inteira na memória. As consultas de dados têm timeout de 90 segundos por lote. Colunas compartilhadas com tipos diferentes são explicitamente listadas como ignoradas; os demais valores são comparados pela representação JSONB do PostgreSQL, que normaliza objetos JSON. Tabelas sem chave primária equivalente são retornadas como não comparáveis.

A resposta fornece contagens e até dez identificadores HMAC temporários de chaves exclusivas por ambiente; ela não inclui valores das linhas nem escreve esses valores em logs. Os identificadores mudam quando o processo da API reinicia. O snapshot é consistente dentro de cada banco, mas os dois snapshots não são simultâneos, portanto alterações concorrentes entre ambientes podem aparecer como diferença. Tipos definidos pelo usuário ou valores para os quais o PostgreSQL não consiga produzir JSONB resultam em falha de consulta e não em equivalência presumida.

### Atualização pontual de dados

O Explorador de tabelas permite escolher schema e tabela, informar os valores da chave primária em JSON (por exemplo, `{"id": 123}`) e comparar somente esse registro entre Produção e Homologação. A interface mostra os valores divergentes antes de apresentar o botão de atualização. O botão solicita uma confirmação explícita; a API aceita apenas uma prévia válida por cinco minutos e uma única vez.

A gravação executa somente `UPDATE` em Homologação, para a chave primária selecionada e nas colunas divergentes que existam nos dois ambientes, tenham tipos compatíveis e não sejam chave primária, identity ou geradas. A operação exige a mesma chave primária nos dois bancos e que o registro já exista em ambos. Não insere registros ausentes, não exclui registros, não grava em Produção e não altera schema. A API compara a versão da linha de Homologação com a versão lida na prévia; se ela mudou nesse intervalo, a atualização é recusada e uma nova prévia é necessária.

O POST de prévia recebe `{"schema":"public","tabela":"nome_da_tabela","chavePrimaria":{"id":123}}`. O POST de aplicação recebe `{"previewId":"<id retornado pela prévia>","confirmado":true}`. A prévia contém valores da linha selecionada, portanto o acesso à aplicação deve ser restrito a operadores autorizados.

## Verificação

```powershell
dotnet build SyncDatabase.slnx
dotnet test SyncDatabase.slnx
```

Os testes atuais validam comparadores em memória. Não há testes de integração que abram conexões PostgreSQL.
