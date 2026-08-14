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
GymShop.Infrastructure  EF Core, servicios externos, transacciones y migraciones
GymShop.Tests           Tests de use cases, pagos, ordenes y autorizacion
GymShop.Web             SPA React + TypeScript, cliente HTTP, sesion y tests frontend
```

## Frontend

El cliente web vive en `GymShop.Web` y usa React, TypeScript, Vite, Vitest y ESLint. La URL de la API se configura de forma central con `VITE_API_URL`; `.env.example` contiene solamente el valor local y no incluye secretos.

La vista inicial es Home tanto para visitantes como para usuarios autenticados. Muestra hasta seis productos activos como destacados según el orden actual de la API; **Ver catalogo** abre la vista independiente con todos los productos activos. Este criterio es temporal para el MVP y no agrega un campo `IsFeatured`. Debajo de los destacados incluye un banner editorial lifestyle asociado por nombre a la Kettlebell; si ese producto no esta activo, la campaña no se muestra para evitar un enlace incorrecto.

Las imagenes locales del frontend se guardan bajo `GymShop.Web/public/images` y se referencian desde productos con rutas publicas que comienzan con `/`, por ejemplo `/images/products/mancuerna-10kg.webp`. Tambien se admiten URLs HTTP/HTTPS. Si una imagen no existe o no puede cargarse, la interfaz muestra el fallback visual `GS`.

El cliente envia el JWT mediante `Authorization: Bearer`, invalida la sesion ante `401` y conserva la sesion ante `403`. Normaliza respuestas `{ message }`, ProblemDetails, ValidationProblemDetails, `409`, `429` con `Retry-After` y errores `500` con `traceId`. La clave de idempotencia del pago se conserva por orden en el almacenamiento local y la creacion usa exclusivamente `POST /api/orders/{orderId}/payments` con el proveedor `Mock`.

Variables frontend:

```text
VITE_API_URL=http://localhost:5093
VITE_GOOGLE_CLIENT_ID=<GOOGLE_CLIENT_ID_PUBLICO>
```

No se deben colocar JWT, credenciales de usuarios ni secretos de Mercado Pago en variables `VITE_*`: Vite las incorpora al bundle publico.

### Registro, verificacion y Google

El registro manual requiere `name`, `lastName`, `email` y `password`. No emite un JWT inmediatamente: crea una cuenta pendiente y envia un codigo de seis digitos que vence a los 60 segundos. El codigo se guarda hasheado, permite hasta cinco intentos y queda consumido al verificar o reenviar. La verificacion correcta marca el email y devuelve la sesion JWT automaticamente.

Endpoints:

- `POST /api/auth/register`
- `POST /api/auth/verify-email`
- `POST /api/auth/resend-verification`
- `POST /api/auth/google`

En esta fase `IVerificationEmailSender` usa un proveedor Mock: el codigo se muestra en la respuesta como `developmentCode` y en el log local. Esto sirve para desarrollo y tests, pero no prueba la propiedad de un correo real y debe reemplazarse antes de staging publico.

El frontend conserva en `localStorage` solamente el email y el vencimiento de una verificacion pendiente para poder retomarla tras recargar o cerrar la pagina. El codigo Mock no se persiste: si ya no esta visible, hay que esperar el vencimiento y usar **Reenviar codigo** para obtener uno nuevo.

### Recuperación de contraseña

La recuperación se implementa completa en desarrollo salvo por el envío real de correo:

```text
Olvidé mi contraseña
→ ingresar email
→ respuesta genérica
→ código de 6 dígitos válido durante 10 minutos
→ ingresar código y contraseña nueva
→ invalidar sesiones anteriores
→ volver al login
```

Contrato HTTP:

```text
POST /api/auth/forgot-password
{ "email": "usuario@example.com" }

