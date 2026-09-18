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

Los casos siguientes describen el contrato funcional implementado actualmente. Una regla interna no se presenta como un caso independiente si no puede ser iniciada por un actor; por ejemplo, la idempotencia forma parte de crear un pago y la validacion de firma forma parte de procesar el webhook.



### Autenticacion


#### Registrar un usuario

**Objetivo:** crear una cuenta local e iniciar la verificacion de su email.

**Actor y permisos:** visitante no autenticado.

**Datos de entrada:** nombre, apellido, email y password.

**Precondiciones:** el email no debe estar registrado y debe existir el rol `User`.

**Flujo esperado:** se normaliza el email, se validan los datos, se crea un usuario activo con rol `User`, se genera un codigo de seis digitos y se envia al email informado.

**Reglas de negocio:** la password debe tener entre 8 y 128 caracteres, con al menos una letra y un numero; el codigo vence a los 60 segundos; la cuenta no puede iniciar sesion hasta verificar el email.

**Resultado:** devuelve el email y el tiempo de vigencia del codigo; en desarrollo puede incluir el codigo generado.

**Errores esperados:** `400 Bad Request` por datos invalidos, `409 Conflict` si el email ya existe y `429 Too Many Requests` al exceder el limite de solicitudes.

**Que queda explicitamente fuera:** no inicia sesion, no permite elegir rol y no crea cuentas administrativas.



#### Verificar el email

**Objetivo:** confirmar la propiedad del email y habilitar el inicio de sesion.

**Actor y permisos:** usuario con registro local pendiente de verificacion.

**Datos de entrada:** email y codigo numerico de seis digitos.

**Precondiciones:** debe existir una cuenta sin verificar y un codigo vigente no consumido.

**Flujo esperado:** se localiza el ultimo codigo activo, se compara de forma segura, se marca como consumido, se verifica la cuenta y se emite un JWT.

**Reglas de negocio:** el codigo vence a los 60 segundos, admite como maximo cinco intentos fallidos y sólo puede consumirse una vez.

**Resultado:** devuelve el JWT y los datos del usuario autenticado.

**Errores esperados:** `400 Bad Request` si no hay verificacion pendiente, el codigo es incorrecto, vencio o supero los intentos permitidos.

**Que queda explicitamente fuera:** no cambia el email, la password ni el rol del usuario.



#### Reenviar el codigo de verificacion

**Objetivo:** emitir un nuevo codigo cuando el anterior ya no esta vigente.

**Actor y permisos:** usuario no autenticado con verificacion pendiente.

**Datos de entrada:** email.

**Precondiciones:** la cuenta debe existir, no estar verificada y no tener un codigo todavia vigente.

**Flujo esperado:** se invalidan codigos anteriores no consumidos, se genera uno nuevo y se envia al email de la cuenta.

**Reglas de negocio:** no se genera otro codigo mientras el actual siga vigente; el nuevo codigo vence a los 60 segundos.

**Resultado:** devuelve el email y el nuevo tiempo de vigencia; en desarrollo puede incluir el codigo.

**Errores esperados:** `400 Bad Request` si no puede reenviarse y `409 Conflict` si el codigo actual sigue vigente.

**Que queda explicitamente fuera:** no verifica automaticamente la cuenta ni revela si un email ajeno esta registrado con otros estados.



#### Iniciar sesion con email y password

**Objetivo:** autenticar una cuenta local y obtener una sesion JWT.

**Actor y permisos:** usuario no autenticado.

**Datos de entrada:** email y password.

**Precondiciones:** la cuenta debe existir, estar activa y tener el email verificado.

**Flujo esperado:** se normaliza el email, se verifica la password almacenada y se genera un token con la identidad y el rol actuales.

**Reglas de negocio:** cuentas inexistentes, inactivas, no verificadas o con password incorrecta producen la misma respuesta; el rate limiting se aplica por IP.

**Resultado:** devuelve el JWT y los datos del usuario.

**Errores esperados:** `400 Bad Request` por formato invalido, `401 Unauthorized` por credenciales no validas y `429 Too Many Requests` por exceso de intentos.

**Que queda explicitamente fuera:** no registra usuarios, no verifica emails y no renueva tokens.



#### Iniciar sesion con Google

**Objetivo:** autenticar o registrar un usuario a partir de una identidad verificada por Google.

**Actor y permisos:** visitante no autenticado con una credencial de Google.

**Datos de entrada:** credencial emitida por Google.

**Precondiciones:** la credencial debe ser valida y el email informado por Google debe estar verificado.

