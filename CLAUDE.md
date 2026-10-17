# Prompt — Kanimambo Market (showcase de Design Patterns em .NET)

> Copia tudo abaixo desta linha para a tua ferramenta de vibe coding (Claude Code, Cursor, Copilot Agent, etc.).
> Recomendo também guardá-lo na raiz do repositório como `CLAUDE.md` / `AGENTS.md` para o agente o reler em cada sessão.

---

## 1. Papel e objetivo

Atua como um **engenheiro .NET sénior / arquiteto de software**. Vais construir comigo, de forma incremental, o **Kanimambo Market**: um backend de marketplace para Moçambique que serve como **projeto de portfólio no GitHub**. O objetivo é demonstrar a aplicação correta dos 26 design patterns listados na secção 5, cada um a resolver um **problema real de negócio** e não um exemplo artificial.

Regra de ouro: **nenhum padrão é aplicado "porque sim"**. Para cada padrão, o código tem de mostrar o problema que existiria sem ele e documentar quando *não* deve ser usado.

## 2. Contexto do negócio (Moçambique)

O Kanimambo Market é um marketplace onde **vendedores locais** (lojas, grossistas e pequenos produtores de Maputo, Matola, Beira e Nampula) vendem a **clientes finais e a clientes B2B** (restaurantes, mercearias).

Realidades locais que o sistema tem de resolver:

- **Moeda:** Metical (MZN). Alguns vendedores importadores definem o preço base em USD ou ZAR e convertem ao câmbio do dia.
- **Pagamentos por mobile money**, dominantes no país:
  - **M-Pesa** (Vodacom; prefixos 84/85)
  - **e-Mola** (Movitel; prefixos 86/87)
  - **mKesh** (Tmcel; prefixos 82/83)
  - Cartão via rede SIMO / POS
  - Pagamento na entrega (cash on delivery)
  - Os números de telefone usam o formato `258XXXXXXXXX`.
  - O pagamento por mobile money é **assíncrono**: o cliente recebe um pedido no telemóvel, confirma com PIN e o gateway responde por callback ou consulta de estado. Pode falhar com saldo insuficiente, transação duplicada, timeout, etc.
- **Fiscalidade:**
  - Identificação fiscal: **NUIT** (Número Único de Identificação Tributária, 9 dígitos).
  - **IVA** à taxa normal de 16%, com taxa reduzida de 5% para certos serviços (ex.: saúde e educação privadas) e alguns bens isentos.
  - Existem vendedores em **regime simplificado (ISPC)** que não liquidam IVA.
  - Todas as taxas, isenções e regimes têm de ser **configuráveis** (tabela/configuração), nunca hardcoded, porque mudam por lei.
  - **Comunicação mensal de faturas** à Autoridade Tributária de Moçambique através do portal e-Declaração, em CSV, XLSX ou XML. O SAF-T ainda não é obrigatório, mas o sistema deve estar preparado para um exportador SAF-T futuro.
- **Entregas:**
  - Mesmo dia em Maputo/Matola por motoqueiro ou txopela, com preço por zona/distância.
  - Envio interprovincial por transportadora, com preço por peso e destino.
  - Levantamento em ponto de recolha.
- **Conectividade instável:** clientes e vendedores perdem rede com frequência, por isso os pedidos repetidos têm de ser seguros (**idempotency keys**) e as integrações externas precisam de retry, timeout e circuit breaker.
- **Notificações:** SMS é o canal principal (nem todos têm smartphone), seguido de WhatsApp e e-mail.

## 3. Stack e restrições técnicas

