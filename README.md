# GymShop API

## Del problema a los casos de uso

### 1. Que problema existe?

Una tienda de productos de gimnasio necesita vender online, mantener el catalogo y el stock actualizados, recibir pedidos, cobrar pagos de forma segura y evitar errores criticos como vender productos sin stock, duplicar pagos o modificar precios de pedidos ya creados.

El objetivo no es simplemente tener endpoints CRUD. El sistema debe coordinar compra, stock, ordenes, pagos, autorizacion y notificaciones externas.

### 2. Quien participa?

Actores principales:

- Cliente: navega productos, arma carrito, compra, paga y consulta sus pedidos.
- Administrador: gestiona productos, stock, pedidos y estados operativos.
- SuperAdmin: administra usuarios, roles y permisos sensibles.
- Pasarela de pago: Mercado Pago confirma o rechaza pagos mediante preferencias y webhooks.
- Sistema de stock: descuenta, restaura y protege unidades disponibles.
- API backend: orquesta reglas de negocio, persistencia, seguridad e integraciones.

Actores posibles para evolucion futura:

- Servicio de correo para confirmaciones y avisos.
- Operador logistico para seguimiento de envios.
- Proveedor o marca asociada para catalogo multi-vendedor.

### 3. Que quiere conseguir cada actor?

Cliente:

- descubrir productos disponibles
- conocer precio y stock
- agregar productos al carrito
- confirmar una compra
- pagar con Mercado Pago
- consultar el estado de sus pedidos
- cancelar pedidos pendientes

Administrador:

- crear y actualizar productos
- modificar precios y stock
- activar o desactivar productos
- consultar todas las ordenes
- cancelar o actualizar pedidos segun reglas permitidas
- controlar que las ventas no rompan stock

SuperAdmin:

- crear usuarios administrativos
- asignar roles
- activar o desactivar usuarios
- evitar operaciones peligrosas, como autodesactivarse

Pasarela de pago:

- recibir una preferencia de pago
- informar pagos aprobados, rechazados, cancelados o expirados
- reenviar notificaciones sin duplicar efectos

### 4. Que entra al sistema?

Entradas internas:

- registro o login de usuario
- alta o modificacion de producto
- cambio de precio o stock
- producto agregado al carrito
- solicitud de checkout
- cancelacion de pedido
- cambio administrativo de estado

Entradas externas:

- respuesta de Mercado Pago al crear una preferencia
- webhook de Mercado Pago con `data.id`
- firma HMAC de Mercado Pago para validar autenticidad
- notificaciones repetidas del proveedor de pagos

Una entrada no siempre es un formulario. Tambien puede ser un evento externo que llega desde otro sistema.

### 5. Que decisiones debe tomar el sistema?

Cuando un cliente intenta comprar, el backend debe decidir:

- el usuario esta autenticado?
- el producto existe?
- el producto esta activo?
- la cantidad es valida?
- hay stock suficiente?
- el usuario ya tiene una orden pendiente?
- que precio debe quedar congelado en el pedido?
- se puede descontar stock sin condiciones de carrera?
- corresponde crear una orden o rechazar la operacion?

Cuando llega una notificacion de pago, debe decidir:

- la firma del webhook es valida?
- el pago existe en el proveedor?
- la referencia externa corresponde a una orden local?
- el monto y la moneda coinciden?
- la orden sigue pendiente?
- la notificacion ya fue procesada?
- el pago aprueba, rechaza, cancela o expira la orden?

Estas decisiones son el centro del dominio. No son detalles de controlador.

### 6. Que proceso ocurre?

Flujo principal de compra:

```text
Cliente agrega productos al carrito
        v
Solicita checkout
        v
Validar usuario
        v
Validar productos activos
        v
Validar cantidades y stock
        v
Capturar precio actual
        v
Crear pedido e items
        v
Descontar stock
        v
Limpiar carrito
        v
Guardar todo en una operacion atomica
        v
Crear preferencia de pago
        v
Esperar webhook de Mercado Pago
        v
Validar autenticidad
        v
Consultar pago al proveedor
        v
Pago aprobado?
   /              \\
 Si                No
 v                 v
Marcar orden       Cancelar orden
como pagada        y restaurar stock
```

### 7. Que sale del sistema?

Salidas del sistema:

- token JWT para sesiones validas
- productos disponibles para compra
- carrito actualizado
- pedido creado
- stock descontado o restaurado
- URL de pago de Mercado Pago
- pago registrado
- orden marcada como pagada
- orden cancelada
- errores de negocio claros
- respuestas ProblemDetails ante excepciones
- resultados verificables por tests automatizados

La pregunta clave es: despues de procesar una entrada, que cambio en el mundo?

### 8. Que reglas nunca deben romperse?

Reglas de negocio e invariantes:

- no se puede comprar un producto inexistente o inactivo
- no se puede comprar una cantidad igual o menor a cero
- no se puede vender mas stock del disponible
- el precio del pedido debe conservarse aunque cambie el catalogo despues
- el checkout debe crear pedido, items, descuento de stock y limpieza de carrito de forma atomica
- un usuario no puede tener mas de una orden pendiente
- un usuario solo puede consultar sus propios pedidos
- un administrador puede consultar y gestionar ordenes
- un producto con stock concurrente debe protegerse con `RowVersion`
- una confirmacion duplicada de pago no debe duplicar efectos
- una clave de idempotencia no debe generar pagos duplicados
- un pago aprobado cambia la orden a `Paid`
- un pago rechazado, cancelado o expirado cancela la orden y restaura stock
- un pedido pagado no puede volver a pendiente arbitrariamente
- un usuario comun no puede crear productos
- solo SuperAdmin puede administrar usuarios
- un usuario no puede desactivarse a si mismo
- las credenciales sensibles no deben vivir en `appsettings.json`

Estas reglas justifican decisiones tecnicas como transacciones, idempotencia, concurrencia, autorizacion por roles, migraciones, tests y manejo centralizado de errores.

## Casos de uso

### Autenticacion y usuarios

- RegistrarUsuario
- IniciarSesion
- ObtenerUsuarioActual
- CrearUsuarioAdministrativo
- ListarUsuarios
- CambiarRolUsuario
- CambiarEstadoUsuario

### Productos

- ListarProductos
- ObtenerProductoPorId
- CrearProducto
- ActualizarProducto
- ActualizarStock
- ActivarODesactivarProducto

### Carrito

- ObtenerCarrito
- AgregarItemAlCarrito
- ActualizarItemDelCarrito
- QuitarItemDelCarrito
- VaciarCarrito
- ConfirmarCheckout

### Ordenes

- CrearOrdenDesdeCarrito
- ConsultarMisOrdenes
- ConsultarOrdenPorId
- ConsultarTodasLasOrdenes
- FiltrarOrdenesPorEmail
- CancelarOrden
- ExpirarOrdenesPendientes
- ActualizarEstadoDeOrden

### Pagos

- CrearPagoParaOrdenPendienteActual
- CrearPreferenciaMercadoPago
- ConsultarPago
- ConsultarPagosDeOrden
- ProcesarWebhookMercadoPago
- ValidarFirmaWebhook
- AplicarEstadoDePago
- ReutilizarPagoPorIdempotencia

### Infraestructura y calidad

- AplicarMigraciones
- InicializarDatosBase
- EjecutarTestsAutomatizados
- EjecutarBuildYTestsEnCI
Backend para un e-commerce de productos de gimnasio construido con .NET, ASP.NET Core, Entity Framework Core y SQL Server. El proyecto aplica una arquitectura por capas con separacion entre API, Application, Domain, Infrastructure y Tests.

## Caracteristicas principales

- Autenticacion con JWT.
- Roles: User, Admin y SuperAdmin.
- Gestion de usuarios para SuperAdmin.
- Catalogo de productos con control de stock.
- Carrito por usuario.
- Checkout de carrito con creacion atomica de ordenes.
- Ordenes con historial, cancelacion y administracion.
- Pagos con proveedor Mock y Mercado Pago Checkout Pro.
- Webhook de Mercado Pago con validacion de firma HMAC.
- Idempotencia en pagos mediante `IdempotencyKey` e indice unico filtrado.
- Concurrencia de stock con `RowVersion` en productos.
- Manejo centralizado de errores con ProblemDetails.
- Migraciones EF Core.
- Suite de tests automatizados.
- CI con GitHub Actions para build y test.

## Arquitectura

```text
GymShop.Api             HTTP API, controllers, auth, Swagger, middleware
GymShop.Application     Use cases, DTOs, contratos, reglas de aplicacion
GymShop.Domain          Entidades y enums del dominio
GymShop.Infrastructure  EF Core, repositorios, servicios externos, migraciones
GymShop.Tests           Tests de use cases, pagos, ordenes y autorizacion
```

La API no accede directamente a la persistencia. La logica se concentra en casos de uso de Application, con EF Core y servicios externos implementados en Infrastructure.

## Flujo de compra

1. El usuario agrega productos al carrito con `POST /api/cart/items`.
2. Ejecuta checkout con `POST /api/cart/checkout` enviando direccion de envio.
3. El backend valida productos, stock y orden pendiente.
4. Se crea la orden, sus items, se descuentan stocks y se limpia el carrito en una unica operacion atomica.
5. El usuario crea el pago con `POST /api/payments/current`.
6. Si usa Mercado Pago, recibe una URL de checkout.
7. Mercado Pago notifica al webhook.
8. El backend valida autenticidad, consulta el pago al proveedor y actualiza la orden.