**Flujo esperado:** se verifica la identidad externa; se reutiliza el vinculo existente o se vincula por email; si no existe usuario se crea uno activo con rol `User`; finalmente se emite un JWT.

**Reglas de negocio:** una cuenta inactiva no puede ingresar; una cuenta local con el mismo email se vincula a la identidad externa; el email se considera verificado por Google.

**Resultado:** devuelve el JWT y los datos del usuario.

**Errores esperados:** `400 Bad Request` si falta la credencial y `401 Unauthorized` si es invalida, el email no esta verificado o la cuenta esta inactiva.

**Que queda explicitamente fuera:** no permite elegir rol, no almacena una password de Google y no gestiona la cuenta en Google.



#### Solicitar recuperacion de password

**Objetivo:** iniciar de forma segura el restablecimiento de una password local.

**Actor y permisos:** visitante no autenticado.

**Datos de entrada:** email.

**Precondiciones:** ninguna precondicion visible; si existe, la cuenta debe estar activa para recibir un codigo utilizable.

**Flujo esperado:** se invalida cualquier codigo anterior, se crea un codigo de seis digitos con vigencia de diez minutos y se envia el email.

**Reglas de negocio:** la respuesta es deliberadamente generica para no revelar si la cuenta existe; el rate limiting se aplica por IP.

**Resultado:** devuelve un mensaje generico y el tiempo de vigencia; en desarrollo puede incluir el codigo.

**Errores esperados:** `400 Bad Request` por email invalido y `429 Too Many Requests` por exceso de solicitudes; un email inexistente no produce `404`.

**Que queda explicitamente fuera:** no cambia la password ni autentica al usuario.



#### Confirmar recuperacion de password

**Objetivo:** establecer una nueva password mediante un codigo de recuperacion valido.

**Actor y permisos:** visitante que posee el codigo enviado al email de una cuenta activa.

**Datos de entrada:** email, codigo numerico de seis digitos y nueva password.

**Precondiciones:** debe existir un codigo no consumido, vigente y con menos de cinco intentos fallidos.

**Flujo esperado:** se valida el codigo, se consumen los codigos activos, se reemplaza el hash de password y se incrementa la version de token.

**Reglas de negocio:** la nueva password debe cumplir la politica fuerte; el cambio invalida sesiones JWT anteriores; los errores del codigo usan un mensaje generico.

**Resultado:** confirma que la password fue actualizada.

**Errores esperados:** `400 Bad Request` por password debil o codigo invalido, vencido, consumido o bloqueado; `429 Too Many Requests` por exceso de intentos.

**Que queda explicitamente fuera:** no inicia sesion automaticamente ni reactiva una cuenta inactiva.



#### Obtener el usuario actual

**Objetivo:** recuperar la identidad asociada a la sesion vigente.

**Actor y permisos:** cualquier usuario autenticado y activo.

**Datos de entrada:** no recibe datos de negocio; usa el identificador contenido en la sesion.

**Precondiciones:** el JWT debe ser valido y corresponder a una sesion vigente.

**Flujo esperado:** se obtiene el usuario activo con su rol y se proyectan sus datos publicos de sesion.

**Reglas de negocio:** la validacion JWT comprueba la version de token y el estado de la cuenta.

**Resultado:** devuelve ID, email, nombre, apellido y rol.

**Errores esperados:** `401 Unauthorized` por sesion invalida y `404 Not Found` si el usuario ya no puede localizarse como activo.

**Que queda explicitamente fuera:** no modifica el perfil ni devuelve password, hashes o datos administrativos.




### Administracion de usuarios

#### Listar usuarios

**Objetivo:** consultar las cuentas administrables del sistema.

**Actor y permisos:** `SuperAdmin` autenticado.

**Datos de entrada:** ninguno.

**Precondiciones:** la sesion debe tener el rol `SuperAdmin`.

**Flujo esperado:** se consultan los usuarios con su rol y se ordenan del mas reciente al mas antiguo.

**Reglas de negocio:** la operacion incluye cuentas activas e inactivas.

**Resultado:** devuelve ID, email, nombre, rol, estado y fecha de creacion de cada usuario.

**Errores esperados:** `401 Unauthorized` sin autenticacion y `403 Forbidden` sin el rol requerido.

**Que queda explicitamente fuera:** no devuelve credenciales, codigos de verificacion ni historial de sesiones.



#### Crear un usuario administrativo

**Objetivo:** crear directamente una cuenta con un rol definido.

**Actor y permisos:** `SuperAdmin` autenticado.

**Datos de entrada:** nombre, email, password y rol (`User`, `Admin` o `SuperAdmin`).

**Precondiciones:** el email no debe existir y el rol solicitado debe estar configurado.