- **.NET 10 (LTS)**, C# atual, `Nullable` ativo, `TreatWarningsAsErrors` ativo.
- **ASP.NET Core Web API com Controllers** (`[ApiController]` + `ControllerBase`). **NÃO usar Minimal APIs.**
- Os controllers são **finos**: validam o input, despacham via o Mediator próprio e mapeiam o resultado para HTTP.
- Tratamento de erros com **ProblemDetails** (RFC 9457) através de filtros/middleware. Nada de `try/catch` nos controllers.
- **EF Core** + **PostgreSQL**.
- **.NET Aspire** (AppHost + ServiceDefaults) para orquestração local: Postgres, Redis e serviços fake dos gateways.
- **Redis** para cache distribuída.
- **Resiliência:** `Microsoft.Extensions.Http.Resilience` (Polly v8).
- **Testes:** xUnit, Shouldly (ou FluentAssertions com licença compatível), NSubstitute, **Testcontainers** para integração e `WebApplicationFactory` para testes de API.
- **BenchmarkDotNet** para os padrões de performance (Object Pool, Flyweight).
- **Não usar MediatR** (passou a licença comercial). O Mediator é implementado no projeto, o que também é parte do showcase.
- **Não usar AutoMapper.** O mapeamento é manual ou com Mapperly (source generator).
- **Gateways externos** (M-Pesa, e-Mola, mKesh, transportadora, câmbio, SMS) têm sempre:
  1. uma interface no domínio/aplicação;
  2. um **simulador local** (fake) que corre no Aspire e reproduz sucesso, saldo insuficiente, duplicado, timeout e callback atrasado;
  3. opcionalmente, um adapter real (ex.: M-Pesa Open API com API key + public key RSA) atrás de configuração.

  O projeto **tem de correr 100% localmente sem credenciais reais**.
- **Idioma:** código, commits, README e docs em **inglês**, para o público internacional. Os termos legais e de domínio moçambicanos mantêm-se em português (NUIT, IVA, ISPC, e-Declaração, txopela).

## 4. Arquitetura

O sistema é um **monólito modular** com Clean Architecture por módulo. Os módulos comunicam só por contratos públicos e eventos, nunca acedendo ao `DbContext` uns dos outros.

```
Kanimambo.sln
src/
  Kanimambo.AppHost/                 # .NET Aspire
  Kanimambo.ServiceDefaults/
  Kanimambo.Api/                     # Program.cs, filtros globais, ProblemDetails, auth
  Kanimambo.SharedKernel/            # Entity, AggregateRoot, DomainEvent, Result<T>,
                                     # Mediator, Specification base, Money (MZN)
  Modules/
    Catalog/        {Api, Application, Domain, Infrastructure}
    Cart/           {Api, Application, Domain, Infrastructure}
    Promotions/     {Api, Application, Domain, Infrastructure}
    Ordering/       {Api, Application, Domain, Infrastructure}
    Payments/       {Api, Application, Domain, Infrastructure}
    Shipping/       {Api, Application, Domain, Infrastructure}
    Invoicing/      {Api, Application, Domain, Infrastructure}
    Notifications/  {Api, Application, Domain, Infrastructure}
  Simulators/
    Kanimambo.Simulators.MobileMoney/  # fake M-Pesa / e-Mola / mKesh com callbacks
    Kanimambo.Simulators.Courier/
    Kanimambo.Simulators.ExchangeRates/
tests/
  Kanimambo.<Module>.UnitTests/
  Kanimambo.<Module>.IntegrationTests/
  Kanimambo.ArchitectureTests/       # NetArchTest: regras de dependência entre camadas/módulos
  Kanimambo.Benchmarks/
docs/
  patterns/<PatternName>.md          # um ficheiro por padrão
  adr/NNNN-<title>.md                # Architecture Decision Records
.github/workflows/ci.yml
```

Cada módulo `*.Api` contém os seus controllers. O `Kanimambo.Api` regista-os com `AddControllers().AddApplicationPart(...)`.

## 5. Mapa de padrões → problemas reais (obrigatório implementar todos)

### Behavioral

