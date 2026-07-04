# Tests y Swagger

## Swagger

Se agrego Swagger con `Swashbuckle.AspNetCore`.

Para levantar la API:

```powershell
dotnet run --project GymShop.Api\GymShop.Api.csproj
```

URL:

```text
http://localhost:5093/swagger
```

Si la consola muestra otro puerto en `Now listening on`, usar ese puerto.

### Autenticacion en Swagger

1. Ejecutar `POST /api/auth/login`.
2. Copiar el token devuelto.
3. Click en `Authorize`.
4. Ingresar:

```text
Bearer <token>
```

Ejemplo:

```text
Bearer eyJhbGciOiJIUzI1NiIs...
```

Credenciales seed:

```text
admin@gymshop.com
Admin123!
```

## Tests

Se creo el proyecto:

```text
GymShop.Tests
```

Stack:

- xUnit
- Microsoft.NET.Test.Sdk
- EF Core InMemory

Los tests apuntan a casos de uso de `GymShop.Application`, no a controllers.

## Tests incluidos

Auth:

- Registro crea usuario y devuelve token.
- Registro rechaza email duplicado.
- Login devuelve token con credenciales validas.
- Login rechaza password invalida.

Products:

- Crear producto persiste datos.
- Crear producto rechaza precio invalido.

Orders:

- Crear pedido crea detalle y descuenta stock.
- Crear pedido rechaza stock insuficiente.

## Comandos

Compilar:

```powershell
dotnet build GymShop.slnx --no-restore
```

Correr tests:

```powershell
dotnet test GymShop.Tests\GymShop.Tests.csproj --no-build --verbosity normal
```

Resultado actual:

```text
Pruebas totales: 8
Correcto: 8
0 warnings
0 errores
```

