# Deployment, Docker & Development Operations Guide

## 1. Prerequisites
- **Docker** & **Docker Compose** (v2+)
- **.NET 9 SDK** (`9.0.x`) for running backend tools directly on the host
- **Node.js** (`v20+` or `v24+`) for running frontend tools directly on the host
- **Git**

## 2. Environment Setup
1. Copy `.env.example` to `.env`:
   ```bash
   cp .env.example .env
   ```
2. Update `POSTGRES_PASSWORD` and `Jwt__SecretKey` in `.env` for your environment.

## 3. Starting Services with Docker Compose
```bash
docker compose up --build -d
```
- **API & Swagger UI:** `http://localhost:8080/swagger`
- **Health Checks:** `http://localhost:8080/health/live` and `http://localhost:8080/health/ready`
- **Frontend Dashboard:** `http://localhost:3000`
- **PostgreSQL:** `localhost:5432` (persisted in named volume `inventory_postgres_data`)

## 4. Stopping Services Safely
```bash
docker compose down
```
> **Important:** Never pass `-v` (`--volumes`) during ordinary development shutdown so the persistent PostgreSQL volume (`inventory_postgres_data`) is preserved.

## 5. Viewing Logs
```bash
docker compose logs -f inventory-api
docker compose logs -f inventory-web
docker compose logs -f inventory-db
```

## 6. Running Database Migrations
Migrations are applied automatically on API startup when configured, or manually via:
```bash
dotnet ef database update --project src/Inventory.Infrastructure --startup-project src/Inventory.API
```

## 7. Running Automated Tests
```bash
# Run all Unit, Architecture, and PostgreSQL Integration Tests
dotnet test Inventory.sln --configuration Release
```

## 8. Backup & Recovery Procedure
- **Create logical backup:**
  ```bash
  docker exec -t inventory-db pg_dump -U inventory_app -d enterprise_inventory_db -F c -f /tmp/backup.dump
  docker cp inventory-db:/tmp/backup.dump ./backups/backup.dump
  ```
- **Restore backup:**
  ```bash
  docker exec -i inventory-db pg_restore -U inventory_app -d enterprise_inventory_db --clean --if-exists /tmp/backup.dump
  ```