| Padrão | Problema real | Módulo | Critério de aceitação |
|---|---|---|---|
| **Chain of Responsibility** | Validar uma encomenda antes de aceitar: NUIT válido (quando B2B), stock, zona de entrega coberta, limite antifraude por número de telefone, valor mínimo do vendedor. Cada handler pode parar a cadeia com erro tipado. | Ordering | A ordem dos handlers é configurável via DI; há testes que provam que a cadeia para no primeiro falhanço. |
| **Command** | Operações de backoffice auditáveis e reversíveis, por exemplo "ajustar preço em massa de um vendedor" e "suspender vendedor", com `Execute`/`Undo` e log de auditoria persistido. | Catalog (Admin) | Endpoint de undo; o log de auditoria mostra quem fez o quê e quando. |
| **Interpreter** | Regras de promoção escritas pelo marketing sem deploy: `cart.total >= 2500 AND customer.city = "Maputo" AND payment.method = "MPesa"`. Inclui tokenizer, parser e AST avaliável. | Promotions | As regras são guardadas em BD como texto e avaliadas em runtime; erros de sintaxe dão mensagem clara com a posição. |
| **Iterator** | Importar o catálogo de um grossista cuja API é paginada e lenta, exposto como `IAsyncEnumerable<Product>` com streaming, sem carregar tudo em memória. | Catalog | O teste prova que só 1 página está em memória de cada vez; o cancelamento funciona. |
| **Mediator** | Despacho in-process de commands, queries e notificações, com pipeline behaviors (logging, validação, transação, idempotência). Implementação própria. | SharedKernel | Os controllers só dependem de `IMediator`; os behaviors são registáveis por ordem. |
| **Memento** | O carrinho guarda snapshots porque o cliente perde rede a meio, e o backoffice tem "desfazer" na edição de rascunhos de encomenda B2B. | Cart | É possível restaurar o snapshot N; o snapshot é imutável e serializável. |
| **Observer** | Domain events: `OrderPaid` dispara a emissão da fatura, o SMS ao cliente, a notificação ao vendedor e a reserva de stock. Usa um outbox para garantir a entrega. | Transversal | Um handler que falha não afeta os outros; existe outbox com retry. |
| **State** | Ciclo de vida da encomenda: `Pending → AwaitingPayment → Paid → Preparing → OutForDelivery → Delivered`, mais `Cancelled`, `PaymentFailed`, `Refunded`. Inclui também o estado do **pagamento mobile money** (`Initiated → AwaitingCustomerPin → Confirmed / Failed / TimedOut`). | Ordering, Payments | As transições inválidas são impossíveis por design e testadas exaustivamente. |
| **Strategy** | Cálculo de portes: mesmo dia Maputo/Matola (por zona), interprovincial (por peso e destino), levantamento (grátis). | Shipping | Seleção da estratégia sem `switch`; adicionar uma nova estratégia não altera código existente. |
| **Template Method** | Pipelines de importação de catálogo dos vendedores (CSV, Excel, XML do grossista) com os passos fixos `Read → Validate → Map → Persist → Report` e hooks por formato. | Catalog | Relatório final com linhas aceites e rejeitadas e o motivo de cada rejeição. |
| **Visitor** | Percorrer a árvore de documentos fiscais (fatura, nota de crédito, linhas, impostos) para gerar o ficheiro mensal da AT em CSV, XLSX ou XML para o e-Declaração, o PDF da fatura e o resumo de IVA por taxa. Preparado para um futuro `SaftVisitor`. | Invoicing | Adicionar um novo formato de exportação = novo visitor, sem tocar nos documentos. |

### Creational

| Padrão | Problema real | Módulo | Critério de aceitação |
|---|---|---|---|
| **Abstract Factory** | Famílias por **regime fiscal do vendedor**: `IvaNormalFactory` e `IspcFactory`, cada uma a produzir calculador de imposto, formatador de documento fiscal e série de numeração coerentes entre si. | Invoicing | É impossível misturar componentes de regimes diferentes. |
| **Builder** | Construir uma `Invoice` válida com NUIT do emitente e do adquirente, série, numeração sequencial, linhas, isenções com motivo legal, totais em MZN e câmbio quando aplicável. | Invoicing | `Build()` valida tudo e devolve `Result<Invoice>`; não existem faturas inválidas em memória. |
| **Factory Method** | Criar o processador de pagamento certo: por **prefixo do número** (84/85 → M-Pesa, 86/87 → e-Mola, 82/83 → mKesh) ou pelo método escolhido (cartão, cash on delivery). | Payments | Tem testes por prefixo, e um prefixo desconhecido dá um erro de domínio claro. |
| **Null Object** | `GuestCustomer`, `NoPromotion` e `NoExchangeRate` (para preços já em MZN) eliminam verificações de null no checkout. | Cart, Promotions | Zero `if (x is null)` no fluxo de checkout. |
| **Object Pool** | Reutilizar objetos caros na geração em massa de PDFs, QR codes e ficheiros mensais da AT (`StringBuilder`, buffers), com `Microsoft.Extensions.ObjectPool`. | Invoicing | Benchmark antes/depois com alocações (`MemoryDiagnoser`) publicado em `docs/patterns/ObjectPool.md`. |
| **Prototype** | Encomendas recorrentes B2B: um restaurante repete a encomenda semanal ao grossista, por isso clona a anterior (deep copy) e ajusta as quantidades. | Ordering | O clone é independente do original (o teste altera um e verifica o outro). |
| **Singleton** | Tabela de taxas de IVA/ISPC carregada e em cache, e o provider de feature flags, ambos via **lifetime de DI** (`AddSingleton`) e thread-safe. | Transversal | O ADR explica porque é que o Singleton clássico (`static Instance`) é evitado (testabilidade e acoplamento). |

