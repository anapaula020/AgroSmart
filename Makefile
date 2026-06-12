# ─────────────────────────────────────────────────────────────────────────────
# Makefile - .NET API + SQL Server Toolkit
# Usage: make <target>
# ─────────────────────────────────────────────────────────────────────────────

# Carrega .env se existir (não falha se não existir ainda)
-include .env
export

COMPOSE       := docker compose
COMPOSE_DEV   := $(COMPOSE) -f docker-compose.yml
COMPOSE_PROD  := $(COMPOSE) -f docker-compose.yml -f docker-compose.prod.yml
API_PROJECT   := src/Api/Api.csproj

.DEFAULT_GOAL := help

# ── Help ──────────────────────────────────────────────────────────────────────
.PHONY: help
help:
	@grep -E '^[a-zA-Z_-]+:.*?## .*$$' $(MAKEFILE_LIST) | \
	awk 'BEGIN {FS = ":.*?## "}; {printf "\033[36m%-22s\033[0m %s\n", $$1, $$2}'

# ── Setup ─────────────────────────────────────────────────────────────────────
.PHONY: pull
pull: ## Faz pull fresco das imagens base
	docker pull mcr.microsoft.com/dotnet/sdk:8.0-alpine
	docker pull mcr.microsoft.com/dotnet/aspnet:8.0-alpine
	docker pull mcr.microsoft.com/mssql/server:2022-latest


.PHONY: env
env: ## Copia .env.example → .env (edite o arquivo para alterar os valores)
	@[ -f .env ] && echo ".env já existe. Delete-o primeiro se quiser recriar." || (cp .env.example .env && echo "✔ .env criado a partir do .env.example")

# ── Development (imagem compilada) ───────────────────────────────────────────
.PHONY: up
up: ## Sobe stack dev com imagem compilada (detached)
	$(COMPOSE_DEV) build --pull && $(COMPOSE_DEV) up -d

.PHONY: down
down: ## Para stack dev e remove volume do banco
	$(COMPOSE_DEV) down -v

.PHONY: restart
restart: down up ## Reinicia stack dev

# ── Logs ─────────────────────────────────────────────────────────────────────
.PHONY: logs
logs: ## Stream logs da API
	$(COMPOSE_DEV) logs -f api

.PHONY: logs-all
logs-all: ## Stream logs de todos os serviços (SQL Server filtrado)
	$(COMPOSE_DEV) logs -f 2>&1 | grep -Ev 'spid[0-9]+s |informational message|No user action'

.PHONY: ps
ps: ## Lista containers rodando
	$(COMPOSE_DEV) ps

# ── Production ────────────────────────────────────────────────────────────────
.PHONY: prod-up
prod-up: ## Sobe stack de produção
	$(COMPOSE_PROD) build --pull && $(COMPOSE_PROD) up -d

.PHONY: prod-down
prod-down: ## Para stack de produção
	$(COMPOSE_PROD) down

.PHONY: prod-logs
prod-logs: ## Stream logs de produção
	$(COMPOSE_PROD) logs -f api

# ── Migrations ────────────────────────────────────────────────────────────────
# Em dev: use apenas make down + make up (EnsureCreated recria o schema).
# Para produção: gere UMA migration consolidada com make migration-squash.
.PHONY: migration
migration: ## Cria migration: make migration name=NomeDaMigration
	dotnet ef migrations add $(name) --project $(API_PROJECT) --output-dir Data/Migrations

.PHONY: migration-squash
migration-squash: ## Gera migration única consolidada para produção (apaga as anteriores)
	@echo "Removendo migrations anteriores..."
	rm -f src/Api/Data/Migrations/*.cs
	@echo "Gerando migration consolidada..."
	dotnet ef migrations add InitialSchema --project $(API_PROJECT) --output-dir Data/Migrations
	@echo "Migration 'InitialSchema' criada. Commit e use make prod-up."


.PHONY: migrate
migrate: ## Aplica migrations localmente
	dotnet ef database update --project $(API_PROJECT)

.PHONY: migrate-docker
migrate-docker: ## Aplica migrations dentro do container
	$(COMPOSE_DEV) exec api dotnet ef database update

.PHONY: migration-list
migration-list: ## Lista migrations
	dotnet ef migrations list --project $(API_PROJECT)

.PHONY: migration-rollback
migration-rollback: ## Remove última migration
	dotnet ef migrations remove --project $(API_PROJECT)

# ── Build & Run (local, sem Docker) ──────────────────────────────────────────
.PHONY: build
build: ## Build local
	dotnet build $(API_PROJECT)

.PHONY: run
run: ## Hot reload local (sem Docker)
	dotnet watch run --project $(API_PROJECT)

.PHONY: publish
publish: ## Publica build de release em ./publish
	dotnet publish $(API_PROJECT) -c Release -o ./publish

# ── Testes ───────────────────────────────────────────────────────────────────
.PHONY: test
test: ## Roda testes
	dotnet test

# ── Utilitários ───────────────────────────────────────────────────────────────
.PHONY: shell-api
shell-api: ## Abre shell no container da API
	$(COMPOSE_DEV) exec api sh

.PHONY: shell-db
shell-db: ## Abre sqlcmd no container do SQL Server
	$(COMPOSE_DEV) exec sqlserver /opt/mssql-tools18/bin/sqlcmd \
		-S localhost -U sa -P "$(MSSQL_SA_PASSWORD)" -C

.PHONY: health
health: ## Testa endpoint /health
	curl -s http://localhost:$(APP_PORT)/health | python3 -m json.tool || \
	curl -s http://localhost:$(APP_PORT)/health

.PHONY: clean
clean: ## Remove containers, volumes e artefatos
	$(COMPOSE_DEV) down -v --remove-orphans
		rm -rf ./publish ./logs

.PHONY: prune
prune: clean ## Limpeza pesada: remove também as imagens Docker
	docker image prune -f
