import { render, screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { beforeEach, describe, expect, it, vi } from 'vitest'
import App from '../../App'

const json = (body: unknown, status = 200) => Promise.resolve(new Response(JSON.stringify(body), { status, headers: { 'Content-Type': 'application/json' } }))
const category = { id: 4, name: 'Fuerza', slug: 'fuerza', description: null, displayOrder: 1 }
const product = { id: 7, name: 'Kettlebell 16kg', description: 'Hierro', price: 42000, stock: 6, imageUrl: '/images/products/kettlebell-16kg.webp', isActive: false, category }

function signIn(role: 'User' | 'Admin' | 'SuperAdmin' = 'Admin') {
  localStorage.setItem('gymshop.token', 'jwt')
  localStorage.setItem('gymshop.user', JSON.stringify({ id: 1, email: `${role.toLowerCase()}@gym.com`, name: role, role }))
}

function defaultApi(input: RequestInfo | URL, init?: RequestInit) {
  const url = String(input)
  if (url.includes('/api/cart')) return json({ id: 1, userId: 1, total: 0, items: [] })
  if (url.endsWith('/api/categories')) return json([category])
  if (url.endsWith('/api/products/7') && (!init?.method || init.method === 'GET')) return json(product)
  if (url.endsWith('/api/products') && init?.method === 'POST') return json({ ...product, id: 8, name: 'Producto nuevo', isActive: true }, 201)
  if (url.endsWith('/api/products/7') && init?.method === 'PUT') return json({ ...product, name: 'Kettlebell Pro' })
  if (url.includes('/api/products')) return json([product])
  return json([])
}

async function fillValidCreateForm() {
  await userEvent.type(screen.getByLabelText('Nombre'), 'Producto nuevo')
  await userEvent.type(screen.getByLabelText('Descripción'), 'Descripción válida')
  await userEvent.type(screen.getByLabelText('Precio'), '12500.50')
  await userEvent.type(screen.getByLabelText('Stock'), '4')
  await userEvent.selectOptions(screen.getByLabelText('Categoría'), '4')
  await userEvent.type(screen.getByLabelText('URL de imagen'), '/images/products/nuevo.webp')
}

describe('alta y edición administrativa de productos', () => {
  beforeEach(() => { localStorage.clear(); window.history.replaceState(null, '', '/'); vi.restoreAllMocks() })

  it('permite a Admin abrir creación y carga las categorías', async () => {
    signIn(); window.history.replaceState(null, '', '/admin/productos/nuevo')
    vi.spyOn(globalThis, 'fetch').mockImplementation(defaultApi)
    render(<App />)
    expect(await screen.findByRole('heading', { name: 'Nuevo producto' })).toBeInTheDocument()
    expect(screen.getByRole('option', { name: 'Fuerza' })).toHaveValue('4')
    expect(screen.getByRole('button', { name: 'Crear producto' })).toBeEnabled()
  })

  it('impide a User acceder al formulario', async () => {
    signIn('User'); window.history.replaceState(null, '', '/admin/productos/nuevo')
    const fetchMock = vi.spyOn(globalThis, 'fetch').mockImplementation(defaultApi)
    render(<App />)
    expect(await screen.findByText('No tenés permisos para acceder a esta sección.')).toBeInTheDocument()
    expect(fetchMock.mock.calls.some(([input]) => String(input).includes('/api/categories'))).toBe(false)
  })

  it('valida campos obligatorios sin llamar a creación', async () => {
    signIn(); window.history.replaceState(null, '', '/admin/productos/nuevo')
    const fetchMock = vi.spyOn(globalThis, 'fetch').mockImplementation(defaultApi)
    render(<App />)
    await screen.findByRole('button', { name: 'Crear producto' })
    await userEvent.click(screen.getByRole('button', { name: 'Crear producto' }))
    expect(screen.getByText('El nombre es obligatorio.')).toBeInTheDocument()
    expect(screen.getByText('El precio debe ser mayor a cero.')).toBeInTheDocument()
    expect(screen.getByText('El stock debe ser un número entero mayor o igual a cero.')).toBeInTheDocument()
    expect(screen.getByText('Seleccioná una categoría.')).toBeInTheDocument()
    expect(fetchMock.mock.calls.some(([, init]) => init?.method === 'POST')).toBe(false)
  })

  it.each([
    ['precio', 'Precio', '-2', 'El precio debe ser mayor a cero.'],
    ['stock', 'Stock', '1.5', 'El stock debe ser un número entero mayor o igual a cero.'],
    ['URL', 'URL de imagen', 'javascript:alert(1)', 'Ingresá una URL http/https o una ruta local que comience con “/”.'],
  ])('no envía un %s inválido', async (_case, label, value, message) => {
    signIn(); window.history.replaceState(null, '', '/admin/productos/nuevo')
    const fetchMock = vi.spyOn(globalThis, 'fetch').mockImplementation(defaultApi)
    render(<App />); await screen.findByRole('button', { name: 'Crear producto' }); await fillValidCreateForm()
    const input = screen.getByLabelText(label); await userEvent.clear(input); await userEvent.type(input, value)
    await userEvent.click(screen.getByRole('button', { name: 'Crear producto' }))
    expect(screen.getByText(message)).toBeInTheDocument()
    expect(fetchMock.mock.calls.some(([, init]) => init?.method === 'POST')).toBe(false)
  })

  it('crea un producto válido una sola vez y vuelve al listado con confirmación', async () => {
    signIn(); window.history.replaceState(null, '', '/admin/productos/nuevo')
    const fetchMock = vi.spyOn(globalThis, 'fetch').mockImplementation(defaultApi)
    render(<App />); await screen.findByRole('button', { name: 'Crear producto' }); await fillValidCreateForm()
    await userEvent.dblClick(screen.getByRole('button', { name: 'Crear producto' }))
    expect(await screen.findByRole('status')).toHaveTextContent('Producto nuevo fue creado correctamente.')
    expect(fetchMock.mock.calls.filter(([, init]) => init?.method === 'POST')).toHaveLength(1)
    expect(window.location.pathname).toBe('/admin/productos')
  })

  it('conserva los datos y muestra el error cuando la creación falla', async () => {
    signIn(); window.history.replaceState(null, '', '/admin/productos/nuevo')
    vi.spyOn(globalThis, 'fetch').mockImplementation((input, init) => init?.method === 'POST' ? json({ message: 'El producto ya existe.' }, 409) : defaultApi(input, init))
    render(<App />); await screen.findByRole('button', { name: 'Crear producto' }); await fillValidCreateForm()
    await userEvent.click(screen.getByRole('button', { name: 'Crear producto' }))
    expect(await screen.findByRole('alert')).toHaveTextContent('El producto ya existe.')
    expect(screen.getByLabelText('Nombre')).toHaveValue('Producto nuevo')
    expect(screen.getByLabelText('Precio')).toHaveValue(12500.5)
  })

  it('mapea los errores de validación del backend al campo correspondiente', async () => {
    signIn(); window.history.replaceState(null, '', '/admin/productos/nuevo')
    vi.spyOn(globalThis, 'fetch').mockImplementation((input, init) => init?.method === 'POST' ? json({ errors: { Name: ['El nombre ya está en uso.'] } }, 400) : defaultApi(input, init))
    render(<App />); await screen.findByRole('button', { name: 'Crear producto' }); await fillValidCreateForm()
    await userEvent.click(screen.getByRole('button', { name: 'Crear producto' }))
    const name = screen.getByLabelText('Nombre')
    expect(await screen.findByText('El nombre ya está en uso.')).toBeInTheDocument()
    expect(screen.queryByRole('alert')).not.toBeInTheDocument()
    expect(name).toHaveAttribute('aria-invalid', 'true')
    expect(name).toHaveValue('Producto nuevo')
  })

  it('carga los datos existentes y actualiza una sola vez', async () => {
    signIn(); window.history.replaceState(null, '', '/admin/productos/7/editar')
    const fetchMock = vi.spyOn(globalThis, 'fetch').mockImplementation(defaultApi)
    render(<App />)
    expect(await screen.findByLabelText('Nombre')).toHaveValue('Kettlebell 16kg')
    expect(screen.getByLabelText('Descripción')).toHaveValue('Hierro')
    expect(screen.getByLabelText('Precio')).toHaveValue(42000)
    expect(screen.getByLabelText('Stock')).toHaveValue(6)
    expect(screen.getByLabelText('Categoría')).toHaveValue('4')
    expect(screen.getByLabelText('Producto activo')).not.toBeChecked()
    await userEvent.clear(screen.getByLabelText('Nombre')); await userEvent.type(screen.getByLabelText('Nombre'), 'Kettlebell Pro')
    await userEvent.dblClick(screen.getByRole('button', { name: 'Guardar cambios' }))
    expect(await screen.findByRole('status')).toHaveTextContent('Kettlebell Pro fue actualizado correctamente.')
    expect(fetchMock.mock.calls.filter(([, init]) => init?.method === 'PUT')).toHaveLength(1)
  })

  it('muestra un estado específico si el producto no existe', async () => {
    signIn(); window.history.replaceState(null, '', '/admin/productos/999/editar')
    vi.spyOn(globalThis, 'fetch').mockImplementation((input, init) => String(input).endsWith('/api/products/999') ? json({ message: 'Producto no encontrado.' }, 404) : defaultApi(input, init))
    render(<App />)
    expect(await screen.findByText('Producto no encontrado.')).toBeInTheDocument()
    expect(screen.queryByRole('button', { name: 'Guardar cambios' })).not.toBeInTheDocument()
  })

  it('no consulta la API cuando el ID de edición no es un entero positivo', async () => {
    signIn(); window.history.replaceState(null, '', '/admin/productos/abc/editar')
    const fetchMock = vi.spyOn(globalThis, 'fetch').mockImplementation(defaultApi)
    render(<App />)
    expect(await screen.findByText('Producto no encontrado.')).toBeInTheDocument()
    expect(fetchMock.mock.calls.some(([input]) => String(input).includes('/api/products/'))).toBe(false)
    expect(fetchMock.mock.calls.some(([input]) => String(input).includes('/api/categories'))).toBe(false)
  })

  it('presenta un error claro cuando falla la carga y permite reintentar', async () => {
    signIn(); window.history.replaceState(null, '', '/admin/productos/nuevo')
    let categoryLoads = 0
    vi.spyOn(globalThis, 'fetch').mockImplementation((input, init) => String(input).endsWith('/api/categories') && ++categoryLoads === 1 ? json({ message: 'No se pudieron cargar las categorías.' }, 500) : defaultApi(input, init))
    render(<App />)
    expect(await screen.findByRole('alert')).toHaveTextContent('No se pudieron cargar las categorías.')
    await userEvent.click(screen.getByRole('button', { name: 'Reintentar' }))
    expect(await screen.findByRole('button', { name: 'Crear producto' })).toBeEnabled()
    expect(categoryLoads).toBe(2)
  })

  it('impide guardar y explica el motivo cuando no hay categorías', async () => {
    signIn(); window.history.replaceState(null, '', '/admin/productos/nuevo')
    vi.spyOn(globalThis, 'fetch').mockImplementation((input, init) => String(input).endsWith('/api/categories') ? json([]) : defaultApi(input, init))
    render(<App />)
    expect(await screen.findByText(/No hay categorías disponibles/)).toBeInTheDocument()
    expect(screen.queryByRole('button', { name: 'Crear producto' })).not.toBeInTheDocument()
  })

  it('mantiene bloqueado un segundo envío mientras el primero está en curso', async () => {
    signIn(); window.history.replaceState(null, '', '/admin/productos/nuevo')
    let resolveCreate!: (response: Response) => void
    const create = new Promise<Response>(resolve => { resolveCreate = resolve })
    const fetchMock = vi.spyOn(globalThis, 'fetch').mockImplementation((input, init) => init?.method === 'POST' ? create : defaultApi(input, init))
    render(<App />); await screen.findByRole('button', { name: 'Crear producto' }); await fillValidCreateForm()
    const button = screen.getByRole('button', { name: 'Crear producto' }); await userEvent.click(button)
    expect(screen.getByRole('button', { name: 'Guardando…' })).toBeDisabled()
    expect(fetchMock.mock.calls.filter(([, init]) => init?.method === 'POST')).toHaveLength(1)
    resolveCreate(new Response(JSON.stringify({ ...product, name: 'Producto nuevo' }), { status: 201, headers: { 'Content-Type': 'application/json' } }))
    await waitFor(() => expect(window.location.pathname).toBe('/admin/productos'))
  })
})
