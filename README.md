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
- Invalidacion inmediata de JWT al desactivar usuarios o cambiar roles.
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

Mercado Pago esta deshabilitado por defecto mediante `MercadoPago:Enabled=false`. En ese estado solo queda disponible el proveedor `Mock`, que no requiere tokens ni secretos de Mercado Pago, y el webhook de Mercado Pago responde 404 sin procesar la notificacion.

La integracion cubre:

- Creacion de preferencias de Checkout Pro.
- Asociacion local entre pago y orden mediante `OrderId` y `ExternalReference`.
- Recepcion de webhook en `POST /api/payments/mercadopago/webhook`.
- Validacion HMAC con `x-signature`, `x-request-id` y `MercadoPago:WebhookSecret`.
- Idempotencia con `IdempotencyKey`.
- Actualizacion de orden a `Paid` cuando el pago queda aprobado.
- Cancelacion y restauracion de stock cuando el pago se rechaza, cancela o expira.
- Manejo seguro de notificaciones repetidas.

## Cancelaciones y reembolsos

GymShop distingue una cancelacion previa a completar la venta de un reembolso confirmado por el proveedor:

| Estado inicial | Evento | Payment final | Order final | Stock |
|---|---|---|---|---|
| `Pending` | Cancelacion del pedido | pagos pendientes `Canceled` | `Canceled` | se restaura una vez |
| `Pending` | pago `Rejected`, `Canceled` o `Expired` | estado informado | `Canceled` | se restaura una vez |
| `Pending` | pago `Approved` | `Approved` | `Paid` | no cambia |
| `Paid` | despacho | `Approved` | `Shipped` | no cambia |
| `Paid` | refund total confirmado | `Refunded` | `Refunded` | se restaura una vez |
| `Shipped` | refund total confirmado | `Refunded` | `Refunded` | no se restaura automaticamente |

`Paid -> Canceled` esta prohibido en el cambio administrativo generico. Un pago aprobado solo pasa a `Refunded` cuando el webhook consulta Mercado Pago y el proveedor confirma el reembolso. GymShop no inicia refunds ni llama a la API de refunds en esta fase; deben iniciarse desde Mercado Pago.

Los webhooks `refunded` repetidos son idempotentes. Si el pedido todavia estaba `Paid`, solo la primera transicion restaura stock. Si ya estaba `Shipped`, la devolucion fisica y el stock quedan pendientes de gestion manual y el motivo se registra en `Payment.FailureReason`.

Los reembolsos parciales no se automatizan: el pago conserva `Approved`, la orden conserva `Paid` o `Shipped`, no se modifica stock y `FailureReason` indica que el caso requiere gestion manual.

Las transiciones incompatibles con el estado actual responden `409 Conflict`. Los estados o formatos desconocidos continúan respondiendo errores de validacion.

## Concurrencia al crear pagos

La creacion de pagos reserva primero un registro local con estado `Creating`. Esa reserva se guarda antes de llamar al gateway y el indice SQL Server `UX_Payments_OrderId_Active` permite solamente un pago `Creating` o `Pending` por orden. Los intentos `CreationFailed`, `Rejected`, `Canceled`, `Expired` y `Refunded` permanecen como historial y no bloquean un nuevo intento.

`IdempotencyKey` puede ser enviada por el cliente. Si se omite, el servidor genera una clave con prefijo `server-`; `PaymentResponse` siempre expone la clave efectiva. Repetir una clave reutiliza el mismo intento. Para reintentar un `CreationFailed` se debe usar una clave nueva.

Si otro request encuentra el pago ganador todavia en `Creating`, `POST /api/payments/current` responde `202 Accepted`, incluye el Payment con `CheckoutUrl=null` y una cabecera `Location` hacia `GET /api/payments/{id}`. Cuando el gateway termina correctamente, el estado pasa a `Pending` y queda disponible la URL de checkout.

