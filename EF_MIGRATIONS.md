# EF Core Migrations

Se reemplazo `EnsureCreatedAsync()` por `MigrateAsync()` y se creo la migracion inicial del modelo SQL Server.

## Archivos generados

```text
GymShop.Infrastructure/Data/Migrations/20260703144057_InitialCreate.cs
GymShop.Infrastructure/Data/Migrations/20260703144057_InitialCreate.Designer.cs
GymShop.Infrastructure/Data/Migrations/GymShopDbContextModelSnapshot.cs
GymShop.Infrastructure/Data/Migrations/InitialCreate.sql
```

## Migracion inicial

La migracion crea:

- `Roles`
- `Users`
- `Products`
- `Orders`
- `OrderItems`
- `__EFMigrationsHistory`

Tambien incluye:

- Seed formal de roles:
  - `User`
  - `Admin`
  - `SuperAdmin`
- Indice unico en `Roles.Name`.
- Indice unico en `Users.Email`.
- Foreign keys entre usuarios, roles, pedidos, items y productos.
- Constraints:
  - `Products.Price > 0`
  - `Products.Stock >= 0`
  - `OrderItems.Quantity > 0`

## Comandos

Crear una migracion:

```powershell
dotnet ef migrations add InitialCreate `
  --project GymShop.Infrastructure\GymShop.Infrastructure.csproj `
  --startup-project GymShop.Api\GymShop.Api.csproj `
  --context GymShopDbContext `
  --output-dir Data\Migrations
```

Aplicar migraciones:

```powershell
dotnet ef database update `
  --project GymShop.Infrastructure\GymShop.Infrastructure.csproj `
  --startup-project GymShop.Api\GymShop.Api.csproj `
  --context GymShopDbContext
```

Generar script SQL:

```powershell
dotnet ef migrations script `
  --project GymShop.Infrastructure\GymShop.Infrastructure.csproj `
  --startup-project GymShop.Api\GymShop.Api.csproj `
  --context GymShopDbContext `
  --output GymShop.Infrastructure\Data\Migrations\InitialCreate.sql
```

## Estado de SQL Server en esta maquina

Connection string actual:

```json
"DefaultConnection": "Server=(localdb)\\MSSQLLocalDB;Database=GymShopDb;Trusted_Connection=True;TrustServerCertificate=True"
```

Verificaciones realizadas:

```powershell
sqllocaldb info
sqllocaldb info MSSQLLocalDB
sqllocaldb start MSSQLLocalDB
sqllocaldb create GymShopLocalDb
Test-NetConnection -ComputerName localhost -Port 1433
sqlcmd -S "(localdb)\MSSQLLocalDB" -Q "SELECT @@VERSION"
```

Resultado:

- `dotnet ef` esta instalado.
- SQLCMD esta instalado.
- LocalDB 17.0 esta instalado.
- No hay SQL Server escuchando en `localhost:1433`.
- `MSSQLLocalDB` no puede crear/iniciar instancia automatica.
- Crear una instancia LocalDB nueva tambien falla.

Error principal:

```text
Cannot create an automatic instance. See the Windows Application event log for error details.
```

Por este motivo, la migracion inicial fue creada correctamente, pero no se pudo aplicar a una base real en esta maquina.

## Que falta para probar endpoints contra DB real

Hace falta una instancia SQL Server accesible. Opciones:

1. Reparar o reinstalar SQL Server LocalDB.
2. Instalar SQL Server Express o Developer Edition.
3. Usar una instancia SQL Server existente y actualizar `DefaultConnection`.

Cuando SQL Server este disponible:

```powershell
dotnet ef database update `
  --project GymShop.Infrastructure\GymShop.Infrastructure.csproj `
  --startup-project GymShop.Api\GymShop.Api.csproj `
  --context GymShopDbContext

dotnet run --project GymShop.Api\GymShop.Api.csproj
```

Luego probar:

```powershell
Invoke-RestMethod http://localhost:5093/api/products
```

