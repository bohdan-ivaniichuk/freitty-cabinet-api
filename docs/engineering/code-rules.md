# Code rules

The conventions every C# file in this repository follows. They exist so that a reviewer reads one
shape everywhere and spends their attention on decisions, not on style. Formatting itself lives in
[`.editorconfig`](../../.editorconfig); architecture and stack decisions live in
[`docs/plan.md`](../plan.md).

A rule can be departed from when the departure is visible and argued — one line at the code or in
the commit message. A silent departure is not allowed.

## Design

### 1. Handlers orchestrate; the domain owns its rules

A handler loads what it needs, calls the domain, saves, and returns. Whether a status transition is
allowed, what the next status is, when a quantity delta raises an alert — that is domain behaviour
and lives on the entity that owns the data.

```csharp
// Bad — the handler decides the transition
order.Status = OrderStatus.Completed;
order.CompletedAt = now;

// Good — one owner, and the intent has a name
order.Complete(now);
```

### 2. Several outcomes are not a `bool`

`Task<bool> ExistsAsync(...)` is fine: one question, two answers. A method where `false` can mean
"not found", "already done" or "conflict" returns an explicit outcome the caller can switch on.

### 3. Read side: SQL does the work

- Read handlers project straight to DTOs with `Select` and `AsNoTracking`. An entity is never loaded
  only to be mapped.
- No lazy loading. Related data is projected or included explicitly; a detail query with several
  collections uses `AsSplitQuery`.
- Filtering, search and pagination run in the database, never on a materialized list.
- Ask whether anything is there with `AnyAsync`, not `CountAsync(...) > 0`. On an in-memory
  `List<T>`, `Count` is right.

### 4. The shapes this codebase uses

- **One operation, one folder.** `{Operation}Query.cs` / `{Operation}Command.cs`, its handler, and
  its validator when there is input to validate. One type per file.
- **Endpoints are FastEndpoints**, one folder per endpoint. An endpoint maps the request to a
  command or query and the result to a response — nothing else.
- **Commands, queries, requests, responses and DTOs are `record`s.**
- **`sealed` by default.** Inheritance is opened deliberately when something actually derives.
- **The API boundary carries DTOs, never entities.**
- **Mapping is not written by hand in a handler.** Read side: a projection in the query. Write side:
  a Mapperly mapper.

### 5. Validation lives in validators

Input is validated by the FluentValidation validator of the command or query. Guard clauses protect
programming contracts and do not repeat what a validator already refused. A nested object gets its
own validator.

### 6. Failures say enough to act on

An expected failure (not found, validation, conflict) is not an exception; it ends as a
`ProblemDetails` response with the right status code. The failure names what was not found —
`Order FR001383 not found`, not `Not found`. An exception message tells the reader what to check,
not only what happened.

## Naming and comments

### 7. Names carry the logic; comments carry the why

A comment explains a constraint, a trade-off, or behaviour that is easy to break by accident. A
comment that restates the code, narrates how a decision was reached, or patches a misleading name is
a defect: fix the name instead.

- No XML documentation on obvious members, private members or test classes.
- No ticket numbers or task references in code.
- When you are about to write a comment saying what the next block does, extract the block into a
  private method with that name. Extract cohesive steps, not single lines.

### 8. Dependencies are named for their role, and nothing carries `_`

`IAppDbContext db`, `TimeProvider clock` — not `_appDbContext`. A constructor that only assigns
dependencies is a primary constructor. Where a field is genuinely needed, it has no `_` prefix.

A variable is named for what it holds, not for the verb that produced it: `operationResult`, not
`added`; `orderDtos`, not `dtos`.

### 9. A name says what the value is

Someone who opens a query, a command or a DTO should know what goes into each field without
reading the handler. The same names travel to the wire: query parameters and JSON fields are
these names in camelCase.

- An identifier is `…Id`: `HubId`, not `Hub`. `Hub` is a hub itself — the entity or its DTO.
- A flag reads as a predicate, `Is…` / `Has…` / `Can…`: `HasOpenAlerts`, not `Alerts`.
- A number of things is `…Count`: `PhotoCount`, not `Photos`, which would be the photos. The
  members of a type that is itself a set of counters or figures need no suffix:
  `OrderCountsDto.Drafts`, `CompletedOrdersDto.Last30Days`.
- No abbreviations and no single letters: `Search`, not `Q`.
- A name that is clear only next to its type or its owner takes the qualifier: `OrderNumber` on a
  command that records an operation, `BestBucket`, `NextStep`.

### 10. Lambda and `out` parameters get real names

`orders.Where(order => order.Status == OrderStatus.Draft)`, not `o =>`. In EF configuration use
`builder` / `entity`. The discard `_` and `catch (Exception ex)` are fine.

### 11. A number or a string with meaning gets a name

A limit, a default, a threshold, a status word the code branches on is a named constant:
`MaxPageSize`, not `100`. `0`, `1` and `string.Empty` in their obvious roles need no ceremony.

### 12. A closed set is an enum

Order type, status, unit type, operation type — enums, never strings. Every member has an explicit
value (`Draft = 1`), because persisted values must not depend on declaration order. Enums travel as
strings in JSON through one globally registered converter, never a `ToString()` at a call site.

## C# mechanics

### 13. Time is injected

Never `DateTime.UtcNow` / `DateTimeOffset.UtcNow`. Inject `TimeProvider`. "Completed in the last
30 days", "today" and chart periods are untestable against a static clock.

### 14. `CancellationToken` is propagated

Every async method takes `CancellationToken cancellationToken` as its last parameter, without a
default, and passes it to every call that accepts one. Framework overrides keep the base signature.

### 15. No `ConfigureAwait(false)`

ASP.NET Core has no synchronization context; in application code it only adds noise.

### 16. `var` when the type is written, explicit when it is not

```csharp
var orders = new List<OrderDto>();                 // the type is right there
OrderDetailsDto details = await LoadAsync(...);    // it is not — say it
int pageSize = 20;                                 // built-in types stay explicit
```

`var` only when the right-hand side is `new T(...)`, a cast or `as T`.

### 17. Class layout and usings

Constants, fields, constructor, public members, private members, nested types — in that order.
No unused usings; `System*` first, the rest alphabetical, outside a file-scoped namespace. A type
gets a `using`, not a fully qualified name inline.

## Tests

### 18. A behaviour change comes with tests

Every meaningful branch, failure path and state transition the change introduces. Domain rules get
unit tests; filters, search, pagination and the write flow get integration tests against a real
PostgreSQL (Testcontainers).

- Test names are one PascalCase phrase stating the behaviour: `SearchMatchesRefNumber`,
  `AddingOperationRecalculatesActualQuantity`. No underscores, no `Method_Scenario_Result`.
- Anything a test asserts against uses a fixed instant through `FakeTimeProvider`, never the wall
  clock.
- Arrange / act / assert, one behaviour per test.

## Before a commit

### 19. Self-review, in this order

1. Read the whole diff hunk by hunk, looking for defects — not the summary of what was done.
2. Check it against the task: everything asked, nothing that was not.
3. Fix what you found.
4. Build and run the tests, and look at the result. "It should pass" is not a result.

Questions worth the two minutes:

- Does a handler decide something an entity could decide?
- Does any class need "and" to describe it?
- Does every business literal have a name, and every variable, field and parameter say what it
  holds — an id `…Id`, a flag a predicate, a count `…Count`?
- Does any `bool` hide a richer outcome?
- Is a pattern applied in this file but not its neighbour?
- Could the read query be answered by SQL instead of memory?

Anything in the diff you cannot explain is not ready to commit.
