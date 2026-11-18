# 0009 — FastEndpoints, with handlers on its command bus

- Status: accepted
- Date: 2026-11-18

## Context

The API is a handful of endpoints over an application layer that must not know HTTP
([ADR 0001](0001-light-clean-architecture.md)). Every request should pass the same steps — log,
validate, handle — without each handler repeating them, and every error should leave as a
`ProblemDetails`. MediatR, the library that used to be the default answer to this, became
commercial in 2025.

## Decision

Endpoints are FastEndpoints classes, one per route, under `/api/v1`. An endpoint binds the
request, executes a query or command and sends the result.

Queries and commands are `ICommand<TResult>` records in Application; their handlers are
`ICommandHandler<,>` and their validators FluentValidation validators. From FastEndpoints,
Application references only `FastEndpoints.Messaging.Core`, a package without dependencies.

The pipeline — logging, then validation — is command middleware registered once in Api. A
validation failure becomes the `400` `ProblemDetails` FastEndpoints produces for a request it
refuses itself.

The OpenAPI document comes from `FastEndpoints.OpenApi`, which builds on
`Microsoft.AspNetCore.OpenApi`, and is shown with Scalar.

## Alternatives

- **Minimal APIs with handlers called directly** — no library at all, and the application layer
  would not change: handlers and validators are plain classes. Rejected for the endpoints
  themselves: grouping, versioning, validation and the error shape would each be wired by hand.
  It remains the exit: an endpoint class is a thin adapter and could be replaced by a `MapGet`.
- **Controllers** — familiar, with filters for the cross-cutting steps. Rejected: a controller
  gathers unrelated actions in one class, and filters run around HTTP actions, not around
  application operations.
- **MediatR** — the pipeline behaviours this design imitates. Rejected: commercial licence, for
  what the command bus already in the chosen framework does.
- **Validation at the endpoint only** (`Validator<TRequest>`) — FastEndpoints' default, and less
  code. Rejected: the rule that a page size has an upper bound belongs to the operation, and an
  operation called from anywhere but HTTP would skip it.
- **`FastEndpoints.Swagger`** — the long-standing NSwag integration. Rejected: no new features
  since version 8.2 and marked for deprecation.

## Consequences

- An operation is one folder in Application — query, handler, validator — and one endpoint class in
  a folder of the same name in Api. Handlers are testable without a web host, though the tests here
  go through HTTP.
- Application depends on a FastEndpoints package. It is an interface-only package with no
  dependencies; replacing the bus means replacing two interfaces.
- The middleware lives in Api, because the bus's middleware contract and `ExecuteAsync` sit in a
  package that references ASP.NET Core. Application stays free of it; the pipeline is host
  configuration.
- FastEndpoints does not register command validators, so Api registers the validators of the
  Application assembly itself, and the validation middleware reports through FastEndpoints'
  validation context to get the standard error body.
- The `400` body is FastEndpoints' shape — `errors` is an array of `{ name, reason }`, not the
  dictionary of ASP.NET Core. The pages read that shape.
- The library is maintained by a small team and has warned about its funding. The exposure is the
  endpoint classes and two middleware classes; the application layer would survive a move to
  Minimal APIs untouched.
- Version 9 targets .NET 10 and 11 only, and its documentation still shows older method names in
  places; the wiring is checked against a running application, not against samples.
