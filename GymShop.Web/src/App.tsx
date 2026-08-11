import { useCallback, useEffect, useState } from 'react'
import { BrowserRouter, Link, Navigate, NavLink, Route, Routes, useLocation, useNavigate } from 'react-router-dom'
import { ApiError } from './api/client'
import { api, paymentKey } from './api/gymshop'
import type { AdminUser, AuditEntry, AuthResponse, Order, OrderSummary, Payment, Product, Role, User } from './api/types'
import { session } from './auth/session'
import { AuthPanel } from './features/auth/AuthPanel'
import { Catalog } from './features/catalog/Catalog'
import { ProductDetailPage } from './features/catalog/ProductDetailPage'
import { CartDrawer } from './features/cart/CartDrawer'
import { CartProvider } from './features/cart/CartContext'
import { CartPage } from './features/cart/CartPage'
import { useCart } from './features/cart/useCart'
import { Home } from './features/home/Home'

const money = (value: number, currency = 'ARS') => new Intl.NumberFormat('es-AR', { style: 'currency', currency }).format(value)
const date = (value: string) => new Intl.DateTimeFormat('es-AR', { dateStyle: 'medium', timeStyle: 'short' }).format(new Date(value))
const roleAtLeastAdmin = (user: User | null) => user?.role === 'Admin' || user?.role === 'SuperAdmin'
const stateLabel: Record<string, string> = { Pending: 'Pendiente', Paid: 'Pagada', Shipped: 'Enviada', Canceled: 'Cancelada', Refunded: 'Reembolsada', Creating: 'Creando pago', CreationFailed: 'Falló la creación', Approved: 'Aprobado', Rejected: 'Rechazado', Expired: 'Vencido' }

function describeError(value: unknown) {
  if (!(value instanceof ApiError)) return 'Ocurrió un error inesperado.'
  const extras = [value.retryAfter ? `Reintentá en ${value.retryAfter} segundos.` : '', value.traceId ? `Referencia: ${value.traceId}` : ''].filter(Boolean)
  return [value.message, ...extras].join(' ')
}

function Empty({ children }: { children: React.ReactNode }) { return <div className="empty">{children}</div> }
function Status({ value }: { value: string | null }) { return value ? <span className={`status status-${value.toLowerCase()}`}>{stateLabel[value] || value}</span> : <span>—</span> }

export default function App() {
  return <BrowserRouter><CartProvider><AppShell /><CartDrawer /></CartProvider></BrowserRouter>
}

