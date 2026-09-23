import { render, screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { beforeEach, describe, expect, it, vi } from 'vitest'
import App from '../../App'
import type { Order, Payment } from '../../api/types'

const product = { id: 4, name: 'Kettlebell 16kg', price: 42000, stock: 12, imageUrl: '/kettlebell.webp' }
const cart = { id: 1, userId: 7, subtotal: 84000, discount: 0, couponCode: null, total: 84000, items: [{ productId: 4, productName: product.name, unitPrice: product.price, quantity: 2, subtotal: 84000, stock: product.stock, imageUrl: product.imageUrl }] }
const emptyCart = { ...cart, total: 0, items: [] }
const order: Order = { id: 81, userId: 7, userEmail: 'u@gym.com', userName: 'Usuario', userPhone: null, createdAt: '2026-08-11T10:00:00Z', updatedAt: null, total: 84000, status: 'Pending', shippingAddress: 'Av. Siempre Viva 742, Córdoba', cancellationReason: null, items: [{ productId: 4, productName: product.name, unitPrice: product.price, quantity: 2, subtotal: 84000 }], payments: [] }
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
      if (url.endsWith('/api/cart/shipping-options')) return json({ homeDeliveryCost: 6500, pickupAddress: 'Av. Demo 123', pickupInstructions: '', pickupHours: '' })
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
    const checkoutCall = calls.find(call => call.url.endsWith('/api/cart/checkout') && call.method === 'POST')
    expect(JSON.parse(checkoutCall!.body || '{}')).toMatchObject({ deliveryMethod: 'HomeDelivery', expectedShippingCost: 6500, expectedSubtotal: 84000, expectedDiscount: 0 })
    const paymentCall = calls.find(call => call.url.endsWith('/api/orders/81/payments') && call.method === 'POST')
    expect(paymentCall).toBeTruthy()
    expect(JSON.parse(paymentCall!.body || '{}').provider).toBe('Mock')
    expect(JSON.parse(paymentCall!.body || '{}').idempotencyKey).toBe(localStorage.getItem('gymshop.payment-key.81'))
    expect(calls.some(call => call.url.includes('/api/payments/current'))).toBe(false)
  })

  it('muestra 409, refresca el carrito y recupera la orden pendiente', async () => {
    vi.spyOn(globalThis, 'fetch').mockImplementation((input, init) => {
      const url = String(input); const method = init?.method || 'GET'
      if (url.endsWith('/api/cart/shipping-options')) return json({ homeDeliveryCost: 6500, pickupAddress: 'Av. Demo 123', pickupInstructions: '', pickupHours: '' })
      if (url.endsWith('/api/cart/checkout') && method === 'POST') return json({ title: 'Conflicto', detail: 'Ya tenes una orden pendiente.', code: 'pending_order_exists' }, 409)
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
      if (url.endsWith('/api/cart/shipping-options')) return json({ homeDeliveryCost: 6500, pickupAddress: 'Av. Demo 123', pickupInstructions: '', pickupHours: '' })
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

  it('bloquea domicilio si falla el costo, permite reintentar y actualiza el total', async () => {
    let attempts = 0
    vi.spyOn(globalThis, 'fetch').mockImplementation((input) => {
      const url = String(input)
      if (url.endsWith('/api/cart/shipping-options')) {
        attempts += 1
        return attempts === 1 ? json({ message: 'sin tarifa' }, 503) : json({ homeDeliveryCost: 6500, pickupAddress: 'Av. Demo 123', pickupInstructions: '', pickupHours: '' })
      }
      if (url.endsWith('/api/cart')) return json(cart)
      return json([])
    })
    render(<App />)
    const confirm = await screen.findByRole('button', { name: 'Confirmar y pagar' })
    expect(confirm).toBeDisabled()
    expect(await screen.findByRole('alert')).toHaveTextContent('No pudimos obtener las opciones de entrega')
    await userEvent.click(screen.getByRole('button', { name: 'Reintentar opciones de entrega' }))
    await waitFor(() => expect(confirm).toBeEnabled())
    expect(screen.queryByText('No pudimos obtener las opciones de entrega')).not.toBeInTheDocument()
    expect(screen.getByText(/90\.500,00/)).toBeInTheDocument()
  })

  it('bloquea retiro si fallan las opciones y permite confirmarlo después de reintentar', async () => {
    let checkoutBody: Record<string, unknown> | null = null
    let optionAttempts = 0
    vi.spyOn(globalThis, 'fetch').mockImplementation((input, init) => {
      const url = String(input); const method = init?.method || 'GET'
      if (url.endsWith('/api/cart/shipping-options')) {
        optionAttempts += 1
        return optionAttempts === 1
          ? json({ message: 'sin opciones' }, 503)
          : json({ homeDeliveryCost: 6500, pickupAddress: 'Av. Demo 123', pickupInstructions: 'Traé tu documento.', pickupHours: 'Lunes a viernes de 9 a 17' })
      }
      if (url.endsWith('/api/cart') && method === 'GET') return json(checkoutBody ? emptyCart : cart)
      if (url.endsWith('/api/cart/checkout') && method === 'POST') { checkoutBody = JSON.parse(String(init?.body)); return json({ ...order, deliveryMethod: 'StorePickup', shippingAddress: '', shippingCost: 0, pickupAddress: 'Av. Demo 123', pickupInstructions: 'Traé tu documento.', pickupHours: 'Lunes a viernes de 9 a 17' }) }
      if (url.endsWith('/api/orders/81/payments')) return json(payment('Pending'))
      if (url.endsWith('/api/orders/81')) return json({ ...order, deliveryMethod: 'StorePickup', shippingAddress: '', shippingCost: 0, pickupAddress: 'Av. Demo 123', pickupInstructions: 'Traé tu documento.', pickupHours: 'Lunes a viernes de 9 a 17' })
      if (url.endsWith('/api/payments/orders/81')) return json([payment('Pending')])
      return json([])
    })
    render(<App />)
    await userEvent.click(await screen.findByRole('radio', { name: /Retiro en tienda/ }))
    const confirm = screen.getByRole('button', { name: 'Confirmar y pagar' })
    expect(confirm).toBeDisabled()
    expect(screen.queryByLabelText('Dirección completa')).not.toBeInTheDocument()
    await userEvent.click(screen.getByRole('button', { name: 'Reintentar opciones de entrega' }))
    await waitFor(() => expect(confirm).toBeEnabled())
    await userEvent.click(confirm)
    await waitFor(() => expect(checkoutBody).toMatchObject({ deliveryMethod: 'StorePickup', shippingAddress: null, expectedShippingCost: 0 }))
    expect(await screen.findByText('Av. Demo 123')).toBeInTheDocument()
    expect(screen.getByText('Lunes a viernes de 9 a 17')).toBeInTheDocument()
    expect(screen.getByText('Traé tu documento.')).toBeInTheDocument()
  })

  it('bloquea retiro cuando falta la dirección configurada', async () => {
    let checkoutPosts = 0
    vi.spyOn(globalThis, 'fetch').mockImplementation((input, init) => {
      const url = String(input); const method = init?.method || 'GET'
      if (url.endsWith('/api/cart/shipping-options')) return json({ homeDeliveryCost: 6500, pickupAddress: '', pickupInstructions: 'Indicaciones', pickupHours: 'Horario' })
      if (url.endsWith('/api/cart') && method === 'GET') return json(cart)
      if (url.endsWith('/api/cart/checkout') && method === 'POST') checkoutPosts += 1
      return json([])
    })
    render(<App />)
    await userEvent.click(await screen.findByRole('radio', { name: /Retiro en tienda/ }))
    expect(screen.getByRole('button', { name: 'Confirmar y pagar' })).toBeDisabled()
    expect(await screen.findByRole('alert')).toHaveTextContent('falta configurar su dirección')
    expect(screen.getByRole('button', { name: 'Reintentar opciones de entrega' })).toBeInTheDocument()
    expect(checkoutPosts).toBe(0)
  })

  it('muestra los datos de retiro provistos por la configuración', async () => {
    vi.spyOn(globalThis, 'fetch').mockImplementation((input) => {
      const url = String(input)
      if (url.endsWith('/api/cart/shipping-options')) return json({ homeDeliveryCost: 6500, pickupAddress: 'Av. Demo 123', pickupInstructions: 'Traé tu documento y número de orden.', pickupHours: 'Lunes a viernes de 9 a 17' })
      if (url.endsWith('/api/cart')) return json(cart)
      return json([])
    })
    render(<App />)
    await userEvent.click(await screen.findByRole('radio', { name: /Retiro en tienda/ }))
    expect(screen.getByText('Av. Demo 123')).toBeInTheDocument()
    expect(screen.getByText('Lunes a viernes de 9 a 17')).toBeInTheDocument()
    expect(screen.getByText('Traé tu documento y número de orden.')).toBeInTheDocument()
  })

  it('ante un 409 comercial refresca carrito y tarifa sin buscar ni crear una orden', async () => {
    const updatedCart = { ...cart, subtotal: 90000, discount: 10000, couponCode: 'NUEVO', total: 80000 }
    let cartReads = 0; let shippingReads = 0; let checkoutPosts = 0; let pendingOrderReads = 0
    vi.spyOn(globalThis, 'fetch').mockImplementation((input, init) => {
      const url = String(input); const method = init?.method || 'GET'
      if (url.endsWith('/api/cart/shipping-options')) { shippingReads += 1; return json({ homeDeliveryCost: shippingReads === 1 ? 6500 : 8000, pickupAddress: 'Local', pickupInstructions: '', pickupHours: '' }) }
      if (url.endsWith('/api/cart') && method === 'GET') { cartReads += 1; return json(cartReads === 1 ? cart : updatedCart) }
      if (url.endsWith('/api/cart/checkout') && method === 'POST') { checkoutPosts += 1; return json({ message: 'El precio, descuento o costo de envio cambio. Revisa el resumen antes de confirmar.', code: 'checkout_pricing_changed' }, 409) }
      if (url.endsWith('/api/orders/my')) { pendingOrderReads += 1; return json([]) }
      return json([])
    })
    render(<App />)
    await userEvent.type(await screen.findByLabelText('Dirección completa'), order.shippingAddress)
    await userEvent.click(screen.getByRole('button', { name: 'Confirmar y pagar' }))
    expect(await screen.findByRole('alert')).toHaveTextContent('Revisá el nuevo total y confirmá nuevamente')
    expect(screen.getByText(/88\.000,00/)).toBeInTheDocument()
    expect(screen.getByText(/Descuento \(NUEVO\)/)).toBeInTheDocument()
    expect(checkoutPosts).toBe(1)
    expect(shippingReads).toBe(2)
    expect(cartReads).toBe(2)
    expect(pendingOrderReads).toBe(0)
    expect(screen.queryByRole('link', { name: 'Ver orden' })).not.toBeInTheDocument()
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
    await waitFor(() => expect(screen.getAllByText('Pendiente').length).toBeGreaterThan(0))
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
    await screen.findAllByText('Pendiente')
    expect(screen.queryByRole('button', { name: 'Actualizar estado' })).not.toBeInTheDocument()
    expect(screen.getByRole('button', { name: 'Cancelar orden' })).toBeInTheDocument()
  })

  it('muestra el snapshot de retiro en el detalle de Mis órdenes', async () => {
    window.history.replaceState(null, '', '/ordenes')
    const pickupOrder = { ...order, deliveryMethod: 'StorePickup' as const, shippingAddress: '', shippingCost: 0, pickupAddress: 'Sucursal histórica 456', pickupHours: 'Sábados de 10 a 13', pickupInstructions: 'Presentá el código de compra.' }
    vi.spyOn(globalThis, 'fetch').mockImplementation((input) => {
      const url = String(input)
      if (url.endsWith('/api/cart')) return json(emptyCart)
      if (url.endsWith('/api/orders/my')) return json([{ id: 81, userId: 7, userEmail: 'u@gym.com', userName: 'Usuario', createdAt: order.createdAt, total: order.total, deliveryMethod: 'StorePickup', status: 'Pending', updatedAt: null, lastPaymentStatus: null, lastPaymentId: null }])
      if (url.endsWith('/api/orders/81')) return json(pickupOrder)
      if (url.endsWith('/api/payments/orders/81')) return json([])
      return json([])
    })
    render(<App />)
    await userEvent.click(await screen.findByRole('button', { name: /#81/ }))
    expect(await screen.findByText('Sucursal histórica 456')).toBeInTheDocument()
    expect(screen.getByText('Sábados de 10 a 13')).toBeInTheDocument()
    expect(screen.getByText('Presentá el código de compra.')).toBeInTheDocument()
  })
})
