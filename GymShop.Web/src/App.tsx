import { useCallback, useEffect, useState } from 'react'
import { BrowserRouter, Link, Navigate, NavLink, Route, Routes, useLocation, useNavigate } from 'react-router-dom'
import type { AuthResponse, User } from './api/types'
import { session } from './auth/session'
import { storefront } from './config/storefront'
import { AdminDashboard } from './features/admin/AdminDashboard'
import { AdminLayout } from './features/admin/AdminLayout'
import { AdminRoute } from './features/admin/AdminRoute'
import { AuditAdmin } from './features/admin/AuditAdmin'
import { CategoriesAdmin } from './features/admin/CategoriesAdmin'
import { CategoryEditorPage } from './features/admin/CategoryEditorPage'
import { isAdmin } from './features/admin/adminConfig'
import { ProductsAdmin } from './features/admin/ProductsAdmin'
import { ProductEditorPage } from './features/admin/ProductEditorPage'
import { UsersAdmin } from './features/admin/UsersAdmin'
import { StockAdmin } from './features/admin/StockAdmin'
import { AuthPanel } from './features/auth/AuthPanel'
import { CartDrawer } from './features/cart/CartDrawer'
import { CartProvider } from './features/cart/CartContext'
import { CartPage } from './features/cart/CartPage'
import { useCart } from './features/cart/useCart'
import { Catalog } from './features/catalog/Catalog'
import { ProductDetailPage } from './features/catalog/ProductDetailPage'
import { CheckoutPage } from './features/checkout/CheckoutPage'
import { CheckoutResultPage } from './features/checkout/CheckoutResultPage'
import { Home } from './features/home/Home'
import { OrdersView } from './features/orders/OrdersView'

export default function App() {
  return <BrowserRouter><CartProvider><AppShell /><CartDrawer /></CartProvider></BrowserRouter>
}

function AppShell() {
  const [user, setUser] = useState(session.user())
  const [notice, setNotice] = useState('')
  const cart = useCart()
  const navigate = useNavigate()
  const location = useLocation()
  const refreshSession = useCallback(() => setUser(session.user()), [])
  useEffect(() => { window.addEventListener('gymshop:session', refreshSession); return () => window.removeEventListener('gymshop:session', refreshSession) }, [refreshSession])
  const logout = () => { session.clear(); navigate('/'); setNotice('Sesión cerrada.') }
  const adminArea = location.pathname === '/admin' || location.pathname.startsWith('/admin/')

  return <div className={adminArea ? 'app admin-app' : 'app'}>
    {!adminArea && <StorefrontHeader user={user} onLogout={logout} onCart={cart.openDrawer} cartCount={cart.count} />}
    {!adminArea && <main>{notice && <div className="notice" role="status">{notice}</div>}<StorefrontRoutes user={user} onAuth={auth => { session.save(auth.token, auth.user); setNotice(`Hola, ${auth.user.name}.`) }} /></main>}
    {adminArea && <AdminRoutes user={user} onLogout={logout} />}
    {!adminArea && <footer>{storefront.copy.footer}</footer>}
  </div>
}

function StorefrontHeader({ user, onLogout, onCart, cartCount }: { user: User | null; onLogout(): void; onCart(): void; cartCount: number }) {
  return <header><Link className="brand" to="/">{storefront.identity.logoUrl ? <img src={storefront.identity.logoUrl} alt="" /> : <span>{storefront.identity.monogram}</span>} {storefront.identity.name}</Link><nav aria-label="Navegación principal"><NavLink to="/catalogo">{storefront.copy.catalogNav}</NavLink>{user && <NavLink to="/ordenes">Órdenes</NavLink>}{isAdmin(user) && <NavLink to="/admin">Administración</NavLink>}</nav><div className="account">{user && <small>{user.name}<br />{user.role}</small>}<button className="cart-button" onClick={onCart}>Carrito <b>{cartCount}</b></button>{user ? <button onClick={onLogout}>Salir</button> : <Link className="primary link-button" to="/login">Ingresar</Link>}</div></header>
}

function StorefrontRoutes({ user, onAuth }: { user: User | null; onAuth(auth: AuthResponse): void }) {
  const navigate = useNavigate()
  return <Routes>
    <Route path="/" element={<Home onCatalog={category => navigate(category ? `/catalogo?categoria=${encodeURIComponent(category)}` : '/catalogo')} onProduct={id => navigate(`/catalogo/${id}`)} />} />
    <Route path="/catalogo" element={<Catalog />} /><Route path="/catalogo/:productId" element={<ProductDetailPage />} /><Route path="/carrito" element={<CartPage />} />
    <Route path="/checkout" element={user ? <CheckoutPage /> : <RequireLogin />} /><Route path="/checkout/orden/:orderId" element={user ? <CheckoutResultPage canRefreshPayment={isAdmin(user)} /> : <RequireLogin />} />
    <Route path="/login" element={<AuthRoute user={user} onDone={onAuth} />} /><Route path="/ordenes" element={user ? <OrdersView /> : <RequireLogin />} />
    <Route path="*" element={<Navigate to="/" replace />} />
  </Routes>
}

function AdminRoutes({ user, onLogout }: { user: User | null; onLogout(): void }) {
  return <Routes><Route path="/admin" element={<AdminRoute user={user}>{user && <AdminLayout user={user} onLogout={onLogout} />}</AdminRoute>}>
    <Route index element={<AdminDashboard />} /><Route path="productos" element={<ProductsAdmin />} /><Route path="stock" element={<StockAdmin />} /><Route path="productos/nuevo" element={<ProductEditorPage mode="create" />} /><Route path="productos/:productId/editar" element={<ProductEditorPage mode="edit" />} /><Route path="categorias" element={<CategoriesAdmin />} /><Route path="categorias/nueva" element={<CategoryEditorPage mode="create" />} /><Route path="categorias/:categoryId/editar" element={<CategoryEditorPage mode="edit" />} /><Route path="pedidos" element={<OrdersView admin />} />
    <Route path="usuarios" element={<AdminRoute user={user} superAdmin><UsersAdmin /></AdminRoute>} /><Route path="auditoria" element={<AdminRoute user={user} superAdmin><AuditAdmin /></AdminRoute>} />
  </Route><Route path="*" element={<Navigate to="/admin" replace />} /></Routes>
}

function AuthRoute({ user, onDone }: { user: User | null; onDone(auth: AuthResponse): void }) {
  const location = useLocation(); const navigate = useNavigate(); const state = location.state as { returnTo?: string; message?: string } | null
  if (user) return <Navigate to={state?.returnTo || '/'} replace />
  return <>{state?.message && <div className="notice" role="status">{state.message}</div>}<AuthPanel onDone={auth => { onDone(auth); navigate(state?.returnTo || '/', { replace: true }) }} /></>
}

function RequireLogin() { const location = useLocation(); return <Navigate to="/login" replace state={{ returnTo: `${location.pathname}${location.search}`, message: 'Iniciá sesión para continuar.' }} /> }
