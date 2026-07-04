# Documentacion inicial para migracion

Proyecto analizado: `C:\Users\HP\Desktop\Ctrl\repo e commerce\UTN-gimnasio-ecommerce-tp`

Objetivo: relevar el estado actual del e-commerce de gimnasio desarrollado con React JavaScript y Node/Express para preparar una migracion futura a TypeScript y .NET.

## 1. Resumen ejecutivo

La aplicacion es un e-commerce simple de productos de gimnasio. Tiene un frontend en React con Vite y Bootstrap, y un backend en Node.js con Express 5, SQLite, JWT y bcrypt.

El sistema permite:

- Ver productos publicos.
- Agregar productos a un carrito local.
- Registrarse e iniciar sesion.
- Confirmar pedidos como usuario autenticado.
- Ver pedidos propios.
- Administrar productos y ordenes como `admin` o `super-admin`.
- Administrar usuarios solo como `super-admin`.

La base de datos actual es SQLite y se encuentra en `backend/src/data/gym.db`.

## 2. Arquitectura actual

```text
UTN-gimnasio-ecommerce-tp
├── backend
│   ├── package.json
│   └── src
│       ├── app.js
│       ├── server.js
│       ├── config
│       │   ├── db.js
│       │   └── env.js
│       ├── middleware
│       │   ├── authRequired.js
│       │   └── roleRequired.js
│       ├── models
│       │   ├── Product.js
│       │   └── Order.js
│       ├── routes
│       │   ├── auth.routes.js
│       │   ├── orders.routes.js
│       │   ├── products.routes.js
│       │   └── users.routes.js
│       └── public/images
└── frontend
    ├── package.json
    ├── vite.config.js
    └── src
        ├── App.jsx
        ├── main.jsx
        ├── components
        ├── context
        ├── routes
        └── services
```

## 3. Backend actual

Tecnologias:

- Node.js con ES Modules.
- Express 5.1.
- SQLite mediante `sqlite3`.
- JWT mediante `jsonwebtoken`.
- Hash de passwords con `bcrypt`.
- CORS habilitado para origen dinamico.

Scripts:

- `npm start`: ejecuta `node src/server.js`.
- `npm run dev`: ejecuta `nodemon src/server.js`.

El servidor escucha en `process.env.PORT` o `4000` por defecto.

### Configuracion principal

`backend/src/app.js`:

- Configura CORS.
- Habilita JSON request bodies con `express.json()`.
- Sirve archivos estaticos desde `src/public`.
- Sirve imagenes desde `/images`.
- Monta rutas:
  - `/auth`
  - `/products`
  - `/orders`
  - `/users`

`backend/src/config/db.js`:

- Abre SQLite en `backend/src/data/gym.db`.
- Exporta helpers promisificados:
  - `run(sql, params)`
  - `get(sql, params)`
  - `all(sql, params)`

`backend/src/config/env.js`:

- Define `JWT_SECRET = 'dev_secret'`.
- Para produccion o migracion debe moverse a variables de entorno.

## 4. Seguridad y roles

Autenticacion:

- JWT enviado por header `Authorization: Bearer <token>`.
- El token se genera al registrar o iniciar sesion.
- Expiracion: 7 dias.

Roles:

- En base de datos se guardan como numeros en `Usuarios.Nivel`.
- Mapeo actual:
  - `1`: `user`
  - `2`: `admin`
  - `3`: `super-admin`

Jerarquia:

- `super-admin` puede acceder a permisos de `super-admin`, `admin` y `user`.
- `admin` puede acceder a permisos de `admin` y `user`.
- `user` solo puede acceder a permisos de `user`.

Observacion importante:

- El backend valida roles correctamente mediante `authRequired` y `roleRequired`.
- El frontend envia `roles` a `ProtectedRoute`, pero `ProtectedRoute.jsx` actualmente no usa esa prop. Por lo tanto, la proteccion real por rol depende del backend y de la visibilidad condicional del navbar.

## 5. Base de datos actual

Esquema SQLite confirmado:

```sql
CREATE TABLE Usuarios (
  Id INTEGER PRIMARY KEY,
  Nombre TEXT NOT NULL,
  Apellido TEXT NOT NULL,
  Telefono INTEGER NOT NULL,
  Direccion TEXT NOT NULL,
  Nivel INTEGER NOT NULL CHECK (Nivel > 0 AND Nivel < 4),
  Email TEXT,
  Password TEXT
);

CREATE UNIQUE INDEX ux_Usuarios_Email ON Usuarios(Email);

CREATE TABLE Productos (
  ProdId INTEGER PRIMARY KEY,
  Nombre TEXT NOT NULL,
  Descripcion TEXT NOT NULL,
  Precio INTEGER NOT NULL CHECK (Precio > 0),
  Stock INTEGER NOT NULL,
  ImageUrl TEXT
);

CREATE TABLE Pedidos (
  Id INTEGER PRIMARY KEY,
  Fecha TEXT NOT NULL,
  Monto REAL NOT NULL,
  UsuarioId INTEGER NOT NULL,
  Status TEXT DEFAULT 'pending',
  FOREIGN KEY (UsuarioId) REFERENCES Usuarios(Id)
);

CREATE TABLE DetalleCompras (
  DetalleId INTEGER PRIMARY KEY AUTOINCREMENT,
  CompraId INTEGER NOT NULL,
  ProdId INTEGER NOT NULL,
  Cantidad INTEGER NOT NULL,
  FOREIGN KEY(CompraId) REFERENCES Pedidos(Id) ON DELETE CASCADE ON UPDATE CASCADE,
  FOREIGN KEY(ProdId) REFERENCES Productos(ProdId) ON DELETE CASCADE ON UPDATE CASCADE
);
```