Si el gateway falla, la reserva pasa a `CreationFailed`, conserva un motivo seguro y libera el indice activo para un nuevo intento. No se mantiene una transaccion SQL abierta durante la llamada externa y no se utilizan locks en memoria.

Un `Creating` sin actividad durante `Payments:CreatingTimeoutSeconds` puede ser retomado por el siguiente request. El valor predeterminado es 300 segundos. La toma se hace con una actualizacion SQL condicional y el recuperador reutiliza la misma `IdempotencyKey`; no existe un worker en segundo plano ni Outbox en esta fase.

La migracion `EnforceSingleActivePaymentPerOrder` se detiene si detecta mas de un pago activo existente para una orden. No modifica estados financieros automaticamente: los duplicados deben revisarse manualmente antes de reintentar el despliegue.

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

Los archivos `appsettings*.json` no deben contener credenciales reales. El valor de `Jwt:Secret` que aparece alli es solo un placeholder deliberadamente invalido: la aplicacion lo rechaza al iniciar.

`Jwt:Secret` es obligatorio en todos los ambientes y debe tener al menos 32 caracteres. Para Development, configura una clave generada con buena entropia mediante User Secrets (los valores siguientes son nombres descriptivos, no secretos reales):

```powershell
dotnet user-secrets set "Jwt:Secret" "<CLAVE-ALEATORIA-DE-32-O-MAS-CARACTERES>" --project "GymShop.Api/GymShop.Api.csproj"
dotnet user-secrets set "SeedSuperAdmin:Password" "<PASSWORD-LOCAL>" --project "GymShop.Api/GymShop.Api.csproj"
```

Si solo se usa el proveedor `Mock`, no hay que configurar nada de Mercado Pago. Para habilitar Mercado Pago en Development se requiere `Enabled=true` y `AccessToken`:

```powershell
dotnet user-secrets set "MercadoPago:Enabled" "true" --project "GymShop.Api/GymShop.Api.csproj"
dotnet user-secrets set "MercadoPago:AccessToken" "<ACCESS-TOKEN-DE-DESARROLLO>" --project "GymShop.Api/GymShop.Api.csproj"
dotnet user-secrets set "MercadoPago:WebhookSecret" "<WEBHOOK-SECRET-DE-DESARROLLO>" --project "GymShop.Api/GymShop.Api.csproj"
```

Development permite omitir `MercadoPago:WebhookSecret` para pruebas locales relajadas. Al hacerlo, la aplicacion emite un warning seguro al iniciar y acepta webhooks sin HMAC. No se debe usar esa modalidad con notificaciones reales. Para webhooks reales en desarrollo local, configura el secreto y una URL publica como ngrok:

```powershell
dotnet user-secrets set "MercadoPago:NotificationUrl" "https://<DOMINIO-PUBLICO>/api/payments/mercadopago/webhook" --project "GymShop.Api/GymShop.Api.csproj"
```

En Production usa variables de entorno o un secret manager. `Jwt__Secret` siempre es obligatorio. Si `MercadoPago__Enabled=true`, tambien son obligatorios `MercadoPago__AccessToken` y `MercadoPago__WebhookSecret`; la aplicacion falla al iniciar si falta alguno y nunca acepta silenciosamente un webhook de produccion sin HMAC. Por ejemplo, configura las claves en la plataforma de despliegue, sin escribir valores reales en archivos versionados:

```text
Jwt__Secret=<SECRET-GESTIONADO-DE-32-O-MAS-CARACTERES>
MercadoPago__Enabled=true
MercadoPago__AccessToken=<SECRET-GESTIONADO>
MercadoPago__WebhookSecret=<SECRET-GESTIONADO>
```

Si falta una configuracion obligatoria, si `Jwt:Secret` conserva el placeholder o si la clave JWT tiene menos de 32 caracteres, el arranque falla con un error de validacion que identifica la clave pero nunca imprime su valor.

El tiempo para considerar abandonada una reserva de pago puede configurarse sin secretos:

```text
Payments__CreatingTimeoutSeconds=300
```

Debe ser un entero mayor que cero. Un valor menor recupera antes los procesos interrumpidos, pero aumenta el riesgo de solaparse con una llamada externa excepcionalmente lenta.

## Sesiones JWT y cambios de rol

Cada JWT incluye el claim privado `token_version`, además de los claims existentes de identidad y rol. En cada request autenticado, la API consulta el usuario actual y exige que:

- el usuario exista y este activo;
- `token_version` coincida con `Users.TokenVersion`;
- el rol del token coincida con el rol persistido.

Cambiar el rol o estado de un usuario incrementa `TokenVersion` cuando el valor realmente cambia. Por eso los tokens emitidos anteriormente reciben `401 Unauthorized` inmediatamente. Un token vigente cuyo rol no alcanza para un endpoint recibe `403 Forbidden`.

La migracion `AddUserTokenVersion` agrega la columna con valor inicial `0`. Al desplegar esta version, todos los JWT emitidos por versiones anteriores —que no contienen `token_version`— quedan invalidados y los usuarios deben iniciar sesion otra vez. La migracion debe aplicarse antes o junto con el backend actualizado.

Esta implementacion realiza una consulta indexada por usuario en cada request autenticado para priorizar invalidacion inmediata y coherencia entre instancias. No utiliza cache de sesiones, refresh tokens ni cambia la duracion configurada de los JWT.

## Validacion de entradas y visibilidad del catalogo

ASP.NET Core valida los contratos antes de ejecutar el caso de uso. Las entradas invalidas responden `400 Bad Request` con `ValidationProblemDetails`; las validaciones de Application repiten las reglas críticas antes de persistir para proteger también llamadas que no provengan de HTTP.

Limites principales:

- email requerido, formato valido y maximo 256 caracteres;
- nombre de usuario requerido y maximo 100;
- password de 8 a 128 caracteres, con al menos una letra y un numero;
- nombre de producto maximo 150 y descripcion maxima 1000;
- precio mayor a cero, hasta 16 digitos enteros y 2 decimales; stock no negativo;
- direccion de envio requerida y maxima 300;
- `IdempotencyKey` maxima 100 y motivos de cancelacion/fallo maximos 500;
- proveedores y estados deben corresponder a proveedores configurados y valores definidos por el dominio.

`ImageUrl` puede omitirse, usar una URL absoluta `http/https` o una ruta web local que comience con `/`, por ejemplo `/images/productos/mancuerna.jpg`. No se aceptan rutas fisicas, `file://`, rutas con `..` ni URLs relativas a otro host mediante `//`. La longitud maxima es 500.

El catalogo publico y los usuarios comunes solo ven productos activos. Solicitar `GET /api/products?includeInactive=true` exige Admin o SuperAdmin: un cliente anonimo recibe 401 y un usuario sin ese rol recibe 403. `GET /api/products/{id}` devuelve 404 para un producto inactivo cuando lo consulta publico/User, igual que para un ID inexistente; Admin y SuperAdmin pueden consultarlo.

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
- Invalidacion por usuario desactivado, cambio de rol, version obsoleta o usuario inexistente.
- Emision del `token_version` actual y respuestas 401/403 de autenticacion y autorizacion.
- Cancelaciones idempotentes con cierre de pagos pendientes y restitucion unica de stock.
- Refund total antes y despues del envio, webhooks repetidos y reembolsos parciales manuales.
- Reservas `Creating`, recuperacion de intentos atascados e historial `CreationFailed`.
- Carreras de pago e indices filtrados verificados sobre SQL Server real.

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
- Mantener `MercadoPago:Enabled=false` cuando se usa el proveedor Mock.
- Configurar `MercadoPago:WebhookSecret` siempre que se procesen notificaciones reales; es obligatorio en Production.
- Mantener `Jwt:Secret` fuera de `appsettings.json`.
