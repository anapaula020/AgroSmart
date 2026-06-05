# .NET API Toolkit

Stack: **.NET 8** · **SQL Server 2022** · **Docker** · **EF Core** · **Identity + JWT** · **Swagger**

## Quick start


```bash
make env
```

```bash
make up
```

Pronto. Sem configuração adicional.

- **Swagger:** http://localhost:5000/swagger  
- **Health:** http://localhost:5000/health  
- **Dashboard:** http://localhost:5000

## Credenciais padrão (dev)

| | |
|---|---|
| Admin | `admin@admin.com` / `Admin@1234!` |
| Banco | `sa` / `Dev@1234!` |

## Autenticação

```
POST /api/v1/auth/login
{ "email": "admin@admin.com", "password": "Admin@1234!" }
```

## Comandos

| Comando | Descrição |
|---|---|
| `make env` | Cria variável de ambiente |
| `make up` | Sobe ambiente dev |
| `make down` | Para ambiente dev |
| `make logs` | Logs da API |
| `make prod-up` | Sobe produção |
| `make shell-db` | sqlcmd no SQL Server |
| `make health` | Testa /health |

## Adicionar entidade nova

1. Crie o model em `Models/Entities.cs`
2. Adicione `DbSet<T>` no `AppDbContext`
3. `make down && make up` (o `EnsureCreated` recria o schema)