Observacion importante:

- El checkout actual crea un registro en `Pedidos` y descuenta stock, pero no inserta registros en `DetalleCompras`.
- El frontend envia items del carrito, pero la orden persistida solo conserva total, usuario, fecha y estado.
- Para migrar a .NET conviene decidir si `DetalleCompras` va a usarse realmente como detalle de pedido.

## 6. Endpoints actuales

Base URL usada por el frontend:

```text
http://127.0.0.1:4000
```

### Auth

`POST /auth/register`

- Body:
  - `email`
  - `password`
  - `name`
  - `role` opcional, por defecto `user`
- Crea usuario con password hasheada.
- Devuelve:
  - `token`
  - `user`

`POST /auth/login`

- Body:
  - `email`
  - `password`
- Valida password con bcrypt.
- Devuelve:
  - `token`
  - `user`

`GET /auth/whoami`

- Requiere JWT.
- Devuelve usuario actual.

### Products

`GET /products`

- Publico.
- Lista productos.

`POST /products`

- Requiere `admin` o `super-admin`.
- Crea producto.

`PUT /products/:id`

- Requiere `admin` o `super-admin`.
- Actualiza producto completo.

`DELETE /products/:id`

- Requiere `admin` o `super-admin`.
- Elimina producto.

### Orders

`POST /orders`

- Requiere usuario autenticado.
- Body:
  - `address`
  - `items`: lista de `{ product_id, qty, price }`
- Valida direccion, carrito, existencia de productos y stock.
- Calcula total en backend usando precios actuales de DB.
- Inserta pedido con estado `pending`.
- Descuenta stock.
- No inserta detalle de compra.

`GET /orders/mine`

- Requiere usuario autenticado.
- Lista pedidos del usuario actual.

`GET /orders`

- Requiere `admin` o `super-admin`.
- Lista todos los pedidos con email de usuario.

`PUT /orders/:id/status`

- Requiere `admin` o `super-admin`.
- Estados permitidos:
  - `pending`
  - `paid`
  - `shipped`
  - `canceled`

`DELETE /orders/:id`

- Requiere `admin` o `super-admin`.
- Borra detalle en `DetalleCompras` y luego pedido.

### Users

`GET /users`

- Requiere `super-admin`.
- Lista usuarios.

`POST /users`

- Requiere `super-admin`.
- Crea usuario con rol elegido.

`PUT /users/:id/role`

- Requiere `super-admin`.
- Cambia rol.

`DELETE /users/:id`

- Requiere `super-admin`.
- Elimina usuario.

## 7. Frontend actual

Tecnologias:

- Vite.
- React 19.
- React Router 7.
- Bootstrap 5.

Scripts:

- `npm run dev`
- `npm run build`
- `npm run lint`
- `npm run preview`

### Rutas frontend

```text
/                  Home / catalogo
/carrito           carrito
/checkout          checkout autenticado
/mis-ordenes       pedidos del usuario autenticado
/login             login
/register          registro
/admin/productos   ABM productos
/admin/ordenes     administracion de ordenes
/admin/usuarios    ABM usuarios super-admin
```

### Servicios frontend

`src/services/api.js`:

- Define `API_URL = 'http://127.0.0.1:4000'`.
- Agrega `Content-Type: application/json`.
- Si existe token en `localStorage`, agrega `Authorization: Bearer <token>`.
- Centraliza manejo de errores HTTP.

`src/services/cart.js`:

- Guarda carrito en `localStorage`.
- Claves:
  - `cart:anon` para usuario anonimo.
  - `cart:u:<id>` para usuario autenticado.
  - `currentUserId` para asociar carrito a usuario.
- Al iniciar sesion migra carrito anonimo al usuario si no tiene carrito previo.

`src/context/AuthContext.jsx`:

- Mantiene `user` en estado.
- Persiste token en `localStorage`.
- Al montar intenta autologin con `/auth/whoami`.
- Expone:
  - `login`
  - `register`
  - `logout`
  - `hasRole`

## 8. Flujos principales

### Registro

1. Usuario completa nombre, email y password.
2. Frontend valida nombre minimo, email con `@` y password minima de 6 caracteres.
3. `AuthContext.register` llama a `POST /auth/register`.
4. Backend crea usuario con `Nivel = 1` salvo que se envie otro rol.
5. Backend devuelve JWT y usuario.
6. Frontend guarda token y usuario, y migra carrito anonimo si corresponde.