## Mercado Pago

La integracion cubre:

- Creacion de preferencias de Checkout Pro.
- Asociacion local entre pago y orden mediante `OrderId` y `ExternalReference`.
- Recepcion de webhook en `POST /api/payments/mercadopago/webhook`.
- Validacion HMAC con `x-signature`, `x-request-id` y `MercadoPago:WebhookSecret`.
- Idempotencia con `IdempotencyKey`.
- Actualizacion de orden a `Paid` cuando el pago queda aprobado.
- Cancelacion y restauracion de stock cuando el pago se rechaza, cancela o expira.
- Manejo seguro de notificaciones repetidas.

## Endpoints principales

### Auth

- `POST /api/auth/register`
- `POST /api/auth/login`
- `GET /api/auth/me`

### Products

- `GET /api/products`
- `GET /api/products/{id}`
- `POST /api/products` - Admin, SuperAdmin
- `PUT /api/products/{id}` - Admin, SuperAdmin
- `PATCH /api/products/{id}/stock` - Admin, SuperAdmin
- `PATCH /api/products/{id}/status` - Admin, SuperAdmin

### Cart

- `GET /api/cart`
- `POST /api/cart/items`
- `PUT /api/cart/items/{productId}`
- `DELETE /api/cart/items/{productId}`
- `DELETE /api/cart`
- `POST /api/cart/checkout`

### Orders

- `GET /api/orders/my`
- `GET /api/orders/{id}`
- `GET /api/orders?userEmail=` - Admin, SuperAdmin
- `PATCH /api/orders/{id}/status` - Admin, SuperAdmin
- `POST /api/orders/{id}/cancel`
- `POST /api/orders/expire-pending` - Admin, SuperAdmin

### Payments

- `POST /api/payments/current`
- `GET /api/payments/{id}`
- `GET /api/payments/orders/{orderId}`
- `POST /api/payments/{id}/status` - Admin, SuperAdmin
- `POST /api/payments/mercadopago/webhook`

## Requisitos

- .NET 10 SDK
- SQL Server LocalDB o SQL Server
- Cuenta de Mercado Pago Developers para probar Checkout Pro

## Configuracion local

El archivo `appsettings.json` no contiene credenciales reales. Configura secretos localmente con User Secrets:

```powershell
dotnet user-secrets set "Jwt:Secret" "TU_SECRET_LARGO" --project "GymShop.Api/GymShop.Api.csproj"
dotnet user-secrets set "SeedSuperAdmin:Password" "TU_PASSWORD_ADMIN" --project "GymShop.Api/GymShop.Api.csproj"
dotnet user-secrets set "MercadoPago:AccessToken" "TU_ACCESS_TOKEN" --project "GymShop.Api/GymShop.Api.csproj"
dotnet user-secrets set "MercadoPago:WebhookSecret" "TU_WEBHOOK_SECRET" --project "GymShop.Api/GymShop.Api.csproj"
```

Para webhooks reales en desarrollo local, usa una URL publica como ngrok y configura:

```powershell
dotnet user-secrets set "MercadoPago:NotificationUrl" "https://TU-DOMINIO/api/payments/mercadopago/webhook" --project "GymShop.Api/GymShop.Api.csproj"
```

## Ejecutar el proyecto

```powershell
dotnet restore GymShop.slnx
dotnet run --project GymShop.Api/GymShop.Api.csproj
```

Swagger queda disponible en:

```text
http://localhost:5093/swagger
```

La aplicacion aplica migraciones automaticamente en ambiente Development.

## Tests

```powershell
dotnet test GymShop.slnx
```

La suite cubre, entre otros puntos:

- Registro, login y duplicados por email case-insensitive.
- Productos y validaciones.
- Carrito y checkout atomico.
- Congelamiento de precio en ordenes.
- Descuento de stock para uno o varios productos.
- Permisos de consulta de ordenes.
- Transiciones invalidas de estado.
- Pagos, idempotencia y estados.
- Webhooks duplicados.
- Firma invalida de webhook.
- Restricciones de autorizacion por roles.

## CI

El repositorio incluye GitHub Actions en `.github/workflows/ci.yml`.

En cada push o pull request hacia `main`, `master` o `develop`, ejecuta:

```powershell
dotnet restore GymShop.slnx
dotnet build GymShop.slnx --configuration Release --no-restore
dotnet test GymShop.slnx --configuration Release --no-build --verbosity normal
```

## Notas de seguridad

- No commitear tokens, passwords ni secretos.
- Usar User Secrets en desarrollo.
- Usar variables de entorno o secret manager en produccion.
- Configurar `MercadoPago:WebhookSecret` en ambientes reales.
- Mantener `Jwt:Secret` fuera de `appsettings.json`.