**Flujo esperado:** se normalizan los datos, se valida el rol, se hashea la password y se crea una cuenta activa con email ya verificado.

**Reglas de negocio:** nombre, email y password son obligatorios; actualmente esta operacion exige una password de al menos seis caracteres.

**Resultado:** devuelve el usuario creado y responde `201 Created`.

**Errores esperados:** `400 Bad Request` por datos o rol invalidos, `409 Conflict` por email duplicado, `401 Unauthorized` y `403 Forbidden`.

**Que queda explicitamente fuera:** no envia verificacion de email y no inicia sesion como el usuario creado.



#### Cambiar el rol de un usuario

**Objetivo:** asignar a una cuenta un rol diferente.

**Actor y permisos:** `SuperAdmin` autenticado.

**Datos de entrada:** ID de usuario y rol (`User`, `Admin` o `SuperAdmin`).

**Precondiciones:** el usuario y el rol deben existir.

**Flujo esperado:** se actualiza el rol, se incrementa la version de token y se registra la accion en auditoria.

**Reglas de negocio:** los nombres de rol no distinguen mayusculas; si el rol no cambia la operacion es idempotente; un cambio invalida sesiones anteriores.

**Resultado:** responde `204 No Content`.

**Errores esperados:** `400 Bad Request` por rol invalido, `404 Not Found` por usuario inexistente, `401 Unauthorized` y `403 Forbidden`.

**Que queda explicitamente fuera:** no cambia el estado, email ni password del usuario.



#### Cambiar el estado de un usuario

**Objetivo:** activar o desactivar una cuenta.

**Actor y permisos:** `SuperAdmin` autenticado.

**Datos de entrada:** ID de usuario y valor booleano `isActive`.

**Precondiciones:** el usuario debe existir.

**Flujo esperado:** se actualiza el estado, se incrementa la version de token y se registra el cambio en auditoria.

**Reglas de negocio:** el actor no puede desactivar su propia cuenta; repetir el estado actual es idempotente; cualquier cambio invalida sesiones anteriores.

**Resultado:** responde `204 No Content`.

**Errores esperados:** `404 Not Found` por usuario inexistente, `409 Conflict` al intentar desactivarse a si mismo, `401 Unauthorized` y `403 Forbidden`.

**Que queda explicitamente fuera:** no elimina la cuenta ni modifica su rol.




### Productos

#### Listar productos

**Objetivo:** obtener el catalogo visible.

**Actor y permisos:** visitante para productos activos; `Admin` o `SuperAdmin` para incluir inactivos.

**Datos de entrada:** parametro opcional `includeInactive`.

**Precondiciones:** sólo se requiere autenticacion cuando se solicitan productos inactivos.

**Flujo esperado:** se consultan los productos y se ordenan del ID mas reciente al mas antiguo.

**Reglas de negocio:** un visitante o usuario sin rol administrativo siempre recibe sólo activos, aunque solicite incluir inactivos.

**Resultado:** devuelve la lista de productos con precio, stock, imagen y estado.

**Errores esperados:** `401 Unauthorized` o `403 Forbidden` al solicitar inactivos sin permisos suficientes.

**Que queda explicitamente fuera:** no pagina, no reserva stock y no modifica productos.



#### Obtener un producto por ID

**Objetivo:** consultar el detalle de un producto.

**Actor y permisos:** cualquier visitante para activos; `Admin` o `SuperAdmin` también para inactivos.

**Datos de entrada:** ID del producto.

**Precondiciones:** el producto debe existir y ser visible para el actor.

**Flujo esperado:** se busca el producto y se proyecta su informacion publica.

**Reglas de negocio:** un producto inactivo se comporta como inexistente para actores no administrativos.

**Resultado:** devuelve el producto solicitado.

**Errores esperados:** `404 Not Found` si no existe o no es visible.

**Que queda explicitamente fuera:** no modifica ni reserva stock.



#### Crear un producto

**Objetivo:** incorporar un producto activo al catalogo.

**Actor y permisos:** `Admin` o `SuperAdmin` autenticado.

**Datos de entrada:** nombre, descripcion opcional, precio, stock inicial e imagen opcional.

**Precondiciones:** los datos deben respetar limites de longitud y formato.

**Flujo esperado:** se validan y normalizan los campos, se crea el producto activo y se persiste.

**Reglas de negocio:** el precio debe ser mayor que cero y compatible con `decimal(18,2)`; el stock no puede ser negativo; la imagen debe ser HTTP/HTTPS o una ruta web local valida.

**Resultado:** devuelve el producto creado y responde `201 Created`.

