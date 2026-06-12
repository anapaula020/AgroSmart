# AGROSMART

Sistema de gestão agrícola com suporte a propriedades rurais, safras, estoque, clima, alertas e workspaces colaborativos.

Stack: **.NET 8** · **SQL Server 2022** · **Docker** · **EF Core** · **ASP.NET Identity + JWT** · **Swagger**

---

## Usuários de demonstração

Os usuários abaixo são criados automaticamente ao subir o ambiente com `make up`:

| Email | Senha | Role |
|---|---|---|
| `admin@admin.com` | `Admin@1234!` | Admin |
| `agronomo@demo.com` | `Demo@1234!` | Agronomo |
| `tecnico@demo.com` | `Demo@1234!` | Tecnico |
| `produtor@demo.com` | `Demo@1234!` | Produtor |

---

## Executar com Docker

**Requisitos:** Docker e Docker Compose instalados.

```bash
# 1. Crie o arquivo de variáveis de ambiente
make env

# 2. Suba os containers (API + SQL Server)
make up
```

Pronto. O banco é criado e populado com dados de demonstração automaticamente.

| URL | Descrição |
|---|---|
| http://localhost:5000 | Dashboard |
| http://localhost:5000/swagger | Documentação da API |
| http://localhost:5000/health | Health check |

---

## Autenticação via API

```bash
POST /api/v1/auth/login
Content-Type: application/json

{ "email": "admin@admin.com", "password": "Admin@1234!" }
```

O token JWT retornado deve ser enviado no header `Authorization: Bearer <token>`.  
Para autenticação via API Key, use o header `X-Api-Key: <key>` (gerenciado na seção Workspaces).

---

## Comandos disponíveis

| Comando | Descrição |
|---|---|
| `make env` | Cria `.env` a partir do `.env.example` |
| `make up` | Sobe stack dev (build + detached) |
| `make down` | Para e remove containers |
| `make logs` | Exibe logs da API em tempo real |
| `make prod-up` | Sobe stack de produção |
| `make shell-db` | Abre `sqlcmd` no SQL Server |
| `make health` | Checa endpoint `/health` |

---

## Adicionar entidade nova

1. Crie o model em `Models/`
2. Adicione `DbSet<T>` no `AppDbContext`
3. `make down && make up` — o `EnsureCreated` recria o schema