POST /api/auth/reset-password
{ "email": "usuario@example.com", "code": "123456", "newPassword": "NuevaClave123" }
```

`forgot-password` devuelve siempre el mismo mensaje y el mismo tiempo de vencimiento, exista o no la cuenta. Esto evita usar el endpoint para enumerar emails registrados. En Development el proveedor Mock incluye `developmentCode` y escribe el código en el log; en staging y producción ese campo debe ser `null` y el código debe enviarse por el proveedor real.

Los códigos se guardan en `PasswordResetCodes`, separados de `EmailVerificationCodes`, porque activar un email y cambiar una credencial son propósitos de seguridad distintos. Solo se persiste un hash con sal mediante el servicio de hashing de credenciales, nunca sus seis dígitos. Cada solicitud invalida códigos anteriores, cada código admite como máximo cinco intentos, vence después de 600 segundos y queda consumido tras usarse.

Al confirmar se aplican las mismas reglas de contraseña fuerte que en registro, se actualiza `PasswordHash` y se incrementa `TokenVersion`. Por eso todos los JWT emitidos previamente reciben `401`; el frontend no inicia sesión automáticamente y vuelve al formulario de login. Una cuenta creada inicialmente con Google también puede establecer una contraseña manual mediante este flujo si controla el correo asociado.

La solicitud y la confirmación tienen límites por IP y por hash del email. El hash se utiliza como clave de partición para no conservar el email en memoria dentro del rate limiter.

La nueva migración `AddPasswordResetCodes` crea solamente la tabla, la clave foránea hacia `Users` con eliminación en cascada y el índice `(UserId, ExpiresAtUtc)`. No modifica usuarios ni códigos de verificación existentes.

Para staging quedan pendientes:

- sustituir `MockPasswordResetEmailSender` por un proveedor de correo real;
- configurar remitente, dominio y secretos fuera del repositorio;
- diseñar y probar la plantilla del mensaje;
- validar entregabilidad, spam y tiempos reales;
- confirmar límites definitivos según tráfico observado.

Google Identity Services requiere el mismo Client ID publico en backend y frontend:

```text
GoogleAuth__ClientId=<GOOGLE_CLIENT_ID_PUBLICO>
VITE_GOOGLE_CLIENT_ID=<GOOGLE_CLIENT_ID_PUBLICO>
```

El backend valida la credencial con Google, exige `email_verified=true` y vincula por el identificador estable `sub`. Si el email ya pertenece a una cuenta manual activa, agrega la identidad externa a ese mismo usuario; no crea un usuario duplicado. No se usa ni se expone un Client Secret en el navegador.

La API no accede directamente a la persistencia. La logica se concentra en casos de uso de Application, con EF Core y servicios externos implementados en Infrastructure.

## Flujo de compra

1. El usuario agrega productos al carrito con `POST /api/cart/items`.
2. Ejecuta checkout con `POST /api/cart/checkout` enviando direccion de envio.
3. El backend valida productos, stock y orden pendiente.
4. Se crea la orden, sus items, se descuentan stocks y se limpia el carrito en una unica operacion atomica.
5. El usuario crea el pago con `POST /api/orders/{orderId}/payments`.
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

`Paid -> Canceled` esta prohibido en el cambio administrativo generico. Un pago aprobado solo pasa a `Refunded` cuando el webhook consulta Mercado Pago y el proveedor confirma el reembolso. GymShop no inicia refunds ni llama a la API de refunds en esta fase; deben iniciarse desde Mercado Pago. La primera razon de cancelacion se conserva en `Order.CancellationReason` y tambien se copia a los pagos activos cancelados; los reintentos no reemplazan el motivo original.

Los webhooks `refunded` repetidos son idempotentes. Si el pedido todavia estaba `Paid`, solo la primera transicion restaura stock. Si ya estaba `Shipped`, la devolucion fisica y el stock quedan pendientes de gestion manual y el motivo se registra en `Payment.FailureReason`.

Los reembolsos parciales no se automatizan: el pago conserva `Approved`, la orden conserva `Paid` o `Shipped`, no se modifica stock y `FailureReason` indica que el caso requiere gestion manual.

Las transiciones incompatibles con el estado actual responden `409 Conflict`. Los estados o formatos desconocidos continúan respondiendo errores de validacion.

## Concurrencia al crear pagos

La creacion de pagos reserva primero un registro local con estado `Creating`. Esa reserva se guarda antes de llamar al gateway y el indice SQL Server `UX_Payments_OrderId_Active` permite solamente un pago `Creating` o `Pending` por orden. Los intentos `CreationFailed`, `Rejected`, `Canceled`, `Expired` y `Refunded` permanecen como historial y no bloquean un nuevo intento.

`IdempotencyKey` puede ser enviada por el cliente. Si se omite, el servidor genera una clave con prefijo `server-`; `PaymentResponse` siempre expone la clave efectiva. Repetir una clave reutiliza el mismo intento. Para reintentar un `CreationFailed` se debe usar una clave nueva.

Si otro request encuentra el pago ganador todavia en `Creating`, `POST /api/orders/{orderId}/payments` responde `202 Accepted`, incluye el Payment con `CheckoutUrl=null` y una cabecera `Location` hacia `GET /api/payments/{id}`. Cuando el gateway termina correctamente, el estado pasa a `Pending` y queda disponible la URL de checkout.

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

- `POST /api/orders/{orderId}/payments`
- `GET /api/payments/{id}`
- `GET /api/payments/orders/{orderId}`
- `POST /api/payments/{id}/status` - Admin, SuperAdmin
- `POST /api/payments/mercadopago/webhook`

### Audit

- `GET /api/audit?page=1&pageSize=50` - solo SuperAdmin

## Auditoria administrativa

Las operaciones sensibles se registran en `AuditEntries` dentro del mismo `SaveChangesAsync` que persiste el cambio. Si la auditoria no puede guardarse, la operacion sensible tambien falla y la transaccion local no se confirma.

Se auditan cambios de rol y estado de usuarios, stock y estado de productos, cambios administrativos de orden, cancelaciones, expiraciones, resoluciones manuales de pago y reembolsos informados por Mercado Pago. Las operaciones idempotentes que no cambian estado no generan entradas duplicadas. Una expiracion masiva genera una entrada por cada orden modificada con el mismo correlation ID del request.

`OldValue` y `NewValue` contienen JSON acotado con campos permitidos explicitamente, hasta 2000 caracteres. La auditoria no serializa entidades, requests completos, passwords, hashes, JWT, secretos, datos de tarjeta, payloads completos de Mercado Pago ni URLs de checkout. `Reason` admite hasta 500 caracteres y `CorrelationId` hasta 100.

Las acciones humanas conservan `ActorUserId`. Los eventos confirmados por proveedor, como un refund de Mercado Pago, permiten actor nulo y usan una accion que identifica el origen. No se almacena IP en esta fase por minimizacion de datos personales; la correlacion con logs se realiza mediante `CorrelationId`.

La consulta de auditoria es de solo lectura y exclusiva para SuperAdmin. Admite filtros `action`, `entityType`, `entityId`, `actorUserId`, `fromUtc` y `toUtc`. `page` comienza en 1, `pageSize` es 50 por defecto y su maximo es 100. No existen endpoints comunes para crear, editar o eliminar entradas. Las fechas de filtro deben enviarse en UTC.

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

## Rate limiting, HTTPS y reverse proxy

Rate limiting esta habilitado de forma segura por defecto y se deshabilita explicitamente en `appsettings.Development.json` para no interferir con el desarrollo local. Puede habilitarse localmente cambiando `RateLimiting:Enabled=true`.

Las solicitudes deben superar todos los limites aplicables:

| Flujo | Particiones predeterminadas de Production |
|---|---|
| Login | 10 por IP/minuto y 5 por cuenta/15 minutos |
| Registro | 5 por IP/hora y 100 globales/hora |
| Crear pago | 10 por `UserId`/minuto y 3 por `OrderId`/minuto |
| Webhook Mercado Pago | 120 por IP/minuto y 3000 globales/minuto, despues de validar HMAC |

La cuenta de login se normaliza y se transforma con SHA-256 para no conservar ni registrar el email como clave del limitador. Un rechazo responde `429 Too Many Requests` con `ProblemDetails`, `traceId` y `Retry-After`, sin indicar si la cuenta existe. Los contadores son locales a cada instancia de la API; un despliegue con varias replicas debe mover esta defensa a un gateway o almacenamiento distribuido como Redis.

Todos los valores se configuran bajo `RateLimiting`. En Production, `RateLimiting:Enabled=false` hace fallar el arranque. Los limites usan ventanas fijas y no encolan solicitudes.

HTTPS es obligatorio en Production. ASP.NET Core activa redireccion HTTPS y HSTS fuera de Development. Si Kestrel recibe trafico directamente, debe tener un endpoint TLS y certificado configurados. Si TLS termina en un reverse proxy, ese proxy tambien debe exigir HTTPS y enviar `X-Forwarded-Proto: https`.

El procesamiento de forwarded headers esta deshabilitado por defecto:

```json
"ReverseProxy": {
  "Enabled": false,
  "ForwardLimit": 1,
  "KnownProxies": []
}
```

Solo debe habilitarse cuando realmente exista un proxy. Cada IP de proxy autorizada debe declararse en `KnownProxies`; habilitarlo sin proxies validos hace fallar el arranque. GymShop procesa solamente `X-Forwarded-For` y `X-Forwarded-Proto`, con un salto por defecto. Un header enviado directamente por un cliente no confiable se ignora.

Ejemplo sin direcciones reales:

```text
ReverseProxy__Enabled=true
ReverseProxy__ForwardLimit=1
ReverseProxy__KnownProxies__0=<IP-PRIVADA-DEL-PROXY>
```

HSTS puede ser emitido por ASP.NET Core como en esta configuracion o centralizarse en el proxy. Si el proxy lo administra, debe revisarse la politica antes de retirar `UseHsts` del backend; no se debe desactivar HTTPS.

## Ejecutar el proyecto

### Backend local con pagos Mock

Mercado Pago ya esta deshabilitado en Development. Configura solo los secretos locales del backend y ejecuta la API:

```powershell
dotnet user-secrets set "Jwt:Secret" "<CLAVE-ALEATORIA-LOCAL-DE-32-O-MAS-CARACTERES>" --project "GymShop.Api/GymShop.Api.csproj"
dotnet user-secrets set "SeedSuperAdmin:Password" "<PASSWORD-LOCAL>" --project "GymShop.Api/GymShop.Api.csproj"
dotnet run --project GymShop.Api/GymShop.Api.csproj
```

Swagger queda disponible en:

```text
http://localhost:5093/swagger
```

### Frontend local

En otra terminal:

```powershell
cd GymShop.Web
Copy-Item .env.example .env.local
pnpm install --frozen-lockfile
pnpm dev
```

El cliente queda disponible en `http://localhost:5173`. El archivo `.env.local` esta ignorado por Git.