**Errores esperados:** `400 Bad Request` por datos invalidos, `401 Unauthorized` y `403 Forbidden`.

**Que queda explicitamente fuera:** no crea categorias, no registra movimientos de inventario y no admite crear el producto inactivo.



#### Actualizar un producto

**Objetivo:** reemplazar los datos editables de un producto existente.

**Actor y permisos:** `Admin` o `SuperAdmin` autenticado.

**Datos de entrada:** ID, nombre, descripcion, precio, stock, imagen y estado completo del producto.

**Precondiciones:** el producto debe existir y los datos deben ser validos.

**Flujo esperado:** se reemplazan los campos, se actualiza la fecha de modificacion y se registra la accion en auditoria.

**Reglas de negocio:** aplica las mismas validaciones que la creacion; el contrato es de reemplazo completo y puede modificar stock y estado.

**Resultado:** devuelve el producto actualizado.

**Errores esperados:** `400 Bad Request`, `404 Not Found`, `409 Conflict` por modificacion concurrente, `401 Unauthorized` y `403 Forbidden`.

**Que queda explicitamente fuera:** no elimina el producto y no conserva automaticamente campos omitidos.



#### Actualizar el stock de un producto

**Objetivo:** establecer el stock disponible de un producto existente.

**Actor y permisos:** `Admin` o `SuperAdmin` autenticado.

**Datos de entrada:** ID del producto y valor final entero `stock`; no recibe una variacion.

**Precondiciones:** el producto debe existir.

**Flujo esperado:** se sustituye el stock actual por el valor recibido, se actualiza la fecha y se registra el cambio en auditoria.

**Reglas de negocio:** el stock no puede ser negativo; repetir el mismo valor es idempotente; se aplica control de concurrencia optimista.

**Resultado:** responde `204 No Content`.

**Errores esperados:** `400 Bad Request` por cantidad invalida, `404 Not Found`, `409 Conflict` por modificacion concurrente, `401 Unauthorized` y `403 Forbidden`.

**Que queda explicitamente fuera:** no modifica otros datos, no exige que el producto este activo y no registra movimientos individuales de inventario.



#### Activar o desactivar un producto

**Objetivo:** controlar la visibilidad y disponibilidad comercial de un producto.

**Actor y permisos:** `Admin` o `SuperAdmin` autenticado.

**Datos de entrada:** ID del producto y valor booleano `isActive`.

**Precondiciones:** el producto debe existir.

**Flujo esperado:** se cambia el estado, se actualiza la fecha y se registra la accion en auditoria.

**Reglas de negocio:** repetir el estado actual es idempotente; un producto inactivo no puede agregarse al carrito ni verse publicamente.

**Resultado:** responde `204 No Content`.

**Errores esperados:** `404 Not Found`, `409 Conflict` por modificacion concurrente, `401 Unauthorized` y `403 Forbidden`.

**Que queda explicitamente fuera:** no elimina el producto, no cambia su stock y no retira automaticamente items ya presentes en carritos.




### Carrito y checkout

#### Obtener el carrito

**Objetivo:** consultar el carrito persistente del usuario.

**Actor y permisos:** cualquier usuario autenticado.

**Datos de entrada:** ninguno; se usa el usuario de la sesion.

**Precondiciones:** la sesion debe ser valida.

**Flujo esperado:** se localiza el carrito o se crea uno vacio y se calculan subtotales y total con los precios actuales.

**Reglas de negocio:** existe como maximo un carrito por usuario; consultar por primera vez puede crear el carrito.

**Resultado:** devuelve carrito, items, stock visible y total.

**Errores esperados:** `401 Unauthorized` por sesion ausente o invalida.

**Que queda explicitamente fuera:** no reserva stock y no devuelve un carrito visitante del navegador.



#### Agregar un item al carrito

**Objetivo:** incorporar unidades de un producto al carrito autenticado.

**Actor y permisos:** cualquier usuario autenticado.

**Datos de entrada:** ID de producto y cantidad positiva.

**Precondiciones:** el producto debe existir, estar activo y tener stock suficiente para la cantidad total resultante.

**Flujo esperado:** se crea el carrito si no existe; se agrega el item o se suma la cantidad a la ya existente.

**Reglas de negocio:** no puede haber dos lineas del mismo producto; la cantidad acumulada no puede superar el stock actual.

**Resultado:** devuelve el carrito actualizado.

**Errores esperados:** `400 Bad Request` por cantidad invalida o stock insuficiente, `404 Not Found` si el producto no existe o esta inactivo y `401 Unauthorized`.

**Que queda explicitamente fuera:** no descuenta ni reserva stock y no crea una orden.



#### Actualizar un item del carrito

