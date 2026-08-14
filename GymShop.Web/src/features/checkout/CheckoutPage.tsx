import { FormEvent, useRef, useState } from 'react'
import { Link, useNavigate } from 'react-router-dom'
import { ApiError } from '../../api/client'
import { api, paymentKey } from '../../api/gymshop'
import type { OrderSummary } from '../../api/types'
import { ProductImage } from '../catalog/ProductImage'
import { useCart } from '../cart/useCart'
import { checkoutErrorMessage } from './checkoutPresentation'

const money = (value: number) => new Intl.NumberFormat('es-AR', { style: 'currency', currency: 'ARS' }).format(value)

export function CheckoutPage() {
  const cart = useCart()
  const navigate = useNavigate()
  const [address, setAddress] = useState('')
  const [error, setError] = useState('')
  const [busy, setBusy] = useState(false)
  const [recoveryOrder, setRecoveryOrder] = useState<OrderSummary | null>(null)
  const submitting = useRef(false)

  const recoverPendingOrder = async () => {
    try {
      const pending = (await api.myOrders()).filter(order => order.status === 'Pending').sort((a, b) => b.id - a.id)[0]
      setRecoveryOrder(pending ?? null)
    } catch { /* el error original sigue siendo el dato útil */ }
  }

  const submit = async (event: FormEvent) => {
    event.preventDefault()
    if (submitting.current) return
    const normalizedAddress = address.trim()
    if (!normalizedAddress) { setError('La dirección de envío es obligatoria.'); return }
    if (normalizedAddress.length > 300) { setError('La dirección de envío no puede superar los 300 caracteres.'); return }
    submitting.current = true; setBusy(true); setError(''); setRecoveryOrder(null)
    try {
      const order = await api.checkout(normalizedAddress)
      sessionStorage.setItem('gymshop.last-order', String(order.id))
      await cart.refresh()
      let paymentError = ''
      try { await api.createPayment(order.id, paymentKey(order.id)) }
      catch (value) { paymentError = checkoutErrorMessage(value) }
      navigate(`/checkout/orden/${order.id}`, { replace: true, state: { paymentError } })
    } catch (value) {
      setError(checkoutErrorMessage(value))
      if (!(value instanceof ApiError) || value.status === 409) await recoverPendingOrder()
      await cart.refresh()
    } finally {
      submitting.current = false; setBusy(false)
    }
  }

  if (cart.loading) return <div className="empty">Validando tu carrito…</div>
  if (cart.items.length === 0) return <section className="checkout-empty"><p className="eyebrow">CHECKOUT</p><h1>Tu carrito está vacío</h1><p>Agregá productos antes de iniciar una compra.</p><Link className="primary link-button" to="/catalogo">Ir al catálogo</Link></section>

  return <section className="checkout-page">
    <div className="checkout-steps" aria-label="Progreso del checkout"><span className="done">1 Carrito</span><span className="active">2 Confirmación</span><span>3 Resultado</span></div>
    <div className="section-title"><div><p className="eyebrow">REVISIÓN FINAL</p><h1>Confirmá tu compra</h1></div><Link to="/carrito">Editar carrito</Link></div>
    {error && <div className="error" role="alert">{error}</div>}
    {recoveryOrder && <div className="notice" role="status">Encontramos la orden pendiente #{recoveryOrder.id}. No crearemos otra hasta que la revises. <Link to={`/checkout/orden/${recoveryOrder.id}`}>Ver orden</Link></div>}
    <div className="checkout-layout"><div>
      <div className="checkout-review-list">{cart.items.map(item => <article key={item.productId}><div className="checkout-thumb"><ProductImage src={item.imageUrl} alt={item.productName} /></div><div><h3>{item.productName}</h3><p>{item.quantity} × {money(item.unitPrice)}</p></div><strong>{money(item.subtotal)}</strong></article>)}</div>
    </div><form className="checkout-confirmation" onSubmit={submit}>
      <p className="eyebrow">ENTREGA</p><h2>Dirección de envío</h2><label>Dirección completa<textarea value={address} onChange={event => setAddress(event.target.value)} required maxLength={300} placeholder="Calle, número, localidad, provincia y referencia" /></label><small>{address.length}/300 caracteres</small>
      <div className="checkout-total"><span>Total</span><strong>{money(cart.total)}</strong></div><p className="checkout-disclaimer">No se agregan costos de envío, cuotas ni impuestos porque todavía no forman parte del contrato.</p>
      <button className="primary" disabled={busy}>{busy ? 'Confirmando compra…' : 'Confirmar y pagar'}</button><Link className="secondary-link" to="/carrito">Volver al carrito</Link>
    </form></div>
  </section>
}
