import { render, screen, waitFor, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { beforeEach, describe, expect, it, vi } from 'vitest'
import App from '../../App'

const response = (body: unknown, status = 200) => Promise.resolve(new Response(JSON.stringify(body), { status, headers: { 'Content-Type': 'application/json' } }))
const products = [
  { id: 1, name: 'Mancuerna 10kg', description: null, price: 15000, stock: 3, imageUrl: null, isActive: true, category: { id: 1, name: 'Fuerza', slug: 'fuerza' } },
  { id: 2, name: 'Colchoneta', description: null, price: 9000, stock: 0, imageUrl: null, isActive: false, category: { id: 2, name: 'Movilidad', slug: 'movilidad' } },
]
const orders = [{ id: 10, userId: 7, userEmail: 'cliente@gym.com', userName: 'Cliente Gym', createdAt: '2026-09-20T12:00:00Z', updatedAt: null, total: 15000, status: 'Pending', lastPaymentStatus: null, lastPaymentId: null }]
const orderPage = { items: orders, page: 1, pageSize: 20, totalItems: 1, totalPages: 1 }
const orderDetail = { ...orders[0], userPhone: null, shippingAddress: 'Av. Siempre Viva 742', cancellationReason: null, items: [{ productId: 1, productName: 'Mancuerna 10kg', unitPrice: 15000, quantity: 1, subtotal: 15000 }], payments: [] }

function signIn(role: 'User' | 'Admin' | 'SuperAdmin') {
  localStorage.setItem('gymshop.token', 'jwt')
  localStorage.setItem('gymshop.user', JSON.stringify({ id: 1, email: `${role.toLowerCase()}@gym.com`, name: role, role }))
}

function apiMock(input: RequestInfo | URL, init?: RequestInit) {
  const url = String(input)
  if (url.includes('/api/cart')) return response({ id: 1, userId: 1, total: 0, items: [] })
  if (url.includes('/api/products')) return init?.method === 'PATCH' ? response(null) : response(products)
  if (/\/api\/orders\/10$/.test(url)) return response(orderDetail)
  if (url.includes('/api/orders')) return init?.method === 'PATCH' ? Promise.resolve(new Response(null, { status: 204 })) : response(orderPage)
  if (url.includes('/api/users')) return response([])
  if (url.includes('/api/audit')) return response({ items: [], page: 1, pageSize: 50, totalItems: 0, totalPages: 0 })
  return response([])
}

const productRequests = (calls: readonly (readonly unknown[])[]) => calls.filter(([input]) => String(input).includes('/api/products'))
const productMutations = (calls: readonly (readonly unknown[])[]) => productRequests(calls).filter(([, init]) => (init as RequestInit | undefined)?.method === 'PATCH')

describe('panel administrativo', () => {
  beforeEach(() => { localStorage.clear(); window.history.replaceState(null, '', '/'); vi.restoreAllMocks() })

  it('redirige visitantes al login conservando la ruta administrativa', async () => {
    window.history.replaceState(null, '', '/admin/productos?estado=activo')
    vi.spyOn(globalThis, 'fetch').mockImplementation(apiMock)
    render(<App />)
    expect(await screen.findByRole('status')).toHaveTextContent('Iniciá sesión para continuar.')
    expect(window.location.pathname).toBe('/login')
    expect(window.history.state.usr.returnTo).toBe('/admin/productos?estado=activo')
  })

  it('deniega el panel a un usuario común', async () => {
    signIn('User'); window.history.replaceState(null, '', '/admin/productos')
    vi.spyOn(globalThis, 'fetch').mockImplementation(apiMock)
    render(<App />)
    expect(await screen.findByText('No tenés permisos para acceder a esta sección.')).toBeInTheDocument()
  })

  it('da a Admin acceso a resumen, productos y pedidos, pero no muestra secciones de SuperAdmin', async () => {
    signIn('Admin'); window.history.replaceState(null, '', '/admin')
    vi.spyOn(globalThis, 'fetch').mockImplementation(apiMock)
    render(<App />)
    expect(await screen.findByText('Total de productos')).toBeInTheDocument()
    expect(screen.getByText('2', { selector: '.admin-stats strong' })).toBeInTheDocument()
    const navigation = screen.getByRole('navigation', { name: 'Navegación administrativa' })
    expect(within(navigation).getByRole('link', { name: 'Productos' })).toBeInTheDocument()
    expect(within(navigation).getByRole('link', { name: 'Pedidos' })).toBeInTheDocument()
    expect(within(navigation).queryByRole('link', { name: 'Usuarios' })).not.toBeInTheDocument()
    expect(within(navigation).queryByRole('link', { name: 'Auditoría' })).not.toBeInTheDocument()
  })

  it('bloquea usuarios y auditoría para Admin aunque conozca la URL', async () => {
    signIn('Admin'); window.history.replaceState(null, '', '/admin/usuarios')
    vi.spyOn(globalThis, 'fetch').mockImplementation(apiMock)
    render(<App />)
    expect(await screen.findByText('No tenés permisos para acceder a esta sección.')).toBeInTheDocument()
    expect(fetch).not.toHaveBeenCalledWith(expect.stringContaining('/api/users'), expect.anything())
  })

  it('muestra navegación completa a SuperAdmin', async () => {
    signIn('SuperAdmin'); window.history.replaceState(null, '', '/admin')
    vi.spyOn(globalThis, 'fetch').mockImplementation(apiMock)
    render(<App />)
    const navigation = await screen.findByRole('navigation', { name: 'Navegación administrativa' })
    expect(within(navigation).getByRole('link', { name: 'Usuarios' })).toBeInTheDocument()
    expect(within(navigation).getByRole('link', { name: 'Auditoría' })).toBeInTheDocument()
  })

  it('consulta pedidos con búsqueda y filtros solo al aplicar', async () => {
    signIn('Admin'); window.history.replaceState(null, '', '/admin/pedidos')
    const fetchMock = vi.spyOn(globalThis, 'fetch').mockImplementation(apiMock)
    render(<App />)
    await screen.findByText('#10')
    const orderCalls = () => fetchMock.mock.calls.filter(([input]) => String(input).includes('/api/orders'))
    expect(orderCalls()).toHaveLength(1)
    await userEvent.type(screen.getByRole('searchbox', { name: 'Buscar' }), 'cliente@gym.com')
    await userEvent.selectOptions(screen.getByLabelText('Estado'), 'Paid')
    expect(orderCalls()).toHaveLength(1)
    await userEvent.click(screen.getByRole('button', { name: 'Aplicar' }))
    await waitFor(() => expect(orderCalls()).toHaveLength(2))
    expect(String(orderCalls()[1][0])).toContain('search=cliente%40gym.com')
    expect(String(orderCalls()[1][0])).toContain('status=Paid')
    await userEvent.click(screen.getByRole('button', { name: 'Limpiar' }))
    await waitFor(() => expect(orderCalls()).toHaveLength(3))
    expect(String(orderCalls()[2][0])).toContain('page=1')
  })

  it('muestra el detalle y confirma un cambio de estado permitido', async () => {
    signIn('Admin'); window.history.replaceState(null, '', '/admin/pedidos')
    const fetchMock = vi.spyOn(globalThis, 'fetch').mockImplementation(apiMock)
    vi.spyOn(window, 'confirm').mockReturnValue(true)
    render(<App />)
    await userEvent.click(await screen.findByText('#10'))
    expect(await screen.findByRole('heading', { name: /15\.000,00/ })).toBeInTheDocument()
    await userEvent.click(screen.getByRole('button', { name: 'Marcar como cancelado' }))
    await waitFor(() => expect(fetchMock).toHaveBeenCalledWith(expect.stringContaining('/api/orders/10/status'), expect.objectContaining({ method: 'PATCH' })))
    expect(window.confirm).toHaveBeenCalled()
  })

  it('muestra errores de API del listado de pedidos y permite reintentar', async () => {
    signIn('Admin'); window.history.replaceState(null, '', '/admin/pedidos')
    let loads = 0
    vi.spyOn(globalThis, 'fetch').mockImplementation((input, init) => String(input).includes('/api/orders?') && ++loads === 1 ? response({ message: 'No se pudieron cargar los pedidos.' }, 500) : apiMock(input, init))
    render(<App />)
    expect(await screen.findByRole('alert')).toHaveTextContent('No se pudieron cargar los pedidos.')
    await userEvent.click(screen.getByRole('button', { name: 'Reintentar' }))
    expect(await screen.findByText('#10')).toBeInTheDocument()
  })

  it('busca, filtra y distingue lista vacía de ausencia de resultados', async () => {
    signIn('Admin'); window.history.replaceState(null, '', '/admin/productos')
    vi.spyOn(globalThis, 'fetch').mockImplementation(apiMock)
    render(<App />)
    expect(await screen.findByText('Mancuerna 10kg')).toBeInTheDocument()
    await userEvent.type(screen.getByRole('searchbox', { name: 'Buscar por nombre' }), 'colch')
    expect(screen.queryByText('Mancuerna 10kg')).not.toBeInTheDocument()
    expect(screen.getByText('Colchoneta')).toBeInTheDocument()
    await userEvent.clear(screen.getByRole('searchbox', { name: 'Buscar por nombre' }))
    await userEvent.selectOptions(screen.getByLabelText('Categoría'), 'Fuerza')
    expect(screen.getByText('Mancuerna 10kg')).toBeInTheDocument()
    await userEvent.type(screen.getByRole('searchbox', { name: 'Buscar por nombre' }), 'inexistente')
    expect(screen.getByText(/No hay productos que coincidan/)).toBeInTheDocument()
  })

  it('muestra un estado vacío cuando todavía no existen productos', async () => {
    signIn('Admin'); window.history.replaceState(null, '', '/admin/productos')
    vi.spyOn(globalThis, 'fetch').mockImplementation((input, init) => String(input).includes('/api/products') ? response([]) : apiMock(input, init))
    render(<App />)
    expect(await screen.findByText('No hay productos cargados.')).toBeInTheDocument()
  })

  it('muestra el error de carga inicial y permite reintentar sin confundirlo con una lista vacía', async () => {
    signIn('Admin'); window.history.replaceState(null, '', '/admin/productos')
    let productLoads = 0
    vi.spyOn(globalThis, 'fetch').mockImplementation((input, init) => {
      if (String(input).includes('/api/products') && init?.method !== 'PATCH') return ++productLoads === 1 ? response({ message: 'No se pudo cargar productos.' }, 500) : response(products)
      return apiMock(input, init)
    })
    render(<App />)
    expect(await screen.findByRole('alert')).toHaveTextContent('No se pudo cargar productos.')
    expect(screen.queryByText('No hay productos cargados.')).not.toBeInTheDocument()
    await userEvent.click(screen.getByRole('button', { name: 'Reintentar' }))
    expect(await screen.findByText('Mancuerna 10kg')).toBeInTheDocument()
    expect(screen.queryByRole('alert')).not.toBeInTheDocument()
    expect(productLoads).toBe(2)
  })

  it('muestra éxito únicamente cuando la mutación y la recarga de productos funcionan', async () => {
    signIn('Admin'); window.history.replaceState(null, '', '/admin/productos')
    const fetchMock = vi.spyOn(globalThis, 'fetch').mockImplementation(apiMock)
    render(<App />)
    const stock = await screen.findByRole('spinbutton', { name: 'Stock de Mancuerna 10kg' })
    await userEvent.clear(stock); await userEvent.type(stock, '8'); await userEvent.tab()
    expect(await screen.findByRole('status')).toHaveTextContent('Stock de Mancuerna 10kg actualizado a 8.')
    expect(screen.queryByRole('alert')).not.toBeInTheDocument()
    expect(productMutations(fetchMock.mock.calls)).toHaveLength(1)
    expect(productRequests(fetchMock.mock.calls)).toHaveLength(3)
  })

  it('informa una mutación fallida y no recarga el listado', async () => {
    signIn('Admin'); window.history.replaceState(null, '', '/admin/productos')
    const fetchMock = vi.spyOn(globalThis, 'fetch').mockImplementation((input, init) => String(input).includes('/stock') && init?.method === 'PATCH' ? response({ message: 'No se pudo actualizar el stock.' }, 500) : apiMock(input, init))
    render(<App />)
    const stock = await screen.findByRole('spinbutton', { name: 'Stock de Mancuerna 10kg' })
    await userEvent.clear(stock); await userEvent.type(stock, '8'); await userEvent.tab()
    expect(await screen.findByRole('alert')).toHaveTextContent('No se pudo actualizar el stock.')
    expect(screen.queryByRole('status')).not.toBeInTheDocument()
    expect(productMutations(fetchMock.mock.calls)).toHaveLength(1)
    expect(productRequests(fetchMock.mock.calls)).toHaveLength(2)
  })

  it('advierte cuando la mutación funciona pero falla la recarga del listado', async () => {
    signIn('Admin'); window.history.replaceState(null, '', '/admin/productos')
    let productLoads = 0
    const fetchMock = vi.spyOn(globalThis, 'fetch').mockImplementation((input, init) => {
      if (String(input).includes('/api/products') && init?.method !== 'PATCH') return ++productLoads === 1 ? response(products) : response({ message: 'Falló la recarga.' }, 500)
      return apiMock(input, init)
    })
    render(<App />)
    const stock = await screen.findByRole('spinbutton', { name: 'Stock de Mancuerna 10kg' })
    await userEvent.clear(stock); await userEvent.type(stock, '8'); await userEvent.tab()
    expect(await screen.findByRole('alert')).toHaveTextContent('El cambio pudo realizarse, pero no se pudo actualizar el listado. Falló la recarga.')
    expect(screen.queryByRole('status')).not.toBeInTheDocument()
    expect(productMutations(fetchMock.mock.calls)).toHaveLength(1)
    expect(productLoads).toBe(2)
  })

  it.each([
    ['vacío', '', '3'],
    ['negativo', '-1', '3'],
    ['decimal', '2.5', '3'],
    ['sin cambios', '3', '3'],
  ])('no actualiza stock ante un valor %s', async (_case, value, restoredValue) => {
    signIn('Admin'); window.history.replaceState(null, '', '/admin/productos')
    const fetchMock = vi.spyOn(globalThis, 'fetch').mockImplementation(apiMock)
    render(<App />)
    const stock = await screen.findByRole('spinbutton', { name: 'Stock de Mancuerna 10kg' })
    await userEvent.clear(stock); if (value) await userEvent.type(stock, value); await userEvent.tab()
    expect(stock).toHaveValue(Number(restoredValue))
    expect(productMutations(fetchMock.mock.calls)).toHaveLength(0)
  })

  it.each([['cero explícito', '0'], ['entero válido', '8']])('actualiza stock con %s', async (_case, value) => {
    signIn('Admin'); window.history.replaceState(null, '', '/admin/productos')
    const fetchMock = vi.spyOn(globalThis, 'fetch').mockImplementation(apiMock)
    render(<App />)
    const stock = await screen.findByRole('spinbutton', { name: 'Stock de Mancuerna 10kg' })
    await userEvent.clear(stock); await userEvent.type(stock, value); await userEvent.tab()
    await waitFor(() => expect(productMutations(fetchMock.mock.calls)).toHaveLength(1))
    expect((productMutations(fetchMock.mock.calls)[0][1] as RequestInit).body).toBe(JSON.stringify({ stock: Number(value) }))
  })

  it('actualiza stock y confirma cambios de estado', async () => {
    signIn('Admin'); window.history.replaceState(null, '', '/admin/productos')
    const fetchMock = vi.spyOn(globalThis, 'fetch').mockImplementation(apiMock)
    vi.spyOn(window, 'confirm').mockReturnValue(true)
    render(<App />)
    const stock = await screen.findByRole('spinbutton', { name: 'Stock de Mancuerna 10kg' })
    await userEvent.clear(stock); await userEvent.type(stock, '8'); await userEvent.tab()
    await waitFor(() => expect(fetchMock).toHaveBeenCalledWith(expect.stringContaining('/api/products/1/stock'), expect.objectContaining({ method: 'PATCH' })))
    await userEvent.click(screen.getByRole('button', { name: 'Desactivar' }))
    await waitFor(() => expect(fetchMock).toHaveBeenCalledWith(expect.stringContaining('/api/products/1/status'), expect.objectContaining({ method: 'PATCH' })))
    expect(window.confirm).toHaveBeenCalledWith(expect.stringContaining('Mancuerna 10kg'))
  })

  it('evita envíos duplicados mientras una mutación está pendiente', async () => {
    signIn('Admin'); window.history.replaceState(null, '', '/admin/productos')
    let resolveMutation!: (value: Response) => void
    const mutation = new Promise<Response>(resolve => { resolveMutation = resolve })
    const fetchMock = vi.spyOn(globalThis, 'fetch').mockImplementation((input, init) => String(input).includes('/status') && init?.method === 'PATCH' ? mutation : apiMock(input, init))
    vi.spyOn(window, 'confirm').mockReturnValue(true)
    render(<App />)
    const button = await screen.findByRole('button', { name: 'Desactivar' })
    await userEvent.click(button)
    expect(button).toBeDisabled()
    await userEvent.click(button)
    expect(fetchMock.mock.calls.filter(([input]) => String(input).includes('/status'))).toHaveLength(1)
    resolveMutation(new Response('null', { status: 200, headers: { 'Content-Type': 'application/json' } }))
    await waitFor(() => expect(screen.getByRole('button', { name: 'Desactivar' })).toBeEnabled())
  })
})
