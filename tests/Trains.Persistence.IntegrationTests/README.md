# Trains.Persistence.IntegrationTests

These are opt-in integration tests that run EF Core against a *real* PostgreSQL instance by creating a temporary database per test.

## Enable

Set `TRAINS_PG_TESTS=1`.

## Connection strings

- `TRAINS_PG_ADMIN` (optional): admin connection string used to `CREATE DATABASE` / `DROP DATABASE`. Default: `Host=127.0.0.1;Database=postgres`
- `TRAINS_PG_BASE` (optional): base connection string used to connect to the temp DB (the test code appends `Database=<temp>` and forces `Pooling=false`). Default: `Host=127.0.0.1`

## Run

`$env:TRAINS_PG_TESTS='1'; dotnet test Trains.slnx -c Release`