Comandos de calidad frontend:

```powershell
pnpm lint
pnpm typecheck
pnpm test
pnpm build
```

Recorrido local esperado con el proveedor Mock:

```text
Registro -> Login -> Catalogo -> Carrito -> Checkout
-> Crear pago por orderId -> Consultar pago -> Consultar orden
```

No hace falta configurar `MercadoPago:AccessToken`, `MercadoPago:WebhookSecret` ni ninguna credencial real mientras `MercadoPago:Enabled=false`.

### Navegación, catálogo y carrito frontend

El frontend usa `react-router-dom` con `BrowserRouter`. Las rutas principales son:

```text
/                         Home
/catalogo                 Búsqueda, filtros y ordenamiento
/catalogo/:productId      Detalle público del producto activo
/carrito                  Carrito visitante o autenticado
/login                    Login, registro y retorno al flujo anterior
/ordenes                  Órdenes del usuario autenticado
/admin/productos          Admin y SuperAdmin
/admin/usuarios           SuperAdmin
/admin/auditoria          SuperAdmin
```

Se eligió `BrowserRouter` porque genera URLs normales, compartibles y compatibles con el historial del navegador. Frente a un router basado en hash ofrece URLs más limpias; como contrapartida, al publicar se debe configurar el servidor para que las rutas desconocidas devuelvan `index.html`. Esto no requiere configuración adicional con el servidor de desarrollo de Vite.

