import { useCallback, useEffect, useRef, useState } from 'react'
import { api, paymentKey } from '../../api/gymshop'
import type { Order, OrderSummary, Payment } from '../../api/types'
import { money, storefront } from '../../config/storefront'
import { describeAdminError } from '../admin/adminErrors'
import { AdminEmpty, AdminFeedback, AdminLoading } from '../admin/adminUi'
import { AdminOrders } from '../admin/AdminOrders'
import { orderStatusLabel } from './orderPresentation'

const date = (value: string) => new Intl.DateTimeFormat(storefront.market.locale, { dateStyle: 'medium', timeStyle: 'short' }).format(new Date(value))
const labels: Record<string, string> = { Pending: 'Pendiente', Paid: 'Pagada', Preparing: 'Preparando', Shipped: 'Enviada', Delivered: 'Entregada', Canceled: 'Cancelada', Refunded: 'Reembolsada', Creating: 'Creando pago', CreationFailed: 'Falló la creación', Approved: 'Aprobado', Rejected: 'Rechazado', Expired: 'Vencido' }
function Status({ value, deliveryMethod }: { value: string | null; deliveryMethod?: Order['deliveryMethod'] }) { return value ? <span className={`status status-${value.toLowerCase()}`}>{deliveryMethod ? orderStatusLabel(value, deliveryMethod, true) : labels[value] || value}</span> : <span>—</span> }

export function OrdersView({ admin = false }: { admin?: boolean }) {
  return admin ? <AdminOrders /> : <CustomerOrders />
}

function CustomerOrders() {
  const [orders, setOrders] = useState<OrderSummary[]>([]); const [detail, setDetail] = useState<Order | null>(null); const [payments, setPayments] = useState<Payment[]>([])
  const [loading, setLoading] = useState(true); const [error, setError] = useState(''); const [success, setSuccess] = useState(''); const [pending, setPending] = useState(false)
  const loadInFlight = useRef(false)
  const load = useCallback(async () => {
    if (loadInFlight.current) return
    loadInFlight.current = true; setLoading(true); setError('')
    try { setOrders(await api.myOrders()) } catch (value) { setError(describeAdminError(value)) } finally { loadInFlight.current = false; setLoading(false) }
  }, [])
  useEffect(() => { void load() }, [load])
  const run = async (action: () => Promise<void>) => { if (pending) return; setPending(true); setError(''); setSuccess(''); try { await action() } catch (value) { setError(describeAdminError(value)) } finally { setPending(false) } }
  const open = (id: number) => void run(async () => { const [order, list] = await Promise.all([api.order(id), api.orderPayments(id)]); setDetail(order); setPayments(list) })
  const pay = (id: number) => void run(async () => { const payment = await api.createPayment(id, paymentKey(id)); setPayments(await api.orderPayments(id)); if (payment.checkoutUrl) window.location.assign(payment.checkoutUrl) })
  return <section><div className="section-title"><div><p className="eyebrow">SEGUIMIENTO</p><h1>Mis órdenes</h1></div></div>
    <AdminFeedback error={error} success={success} />
    {loading && orders.length === 0 ? <AdminLoading label="Cargando pedidos…" /> : orders.length === 0 ? <AdminEmpty>No hay pedidos para mostrar.</AdminEmpty> : <div className="list">{orders.map(order => <button disabled={pending} className="order-row" key={order.id} onClick={() => open(order.id)}><b>#{order.id}</b><span>{date(order.createdAt)}</span>{order.userEmail && <span>{order.userEmail}</span>}<strong>{money(order.total)}</strong><span className="order-state"><small>Orden</small><Status value={order.status} deliveryMethod={order.deliveryMethod} /></span><span className="order-state"><small>Pago</small><Status value={order.lastPaymentStatus} /></span></button>)}</div>}
    {detail && <div className="drawer"><button className="close" onClick={() => setDetail(null)} aria-label="Cerrar detalle">×</button><p className="eyebrow">ORDEN #{detail.id}</p><h2>{money(detail.total)}</h2><Status value={detail.status} deliveryMethod={detail.deliveryMethod} /><section><h3>Entrega</h3><p><strong>{detail.deliveryMethod === 'StorePickup' ? 'Retiro en tienda' : 'Envío a domicilio'}</strong>{detail.shippingAddress && <><br />{detail.shippingAddress}</>}<br />Costo: {detail.shippingCost ? money(detail.shippingCost) : 'Sin costo'}</p>{detail.deliveryMethod === 'StorePickup' && <div className="pickup-details"><strong>{detail.pickupAddress}</strong>{detail.pickupHours && <span>{detail.pickupHours}</span>}{detail.pickupInstructions && <p>{detail.pickupInstructions}</p>}</div>}{detail.deliveryMethod !== 'StorePickup' && detail.carrier && <p>{detail.carrier}{detail.trackingNumber && <> · {detail.trackingNumber}</>}</p>}{detail.deliveryMethod !== 'StorePickup' && detail.trackingUrl && <a className="link-button primary" href={detail.trackingUrl} target="_blank" rel="noopener noreferrer">Seguir mi paquete</a>}</section><div className="list">{detail.items.map(item => <div className="list-row" key={item.productId}><span>{item.quantity} × {item.productName}</span><strong>{money(item.subtotal)}</strong></div>)}</div>
      {detail.status === 'Pending' && <div className="actions"><button disabled={pending} className="primary" onClick={() => pay(detail.id)}>{pending ? 'Procesando…' : storefront.copy.orderPaymentAction}</button><button disabled={pending} onClick={() => void run(async () => { const updated = await api.cancelOrder(detail.id, 'Cancelada desde el frontend'); setDetail(updated); await load(); setSuccess('Pedido cancelado correctamente.') })}>Cancelar orden</button></div>}
      <h3>Pagos</h3>{payments.length === 0 ? <p>Sin pagos.</p> : payments.map(payment => <div className="payment" key={payment.id}><span>#{payment.id} · {payment.provider}</span><Status value={payment.status} />{payment.status === 'Creating' && !payment.checkoutUrl && <small>El pago se está creando. Consultá nuevamente en unos instantes.</small>}{payment.failureReason && <small>{payment.failureReason}</small>}</div>)}
    </div>}
  </section>
}
