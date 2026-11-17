---
name: stack-up
description: Bring the local Docker stack (API and PostgreSQL) up and report its health. Use when the user asks to start, run or bring up the stack or the app in Docker, or to check that it is healthy.
---

# stack-up

Starts the whole stack from `docker-compose.yml` and confirms that the API is ready.

## Steps

1. From the repository root, run:

   ```bash
   docker compose up -d --build --wait --wait-timeout 180
   ```

2. Confirm readiness:

   ```bash
   curl -fsS -o /dev/null -w "%{http_code}\n" http://localhost:8080/health/ready
   ```

   `200` means the API is up and reaches PostgreSQL.

3. **On success**, report the service states from `docker compose ps` and the API URL,
   `http://localhost:8080`.

4. **On failure** — `up` exits non-zero or readiness is not `200`:
   - show `docker compose ps`;
   - for each service that is not `healthy`, show `docker compose logs --tail 50 <service>`;
   - name the likely cause from what the output shows:
     - *port already in use* — `bind: address already in use` for `5432` or `8080`; another
       PostgreSQL or a `dotnet run` instance is holding the port;
     - *image build error* — `docker compose up` failed during `build`; quote the failing step;
     - *database not ready* — `postgres` is not `healthy`, or the API logs show connection errors.

   Do not retry blindly; report and let the user decide.

## Stopping

- `docker compose down` stops the stack and keeps the database volume.
- `docker compose down -v` also deletes the database volume. Run it only when the user asks for a
  reset.