El catálogo público siempre solicita `GET /api/products` sin `includeInactive=true`. La búsqueda por nombre o descripción, el rango de precio, la disponibilidad y el orden se aplican en el cliente sobre esa respuesta. Esto da respuesta inmediata y evita modificar el contrato del backend para el volumen actual; como contrapartida, si el catálogo crece será preferible incorporar búsqueda, paginación y filtros en la API. `CatalogFilters` deja señalado el punto de extensión para categorías y SKU, pero no inventa esos campos mientras no existan en el contrato.

#### Carrito visitante y fusión al iniciar sesión

El carrito visitante se guarda en `localStorage` bajo una clave versionada. Se eligió este mecanismo porque sobrevive recargas y cierres sin necesitar una identidad anónima ni cambios de base de datos. Sus límites son que pertenece a ese navegador, puede borrarse desde las herramientas del navegador y no debe contener información sensible.

Al iniciar sesión se combina con el carrito existente del usuario:

1. Se obtiene el carrito del backend y se vuelve a validar cada producto visitante contra el catálogo activo.
2. Para cada producto se calcula `cantidad existente + cantidad visitante`.
3. La cantidad final se limita al stock vigente y se informa cualquier ajuste.
4. Se persiste un plan de cantidades objetivo antes de enviar cambios.
5. Los productos existentes se actualizan con cantidad absoluta; los nuevos se agregan.
6. Cada elemento visitante se elimina solamente después de confirmar su sincronización.