**Objetivo:** establecer la cantidad final de un producto ya agregado.

**Actor y permisos:** cualquier usuario autenticado propietario del carrito.

**Datos de entrada:** ID del producto y cantidad final positiva.

**Precondiciones:** deben existir el carrito y el item; el producto debe continuar activo y con stock suficiente.

**Flujo esperado:** se reemplaza la cantidad y se recalculan subtotales y total.

**Reglas de negocio:** la cantidad recibida no es una variacion y debe ser mayor que cero.

**Resultado:** devuelve el carrito actualizado.

**Errores esperados:** `400 Bad Request` por cantidad, producto inactivo o stock insuficiente, `404 Not Found` por carrito o item inexistente y `401 Unauthorized`.

**Que queda explicitamente fuera:** una cantidad cero no elimina el item; debe usarse la operacion de quitar.



#### Quitar un item del carrito

**Objetivo:** eliminar por completo un producto del carrito.

**Actor y permisos:** cualquier usuario autenticado propietario del carrito.

**Datos de entrada:** ID del producto.

**Precondiciones:** deben existir el carrito y el item indicado.

**Flujo esperado:** se elimina la linea y se devuelve el carrito recalculado.

**Reglas de negocio:** sólo se modifica el carrito del usuario de la sesion.

**Resultado:** devuelve el carrito actualizado.

**Errores esperados:** `404 Not Found` por carrito o item inexistente y `401 Unauthorized`.

**Que queda explicitamente fuera:** no modifica el producto ni el stock.



#### Vaciar el carrito

**Objetivo:** eliminar todos los items del carrito autenticado.

**Actor y permisos:** cualquier usuario autenticado.

**Datos de entrada:** ninguno.

**Precondiciones:** sólo se requiere una sesion valida.

**Flujo esperado:** se eliminan todos los items si el carrito existe.

**Reglas de negocio:** la operacion es idempotente; un carrito inexistente o ya vacio tambien se considera exito.

**Resultado:** responde `204 No Content`.

**Errores esperados:** `401 Unauthorized` por sesion ausente o invalida.

**Que queda explicitamente fuera:** no elimina el carrito, no cancela ordenes y no modifica stock.



#### Confirmar checkout y crear la orden

**Objetivo:** convertir el carrito en una orden pendiente y reservar sus unidades mediante descuento de stock.

**Actor y permisos:** cualquier usuario autenticado.

**Datos de entrada:** direccion de envio.

**Precondiciones:** el carrito debe tener items; todos los productos deben existir, estar activos y tener stock; el usuario no debe poseer otra orden `Pending`.

**Flujo esperado:** se validan nuevamente productos, precio y stock; se copian nombre y precio a las lineas de orden, se descuenta stock, se crea la orden `Pending` y se vacia el carrito dentro de una transaccion.

**Reglas de negocio:** la direccion es obligatoria y admite hasta 300 caracteres; sólo puede existir una orden pendiente por usuario; el total usa precios actuales al confirmar, no precios historicos del carrito.

**Resultado:** devuelve la orden creada con items, total y estado `Pending`.

**Errores esperados:** `400 Bad Request` por direccion, carrito, productos o stock invalidos; `409 Conflict` si ya existe una orden pendiente; `401 Unauthorized`.

**Que queda explicitamente fuera:** no crea el pago, no garantiza entrega y no mantiene los items en el carrito despues del exito.




### Ordenes

#### Consultar mis ordenes

**Objetivo:** listar el historial de ordenes del usuario autenticado.

**Actor y permisos:** cualquier usuario autenticado.

**Datos de entrada:** ninguno.

**Precondiciones:** sesion valida.

**Flujo esperado:** se filtran las ordenes por el usuario de la sesion y se ordenan de la mas reciente a la mas antigua.

**Reglas de negocio:** cada resumen incluye el estado del pago mas reciente cuando existe.

**Resultado:** devuelve una lista de resumenes de orden.

**Errores esperados:** `401 Unauthorized`.

**Que queda explicitamente fuera:** no incluye el detalle completo de items ni ordenes de otros usuarios.



#### Consultar una orden por ID

**Objetivo:** obtener el detalle completo de una orden.

**Actor y permisos:** el propietario; `Admin` y `SuperAdmin` pueden consultar cualquier orden.

**Datos de entrada:** ID de orden.

**Precondiciones:** la orden debe existir.

**Flujo esperado:** se carga la orden con usuario, items y pagos y se verifica su pertenencia.

**Reglas de negocio:** un usuario comun sólo accede a sus propias ordenes.

**Resultado:** devuelve datos de envio, estado, cancelacion, items y pagos.