### Structural

| Padrão | Problema real | Módulo | Critério de aceitação |
|---|---|---|---|
| **Adapter** | As APIs de M-Pesa, e-Mola e mKesh têm formatos, códigos de erro e autenticação diferentes, e todas são adaptadas para uma única `IMobileMoneyGateway` com erros de domínio unificados. O mesmo se aplica às transportadoras. | Payments, Shipping | Os códigos nativos (ex.: `INS-6` saldo insuficiente, `INS-5` duplicado) são mapeados para um enum de domínio. |
| **Bridge** | Notificações: a abstração (`OrderConfirmation`, `PaymentReminder`, `DeliveryUpdate`) é separada da implementação de canal (SMS, WhatsApp, e-mail). As duas hierarquias variam de forma independente, e o SMS respeita o limite de 160 caracteres. | Notifications | Adicionar um canal ou um tipo de mensagem não cria uma explosão de classes. |
| **Composite** | Cabazes e kits (ex.: "cabaz do mês" com arroz, óleo, açúcar e farinha). Um produto pode conter produtos, e o preço, o peso (para portes) e o stock calculam-se recursivamente. O IVA é calculado por componente, porque as taxas podem diferir. | Catalog | Cabazes aninhados funcionam; o stock do cabaz é o mínimo derivado dos componentes. |
| **Decorator** | Adicionar caching (Redis), retry, logging e métricas a repositórios e clientes HTTP sem os alterar. | Infra | A ordem dos decorators é visível no registo de DI; cada decorator é testado isoladamente. |
| **Facade** | O `CheckoutService` orquestra carrinho, promoções, portes, encomenda, pagamento e idempotência numa única operação para o controller. | Checkout (Ordering) | O controller de checkout tem uma única dependência funcional. |
| **Flyweight** | Milhares de variantes de produto (tamanho, cor, unidade: kg, saco de 25 kg, caixa) partilham atributos imutáveis internados em vez de duplicados. | Catalog | Benchmark de memória com 100k variantes, antes e depois. |
| **Proxy** | Proxy de cache e rate-limit à frente da API de câmbio (USD/ZAR → MZN), que é lenta e tem limite de chamadas. O câmbio é cacheado por dia. | Pricing (Catalog) | O teste prova 1 chamada externa para N pedidos no mesmo dia. |
| **Specification** | Filtros combináveis do catálogo (`InStock & PriceBetween & Category & DeliversTo("Matola") & SellerRating >= 4`) traduzidos para `Expression<Func<T,bool>>` e executados pelo EF Core no SQL. | Catalog | O teste de integração confirma que o filtro corre no PostgreSQL e não em memória. |

## 6. Regras de qualidade (Definition of Done de cada padrão)

1. **Documentação** em `docs/patterns/<Pattern>.md`, sempre com esta estrutura:
   - *Problem*: o cenário de negócio real.
   - *Naive solution*: o snippet sem o padrão (ifs/switch/duplicação) e porque dói.
   - *Solution*: diagrama Mermaid de classes ou de sequência e explicação.
   - *Where in the code*: links para ficheiros.
   - *Tests*: links para os testes que provam o comportamento.
   - *Trade-offs & when NOT to use*.
2. **Testes unitários** a cobrir o comportamento do padrão, e **testes de integração** quando há BD ou HTTP.
3. **Nenhum warning** de compilação.
4. **Entrada no README**: a tabela-índice dos 26 padrões, com link para o doc e para o código.
5. **Commit convencional**, por exemplo `feat(ordering): order lifecycle with State pattern`. Um padrão por PR/branch quando possível.

Regras gerais:

