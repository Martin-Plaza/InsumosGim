import { useCallback, useEffect, useState } from 'react'
import { api } from '../../api/gymshop'
import type { Order, OrderFilters, OrderPage, OrderStatus } from '../../api/types'
import { money, storefront } from '../../config/storefront'
import { describeAdminError } from './adminErrors'
import { AdminEmpty, AdminFeedback, AdminLoading } from './adminUi'

const initialPage: OrderPage = { items: [], page: 1, pageSize: 20, totalItems: 0, totalPages: 0 }
const statusLabels: Record<string, string> = { Pending: 'Pendiente', Paid: 'Pagado', Preparing: 'Preparando', Shipped: 'Enviado', Delivered: 'Entregado', Canceled: 'Cancelado', Refunded: 'Reembolsado', Creating: 'Creando pago', Approved: 'Aprobado', Rejected: 'Rechazado', Expired: 'Vencido' }
const nextStatuses: Partial<Record<OrderStatus, OrderStatus[]>> = { Pending: ['Canceled'], Paid: ['Preparing'], Preparing: ['Shipped'], Shipped: ['Delivered'] }
const formatDate = (value: string) => new Intl.DateTimeFormat(storefront.market.locale, { dateStyle: 'medium', timeStyle: 'short' }).format(new Date(value))
const utcStart = (value: string) => value ? new Date(`${value}T00:00:00`).toISOString() : undefined
const utcEnd = (value: string) => value ? new Date(`${value}T23:59:59.999`).toISOString() : undefined
function Status({ value }: { value: string | null }) { return value ? <span className={`status status-${value.toLowerCase()}`}>{statusLabels[value] || value}</span> : <span>—</span> }

