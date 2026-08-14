import { render, screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { beforeEach, describe, expect, it, vi } from 'vitest'
import App from './App'

const json = (body: unknown, status = 200) => Promise.resolve(new Response(JSON.stringify(body), { status, headers: { 'Content-Type': 'application/json' } }))
const product = { id: 1, name: 'Mancuerna', description: 'Fuerza', price: 1000, stock: 4, imageUrl: '/images/mancuerna.jpg', isActive: true }
const products = Array.from({ length: 7 }, (_, index) => ({ ...product, id: index + 1, name: index === 0 ? 'Kettlebell 16kg' : `Producto ${index + 1}` }))

describe('flujos y permisos de la aplicación', () => {
  beforeEach(() => { localStorage.clear(); window.history.replaceState(null, '', '/'); vi.restoreAllMocks() })

  it('muestra seis destacados y navega a un catálogo separado', async () => {
    vi.spyOn(globalThis, 'fetch').mockImplementation(() => json(products))
    render(<App />)
    expect(await screen.findByText('Productos destacados')).toBeInTheDocument()
    expect(screen.queryByRole('button', { name: 'Inicio' })).not.toBeInTheDocument()
    expect(screen.getByText('Producto 6')).toBeInTheDocument()
    expect(screen.queryByText('Producto 7')).not.toBeInTheDocument()
    expect(screen.getByText(/Entrená fuerte/)).toBeInTheDocument()
    expect(screen.getByText('Envíos a todo el país')).toBeInTheDocument()
    expect(screen.getByText('Opciones de pago')).toBeInTheDocument()
    await userEvent.click(screen.getByRole('button', { name: 'Ver todos' }))
    expect(screen.getByText('CATÁLOGO ACTIVO')).toBeInTheDocument()
    expect(await screen.findByText('Producto 7')).toBeInTheDocument()
  })

  it('prioriza productos con imágenes válidas en los destacados', async () => {
    const invalid = { ...product, id: 99, name: 'Producto sin imagen', imageUrl: 'string' }
    vi.spyOn(globalThis, 'fetch').mockImplementation(() => json([invalid, ...products]))
    render(<App />)
    expect(await screen.findByText('Productos destacados')).toBeInTheDocument()
    expect(screen.queryByText('Producto sin imagen')).not.toBeInTheDocument()
    expect(screen.getByText(/Entrená fuerte/)).toBeInTheDocument()
  })

  it('completa un login exitoso', async () => {
    vi.spyOn(globalThis, 'fetch').mockImplementation((input) => String(input).includes('/products') ? json([]) : String(input).includes('/login') ? json({ token: 'jwt', user: { id: 1, email: 'user@gym.com', name: 'Ana', role: 'User' } }) : json({ items: [] }))
    render(<App />)
    await userEvent.click(screen.getByRole('link', { name: 'Ingresar' }))
    await userEvent.type(screen.getByLabelText('Email'), 'user@gym.com')
    await userEvent.type(screen.getByLabelText('Contraseña'), 'clave123')
    await userEvent.click(screen.getAllByRole('button', { name: 'Ingresar' }).at(-1)!)
    expect(await screen.findByRole('status')).toHaveTextContent('Hola, Ana.')
    expect(localStorage.getItem('gymshop.token')).toBe('jwt')
  })

  it('muestra credenciales inválidas y no crea sesión', async () => {
    vi.spyOn(globalThis, 'fetch').mockImplementation((input) => String(input).includes('/login') ? json({ message: 'Credenciales invalidas.' }, 401) : json([]))
    render(<App />)
    await userEvent.click(screen.getByRole('link', { name: 'Ingresar' }))
    await userEvent.type(screen.getByLabelText('Email'), 'bad@gym.com')
    await userEvent.type(screen.getByLabelText('Contraseña'), 'incorrecta')
    await userEvent.click(screen.getAllByRole('button', { name: 'Ingresar' }).at(-1)!)
    expect(await screen.findByRole('alert')).toHaveTextContent('Credenciales invalidas.')
    expect(localStorage.getItem('gymshop.token')).toBeNull()
  })

  it('registra, permite ver la contraseña y verifica el código Mock antes de iniciar sesión', async () => {
    vi.spyOn(globalThis, 'fetch').mockImplementation((input) => {
      const url = String(input)
      if (url.includes('/auth/register')) return json({ email: 'new@gym.com', expiresInSeconds: 60, developmentCode: '123456' })
      if (url.includes('/auth/verify-email')) return json({ token: 'verified-jwt', user: { id: 8, email: 'new@gym.com', name: 'Nueva', lastName: 'Persona', role: 'User' } })
      if (url.includes('/cart')) return json({ items: [] })
      return json([])
    })
    render(<App />)
    await userEvent.click(screen.getByRole('link', { name: 'Ingresar' }))
    await userEvent.click(screen.getByRole('button', { name: 'Quiero registrarme' }))
    await userEvent.type(screen.getByLabelText('Nombre'), 'Nueva')
    await userEvent.type(screen.getByLabelText('Apellido'), 'Persona')
    await userEvent.type(screen.getByLabelText('Email'), 'new@gym.com')
    const password = screen.getByLabelText('Contraseña')
    await userEvent.type(password, 'Clave1234')
    expect(password).toHaveAttribute('type', 'password')
    await userEvent.click(screen.getByRole('button', { name: 'Mostrar contraseña' }))
    expect(password).toHaveAttribute('type', 'text')
    await userEvent.click(screen.getByRole('button', { name: 'Crear cuenta' }))
    expect(await screen.findByText(/Código Mock local/)).toHaveTextContent('123456')
    await userEvent.type(screen.getByLabelText('Código de verificación'), '123456')
    await userEvent.click(screen.getByRole('button', { name: 'Verificar e ingresar' }))
    expect(await screen.findByRole('status')).toHaveTextContent('Nueva')
    expect(localStorage.getItem('gymshop.token')).toBe('verified-jwt')
  })

  it('retoma una verificación pendiente después de volver a abrir el frontend', async () => {
    localStorage.setItem('gymshop.pending-registration', JSON.stringify({ email: 'pendiente@gym.com', expiresAt: Date.now() + 60_000 }))
    vi.spyOn(globalThis, 'fetch').mockImplementation(() => json([]))
    render(<App />)
    await userEvent.click(screen.getByRole('link', { name: 'Ingresar' }))
    expect(screen.getByText('pendiente@gym.com')).toBeInTheDocument()
    expect(screen.getByRole('status')).toHaveTextContent('Retomamos tu verificación pendiente.')
    expect(screen.queryByText(/Código Mock local/)).not.toBeInTheDocument()
  })

  it('muestra de forma útil un código incorrecto', async () => {
    localStorage.setItem('gymshop.pending-registration', JSON.stringify({ email: 'pendiente@gym.com', expiresAt: Date.now() + 60_000 }))
    vi.spyOn(globalThis, 'fetch').mockImplementation((input) => String(input).includes('/verify-email') ? json({ message: 'El codigo es incorrecto.' }, 400) : json([]))
    render(<App />)
    await userEvent.click(screen.getByRole('link', { name: 'Ingresar' }))
    await userEvent.type(screen.getByLabelText('Código de verificación'), '000000')
    await userEvent.click(screen.getByRole('button', { name: 'Verificar e ingresar' }))
    expect(await screen.findByRole('alert')).toHaveTextContent('El codigo es incorrecto.')
    expect(localStorage.getItem('gymshop.pending-registration')).not.toBeNull()
  })

  it('recupera la contraseña con código Mock y vuelve al login sin crear sesión', async () => {
    const requests: Array<{ url: string; body: Record<string, string> }> = []
    vi.spyOn(globalThis, 'fetch').mockImplementation((input, init) => {
      const url = String(input); const body = init?.body ? JSON.parse(String(init.body)) as Record<string, string> : {}
      requests.push({ url, body })
      if (url.includes('/forgot-password')) return json({ message: 'Si el email corresponde a una cuenta, enviamos un codigo para restablecer la password.', expiresInSeconds: 600, developmentCode: '654321' })
      if (url.includes('/reset-password')) return json({ message: 'La password fue actualizada. Ya podes iniciar sesion.' })
      return json([])
    })
    render(<App />)
    await userEvent.click(screen.getByRole('link', { name: 'Ingresar' }))
    await userEvent.click(screen.getByRole('button', { name: 'Olvidé mi contraseña' }))
    await userEvent.type(screen.getByLabelText('Email'), 'USER@GYM.COM')
    await userEvent.click(screen.getByRole('button', { name: 'Enviar código' }))
    expect(await screen.findByText(/Código Mock local/)).toHaveTextContent('654321')
    expect(screen.getByText(/vence en 10 minutos/)).toBeInTheDocument()
    const resetCode = screen.getByLabelText('Código de recuperación')
    await userEvent.type(resetCode, '654321')
    const password = screen.getByLabelText('Nueva contraseña')
    await userEvent.type(password, 'NuevaClave456')
    await userEvent.click(screen.getByRole('button', { name: 'Mostrar contraseña' }))
    expect(password).toHaveAttribute('type', 'text')
    expect(resetCode).toHaveValue('654321')
    expect(resetCode).toBeValid()
    expect(password).toBeValid()
    expect(password).toHaveValue('NuevaClave456')
    await userEvent.click(screen.getByRole('button', { name: 'Cambiar contraseña' }))
    expect(await screen.findByText(/La password fue actualizada/)).toHaveAttribute('role', 'status')
    expect(screen.getByRole('button', { name: 'Ingresar' })).toBeInTheDocument()
    expect(localStorage.getItem('gymshop.token')).toBeNull()
    expect(requests.find(request => request.url.includes('/forgot-password'))?.body.email).toBe('user@gym.com')
    expect(requests.find(request => request.url.includes('/reset-password'))?.body).toEqual({ email: 'user@gym.com', code: '654321', newPassword: 'NuevaClave456' })
  })

  it('oculta controles Admin y SuperAdmin a User', async () => {
    localStorage.setItem('gymshop.token', 'jwt'); localStorage.setItem('gymshop.user', JSON.stringify({ id: 1, email: 'u@gym.com', name: 'U', role: 'User' }))
    vi.spyOn(globalThis, 'fetch').mockImplementation((input) => String(input).includes('/cart') ? json({ items: [] }) : json([]))
    render(<App />)
    await waitFor(() => expect(screen.queryByText('Administración')).not.toBeInTheDocument())
    expect(screen.queryByText('Usuarios')).not.toBeInTheDocument()
    expect(screen.queryByText('Auditoría')).not.toBeInTheDocument()
  })

  it('expone productos a Admin pero reserva usuarios y auditoría a SuperAdmin', async () => {
    localStorage.setItem('gymshop.token', 'jwt'); localStorage.setItem('gymshop.user', JSON.stringify({ id: 2, email: 'a@gym.com', name: 'A', role: 'Admin' }))
    vi.spyOn(globalThis, 'fetch').mockImplementation((input) => String(input).includes('/cart') ? json({ items: [] }) : json([]))
    const rendered = render(<App />)
    expect(screen.getByText('Administración')).toBeInTheDocument()
    expect(screen.queryByText('Usuarios')).not.toBeInTheDocument()
    rendered.unmount()
    localStorage.setItem('gymshop.user', JSON.stringify({ id: 3, email: 's@gym.com', name: 'S', role: 'SuperAdmin' }))
    render(<App />)
    expect(screen.getByText('Administración')).toBeInTheDocument()
    expect(screen.getByText('Usuarios')).toBeInTheDocument()
    expect(screen.getByText('Auditoría')).toBeInTheDocument()
  })
})