function AppShell() {
  const [user, setUser] = useState(session.user())
  const [notice, setNotice] = useState('')
  const [error, setError] = useState('')
  const cart = useCart()
  const navigate = useNavigate()

  const refreshSession = useCallback(() => setUser(session.user()), [])
  useEffect(() => { window.addEventListener('gymshop:session', refreshSession); return () => window.removeEventListener('gymshop:session', refreshSession) }, [refreshSession])
  const run = async (action: () => Promise<void>) => { setError(''); setNotice(''); try { await action() } catch (e) { setError(describeError(e)) } }
  const logout = () => { session.clear(); navigate('/'); setNotice('Sesión cerrada.') }

  return <div className="app">
    <header>
      <Link className="brand" to="/"><span>G</span> GymShop</Link>
      <nav aria-label="Navegación principal">
        <NavLink to="/catalogo">Catálogo</NavLink>
        {user && <NavLink to="/ordenes">Órdenes</NavLink>}
        {roleAtLeastAdmin(user) && <NavLink to="/admin/productos">Administración</NavLink>}
        {user?.role === 'SuperAdmin' && <NavLink to="/admin/usuarios">Usuarios</NavLink>}
        {user?.role === 'SuperAdmin' && <NavLink to="/admin/auditoria">Auditoría</NavLink>}
      </nav>
      <div className="account">
        {user && <small>{user.name}<br />{user.role}</small>}
        <button className="cart-button" onClick={cart.openDrawer}>Carrito <b>{cart.count}</b></button>
        {user ? <button onClick={logout}>Salir</button> : <Link className="primary link-button" to="/login">Ingresar</Link>}
      </div>
    </header>
    <main>
      {notice && <div className="notice" role="status">{notice}</div>}
      {error && <div className="error" role="alert">{error}</div>}
      <Routes>
        <Route path="/" element={<Home onCatalog={() => navigate('/catalogo')} onProduct={id => navigate(`/catalogo/${id}`)} />} />
        <Route path="/catalogo" element={<Catalog />} />
        <Route path="/catalogo/:productId" element={<ProductDetailPage />} />
        <Route path="/carrito" element={<CartPage />} />
        <Route path="/login" element={<AuthRoute user={user} onDone={auth => { session.save(auth.token, auth.user); setNotice(`Hola, ${auth.user.name}.`) }} />} />
        <Route path="/ordenes" element={user ? <OrdersView admin={roleAtLeastAdmin(user)} run={run} /> : <RequireLogin />} />
        <Route path="/admin/productos" element={roleAtLeastAdmin(user) ? <ProductsAdmin run={run} /> : <ForbiddenOrLogin user={user} />} />
        <Route path="/admin/usuarios" element={user?.role === 'SuperAdmin' ? <UsersAdmin run={run} /> : <ForbiddenOrLogin user={user} />} />
        <Route path="/admin/auditoria" element={user?.role === 'SuperAdmin' ? <Audit run={run} /> : <ForbiddenOrLogin user={user} />} />
        <Route path="*" element={<Navigate to="/" replace />} />
      </Routes>
    </main>
    <footer>GymShop · Integración local con proveedor de pagos Mock</footer>
  </div>
}

function AuthRoute({ user, onDone }: { user: User | null; onDone(auth: AuthResponse): void }) {
  const location = useLocation()
  const navigate = useNavigate()
  const state = location.state as { returnTo?: string; message?: string } | null
  if (user) return <Navigate to={state?.returnTo || '/'} replace />
  return <>{state?.message && <div className="notice" role="status">{state.message}</div>}<AuthPanel onDone={auth => { onDone(auth); navigate(state?.returnTo || '/', { replace: true }) }} /></>
}

function RequireLogin() { const location = useLocation(); return <Navigate to="/login" replace state={{ returnTo: location.pathname, message: 'Iniciá sesión para continuar.' }} /> }
function ForbiddenOrLogin({ user }: { user: User | null }) { return user ? <Empty>No tenés permisos para acceder a esta sección.</Empty> : <RequireLogin /> }