export function AdminOrders() {
  const [page, setPage] = useState(initialPage)
  const [detail, setDetail] = useState<Order | null>(null)
  const [draft, setDraft] = useState({ search: '', status: '', from: '', to: '' })
  const [filters, setFilters] = useState<OrderFilters>({ page: 1, pageSize: 20 })
  const [loading, setLoading] = useState(true)
  const [pending, setPending] = useState(false)
  const [error, setError] = useState('')
  const [success, setSuccess] = useState('')

  const load = useCallback(async (next = filters) => {
    setLoading(true); setError('')
    try { setPage(await api.orders(next)) } catch (value) { setError(describeAdminError(value)) } finally { setLoading(false) }
  }, [filters])
  useEffect(() => { void load(filters) }, [filters, load])

  const apply = () => setFilters({ page: 1, pageSize: 20, search: draft.search.trim(), status: draft.status, fromUtc: utcStart(draft.from), toUtc: utcEnd(draft.to) })
  const clear = () => { setDraft({ search: '', status: '', from: '', to: '' }); setFilters({ page: 1, pageSize: 20 }) }
  const open = async (id: number) => {
    setPending(true); setError(''); setSuccess('')
    try { setDetail(await api.order(id)) } catch (value) { setError(describeAdminError(value)) } finally { setPending(false) }
  }
  const changeStatus = async (status: OrderStatus) => {
    if (!detail || pending || !window.confirm(`¿Cambiar el pedido #${detail.id} a “${statusLabels[status]}”?`)) return
    setPending(true); setError(''); setSuccess('')
    try {
      await api.setOrderStatus(detail.id, status, detail.updatedAt)
      const updated = await api.order(detail.id)
      setDetail(updated); setSuccess(`Pedido #${detail.id} actualizado a ${statusLabels[status]}.`); await load()
    } catch (value) { setError(describeAdminError(value)) } finally { setPending(false) }
  }
  const transitions = detail ? nextStatuses[detail.status] || [] : []

  return <section className="admin-page">
    <div className="admin-page-heading"><div><p className="eyebrow">OPERACIONES</p><h1>Pedidos</h1><p>Gestioná el ciclo de los pedidos y consultá sus pagos.</p></div><strong>{page.totalItems} pedidos</strong></div>
    <AdminFeedback error={error} success={success} />
    <form className="admin-filters order-filters" aria-label="Filtros de pedidos" onSubmit={event => { event.preventDefault(); apply() }}>
      <label className="admin-search">Buscar<input type="search" placeholder="Número, email o cliente" value={draft.search} onChange={event => setDraft(current => ({ ...current, search: event.target.value }))} /></label>
      <label>Estado<select value={draft.status} onChange={event => setDraft(current => ({ ...current, status: event.target.value }))}><option value="">Todos</option>{['Pending','Paid','Preparing','Shipped','Delivered','Canceled'].map(value => <option key={value} value={value}>{statusLabels[value]}</option>)}</select></label>
      <label>Desde<input type="date" value={draft.from} onChange={event => setDraft(current => ({ ...current, from: event.target.value }))} /></label>
      <label>Hasta<input type="date" value={draft.to} onChange={event => setDraft(current => ({ ...current, to: event.target.value }))} /></label>
      <div className="order-filter-actions"><button disabled={loading}>Aplicar</button><button type="button" onClick={clear}>Limpiar</button></div>
    </form>
    {loading && page.items.length === 0 ? <AdminLoading label="Cargando pedidos…" /> : error && page.items.length === 0 ? <button onClick={() => void load()}>Reintentar</button> : page.items.length === 0 ? <AdminEmpty>No hay pedidos que coincidan con los filtros.</AdminEmpty> : <div className="admin-order-list">
      <div className="admin-order-row admin-order-header" aria-hidden="true"><span>Pedido</span><span>Cliente</span><span>Fecha</span><span>Total</span><span>Pedido</span><span>Pago</span></div>
      {page.items.map(order => <button type="button" className="admin-order-row" key={order.id} disabled={pending} onClick={() => void open(order.id)}><b>#{order.id}</b><span data-label="Cliente"><strong>{order.userName}</strong><small>{order.userEmail}</small></span><span data-label="Fecha">{formatDate(order.createdAt)}</span><strong data-label="Total">{money(order.total)}</strong><span data-label="Pedido"><Status value={order.status} /></span><span data-label="Pago"><Status value={order.lastPaymentStatus} /></span></button>)}
    </div>}
    {page.totalPages > 1 && <nav className="admin-pagination" aria-label="Paginación de pedidos"><button disabled={loading || page.page <= 1} onClick={() => setFilters(current => ({ ...current, page: page.page - 1 }))}>Anterior</button><span>Página {page.page} de {page.totalPages}</span><button disabled={loading || page.page >= page.totalPages} onClick={() => setFilters(current => ({ ...current, page: page.page + 1 }))}>Siguiente</button></nav>}
    {detail && <aside className="drawer admin-order-detail" aria-label={`Detalle del pedido ${detail.id}`} aria-busy={pending}><button className="close" onClick={() => setDetail(null)} aria-label="Cerrar detalle">×</button><p className="eyebrow">PEDIDO #{detail.id}</p><h2>{money(detail.total)}</h2><Status value={detail.status} />
      <section><h3>Cliente</h3><p><strong>{detail.userName}</strong><br />{detail.userEmail}<br />{detail.userPhone || 'Sin teléfono informado'}</p><p>{detail.shippingAddress || 'Sin dirección informada'}</p></section>
      <section><h3>Productos</h3>{detail.items.map(item => <div className="list-row" key={item.productId}><span>{item.quantity} × {item.productName}<small>{money(item.unitPrice)} c/u</small></span><strong>{money(item.subtotal)}</strong></div>)}<div className="list-row"><strong>Total</strong><strong>{money(detail.total)}</strong></div></section>
      <section><h3>Pago</h3>{detail.payments.length === 0 ? <p>Sin pagos registrados.</p> : detail.payments.map(payment => <div className="payment" key={payment.id}><span>#{payment.id} · {payment.provider}<small>{money(payment.amount)} {payment.currency}</small></span><Status value={payment.status} /><small>Creado: {formatDate(payment.createdAt)}{payment.paidAt ? ` · Pagado: ${formatDate(payment.paidAt)}` : ''}</small></div>)}</section>
      <section><h3>Fechas</h3><p>Creado: {formatDate(detail.createdAt)}<br />Última actualización: {detail.updatedAt ? formatDate(detail.updatedAt) : 'Sin cambios posteriores'}</p>{detail.cancellationReason && <p>Motivo: {detail.cancellationReason}</p>}</section>
      {transitions.length > 0 && <div className="actions">{transitions.map(status => <button className={status === 'Canceled' ? '' : 'primary'} disabled={pending} key={status} onClick={() => void changeStatus(status)}>{pending ? 'Guardando…' : `Marcar como ${statusLabels[status].toLowerCase()}`}</button>)}</div>}
      {transitions.length === 0 && <p className="admin-footnote">Este estado no admite cambios administrativos.</p>}
    </aside>}
  </section>
}
