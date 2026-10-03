# QLStudy production deployment

The production stack contains three containers:

- `web`: Angular static files served by Nginx, exposed on port 80.
- `api`: ASP.NET Core API, reachable only inside the Docker network.
- `db`: PostgreSQL 17, persisted in a named Docker volume.

## 1. Build and package images on Windows

Install and start Docker Desktop, then run from PowerShell:

```powershell
cd D:\Work\WorkHome\QLStudy_v1\QLStudy.Service.Api
powershell -ExecutionPolicy Bypass -File .\deploy\build-images.ps1 -Version 1.0.0
```

The script creates `deploy/out/qlstudy-images-1.0.0-linux-amd64.tar`.

If Docker is available only on the Ubuntu server, upload both repositories as
sibling directories and build there instead:

```text
/opt/qlstudy/QLStudy.Service.Api
/opt/qlstudy/QLStudy.Web.Portal
```

A clean source archive can be created on Windows without Docker:

```powershell
powershell -ExecutionPolicy Bypass -File .\deploy\package-source.ps1 -Version 1.0.0
```

Upload `deploy/out/qlstudy-source-1.0.0.tar.gz`, then extract it on the server:

```bash
mkdir -p /opt/qlstudy
tar -xzf qlstudy-source-1.0.0.tar.gz -C /opt/qlstudy
```

Then run:

```bash
cd /opt/qlstudy/QLStudy.Service.Api/deploy
cp .env.example .env.production
nano .env.production
docker compose --env-file .env.production \
  -f compose.production.yml -f compose.build.yml build
docker compose --env-file .env.production -f compose.production.yml up -d
```

## 2. Prepare the server files

Copy these files to `/opt/qlstudy` on the Ubuntu server:

- `deploy/out/qlstudy-images-1.0.0-linux-amd64.tar`
- `deploy/compose.production.yml`
- `deploy/.env.example` as `.env.production`

Set strong, unique values for `POSTGRES_PASSWORD` and `JWT_SECRET` in `.env.production`.

## 3. Start the stack on Ubuntu

```bash
cd /opt/qlstudy
docker load --input qlstudy-images-1.0.0-linux-amd64.tar
docker compose --env-file .env.production -f compose.production.yml config
docker compose --env-file .env.production -f compose.production.yml up -d
docker compose --env-file .env.production -f compose.production.yml ps
```

Open `http://SERVER_IP` after all containers are healthy/running.

## Operations

View logs:

```bash
docker compose --env-file .env.production -f compose.production.yml logs -f --tail=200
```

Restart without deleting data:

```bash
docker compose --env-file .env.production -f compose.production.yml restart
```

Stop without deleting data:

```bash
docker compose --env-file .env.production -f compose.production.yml down
```

Do not run `docker compose down -v` in production because `-v` deletes the database volume.

Back up PostgreSQL:

```bash
mkdir -p /opt/qlstudy/backups
docker compose --env-file .env.production -f compose.production.yml exec -T db \
  sh -c 'pg_dump -U "$POSTGRES_USER" -d "$POSTGRES_DB" -Fc' \
  > /opt/qlstudy/backups/qlstudy-$(date +%F-%H%M).dump
```

Restore PostgreSQL into an empty database:

```bash
cat /opt/qlstudy/backups/qlstudy.dump | \
  docker compose --env-file .env.production -f compose.production.yml exec -T db \
  sh -c 'pg_restore -U "$POSTGRES_USER" -d "$POSTGRES_DB" --clean --if-exists'
```
