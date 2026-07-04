# Implementacion backend .NET

Se creo el backend propuesto en:

```text
GymShop.Api
```

Solucion:

```text
GymShop.slnx
```

La solucion fue reorganizada en estructura Clean Architecture:

```text
GymShop.Api
GymShop.Application
GymShop.Domain
GymShop.Infrastructure
```

Detalle de la organizacion:

```text
CLEAN_ARCHITECTURE.md
```

## Stack

- .NET 10 Web API.
- Entity Framework Core.
- SQL Server.
- JWT Bearer Authentication.
- Password hashing con PBKDF2 nativo de .NET.
- CORS habilitado para Vite en `localhost:5173` y `127.0.0.1:5173`.

## Como ejecutar

Desde `C:\Users\HP\Documents\E-Commerce gimnasio`:

```powershell
dotnet restore GymShop.slnx
dotnet run --project GymShop.Api\GymShop.Api.csproj
```

URL local:

```text
http://localhost:5093
```

Connection string actual:

```json
"DefaultConnection": "Server=(localdb)\\MSSQLLocalDB;Database=GymShopDb;Trusted_Connection=True;TrustServerCertificate=True"
```

En ambiente `Development`, la API ejecuta `EnsureCreatedAsync()` y crea la base si no existe.

## Modelo implementado

Entidades:

- `Role`
- `User`
- `Product`
- `Order`
- `OrderItem`

Roles sembrados:

- `User`
- `Admin`
- `SuperAdmin`

Usuario super-admin de desarrollo:

```text
Email: admin@gymshop.com
Password: Admin123!
```

Productos demo sembrados si la tabla esta vacia:

- `Mancuerna 10kg`
- `Colchoneta fitness`

## Endpoints implementados

Auth:

```text
POST /api/auth/register
POST /api/auth/login
GET  /api/auth/me
```

Products:

```text
GET   /api/products
GET   /api/products/{id}
POST  /api/products
PUT   /api/products/{id}
PATCH /api/products/{id}/stock
PATCH /api/products/{id}/status
```

Orders:

```text
POST  /api/orders
GET   /api/orders/my
GET   /api/orders/{id}
GET   /api/orders
PATCH /api/orders/{id}/status
```

Users:

```text
GET   /api/users
POST  /api/users
PATCH /api/users/{id}/role
PATCH /api/users/{id}/status
```

## Verificacion realizada

```powershell
dotnet restore GymShop.slnx
dotnet build GymShop.slnx --no-restore
dotnet run --no-build --project GymShop.Api\GymShop.Api.csproj
```

Resultado:

- Restore exitoso.
- Build exitoso, 0 warnings, 0 errores.
- Arranque probado; el proceso quedo activo hasta el timeout de verificacion.

## EF Core Migrations

Se reemplazo `EnsureCreatedAsync()` por `MigrateAsync()` y se creo la migracion inicial:

```text
GymShop.Infrastructure/Data/Migrations/20260703144057_InitialCreate.cs
```

Tambien se genero el script SQL:

```text
GymShop.Infrastructure/Data/Migrations/InitialCreate.sql
```

Detalle:

```text
EF_MIGRATIONS.md
```

## Tests y Swagger

Se agrego Swagger para probar endpoints desde navegador:

```text
http://localhost:5093/swagger
```

Se agrego el proyecto de tests:

```text
GymShop.Tests
```

Detalle:

```text
TESTS_Y_SWAGGER.md
```

## Proximos pasos recomendados

1. Definir si se usara LocalDB, SQL Server Express o una instancia remota.
2. Cambiar credenciales y secreto JWT antes de cualquier ambiente compartido.
3. Conectar el frontend actual contra `/api`.
4. Migrar el frontend a TypeScript consumiendo estos contratos.
5. Agregar tests de servicios/controladores.