- Domínio rico: entidades com comportamento e invariantes protegidas, sem setters públicos.
- `Money` como value object (MZN, arredondamento a 2 casas, `MidpointRounding.AwayFromZero`). Os cálculos de IVA usam `decimal`, nunca `double`.
- `Result<T>` para erros de negócio; as exceções ficam para situações excecionais.
- Todos os endpoints de escrita aceitam o header `Idempotency-Key`.
- `TimeProvider` injetado em vez de `DateTime.Now`.
- Logging estruturado e OpenTelemetry via ServiceDefaults.
- Seed de dados realista: vendedores em Maputo, Matola, Beira e Nampula, produtos locais e preços em MZN.
- **Testes de arquitetura** (NetArchTest): o Domain não referencia a Infrastructure, e os módulos não referenciam o interior uns dos outros.

## 7. Plano de execução (trabalha por fases e PARA no fim de cada uma)

No fim de cada fase: compila, corre todos os testes, mostra-me um resumo do que foi feito e **espera pela minha aprovação** antes de avançar.

- **Fase 0: Fundações**
  - Solução, `Directory.Build.props` (versões centralizadas com `Directory.Packages.props`), AppHost Aspire com Postgres e Redis, ServiceDefaults.
  - SharedKernel: `Entity`, `AggregateRoot`, `DomainEvent`, `Result<T>`, `Money`, **Mediator** com pipeline behaviors, **Specification** base.
  - `Kanimambo.Api` com controllers, ProblemDetails, filtro de validação e middleware de idempotência.
  - CI no GitHub Actions (build + testes + cobertura) e testes de arquitetura.
  - README inicial com a visão do projeto e o índice dos padrões (todos marcados como "planned").
- **Fase 1: Catalog** (Composite, Flyweight, Specification, Iterator, Template Method, Command, Proxy de câmbio).
- **Fase 2: Cart & Promotions** (Memento, Null Object, Interpreter).
- **Fase 3: Ordering & Checkout** (State, Chain of Responsibility, Prototype, Facade, Observer + outbox).
- **Fase 4: Payments** (Factory Method, Adapter, State do pagamento, simulador mobile money com callbacks assíncronos, Decorator de resiliência).
- **Fase 5: Shipping** (Strategy, Adapter de transportadora, simulador).
- **Fase 6: Invoicing** (Builder, Abstract Factory por regime fiscal, Visitor para o ficheiro mensal da AT e PDF, Object Pool + benchmarks, Singleton da tabela de taxas).
- **Fase 7: Notifications** (Bridge: SMS, WhatsApp, e-mail).
- **Fase 8: Polimento**
  - Revisão dos 26 docs e diagrama de arquitetura (C4 nível 1 e 2, em Mermaid) no README.
  - Coleção `.http` com o fluxo completo: procurar produto → carrinho → promoção → checkout com M-Pesa (simulado) → callback → fatura → SMS.
  - Badges de build e cobertura, e um GIF/demo.

## 8. Anti-padrões a evitar

- Forçar um padrão onde um `if` simples chega. Se te parecer forçado, diz-me e propõe um cenário melhor antes de implementar.
- Controllers com lógica de negócio ou acesso direto ao `DbContext`.
- Classes "god service" ou repositórios genéricos que expõem `IQueryable` para fora da Infrastructure.
- Taxas de imposto, prefixos de operadoras ou preços de portes hardcoded. Tudo isto vai para configuração ou BD.
- Código gerado sem testes, ou testes que só testam mocks.
- Inventar endpoints ou contratos das APIs reais de M-Pesa, e-Mola ou mKesh como se fossem oficiais. O simulador é a fonte de verdade local, e o adapter real fica marcado como "requires sandbox credentials".

## 9. Primeira ação

Começa pela **Fase 0**. Antes de escrever código, apresenta:

1. a árvore de projetos final;
2. a lista de pacotes NuGet com versões;
3. o design da interface do Mediator e dos pipeline behaviors.

Depois aguarda a minha confirmação.

---

## 10. Decision log & current status (kept up to date by the agent)

**Current phase:** Phase 2 (Cart & Promotions) complete; awaiting approval for Phase 3 (Ordering & Checkout).

