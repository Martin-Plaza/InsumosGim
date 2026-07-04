# Organizacion Clean Architecture

Se reorganizo el backend en proyectos separados para dejar una base mas mantenible.

## Solucion

```text
GymShop.slnx
├── GymShop.Api
├── GymShop.Application
├── GymShop.Domain
└── GymShop.Infrastructure
```

## Capas

### GymShop.Domain

Contiene el nucleo del negocio.

```text
GymShop.Domain
├── Entities
│   ├── Order.cs
│   ├── OrderItem.cs
│   ├── Product.cs
│   ├── Role.cs
│   └── User.cs
└── Enums
    └── OrderStatus.cs
```

Responsabilidades:

- Entidades del dominio.
- Enums del dominio.
- Reglas propias del modelo cuando se agreguen.

No depende de ningun otro proyecto.

### GymShop.Application

Contiene contratos y modelos de entrada/salida de la aplicacion.

```text
GymShop.Application
├── Abstractions
│   ├── ICurrentUserService.cs
│   ├── IApplicationDbContext.cs
│   ├── IJwtTokenService.cs
│   └── IPasswordHasher.cs
├── Common
│   └── AppResult.cs
└── DTOs
    ├── Auth
    ├── Orders
    ├── Products
    └── Users
└── UseCases
    ├── Auth
    ├── Orders
    ├── Products
    └── Users
```

Responsabilidades:

- DTOs.
- Interfaces/abstracciones.
- Casos de uso de autenticacion, productos, pedidos y usuarios.
- Resultados de aplicacion independientes de HTTP.

Depende solo de `GymShop.Domain`.

### GymShop.Infrastructure

Contiene detalles tecnicos externos al dominio.

```text
GymShop.Infrastructure
├── Data
│   ├── DatabaseInitializer.cs
│   └── GymShopDbContext.cs
├── Services
│   ├── JwtTokenService.cs
│   └── PasswordHasher.cs
└── DependencyInjection.cs
```

Responsabilidades:

- Entity Framework Core.
- SQL Server.
- Inicializacion/seed de base de datos.
- Implementacion de hashing de password.
- Implementacion de JWT.
- Registro de dependencias de infraestructura.

Depende de:

- `GymShop.Application`
- `GymShop.Domain`

### GymShop.Api

Contiene el borde HTTP.

```text
GymShop.Api
├── Controllers
├── Services
│   └── CurrentUserService.cs
├── Program.cs
└── appsettings.json
```

Responsabilidades:

- Controllers delgados.
- Configuracion HTTP.
- CORS.
- Autenticacion JWT Bearer.
- Composition root.
- Servicio de usuario actual basado en `HttpContext`.

Depende de:

- `GymShop.Application`
- `GymShop.Infrastructure`

## Direccion de dependencias

```text
GymShop.Api ───────────────┐
                           ▼
                    GymShop.Application ──► GymShop.Domain
                           ▲
                           │
GymShop.Infrastructure ─────┘
```

Regla principal:

- `Domain` no conoce a nadie.
- `Application` contiene los casos de uso y conoce solo a `Domain`.
- `Infrastructure` implementa detalles definidos por `Application`.
- `Api` expone HTTP, registra dependencias y traduce resultados de aplicacion a respuestas HTTP.

## Estado actual

Los controllers ya no contienen consultas ni logica de negocio. Delegan en casos de uso concretos de `GymShop.Application`.

```text
AuthController
├── IRegisterUserUseCase
├── ILoginUserUseCase
└── IGetCurrentUserUseCase

ProductsController
├── IGetProductsUseCase
├── IGetProductByIdUseCase
├── ICreateProductUseCase
├── IUpdateProductUseCase
├── IUpdateProductStockUseCase
└── IUpdateProductStatusUseCase

OrdersController
├── ICreateOrderUseCase
├── IGetMyOrdersUseCase
├── IGetOrderByIdUseCase
├── IGetOrdersUseCase
└── IUpdateOrderStatusUseCase

UsersController
├── IGetUsersUseCase
├── ICreateUserUseCase
├── IUpdateUserRoleUseCase
└── IUpdateUserStatusUseCase
```

La solucion compila correctamente:

```text
0 warnings
0 errores
```

Comando usado:

```powershell
dotnet build GymShop.slnx --no-restore
```

## Siguiente mejora recomendada

La logica ya esta fuera de los controllers y organizada en casos de uso por accion. El siguiente refinamiento posible seria reemplazar el uso de `DbSet` en `IApplicationDbContext` por repositorios o puertos mas especificos si se quiere reducir aun mas la exposicion de Entity Framework dentro de Application.
