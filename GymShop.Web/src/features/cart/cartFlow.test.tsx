import { render, screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { beforeEach, describe, expect, it, vi } from 'vitest'
import App from '../../App'

const product = { id: 42, name: 'Mancuerna Pro', description: 'Acero', price: 1000, stock: 5, imageUrl: '/product.webp', isActive: true }
const cartItem = (quantity: number) => ({ productId: 42, productName: product.name, unitPrice: product.price, quantity, subtotal: product.price * quantity, stock: product.stock, imageUrl: product.imageUrl })
const json = (body: unknown, status = 200) => Promise.resolve(new Response(status === 204 ? null : JSON.stringify(body), { status, headers: { 'Content-Type': 'application/json' } }))

describe('carrito visitante y fusión autenticada', () => {
  beforeEach(() => { localStorage.clear(); window.history.replaceState(null, '', '/'); vi.restoreAllMocks() })

  it('agrega como visitante desde una URL de producto y limita por stock', async () => {
    window.history.replaceState(null, '', '/catalogo/42')
    vi.spyOn(globalThis, 'fetch').mockImplementation(() => json(product))
    render(<App />)
    expect(await screen.findByRole('heading', { name: product.name })).toBeInTheDocument()
    const quantity = screen.getByLabelText('Cantidad')
    await userEvent.clear(quantity)
    await userEvent.type(quantity, '5')
    await userEvent.click(screen.getByRole('button', { name: 'Agregar al carrito' }))
    expect(await screen.findByRole('dialog', { name: 'Carrito' })).toBeInTheDocument()
    expect(screen.getByLabelText(`Cantidad de ${product.name}`)).toHaveTextContent('5')
    expect(screen.getByRole('button', { name: `Sumar una unidad de ${product.name}` })).toBeDisabled()
    expect(JSON.parse(localStorage.getItem('gymshop.guest-cart.v1') || '[]')[0].quantity).toBe(5)
  })

  it('combina cantidades con el carrito existente usando un objetivo absoluto', async () => {
    localStorage.setItem('gymshop.guest-cart.v1', JSON.stringify([cartItem(4)]))
    localStorage.setItem('gymshop.token', 'jwt')
    localStorage.setItem('gymshop.user', JSON.stringify({ id: 7, email: 'u@gym.com', name: 'U', role: 'User' }))
    window.history.replaceState(null, '', '/carrito')
    const calls: Array<{ url: string; method: string; body: string | null }> = []
    vi.spyOn(globalThis, 'fetch').mockImplementation((input, init) => {
      const url = String(input); const method = init?.method || 'GET'
      calls.push({ url, method, body: init?.body ? String(init.body) : null })
      if (url.endsWith('/api/cart') && method === 'GET') return json({ id: 1, userId: 7, total: 2000, items: [cartItem(2)] })
      if (url.endsWith('/api/products/42')) return json(product)
      if (url.endsWith('/api/cart/items/42') && method === 'PUT') return json({ id: 1, userId: 7, total: 5000, items: [cartItem(5)] })
      return json([])
    })
    render(<App />)
    await waitFor(() => expect(screen.getByLabelText(`Cantidad de ${product.name}`)).toHaveValue(5))
    expect(calls.find(call => call.method === 'PUT')?.body).toBe(JSON.stringify({ quantity: 5 }))
    expect(localStorage.getItem('gymshop.guest-cart.v1')).toBe('[]')
    expect(await screen.findByRole('status')).toHaveTextContent('Ajustamos al stock disponible')
  })

  it('avisa y conserva un producto visitante que ya no existe', async () => {
    localStorage.setItem('gymshop.guest-cart.v1', JSON.stringify([cartItem(1)]))
    localStorage.setItem('gymshop.token', 'jwt')
    localStorage.setItem('gymshop.user', JSON.stringify({ id: 7, email: 'u@gym.com', name: 'U', role: 'User' }))
    window.history.replaceState(null, '', '/carrito')
    vi.spyOn(globalThis, 'fetch').mockImplementation((input) => String(input).endsWith('/api/products/42') ? json({ message: 'No existe.' }, 404) : json({ id: 1, userId: 7, total: 0, items: [] }))
    render(<App />)
    expect(await screen.findByRole('status')).toHaveTextContent('inexistentes, inactivos o sin stock')
    expect(JSON.parse(localStorage.getItem('gymshop.guest-cart.v1') || '[]')).toHaveLength(1)
  })
})