### Login

1. Usuario ingresa email y password.
2. Frontend llama a `POST /auth/login`.
3. Backend busca usuario por email y compara bcrypt.
4. Backend devuelve JWT y usuario.
5. Frontend guarda token y actualiza contexto.

### Compra

1. Usuario agrega productos al carrito desde `/`.
2. Carrito se guarda en `localStorage`.
3. Usuario va a `/checkout`.
4. Frontend envia `address` e items a `POST /orders`.
5. Backend valida productos y stock.
6. Backend calcula total desde la base.
7. Backend crea pedido y descuenta stock.
8. Frontend limpia carrito y redirige a `/mis-ordenes`.

### Administracion

Productos:

- Admin y super-admin pueden crear, editar y eliminar productos.

Ordenes:

- Admin y super-admin pueden listar ordenes, cambiar estado y borrar ordenes.

Usuarios:

- Solo super-admin puede listar, crear, cambiar roles y eliminar usuarios.

## 9. Riesgos y puntos a corregir antes o durante la migracion

1. `JWT_SECRET` esta hardcodeado como `dev_secret`.
2. `ProtectedRoute` no aplica roles aunque `App.jsx` se los pase.
3. `AuthContext` no expone `loading`, pero `ProtectedRoute` intenta leerlo.
4. La creacion de pedidos no persiste items en `DetalleCompras`.
5. `Order.js` contiene logica de modelo que no coincide completamente con `orders.routes.js` y parece no usarse para checkout.
6. El frontend tiene `API_URL` hardcodeada.
7. Hay textos con problemas de encoding en algunos archivos (`Ã³`, `Â¿`, etc.).
8. `Telefono` y `Direccion` son `NOT NULL`; el registro los completa con valores vacios.
9. `Precio` en DB es `INTEGER`, pero el frontend y backend manejan decimales.
10. No se observaron tests automatizados.

## 10. Propuesta de migracion a TypeScript y .NET

### Frontend a TypeScript

Convertir gradualmente:

1. `api.js` a `api.ts`.
2. `cart.js` a `cart.ts`.
3. `AuthContext.jsx` a `AuthContext.tsx`.
4. Componentes de rutas a `.tsx`.

Tipos recomendados:

```ts
type Role = 'user' | 'admin' | 'super-admin';

interface User {
  id: number;
  email: string;
  name?: string;
  role: Role;
}

interface Product {
  id: number;
  name: string;
  description: string;
  price: number;
  stock: number;
  image_url?: string;
}

interface CartItem {
  id: number;
  name: string;
  price: number;
  qty: number;
}

type OrderStatus = 'pending' | 'paid' | 'shipped' | 'canceled';

interface Order {
  id: number;
  date: string;
  total: number;
  status: OrderStatus;
  user_id?: number;
  user_email?: string;
}
```

### Backend a .NET

Arquitectura sugerida:

```text
GymShop.Api
├── Controllers
│   ├── AuthController.cs
│   ├── ProductsController.cs
│   ├── OrdersController.cs
│   └── UsersController.cs
├── Domain
│   ├── Entities
│   └── Enums
├── Infrastructure
│   ├── Data
│   └── Repositories
├── Application
│   ├── DTOs
│   └── Services
└── Program.cs
```

Tecnologias sugeridas:

- ASP.NET Core Web API.
- Entity Framework Core.
- SQLite inicialmente para conservar compatibilidad.
- JWT Bearer Authentication.
- BCrypt.Net para passwords o ASP.NET Core Identity si se decide formalizar usuarios.
- Swagger/OpenAPI para documentar endpoints.

Mapeo de endpoints:

```text
POST   /auth/register       -> AuthController.Register
POST   /auth/login          -> AuthController.Login
GET    /auth/whoami         -> AuthController.WhoAmI

GET    /products            -> ProductsController.GetAll
POST   /products            -> ProductsController.Create
PUT    /products/{id}       -> ProductsController.Update
DELETE /products/{id}       -> ProductsController.Delete

POST   /orders              -> OrdersController.Create
GET    /orders/mine         -> OrdersController.GetMine
GET    /orders              -> OrdersController.GetAll
PUT    /orders/{id}/status  -> OrdersController.UpdateStatus
DELETE /orders/{id}         -> OrdersController.Delete

GET    /users               -> UsersController.GetAll
POST   /users               -> UsersController.Create
PUT    /users/{id}/role     -> UsersController.UpdateRole
DELETE /users/{id}          -> UsersController.Delete
```

## 11. Decisiones pendientes

- Mantener SQLite o pasar a SQL Server/PostgreSQL.
- Usar ASP.NET Core Identity o mantener modelo simple de usuarios.
- Persistir detalle de pedido en `DetalleCompras`.
- Cambiar `Precio` a decimal real.
- Definir si las imagenes seguiran como archivos estaticos o pasaran a storage externo.
- Definir estrategia de variables de entorno para frontend y backend.
- Agregar tests antes de migrar o durante la migracion.

