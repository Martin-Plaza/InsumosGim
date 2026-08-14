import { useCallback, useEffect, useRef, useState } from 'react'
import { Link, useLocation, useParams } from 'react-router-dom'
import { api, paymentKey, rotatePaymentKey } from '../../api/gymshop'
import type { Order, Payment } from '../../api/types'
import { checkoutErrorMessage, paymentExplanation, paymentLabels, terminalRetryablePayments } from './checkoutPresentation'

const money = (value: number, currency = 'ARS') => new Intl.NumberFormat('es-AR', { style: 'currency', currency }).format(value)

export function CheckoutResultPage({ canRefreshPayment }: { canRefreshPayment: boolean }) {
  const { orderId } = useParams()
  const location = useLocation()
  const initialState = location.state as { paymentError?: string } | null
  const id = Number(orderId)
  const [order, setOrder] = useState<Order | null>(null)
  const [payments, setPayments] = useState<Payment[]>([])
  const [loading, setLoading] = useState(true)
  const [busy, setBusy] = useState(false)
  const [error, setError] = useState(initialState?.paymentError ?? '')
  const actionInProgress = useRef(false)

  const load = useCallback(async () => {
    if (!Number.isInteger(id) || id < 1) { setError('La orden solicitada no es válida.'); setLoading(false); return }
    setLoading(true)
    try {
      const [currentOrder, currentPayments] = await Promise.all([api.order(id), api.orderPayments(id)])
      setOrder(currentOrder); setPayments([...currentPayments].sort((a, b) => b.id - a.id))
    } catch (value) { setError(checkoutErrorMessage(value)) }
    finally { setLoading(false) }
  }, [id])
  useEffect(() => { void load() }, [load])

  const handlePaymentAction = async () => {
    if (!order || actionInProgress.current) return
    actionInProgress.current = true; setBusy(true); setError('')
    try {
      const latest = payments[0]
      if (latest?.status === 'Pending') {
        await load()
        return
      }
      const key = latest && terminalRetryablePayments.includes(latest.status) ? rotatePaymentKey(order.id) : paymentKey(order.id)
      await api.createPayment(order.id, key)
      await load()
    } catch (value) { setError(checkoutErrorMessage(value)) }
    finally { actionInProgress.current = false; setBusy(false) }
  }

  const cancel = async () => {
    if (!order || actionInProgress.current) return
    actionInProgress.current = true; setBusy(true); setError('')
    try { setOrder(await api.cancelOrder(order.id, 'Cancelada por el usuario desde checkout')); setPayments(await api.orderPayments(order.id)) }
    catch (value) { setError(checkoutErrorMessage(value)) }
    finally { actionInProgress.current = false; setBusy(false) }
  }

  if (loading) return <div className="empty">Consultando orden y pago…</div>
  if (!order) return <div className="empty"><p>{error || 'No encontramos la orden.'}</p><Link to="/ordenes">Ir a mis órdenes</Link></div>
  const latest = payments[0]
  const paid = order.status === 'Paid' || latest?.status === 'Approved'
  const pending = latest?.status === 'Creating' || latest?.status === 'Pending'
  const canPay = order.status === 'Pending' && (!latest || pending || terminalRetryablePayments.includes(latest.status))

  return <section className="checkout-result">
    <div className="checkout-steps" aria-label="Progreso del checkout"><span className="done">1 Carrito</span><span className="done">2 Confirmación</span><span className="active">3 Resultado</span></div>
    <div className={`result-hero ${paid ? 'success' : pending ? 'pending' : ''}`}><p className="eyebrow">ORDEN #{order.id}</p><h1>{paid ? '¡Pago aprobado!' : order.status === 'Canceled' ? 'Orden cancelada' : pending ? 'Estamos confirmando tu pago' : 'Orden creada'}</h1><p>{paid ? 'La compra quedó confirmada.' : pending ? 'El proveedor todavía no informó el resultado. Podés salir y volver a consultar esta orden.' : 'La orden permanece pendiente hasta que se apruebe un pago.'}</p></div>
    {error && <div className="error" role="alert">{error}</div>}
    <div className="checkout-result-layout"><div className="order-summary-card"><h2>Resumen de la orden</h2>{order.items.map(item => <div className="result-line" key={item.productId}><span>{item.quantity} × {item.productName}</span><strong>{money(item.subtotal)}</strong></div>)}<div className="checkout-total"><span>Total</span><strong>{money(order.total)}</strong></div><p><strong>Entrega:</strong><br />{order.shippingAddress}</p><p><strong>Estado:</strong> {order.status}</p></div>
      <div className="payment-card"><div className="payment-card-title"><h2>Estado del pago</h2></div>{!latest ? <p>Todavía no hay intentos de pago.</p> : <><span className={`status status-${latest.status.toLowerCase()}`}>{paymentLabels[latest.status]}</span><p>{paymentExplanation(latest.status, Boolean(latest.checkoutUrl))}</p><p><strong>{money(latest.amount, latest.currency)}</strong></p>{latest.failureReason && <p className="payment-failure">{latest.failureReason}</p>}{latest.checkoutUrl && /^https?:\/\//i.test(latest.checkoutUrl) && <a className="primary link-button" href={latest.checkoutUrl}>Continuar con el proveedor</a>}</>}
        <div className="result-actions">{canPay && (!latest || terminalRetryablePayments.includes(latest.status) || canRefreshPayment) && <button className="primary" disabled={busy} onClick={() => void handlePaymentAction()}>{busy ? 'Procesando…' : latest && terminalRetryablePayments.includes(latest.status) ? 'Intentar pagar nuevamente' : latest ? 'Actualizar estado' : 'Iniciar pago'}</button>}{order.status === 'Pending' && <button disabled={busy} onClick={() => void cancel()}>Cancelar orden</button>}</div>
      </div></div>
    <div className="result-navigation"><Link to="/ordenes">Ver todas mis órdenes</Link><Link to="/catalogo">Seguir comprando</Link></div>
  </section>
}