El plan persistido hace que un reintento converja a la misma cantidad objetivo y evita sumar dos veces si la red se corta después de una respuesta. Es más seguro que encadenar operaciones aditivas sin memoria, aunque agrega lógica local. Una alternativa futura es un endpoint transaccional de fusión en backend, que sería más robusto entre dispositivos pero implica ampliar explícitamente el contrato.

Si un producto ya no existe, está inactivo o no tiene stock, se avisa y su entrada visitante se conserva en el almacenamiento local en lugar de descartarla silenciosamente. Si la API rechaza toda la fusión, el carrito visitante también se conserva para reintentar. El backend continúa siendo la autoridad final para stock, permisos y conflictos.

El estado compartido usa React Context porque el carrito solamente cruza catálogo, detalle, encabezado, popup y checkout. Es una opción pequeña y sin dependencias adicionales; Redux u otra store ofrecerían herramientas más potentes para estados globales muy grandes, pero aumentarían complejidad y tamaño sin una ventaja clara en este MVP.

El checkout permite armar el carrito sin sesión, pero redirige a `/login` antes de crear la orden y vuelve a `/carrito` después de autenticar. La fusión ocurre en ese cambio de sesión. No se agregaron refresh tokens, secretos ni cambios al proveedor Mock.

Una orden pendiente no bloquea el carrito: el usuario puede agregar, actualizar, quitar o vaciar productos porque ese carrito representa una compra futura distinta de la orden ya creada. El checkout sí evita crear una segunda orden pendiente por ahora. Esta separación permite seguir comprando sin acumular órdenes ni mezclar productos nuevos con una orden cuyo precio y stock ya quedaron congelados.

### Flujo de checkout local

El checkout está separado en tres responsabilidades:

```text
/carrito
  edición de productos y cantidades
       ↓
/checkout
  revisión, dirección y confirmación
       ↓
/checkout/orden/:orderId
  resultado, orden, pagos, reconsulta y cancelación
```

Se eligieron páginas separadas en lugar de mantener todo dentro del carrito porque permiten usar atrás/adelante, recuperar una orden por URL y distinguir claramente entre modificar la compra y confirmarla. La contrapartida es una ruta y componentes adicionales, pero reduce el riesgo de recrear una orden al volver a la pantalla anterior.

El contrato actual solamente admite `shippingAddress`, con un máximo de 300 caracteres. El frontend no agrega costos de envío, cuotas, impuestos, códigos postales estructurados ni datos fiscales porque todavía no existen en el backend. Agregar esos conceptos solamente en la interfaz produciría totales o promesas que la API no puede validar.