**Errores esperados:** `401 Unauthorized`, `403 Forbidden` si no es propietario y `404 Not Found` si no existe.

**Que queda explicitamente fuera:** no actualiza el estado ni consulta informacion interna del proveedor de pagos.



#### Consultar y filtrar todas las ordenes

**Objetivo:** brindar una vista administrativa de las ordenes.

**Actor y permisos:** `Admin` o `SuperAdmin` autenticado.

**Datos de entrada:** email de usuario opcional como filtro.

**Precondiciones:** sesion con rol administrativo.

**Flujo esperado:** se consultan todas las ordenes o sólo las asociadas al email normalizado y se ordenan de la mas reciente a la mas antigua.

**Reglas de negocio:** un email sin coincidencias produce una lista vacia, no un error.

**Resultado:** devuelve resumenes con usuario, estado de orden y ultimo pago.

**Errores esperados:** `401 Unauthorized` y `403 Forbidden`.

**Que queda explicitamente fuera:** no pagina, no modifica ordenes y no realiza busqueda parcial por email.



#### Cancelar una orden

**Objetivo:** cancelar una orden pendiente y compensar sus efectos locales.

**Actor y permisos:** el propietario; `Admin` y `SuperAdmin` pueden cancelar cualquier orden.

**Datos de entrada:** ID de orden y motivo opcional de hasta 500 caracteres.

**Precondiciones:** la orden debe existir y estar `Pending`, salvo que ya este `Canceled`.

**Flujo esperado:** se marca `Canceled`, se cancelan pagos `Creating` o `Pending`, se repone el stock y se audita la accion.

**Reglas de negocio:** repetir la cancelacion es idempotente; este flujo no cancela ordenes pagadas, enviadas o reembolsadas.

**Resultado:** devuelve la orden cancelada.

**Errores esperados:** `400 Bad Request` por motivo invalido, `401 Unauthorized`, `403 Forbidden`, `404 Not Found` y `409 Conflict` por estado incompatible.

**Que queda explicitamente fuera:** no solicita reembolsos al proveedor ni cancela una orden ya pagada.



#### Expirar ordenes pendientes

**Objetivo:** cancelar administrativamente ordenes pendientes antiguas.

**Actor y permisos:** `Admin` o `SuperAdmin` autenticado.

**Datos de entrada:** antiguedad minima `olderThanMinutes`.

**Precondiciones:** el umbral debe ser mayor que cero.

**Flujo esperado:** se seleccionan ordenes `Pending` anteriores al corte, se cancelan, se cancelan pagos activos, se repone stock y se audita cada orden.

**Reglas de negocio:** sólo afecta ordenes pendientes; el corte se calcula en UTC desde la fecha de creacion.

**Resultado:** devuelve la cantidad de ordenes canceladas.

**Errores esperados:** `400 Bad Request` por umbral invalido, `401 Unauthorized` y `403 Forbidden`.

**Que queda explicitamente fuera:** no se ejecuta automaticamente y no expira pagos u ordenes con otros estados.



#### Actualizar el estado de una orden

**Objetivo:** ejecutar las transiciones administrativas permitidas.

**Actor y permisos:** `Admin` o `SuperAdmin` autenticado.

**Datos de entrada:** ID de orden y estado destino.

**Precondiciones:** la orden debe existir y la transicion debe ser valida.

**Flujo esperado:** se valida el estado; para cancelar se compensa stock y pagos; para enviar se cambia el estado; finalmente se audita.

**Reglas de negocio:** sólo se permiten `Pending -> Canceled` y `Paid -> Shipped`; repetir el estado actual es idempotente; pago y reembolso pertenecen al flujo de pagos.

**Resultado:** responde `204 No Content`.

**Errores esperados:** `400 Bad Request` por estado invalido, `404 Not Found`, `409 Conflict` por transicion invalida, `401 Unauthorized` y `403 Forbidden`.

**Que queda explicitamente fuera:** no puede marcar manualmente una orden como `Paid` o `Refunded`.




### Pagos

#### Crear un pago para una orden

**Objetivo:** crear o reutilizar el intento de pago activo de una orden pendiente.

**Actor y permisos:** propietario de la orden; `Admin` y `SuperAdmin` pueden operar cualquier orden.

**Datos de entrada:** ID de orden, proveedor opcional y clave de idempotencia opcional.

**Precondiciones:** la orden debe existir y admitir pagos; el proveedor debe estar configurado.

**Flujo esperado:** se valida pertenencia y estado, se reutiliza un pago idempotente o activo, o se reserva uno en estado `Creating`; luego el gateway crea la preferencia y el pago pasa a `Pending` con su URL de checkout.