function OrdersView({ admin, run }: { admin: boolean; run: (a: () => Promise<void>) => Promise<void> }) {
  const [orders, setOrders] = useState<OrderSummary[]>([]); const [detail, setDetail] = useState<Order | null>(null); const [payments, setPayments] = useState<Payment[]>([]); const [email, setEmail] = useState('')
  const load = useCallback(() => (admin ? api.orders(email) : api.myOrders()).then(setOrders), [admin, email])
  useEffect(() => { void load() }, [load])
  const open = (id: number) => run(async () => { const [order, paymentList] = await Promise.all([api.order(id), api.orderPayments(id)]); setDetail(order); setPayments(paymentList) })
  const pay = (id: number) => run(async () => { const payment = await api.createPayment(id, paymentKey(id)); setPayments(await api.orderPayments(id)); if (payment.checkoutUrl) window.location.assign(payment.checkoutUrl) })
  return <section><div className="section-title"><div><p className="eyebrow">SEGUIMIENTO</p><h1>{admin ? 'Órdenes' : 'Mis órdenes'}</h1></div>{admin && <form onSubmit={e => { e.preventDefault(); void load() }}><input placeholder="Filtrar por email" value={email} onChange={e => setEmail(e.target.value)} /><button>Buscar</button></form>}</div>
    {orders.length === 0 ? <Empty>No hay órdenes para mostrar.</Empty> : <div className="list">{orders.map(order => <button className="order-row" key={order.id} onClick={() => void open(order.id)}><b>#{order.id}</b><span>{date(order.createdAt)}</span>{order.userEmail && <span>{order.userEmail}</span>}<strong>{money(order.total)}</strong><Status value={order.status} /><Status value={order.lastPaymentStatus} /></button>)}</div>}
    {detail && <div className="drawer"><button className="close" onClick={() => setDetail(null)}>×</button><p className="eyebrow">ORDEN #{detail.id}</p><h2>{money(detail.total)}</h2><Status value={detail.status} /><p>{detail.shippingAddress}</p><div className="list">{detail.items.map(i => <div className="list-row" key={i.productId}><span>{i.quantity} × {i.productName}</span><strong>{money(i.subtotal)}</strong></div>)}</div>
      {detail.status === 'Pending' && <div className="actions"><button className="primary" onClick={() => void pay(detail.id)}>Crear / consultar pago Mock</button><button onClick={() => void run(async () => { setDetail(await api.cancelOrder(detail.id, 'Cancelada desde el frontend')); await load() })}>Cancelar orden</button></div>}
      <h3>Pagos</h3>{payments.length === 0 ? <p>Sin pagos.</p> : payments.map(p => <div className="payment" key={p.id}><span>#{p.id} · {p.provider}</span><Status value={p.status} />{p.status === 'Creating' && !p.checkoutUrl && <small>El pago se está creando. Consultá nuevamente en unos instantes.</small>}{p.failureReason && <small>{p.failureReason}</small>}<button onClick={() => void run(async () => setPayments(await api.orderPayments(detail.id)))}>Actualizar</button></div>)}
    </div>}
  </section>
}

function ProductsAdmin({ run }: { run: (a: () => Promise<void>) => Promise<void> }) {
  const [products, setProducts] = useState<Product[]>([]); const load = useCallback(() => api.products(true).then(setProducts), []); useEffect(() => { void load() }, [load])
  return <section><div className="section-title"><div><p className="eyebrow">ADMIN</p><h1>Productos</h1></div></div><div className="list">{products.map(p => <div className="list-row" key={p.id}><div><h3>{p.name}</h3><p>{money(p.price)} · {p.isActive ? 'Activo' : 'Inactivo'}</p></div><label>Stock<input type="number" min="0" defaultValue={p.stock} onBlur={e => void run(async () => { await api.setProductStock(p.id, Number(e.target.value)); await load() })} /></label><button onClick={() => void run(async () => { await api.setProductStatus(p.id, !p.isActive); await load() })}>{p.isActive ? 'Desactivar' : 'Activar'}</button></div>)}</div></section>
}

function UsersAdmin({ run }: { run: (a: () => Promise<void>) => Promise<void> }) {
  const [users, setUsers] = useState<AdminUser[]>([]); const load = useCallback(() => api.users().then(setUsers), []); useEffect(() => { void load() }, [load])
  return <section><div className="section-title"><div><p className="eyebrow">SUPERADMIN</p><h1>Usuarios y roles</h1></div></div><div className="list">{users.map(u => <div className="list-row" key={u.id}><div><h3>{u.name}</h3><p>{u.email}</p></div><select aria-label={`Rol de ${u.name}`} value={u.role} onChange={e => void run(async () => { await api.setUserRole(u.id, e.target.value as Role); await load() })}><option>User</option><option>Admin</option><option>SuperAdmin</option></select><button onClick={() => void run(async () => { await api.setUserStatus(u.id, !u.isActive); await load() })}>{u.isActive ? 'Desactivar' : 'Activar'}</button></div>)}</div></section>
}

function Audit({ run }: { run: (a: () => Promise<void>) => Promise<void> }) {
  const [entries, setEntries] = useState<AuditEntry[]>([]); useEffect(() => { void run(async () => setEntries((await api.audit()).items)) }, [])
  return <section><div className="section-title"><div><p className="eyebrow">SUPERADMIN</p><h1>Auditoría</h1></div></div><div className="list">{entries.map(e => <div className="list-row" key={e.id}><div><h3>{e.action}</h3><p>{e.entityType} #{e.entityId} · {date(e.createdAtUtc)}</p></div><small>{e.correlationId}</small></div>)}</div></section>
}