Al confirmar, el frontend ejecuta:

```text
POST /api/cart/checkout
POST /api/orders/{orderId}/payments
GET  /api/orders/{orderId}
GET  /api/payments/orders/{orderId}
```

El pago siempre envía `provider: "Mock"`. No se utiliza ni se recrea `/api/payments/current`.

El botón de confirmación usa un bloqueo sincrónico además del estado visual de carga. Esto evita dobles envíos dentro de la misma instancia de la interfaz. Sin embargo, `POST /api/cart/checkout` no acepta una clave de idempotencia: un corte de red después de que el servidor creó la orden no permite demostrar desde el cliente si la respuesta se perdió. Ante un error incierto o un `409`, el frontend consulta las órdenes pendientes y ofrece recuperar la encontrada antes de permitir otro intento. Una garantía completa requeriría agregar, con una decisión explícita de contrato, idempotencia para la creación de órdenes en backend.

La creación del pago sí utiliza una clave estable por orden. Actualizar un pago `Pending` solo vuelve a consultar su estado y no crea otro intento. Si un pago `Creating` necesita retomarse se conserva la misma clave. Un nuevo intento después de `CreationFailed`, `Rejected`, `Canceled` o `Expired` genera una clave nueva porque representa otra operación de negocio. Esto evita duplicar un mismo intento sin impedir que el usuario vuelva a pagar después de un resultado terminal.

La pantalla de resultado contempla todos los estados definidos por el backend:

```text
Creating, Pending, CreationFailed, Approved,
Rejected, Canceled, Expired y Refunded
```

`Creating` no presupone la existencia de `CheckoutUrl`. La interfaz presenta `Pending` como una confirmación en curso, no como una compra guardada para pagar después. Ofrece una sola acción **Actualizar estado** para pagos activos, **Intentar pagar nuevamente** para estados terminales recuperables y cancelación para una orden `Pending`. El backend continúa decidiendo si cada transición o cancelación es válida.

La dirección no se persiste en `localStorage`: es información personal y solamente se envía al backend cuando el usuario confirma. Después de crear la orden se conserva en `sessionStorage` únicamente el último `orderId`, no la dirección ni información de pago.

Ventajas del diseño:

- La orden creada no se pierde aunque falle la creación del pago.
- La URL del resultado puede recargarse y vuelve a consultar la autoridad del backend.
- Los errores `409`, `429`, `ProblemDetails` y `traceId` se muestran sin inventar estados.
- No se guardan secretos ni datos financieros en el navegador.

Limitaciones actuales:

- La creación de la orden no tiene idempotencia distribuida.
- No hay cálculo de envío, promociones, impuestos ni cuotas.
- El proveedor Mock no representa una aprobación financiera real.
- Para staging será necesario definir las reglas comerciales de entrega antes de ampliar los campos.

La aplicacion aplica migraciones automaticamente en ambiente Development.

## Docker local

El entorno Docker levanta dos servicios definidos en `compose.yaml`:

```text
api       -> API ASP.NET Core, publicada en http://localhost:8080
database  -> SQL Server Express, accesible solo desde la red de Docker
```

Crear primero el archivo local de variables a partir de la plantilla y reemplazar los valores de ejemplo:

```powershell
Copy-Item .env.docker.example .env.docker
```

`.env.docker` contiene credenciales locales y esta ignorado por Git. No debe commitearse ni compartirse.

Comandos habituales, ejecutados desde la raiz del repositorio:

```powershell
# Construir las imagenes e iniciar los servicios en segundo plano
docker compose --env-file .env.docker up --build -d

# Ver el estado de los contenedores
docker compose --env-file .env.docker ps

# Seguir los logs de la API; Ctrl+C deja de seguirlos sin detener el servicio
docker compose --env-file .env.docker logs -f api

# Detener y eliminar los contenedores y la red, conservando la base
docker compose --env-file .env.docker down

# Volver a iniciar sin reconstruir las imagenes
docker compose --env-file .env.docker up -d
```

La API se puede comprobar en:

```text
http://localhost:8080/health
http://localhost:8080/swagger
```

### Persistencia de SQL Server

El servicio `database` guarda los archivos de SQL Server en el volumen nombrado
`gymshop-sql-data`. Un volumen es almacenamiento administrado por Docker que existe
fuera del contenedor. Por eso eliminar y volver a crear el contenedor no elimina
usuarios, productos, ordenes ni migraciones ya aplicadas.

Este comando conserva el volumen y los datos:

```powershell
docker compose --env-file .env.docker down
```

Este comando tambien elimina el volumen y reinicia la base desde cero en el siguiente arranque:

```powershell
docker compose --env-file .env.docker down --volumes
```

Usar `--volumes` solamente cuando se quiera borrar deliberadamente toda la base local
de Docker. No afecta una base LocalDB ni una futura base alojada en RDS.

## Tests

```powershell
dotnet test GymShop.slnx
```

Los tests con `Category=Integration` levantan el pipeline HTTP real y/o verifican comportamiento propio de SQL Server. En Windows usan LocalDB por defecto. En Linux, CI o cuando se prefiera una instancia completa, definir una conexion administrativa en `GYMSHOP_TEST_SQLSERVER`; cada test crea una base aislada, aplica todas las migraciones desde cero y la elimina al terminar.

Ejemplo sin credenciales reales:

```powershell
$env:GYMSHOP_TEST_SQLSERVER="Server=<HOST>,1433;Database=master;User Id=<USUARIO>;Password=<PASSWORD>;TrustServerCertificate=True"
dotnet test GymShop.slnx --configuration Release --filter "Category=Integration"
```

Para ejecutar solamente la suite rapida:

```powershell
dotnet test GymShop.slnx --configuration Release --filter "Category!=Integration"
```

Para generar y resumir cobertura por proyecto:

```powershell
dotnet test GymShop.slnx --configuration Release --collect:"XPlat Code Coverage" --results-directory TestResults
powershell -NoProfile -ExecutionPolicy Bypass -File ./scripts/Summarize-Coverage.ps1 -ResultsDirectory TestResults
```

La cobertura indica que lineas y ramas fueron ejecutadas durante los tests; no indica que porcentaje de funciones usa un cliente real ni demuestra por si sola ausencia de defectos. Se reporta por proyecto como señal para detectar huecos, sin imponer un porcentaje artificial.

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
- Pipeline HTTP real: login, JWT, 401/403, roles, serializacion, ProblemDetails, errores 500, productos inactivos, rate limiting y webhook HMAC.
- Migraciones desde una base vacia, restricciones, indices filtrados, RowVersion, rollback de checkout y consultas traducidas por SQL Server.
- Concurrencia sobre ultimo stock, actualizacion de stock y creacion de pagos activos.
- Gateway HTTP de Mercado Pago ante exito, timeout, JSON invalido, 4xx/5xx, reintento idempotente y estado refunded.

## CI

El repositorio incluye GitHub Actions en `.github/workflows/ci.yml`.

En cada push o pull request hacia `main`, `master` o `develop`, ejecuta:

```powershell
dotnet restore GymShop.slnx
dotnet build GymShop.slnx --configuration Release --no-restore
dotnet test GymShop.slnx --configuration Release --no-build --filter "Category!=Integration" --verbosity normal
dotnet test GymShop.slnx --configuration Release --no-build --collect:"XPlat Code Coverage" --results-directory TestResults --verbosity normal
```

El segundo comando usa el servicio SQL Server de CI, ejecuta la suite completa, publica el XML Cobertura como artefacto y agrega al resumen del job los porcentajes de lineas y ramas por proyecto.

## Notas de seguridad

- No commitear tokens, passwords ni secretos.
- Usar User Secrets en desarrollo.
- Usar variables de entorno o secret manager en produccion.
- Mantener `MercadoPago:Enabled=false` cuando se usa el proveedor Mock.
- Configurar `MercadoPago:WebhookSecret` siempre que se procesen notificaciones reales; es obligatorio en Production.
- Mantener `Jwt:Secret` fuera de `appsettings.json`.