**Reglas de negocio:** el importe y moneda se toman de la orden (`ARS`); sólo existe un pago `Creating` o `Pending` por orden; una clave no puede reutilizarse entre ordenes; reservas `Creating` estancadas pueden retomarse; sin proveedor se usa `Mock`.

**Resultado:** devuelve el pago; puede responder `202 Accepted` mientras siga `Creating` o `200 OK` cuando haya un estado reutilizable o preferencia creada.

**Errores esperados:** `400 Bad Request` por proveedor, clave o gateway invalidos; `401 Unauthorized`, `403 Forbidden`, `404 Not Found`, `409 Conflict` por orden o clave incompatibles y `429 Too Many Requests`.

**Que queda explicitamente fuera:** no aprueba el pago por si mismo y no permite pagar ordenes canceladas, enviadas o ya pagadas.



#### Consultar un pago

**Objetivo:** obtener el estado y los identificadores de un pago.

**Actor y permisos:** propietario de la orden; `Admin` o `SuperAdmin` pueden consultar cualquiera.

**Datos de entrada:** ID de pago.

**Precondiciones:** el pago debe existir.

**Flujo esperado:** se carga el pago con su orden y se verifica pertenencia.

**Reglas de negocio:** la visibilidad se determina por el propietario de la orden.

**Resultado:** devuelve proveedor, referencias, importe, estado, URL y fechas.

**Errores esperados:** `401 Unauthorized`, `403 Forbidden` y `404 Not Found`.

**Que queda explicitamente fuera:** no consulta en tiempo real al proveedor ni cambia el estado.



#### Consultar los pagos de una orden

**Objetivo:** listar todos los intentos de pago asociados a una orden.

**Actor y permisos:** propietario de la orden; `Admin` o `SuperAdmin` pueden consultar cualquiera.

**Datos de entrada:** ID de orden.

**Precondiciones:** la orden debe existir.

**Flujo esperado:** se verifica pertenencia y se devuelven los pagos del mas reciente al mas antiguo.

**Reglas de negocio:** una orden sin pagos devuelve una lista vacia.

**Resultado:** devuelve la lista de pagos.

**Errores esperados:** `401 Unauthorized`, `403 Forbidden` y `404 Not Found`.

**Que queda explicitamente fuera:** no consolida intentos ni crea un nuevo pago.



#### Actualizar administrativamente el estado de un pago

**Objetivo:** aplicar manualmente una transicion permitida de pago para soporte o pruebas operativas.

**Actor y permisos:** `Admin` o `SuperAdmin` autenticado.

**Datos de entrada:** ID, estado, ID del proveedor opcional y motivo de fallo opcional.

**Precondiciones:** el pago debe existir y la transicion debe ser compatible con el pago y la orden.

**Flujo esperado:** se valida el estado, se aplican los efectos sobre la orden y se registra la accion en auditoria.

**Reglas de negocio:** aprobar marca la orden como `Paid`; rechazar, cancelar o expirar mantiene la coherencia definida por el aplicador; un reembolso sólo puede confirmarse desde una notificacion del proveedor.

**Resultado:** devuelve el pago actualizado.

**Errores esperados:** `400 Bad Request` por datos invalidos, `404 Not Found`, `409 Conflict` por transicion incompatible, `401 Unauthorized` y `403 Forbidden`.

**Que queda explicitamente fuera:** no ejecuta un cobro ni un reembolso remoto y no permite simular manualmente la confirmacion de un reembolso.



#### Procesar un webhook de Mercado Pago

**Objetivo:** sincronizar el pago y la orden con el estado confirmado por Mercado Pago.

**Actor y permisos:** Mercado Pago como sistema externo; endpoint anonimo protegido por firma cuando existe un secreto configurado y por rate limiting.

**Datos de entrada:** `data.id` en query o cuerpo, `x-signature` y `x-request-id` cuando la firma esta habilitada.

**Precondiciones:** la integracion debe estar habilitada, el ID debe existir y debe haber un pago local correlacionable por proveedor, referencia externa y orden.

**Flujo esperado:** se extrae el ID, se valida HMAC-SHA256, se consulta el pago al gateway, se valida referencia, importe y moneda, y se aplica idempotentemente el estado informado.

**Reglas de negocio:** nunca se confia sólo en el cuerpo del webhook; la informacion se vuelve a consultar al proveedor; un reembolso total restaura stock sólo si la orden no fue enviada; un reembolso parcial se marca para gestion manual.

**Resultado:** responde `200 OK` con `received: true` y actualiza pago, orden, stock y auditoria cuando corresponda.

