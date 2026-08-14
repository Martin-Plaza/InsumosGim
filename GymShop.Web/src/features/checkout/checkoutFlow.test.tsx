import { render, screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { beforeEach, describe, expect, it, vi } from 'vitest'
import App from '../../App'
import type { Order, Payment } from '../../api/types'

const product = { id: 4, name: 'Kettlebell 16kg', price: 42000, stock: 12, imageUrl: '/kettlebell.webp' }
const cart = { id: 1, userId: 7, total: 84000, items: [{ productId: 4, productName: product.name, unitPrice: product.price, quantity: 2, subtotal: 84000, stock: product.stock, imageUrl: product.imageUrl }] }
const emptyCart = { ...cart, total: 0, items: [] }
const order: Order = { id: 81, userId: 7, userEmail: 'u@gym.com', createdAt: '2026-08-11T10:00:00Z', total: 84000, status: 'Pending', shippingAddress: 'Av. Siempre Viva 742, Córdoba', cancellationReason: null, items: [{ productId: 4, productName: product.name, unitPrice: product.price, quantity: 2, subtotal: 84000 }], payments: [] }
const payment = (status: Payment['status']): Payment => ({ id: 91, orderId: 81, provider: 'Mock', externalReference: 'order-81', providerPreferenceId: null, providerPaymentId: null, idempotencyKey: 'key', amount: 84000, currency: 'ARS', status, checkoutUrl: null, failureReason: status === 'Rejected' ? 'Rechazado por Mock.' : null, createdAt: '2026-08-11T10:00:01Z', updatedAt: null, paidAt: null })
const json = (body: unknown, status = 200, headers: Record<string, string> = {}) => Promise.resolve(new Response(JSON.stringify(body), { status, headers: { 'Content-Type': 'application/json', ...headers } }))

function authenticate() {
  localStorage.setItem('gymshop.token', 'jwt')
  localStorage.setItem('gymshop.user', JSON.stringify({ id: 7, email: 'u@gym.com', name: 'Usuario', role: 'User' }))
}

describe('checkout y pago por orderId', () => {
  beforeEach(() => { localStorage.clear(); sessionStorage.clear(); window.history.replaceState(null, '', '/checkout'); vi.restoreAllMocks(); authenticate() })

  it('revisa, crea una sola orden y crea el pago Mock por orderId con clave estable', async () => {
    let checkedOut = false
    const calls: Array<{ url: string; method: string; body: string | null }> = []
    vi.spyOn(globalThis, 'fetch').mockImplementation((input, init) => {
      const url = String(input); const method = init?.method || 'GET'; calls.push({ url, method, body: init?.body ? String(init.body) : null })
      if (url.endsWith('/api/cart') && method === 'GET') return json(checkedOut ? emptyCart : cart)
      if (url.endsWith('/api/cart/checkout') && method === 'POST') { checkedOut = true; return json(order) }
      if (url.endsWith('/api/orders/81/payments') && method === 'POST') return json(payment('Creating'), 202)
      if (url.endsWith('/api/orders/81')) return json(order)
      if (url.endsWith('/api/payments/orders/81')) return json([payment('Creating')])
      return json([])
    })
    render(<App />)
    await screen.findByRole('heading', { name: 'Confirmá tu compra' })
    await userEvent.type(screen.getByLabelText('Dirección completa'), order.shippingAddress)
    const confirm = screen.getByRole('button', { name: 'Confirmar y pagar' })
    await userEvent.dblClick(confirm)
    expect(await screen.findByRole('heading', { name: 'Estamos confirmando tu pago' })).toBeInTheDocument()
    expect(screen.getByText(/todavía no tiene un enlace/)).toBeInTheDocument()
    expect(calls.filter(call => call.url.endsWith('/api/cart/checkout') && call.method === 'POST')).toHaveLength(1)
    const paymentCall = calls.find(call => call.url.endsWith('/api/orders/81/payments') && call.method === 'POST')
    expect(paymentCall).toBeTruthy()
    expect(JSON.parse(paymentCall!.body || '{}').provider).toBe('Mock')
    expect(JSON.parse(paymentCall!.body || '{}').idempotencyKey).toBe(localStorage.getItem('gymshop.payment-key.81'))
    expect(calls.some(call => call.url.includes('/api/payments/current'))).toBe(false)
  })

  it('muestra 409, refresca el carrito y recupera la orden pendiente', async () => {
    vi.spyOn(globalThis, 'fetch').mockImplementation((input, init) => {
      const url = String(input); const method = init?.method || 'GET'
      if (url.endsWith('/api/cart/checkout') && method === 'POST') return json({ title: 'Conflicto', detail: 'Ya tenes una orden pendiente.' }, 409)
      if (url.endsWith('/api/orders/my')) return json([{ id: 70, userId: 7, createdAt: '2026-08-11T09:00:00Z', total: 84000, status: 'Pending', lastPaymentStatus: null, lastPaymentId: null }])
      if (url.endsWith('/api/cart')) return json(cart)
      return json([])
    })
    render(<App />)
    await userEvent.type(await screen.findByLabelText('Dirección completa'), order.shippingAddress)
    await userEvent.click(screen.getByRole('button', { name: 'Confirmar y pagar' }))
    expect(await screen.findByRole('alert')).toHaveTextContent('Ya tenes una orden pendiente')
    expect(await screen.findByRole('link', { name: 'Ver orden' })).toHaveAttribute('href', '/checkout/orden/70')
  })

  it('conserva la orden y muestra Retry-After cuando falla la creación del pago', async () => {
    let checkedOut = false
    vi.spyOn(globalThis, 'fetch').mockImplementation((input, init) => {
      const url = String(input); const method = init?.method || 'GET'
      if (url.endsWith('/api/cart') && method === 'GET') return json(checkedOut ? emptyCart : cart)
      if (url.endsWith('/api/cart/checkout') && method === 'POST') { checkedOut = true; return json(order) }
      if (url.endsWith('/api/orders/81/payments') && method === 'POST') return json({ title: 'Demasiadas solicitudes' }, 429, { 'Retry-After': '9' })
      if (url.endsWith('/api/orders/81')) return json(order)
      if (url.endsWith('/api/payments/orders/81')) return json([])
      return json([])
    })
    render(<App />)
    await userEvent.type(await screen.findByLabelText('Dirección completa'), order.shippingAddress)
    await userEvent.click(screen.getByRole('button', { name: 'Confirmar y pagar' }))
    expect(await screen.findByRole('heading', { name: 'Orden creada' })).toBeInTheDocument()
    expect(screen.getByRole('alert')).toHaveTextContent('9 segundos')
    expect(sessionStorage.getItem('gymshop.last-order')).toBe('81')
  })

  it('genera otra clave solo al iniciar un nuevo intento después de Rejected', async () => {
    window.history.replaceState(null, '', '/checkout/orden/81')
    localStorage.setItem('gymshop.payment-key.81', 'old-key')
    let current = payment('Rejected')
    let sentKey = ''
    vi.spyOn(globalThis, 'fetch').mockImplementation((input, init) => {
      const url = String(input); const method = init?.method || 'GET'
      if (url.endsWith('/api/cart')) return json(emptyCart)
      if (url.endsWith('/api/orders/81')) return json(order)
      if (url.endsWith('/api/payments/orders/81')) return json([current])
      if (url.endsWith('/api/orders/81/payments') && method === 'POST') { sentKey = JSON.parse(String(init?.body)).idempotencyKey; current = payment('Pending'); return json(current) }
      return json([])
    })
    render(<App />)
    await userEvent.click(await screen.findByRole('button', { name: 'Intentar pagar nuevamente' }))
    await waitFor(() => expect(screen.getByText('Pendiente')).toBeInTheDocument())
    expect(sentKey).not.toBe('old-key')
    expect(sentKey).toBe(localStorage.getItem('gymshop.payment-key.81'))
  })

  it('actualiza un pago Pending sin crear otro intento', async () => {
    window.history.replaceState(null, '', '/checkout/orden/81')
    localStorage.setItem('gymshop.user', JSON.stringify({ id: 7, email: 'admin@gym.com', name: 'Admin', role: 'Admin' }))
    let paymentPosts = 0
    let paymentReads = 0
    vi.spyOn(globalThis, 'fetch').mockImplementation((input, init) => {
      const url = String(input); const method = init?.method || 'GET'
      if (url.endsWith('/api/cart')) return json(emptyCart)
      if (url.endsWith('/api/orders/81')) return json(order)
      if (url.endsWith('/api/payments/orders/81')) { paymentReads++; return json([payment('Pending')]) }
      if (url.endsWith('/api/orders/81/payments') && method === 'POST') { paymentPosts++; return json(payment('Pending')) }
      return json([])
    })
    render(<App />)
    await userEvent.click(await screen.findByRole('button', { name: 'Actualizar estado' }))
    await waitFor(() => expect(paymentReads).toBeGreaterThan(1))
    expect(paymentPosts).toBe(0)
  })

  it('no muestra Actualizar estado a un usuario común', async () => {
    window.history.replaceState(null, '', '/checkout/orden/81')
    vi.spyOn(globalThis, 'fetch').mockImplementation((input) => {
      const url = String(input)
      if (url.endsWith('/api/cart')) return json(emptyCart)
      if (url.endsWith('/api/orders/81')) return json(order)
      if (url.endsWith('/api/payments/orders/81')) return json([payment('Pending')])
      return json([])
    })
    render(<App />)
    await screen.findByText('Pendiente')
    expect(screen.queryByRole('button', { name: 'Actualizar estado' })).not.toBeInTheDocument()
    expect(screen.getByRole('button', { name: 'Cancelar orden' })).toBeInTheDocument()
  })
})