Decisions taken (see `docs/adr/`):
- Solution file is `Kanimambo.slnx`; SDK pinned in `global.json` (10.0.402, rollForward latestFeature).
- One PostgreSQL database, **one schema per module** (+ `platform` schema for host tables); each DbContext keeps its migrations history in its own schema.
- `Kanimambo.SharedKernel.Web` holds `ApiControllerBase` (Result → ProblemDetails with `code` extension) and `[RequiresIdempotencyKey]`; module `*.Api` projects reference it (they cannot reference the host).
- Own `IValidator<T>` (no FluentValidation).
- Mediator behavior order: Logging → Validation → UnitOfWork → Idempotency (idempotency inside the UoW so the inbox mark commits atomically). Behaviors are scoped with generic constraints.
- Each module registers keyed `IUnitOfWork` and `IInboxStore` (key = module name) and is declared with `AddMediator(o => o.AddModule("<Module>", assembly))` in `Program.cs`.
- HTTP idempotency is stored in PostgreSQL (`platform.IdempotencyRecords`), not Redis.
- Tests run on **Microsoft.Testing.Platform** (xUnit v3 4.x): `dotnet test --solution Kanimambo.slnx`; coverage via `Microsoft.Testing.Extensions.CodeCoverage` + ReportGenerator (local tool).
- Pinned transitive `MessagePack` 2.5.303 (vulnerability in the version pulled by Aspire.Hosting).
- Integration tests derive from `tests/Kanimambo.Testing/KanimamboWebApplicationFactory` (containers + eager host start), one assembly fixture per test project.
- New module checklist: `builder.Add<Module>Module()` (uses `AddModuleDbContext<T>` → own schema, migrations, keyed UoW + inbox), `AddApplicationPart` + `AddModule` in `Program.cs`, reference the module from `Kanimambo.Api` so architecture tests discover it.
- Catalog search runs specifications over the `catalog.catalog_entries` view; its recursive CTE mirrors the Composite rules (ADR 0004). Changing bundle arithmetic means changing both — an integration test guards that they agree.
- Shared EF plumbing: `Kanimambo.SharedKernel.Infrastructure` (`AddModuleDbContext<T>`, `EfUnitOfWork<T>`, `EfInboxStore<T>`, `MigratableDbContext`).
- Auth: JWT bearer; policy/role `backoffice`; locally `dotnet user-jwts` (ADR 0005); tests mint tokens via `KanimamboWebApplicationFactory.CreateClientAs`.
- Simulators: ExchangeRates (800 ms, 30 calls/min, simulated rates) and Wholesaler (2 400 SKUs, 300 ms/page). The Wholesaler simulator is an addition to the original tree, needed to run the Iterator locally.
- Error type `Unavailable` → 503 for upstream outages and exhausted quotas.
- Flyweight pool is process-wide static (`VariantAttributesPool.Shared`) because EF value converters cannot take DI services.
- Integration tests share one database per test assembly: tests that mutate data create their own seller (`CatalogApiFactory.CreateSellerAsync`); assertions on seed data filter by `CatalogSeeder.SellerIds`.
- Cross-module calls go through `Kanimambo.<Module>.Contracts` (interfaces + DTOs, SharedKernel only; ADR 0006). Catalog exposes `ICatalogReader` / `ICurrencyConverter`; Promotions exposes `IPromotionEvaluator` + `NoPromotion`.
- `IExchangeRate` + `NoExchangeRate` live in SharedKernel; `IExchangeRateProvider` returns `Result<IExchangeRate>` (use `Result.Success<IExchangeRate>(...)` — implicit conversions do not apply to interface types).
- Cart: class is `ShoppingCart` (avoids a namespace/type clash), ids are random v4 (a guest cart id is the access key); every change saves a `CartMemento` (jsonb) in the same UoW; history is a linear stack, last 20 kept; `xmin` concurrency → 409 `concurrency.conflict`.
- Promotions: rules stored as typed by marketing, type-checked against `RuleVariables` on save; API takes `discountKind` as text (`Percentage`/`FixedAmount`) so `*.Api` never references Domain types. JSON enums are serialized as strings API-wide.
- Role names live in `SharedKernel.Identity.KnownRoles` (`backoffice`, `b2b`); `ICurrentActor.IsInRole`.
- Npgsql only accepts UTC `DateTimeOffset` for `timestamptz`: never persist Maputo-offset values (use `MozambiqueTime` only for date logic).
- Shared test factory routes every HttpClient to in-process simulators by host (`SimulatorRouter`); seeds Catalog + Promotions.