**Errores esperados:** `400 Bad Request` por ID o datos del proveedor invalidos, `401 Unauthorized` por firma invalida, `404 Not Found` si la integracion o pago local no existe, `409 Conflict` por importe, moneda o estados incompatibles y `429 Too Many Requests`.

**Que queda explicitamente fuera:** no inicia pagos, no confia en estados enviados sin verificacion y no resuelve automaticamente reembolsos parciales.




### Auditoria

#### Consultar el registro de auditoria

**Objetivo:** revisar cambios administrativos sensibles y su contexto.

**Actor y permisos:** `SuperAdmin` autenticado.

**Datos de entrada:** pagina, tamaño de pagina y filtros opcionales por accion, entidad, ID, actor y rango UTC.

**Precondiciones:** pagina mayor o igual a uno, tamaño entre 1 y 100 y rango de fechas coherente.

**Flujo esperado:** se aplican filtros exactos, se ordena por fecha e ID descendentes y se pagina el resultado.

**Reglas de negocio:** las fechas se interpretan en UTC; la consulta es de solo lectura.

**Resultado:** devuelve items, pagina, tamaño, total de registros y total de paginas.

**Errores esperados:** `400 Bad Request` por paginacion o fechas invalidas, `401 Unauthorized` y `403 Forbidden`.

**Que queda explicitamente fuera:** no modifica ni elimina eventos y no reemplaza los logs tecnicos de aplicacion.



### Capacidades operativas y de calidad

Estas operaciones sostienen el sistema, pero no son casos de uso iniciados por un cliente de la API.



#### Aplicar migraciones e inicializar datos base

**Objetivo:** llevar la base al esquema esperado y asegurar los datos minimos de operacion.

**Actor y permisos:** proceso de arranque con credenciales de base de datos y configuracion que habilite la inicializacion.

**Datos de entrada:** cadena de conexion, migraciones compiladas y opciones de inicializacion.

**Precondiciones:** SQL Server disponible y permisos suficientes sobre la base.

**Flujo esperado:** se aplican migraciones pendientes y se crean roles y datos iniciales requeridos de forma controlada.

**Reglas de negocio:** la inicializacion depende del ambiente y de `DatabaseInitialization:Enabled`; los secretos no deben almacenarse en el repositorio.

**Resultado:** la aplicacion inicia sobre un esquema compatible o falla explicitamente.

**Errores esperados:** error de arranque por conexion, permisos, configuracion o migracion incompatible.

**Que queda explicitamente fuera:** no reemplaza backups, restauraciones ni una estrategia de despliegue sin interrupciones.



#### Ejecutar validaciones automatizadas

**Objetivo:** detectar regresiones del backend y frontend antes de integrar cambios.

**Actor y permisos:** desarrollador local o agente de CI con acceso al codigo y dependencias.

**Datos de entrada:** codigo fuente, configuracion de pruebas y servicios externos requeridos por tests de integracion.

**Precondiciones:** SDK de .NET, Node.js, pnpm y dependencias instaladas; SQL Server cuando la suite lo requiera.

**Flujo esperado:** se compila y prueba la solucion; en frontend se ejecutan lint, typecheck, tests y build; CI repite los controles configurados.

**Reglas de negocio:** un control fallido debe bloquear la integracion; los tests no deben depender de secretos productivos.

**Resultado:** evidencia reproducible de controles aprobados o detalle del fallo.

**Errores esperados:** compilacion fallida, test fallido, lint o typecheck fallido, dependencia ausente o servicio de integracion no disponible.

**Que queda explicitamente fuera:** no demuestra ausencia total de defectos y no sustituye pruebas exploratorias, revision de seguridad ni observabilidad en produccion.
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
- PostgreSQL 17 local, en Docker o una base alojada en Neon
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
de Docker. No afecta las bases alojadas en Neon.

## Tests

```powershell
dotnet test GymShop.slnx
```

Los tests con `Category=Integration` levantan el pipeline HTTP real y verifican el comportamiento de PostgreSQL, incluidas las restricciones, transacciones y actualizaciones concurrentes mediante `xmin`. Por defecto se conectan a PostgreSQL en `localhost:5432`. Tambien se puede definir una conexion administrativa mediante `GYMSHOP_TEST_POSTGRES`; cada test crea una base aislada, aplica todas las migraciones desde cero y la elimina al terminar. La cuenta utilizada debe tener permiso para crear y eliminar bases de datos.

Ejemplo sin credenciales reales:

```powershell
$env:GYMSHOP_TEST_POSTGRES="Host=<HOST>;Port=5432;Database=postgres;Username=<USUARIO>;Password=<PASSWORD>"
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
