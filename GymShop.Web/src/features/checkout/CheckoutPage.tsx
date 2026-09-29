import { FormEvent, useCallback, useEffect, useRef, useState } from 'react'
import { Link, useNavigate } from 'react-router-dom'
import { ApiError } from '../../api/client'
import { api, paymentKey, savePaymentProvider } from '../../api/gymshop'
import type { DeliveryMethod, OrderSummary, PaymentMethods, PaymentProvider, ShippingOptions } from '../../api/types'
import { ProductImage } from '../catalog/ProductImage'
import { money } from '../../config/storefront'
import { useCart } from '../cart/useCart'
import { checkoutErrorMessage, isCheckoutPricingConflict, isPendingOrderConflict } from './checkoutPresentation'


export function CheckoutPage() {
  const cart = useCart()
  const navigate = useNavigate()
  const [address, setAddress] = useState('')
  const [deliveryMethod, setDeliveryMethod] = useState<DeliveryMethod>('HomeDelivery')
  const [paymentProvider, setPaymentProvider] = useState<PaymentProvider>('BankTransfer')
  const [paymentMethods, setPaymentMethods] = useState<PaymentMethods | null>(null)
  const [paymentMethodsLoading, setPaymentMethodsLoading] = useState(true)
  const [homeDeliveryCost, setHomeDeliveryCost] = useState<number | null>(null)
  const [pickupDetails, setPickupDetails] = useState<Pick<ShippingOptions, 'pickupAddress' | 'pickupInstructions' | 'pickupHours'> | null>(null)
  const [shippingError, setShippingError] = useState('')
  const [shippingLoading, setShippingLoading] = useState(true)
  const [error, setError] = useState('')
  const [busy, setBusy] = useState(false)
  const [recoveryOrder, setRecoveryOrder] = useState<OrderSummary | null>(null)
  const submitting = useRef(false)
  const loadShippingOptions = useCallback(async () => {
    setShippingLoading(true)
    try {
      const value = await api.shippingOptions()
      if (!Number.isFinite(value.homeDeliveryCost) || value.homeDeliveryCost < 0) throw new Error('invalid shipping cost')
      setHomeDeliveryCost(value.homeDeliveryCost)
      setPickupDetails({ pickupAddress: value.pickupAddress, pickupInstructions: value.pickupInstructions, pickupHours: value.pickupHours })
      setShippingError(value.pickupAddress?.trim() ? '' : 'El retiro en tienda no está disponible porque falta configurar su dirección.')
    } catch {
      setHomeDeliveryCost(null)
      setPickupDetails(null)
      setShippingError('No pudimos obtener las opciones de entrega.')
    } finally { setShippingLoading(false) }
  }, [])
  useEffect(() => { void loadShippingOptions() }, [loadShippingOptions])
  useEffect(() => {
    let active = true
    api.paymentMethods()
      .then(value => { if (active) setPaymentMethods(value) })
      .catch(() => { if (active) setPaymentMethods({ bankTransferAvailable: true, mercadoPagoAvailable: false, mercadoPagoUnavailableReason: 'No pudimos verificar la disponibilidad de Mercado Pago.' }) })
      .finally(() => { if (active) setPaymentMethodsLoading(false) })
    return () => { active = false }
  }, [])

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
    if (deliveryMethod === 'HomeDelivery' && !normalizedAddress) { setError('La dirección de envío es obligatoria.'); return }
    if (deliveryMethod === 'StorePickup' && !pickupDetails?.pickupAddress?.trim()) { setError('El retiro en tienda no está disponible. Reintentá cargar las opciones de entrega.'); return }
    if (normalizedAddress.length > 300) { setError('La dirección de envío no puede superar los 300 caracteres.'); return }
    const shippingCost = deliveryMethod === 'HomeDelivery' ? homeDeliveryCost : 0
    if (shippingCost === null) { setError('Esperá mientras cargamos el costo de envío.'); return }
    const isFreeCheckout = cart.total + shippingCost === 0
    if (!isFreeCheckout && paymentMethodsLoading) { setError('Esperá mientras verificamos los medios de pago disponibles.'); return }
    if (!isFreeCheckout && paymentProvider === 'MercadoPago' && !paymentMethods?.mercadoPagoAvailable) { setError(paymentMethods?.mercadoPagoUnavailableReason || 'Mercado Pago no está disponible.'); return }
    submitting.current = true; setBusy(true); setError(''); setRecoveryOrder(null)
    try {
      const order = await api.checkout({ deliveryMethod, shippingAddress: deliveryMethod === 'HomeDelivery' ? normalizedAddress : null, expectedShippingCost: shippingCost, expectedSubtotal: cart.subtotal, expectedDiscount: cart.discount })
      sessionStorage.setItem('gymshop.last-order', String(order.id))
      await cart.refresh()
      let paymentError = ''
      if (order.total > 0) {
        savePaymentProvider(order.id, paymentProvider)
        try { await api.createPayment(order.id, paymentProvider, paymentKey(order.id)) }
        catch (value) { paymentError = checkoutErrorMessage(value) }
      }
      navigate(`/checkout/orden/${order.id}`, { replace: true, state: { paymentError } })
    } catch (value) {
      if (isCheckoutPricingConflict(value)) {
        setRecoveryOrder(null)
        await Promise.all([cart.refresh(), loadShippingOptions()])
        setError('Actualizamos los precios, el descuento y el costo de envío. Revisá el nuevo total y confirmá nuevamente.')
      } else {
        setError(checkoutErrorMessage(value))
        if (!(value instanceof ApiError) || isPendingOrderConflict(value)) await recoverPendingOrder()
        await cart.refresh()
      }
    } finally {
      submitting.current = false; setBusy(false)
    }
  }

  if (cart.loading) return <div className="empty">Validando tu carrito…</div>
  if (cart.items.length === 0) return <section className="checkout-empty"><p className="eyebrow">CHECKOUT</p><h1>Tu carrito está vacío</h1><p>Agregá productos antes de iniciar una compra.</p><Link className="primary link-button" to="/catalogo">Ir al catálogo</Link></section>
  const displayedTotal = cart.total + (deliveryMethod === 'HomeDelivery' ? homeDeliveryCost ?? 0 : 0)
  const isFreeCheckout = displayedTotal === 0

  return <section className="checkout-page">
    <div className="checkout-steps" aria-label="Progreso del checkout"><span className="done">1 Carrito</span><span className="active">2 Confirmación</span><span>3 Resultado</span></div>
    <div className="section-title"><div><p className="eyebrow">REVISIÓN FINAL</p><h1>Confirmá tu compra</h1></div><Link to="/carrito">Editar carrito</Link></div>
    {error && <div className="error" role="alert">{error}</div>}
    {shippingError && <div className="error shipping-cost-error" role="alert"><span>{shippingError}</span><button type="button" onClick={() => void loadShippingOptions()} disabled={shippingLoading}>{shippingLoading ? 'Reintentando…' : 'Reintentar opciones de entrega'}</button></div>}
    {recoveryOrder && <div className="notice" role="status">Encontramos la orden pendiente #{recoveryOrder.id}. No crearemos otra hasta que la revises. <Link to={`/checkout/orden/${recoveryOrder.id}`}>Ver orden</Link></div>}
    <div className="checkout-layout"><div>
      <div className="checkout-review-list">{cart.items.map(item => <article key={item.productId}><div className="checkout-thumb"><ProductImage src={item.imageUrl} alt={item.productName} /></div><div><h3>{item.productName}</h3><p>{item.quantity} × {money(item.unitPrice)}</p></div><strong>{money(item.subtotal)}</strong></article>)}</div>
    </div><form className="checkout-confirmation" onSubmit={submit}>
      <p className="eyebrow">ENTREGA</p><h2>Modalidad de entrega</h2><div className="delivery-options" role="radiogroup" aria-label="Modalidad de entrega"><label><input type="radio" name="delivery" checked={deliveryMethod === 'StorePickup'} onChange={() => setDeliveryMethod('StorePickup')} /> Retiro en tienda <small>Sin costo</small></label><label><input type="radio" name="delivery" checked={deliveryMethod === 'HomeDelivery'} onChange={() => setDeliveryMethod('HomeDelivery')} /> Envío a domicilio <small>{homeDeliveryCost === null ? shippingLoading ? 'Cargando costo…' : 'Costo no disponible' : money(homeDeliveryCost)}</small></label></div>{deliveryMethod === 'HomeDelivery' ? <><label>Dirección completa<textarea value={address} onChange={event => setAddress(event.target.value)} required maxLength={300} placeholder="Calle, número, localidad, provincia y referencia" /></label><small>{address.length}/300 caracteres</small></> : pickupDetails && <div className="pickup-details"><strong>{pickupDetails.pickupAddress}</strong>{pickupDetails.pickupHours && <span>{pickupDetails.pickupHours}</span>}{pickupDetails.pickupInstructions && <p>{pickupDetails.pickupInstructions}</p>}</div>}
      {isFreeCheckout ? <div className="notice" role="status">El cupón cubre el total. Este pedido no requiere pago.</div> : <fieldset className="payment-methods" aria-busy={paymentMethodsLoading}><legend>Medio de pago</legend><label><input type="radio" name="paymentProvider" checked={paymentProvider === 'BankTransfer'} onChange={() => setPaymentProvider('BankTransfer')} /> Transferencia bancaria<small>Quedará pendiente hasta verificar la acreditación.</small></label><label><input type="radio" name="paymentProvider" checked={paymentProvider === 'MercadoPago'} disabled={paymentMethodsLoading || !paymentMethods?.mercadoPagoAvailable} onChange={() => setPaymentProvider('MercadoPago')} /> Mercado Pago<small>{paymentMethodsLoading ? 'Verificando disponibilidad…' : paymentMethods?.mercadoPagoAvailable ? 'La aprobación la informa Mercado Pago.' : paymentMethods?.mercadoPagoUnavailableReason || 'No disponible.'}</small></label></fieldset>}
      <div className="checkout-total"><span>Subtotal</span><strong>{money(cart.subtotal)}</strong></div>{cart.discount > 0 && <div className="checkout-total"><span>Descuento {cart.couponCode && `(${cart.couponCode})`}</span><strong>−{money(cart.discount)}</strong></div>}<div className="checkout-total"><span>Envío</span><strong>{deliveryMethod === 'StorePickup' ? 'Sin costo' : homeDeliveryCost === null ? '—' : money(homeDeliveryCost)}</strong></div><div className="checkout-total"><span>Total</span><strong>{money(displayedTotal)}</strong></div><p className="checkout-disclaimer">El cupón se aplica a los productos y se vuelve a validar al confirmar. El envío se agrega después del descuento.</p>
      <button className="primary" disabled={busy || (!isFreeCheckout && paymentMethodsLoading) || (deliveryMethod === 'HomeDelivery' && homeDeliveryCost === null) || (deliveryMethod === 'StorePickup' && !pickupDetails?.pickupAddress?.trim())}>{busy ? 'Confirmando compra…' : isFreeCheckout ? 'Confirmar pedido' : 'Confirmar y pagar'}</button><Link className="secondary-link" to="/carrito">Volver al carrito</Link>
    </form></div>
  </section>
}
