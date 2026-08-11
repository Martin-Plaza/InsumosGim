import { useCallback, useEffect, useMemo, useState } from 'react'
import { ApiError } from '../../api/client'
import { api } from '../../api/gymshop'
import type { CartItem, Product, User } from '../../api/types'
import { session } from '../../auth/session'
import { CartContext, type CartContextValue } from './cartContextValue'
import { guestCartStore, mergePlanStore, type MergePlan } from './guestCart'
const describe = (error: unknown) => error instanceof ApiError ? error.message : 'No pudimos actualizar el carrito.'

export function CartProvider({ children }: { children: React.ReactNode }) {
  const [user, setUser] = useState<User | null>(() => session.user())
  const [cart, setCart] = useState<CartItem[]>(() => user ? [] : guestCartStore.read())
  const [loading, setLoading] = useState(Boolean(user))
  const [error, setError] = useState('')
  const [notice, setNotice] = useState('')
  const [drawerOpen, setDrawerOpen] = useState(false)

  const refresh = useCallback(async () => {
    const currentUser = session.user()
    if (!currentUser) {
      setCart(guestCartStore.read())
      setLoading(false)
      return
    }
    setLoading(true)
    try { setCart((await api.cart()).items) }
    catch (value) { setError(describe(value)) }
    finally { setLoading(false) }
  }, [])

  const mergeGuestCart = useCallback(async (currentUser: User) => {
    const guestItems = guestCartStore.read()
    let serverCart = await api.cart()
    let plan = mergePlanStore.read()

    if (!plan || plan.userId !== currentUser.id) {
      const planned = []
      const unavailable: string[] = []
      const capped: string[] = []
      for (const guest of guestItems) {
        try {
          const product = await api.product(guest.productId)
          const serverQuantity = serverCart.items.find(item => item.productId === product.id)?.quantity ?? 0
          const requested = serverQuantity + guest.quantity
          const targetQuantity = Math.min(requested, product.stock)
          if (targetQuantity < requested) capped.push(product.name)
          if (targetQuantity > 0) planned.push({ productId: product.id, productName: product.name, guestQuantity: guest.quantity, targetQuantity })
          else unavailable.push(product.name)
        } catch (value) {
          if (value instanceof ApiError && value.status === 404) unavailable.push(guest.productName)
          else throw value
        }
      }
      plan = { userId: currentUser.id, items: planned } satisfies MergePlan
      mergePlanStore.write(plan)
      const messages = []
      if (planned.length) messages.push('Combinamos tu carrito de visitante con tu carrito guardado.')
      if (capped.length) messages.push(`Ajustamos al stock disponible: ${capped.join(', ')}.`)
      if (unavailable.length) messages.push(`No pudimos sumar productos inexistentes, inactivos o sin stock: ${unavailable.join(', ')}.`)
      setNotice(messages.join(' '))
    }

    for (const item of [...plan.items]) {
      const current = serverCart.items.find(serverItem => serverItem.productId === item.productId)
      if (current?.quantity !== item.targetQuantity) {
        serverCart = current
          ? await api.updateCartItem(item.productId, item.targetQuantity)
          : await api.addCartItem(item.productId, item.targetQuantity)
      }
      guestCartStore.remove(item.productId)
      plan = { ...plan, items: plan.items.filter(candidate => candidate.productId !== item.productId) }
      mergePlanStore.write(plan)
    }
    mergePlanStore.clear()
    setCart(serverCart.items)
  }, [])

  useEffect(() => {
    const changed = () => setUser(session.user())
    window.addEventListener('gymshop:session', changed)
    return () => window.removeEventListener('gymshop:session', changed)
  }, [])

  useEffect(() => {
    let active = true
    setError('')
    if (!user) {
      setCart(guestCartStore.read())
      setLoading(false)
      return () => { active = false }
    }
    setLoading(true)
    mergeGuestCart(user).catch(value => {
      if (active) setError(`${describe(value)} Tu carrito de visitante se conservó para reintentarlo.`)
    }).finally(() => { if (active) setLoading(false) })
    return () => { active = false }
  }, [mergeGuestCart, user])

  const add = useCallback(async (product: Product, quantity: number) => {
    setError('')
    setNotice('')
    const safeQuantity = Math.max(1, Math.floor(quantity))
    if (session.user()) {
      const existing = cart.find(item => item.productId === product.id)?.quantity ?? 0
      const accepted = Math.min(safeQuantity, Math.max(product.stock - existing, 0))
      if (accepted < 1) { setNotice(`Ya alcanzaste el stock disponible de ${product.name}.`); setDrawerOpen(true); return }
      const result = await api.addCartItem(product.id, accepted)
      setCart(result.items)
      if (accepted < safeQuantity) setNotice(`Sumamos ${accepted}; alcanzaste el límite de stock de ${product.name}.`)
    } else {
      const items = guestCartStore.read()
      const existing = items.find(item => item.productId === product.id)
      const target = Math.min((existing?.quantity ?? 0) + safeQuantity, product.stock)
      const next = existing
        ? items.map(item => item.productId === product.id ? { ...item, quantity: target, subtotal: item.unitPrice * target, stock: product.stock } : item)
        : [...items, guestCartStore.fromProduct(product, target)]
      guestCartStore.write(next)
      setCart(next)
      if (target < (existing?.quantity ?? 0) + safeQuantity) setNotice(`Ajustamos la cantidad al stock disponible de ${product.name}.`)
    }
    setDrawerOpen(true)
  }, [cart])

  const update = useCallback(async (productId: number, quantity: number) => {
    const item = cart.find(candidate => candidate.productId === productId)
    if (!item) return
    const target = Math.min(Math.max(1, Math.floor(quantity)), item.stock)
    if (session.user()) setCart((await api.updateCartItem(productId, target)).items)
    else {
      const next = cart.map(candidate => candidate.productId === productId ? { ...candidate, quantity: target, subtotal: candidate.unitPrice * target } : candidate)
      guestCartStore.write(next); setCart(next)
    }
    if (target !== quantity) setNotice(`La cantidad máxima disponible de ${item.productName} es ${item.stock}.`)
  }, [cart])

  const remove = useCallback(async (productId: number) => {
    if (session.user()) setCart((await api.removeCartItem(productId)).items)
    else setCart(guestCartStore.remove(productId))
  }, [])

  const clear = useCallback(async () => {
    if (session.user()) { await api.clearCart(); setCart([]) }
    else { guestCartStore.clear(); setCart([]) }
  }, [])

  const value = useMemo<CartContextValue>(() => ({
    items: cart,
    total: cart.reduce((sum, item) => sum + item.subtotal, 0),
    count: cart.reduce((sum, item) => sum + item.quantity, 0),
    loading, error, notice, drawerOpen, add, update, remove, clear, refresh,
    openDrawer: () => setDrawerOpen(true),
    closeDrawer: () => setDrawerOpen(false),
    dismissMessages: () => { setError(''); setNotice('') },
  }), [add, cart, clear, drawerOpen, error, loading, notice, refresh, remove, update])

  return <CartContext.Provider value={value}>{children}</CartContext.Provider>
}
