import { FormEvent, useEffect, useRef, useState } from 'react'
import { api } from '../../api/gymshop'
import type { Coupon, CouponInput, CouponPage, CouponType } from '../../api/types'
import { ApiError } from '../../api/client'
import { money } from '../../config/storefront'
import { AdminEmpty, AdminFeedback, AdminLoading } from './adminUi'
import { localDateTimeInputToUtcIso, utcIsoToLocalDateTimeInput } from './couponDateTime'

const empty = (): CouponInput => ({ code: '', name: '', type: 'Percentage', value: 0, minimumPurchase: null, maximumDiscount: null, startsAtUtc: null, endsAtUtc: null, totalUsageLimit: null, usageLimitPerUser: null, isActive: true })
const input = (coupon: Coupon): CouponInput => ({ code: coupon.code, name: coupon.name, type: coupon.type, value: coupon.value, minimumPurchase: coupon.minimumPurchase, maximumDiscount: coupon.maximumDiscount, startsAtUtc: coupon.startsAtUtc, endsAtUtc: coupon.endsAtUtc, totalUsageLimit: coupon.totalUsageLimit, usageLimitPerUser: coupon.usageLimitPerUser, isActive: coupon.isActive })
const errorText = (value: unknown) => value instanceof ApiError ? value.message : 'No se pudo completar la operación.'

export function CouponsAdmin() {
  const [result, setResult] = useState<CouponPage>({ items: [], page: 1, pageSize: 20, totalItems: 0, totalPages: 0 })
  const [filters, setFilters] = useState({ search: '', status: '', type: '', validity: '' })
  const [page, setPage] = useState(1); const [loading, setLoading] = useState(true); const [error, setError] = useState(''); const [notice, setNotice] = useState('')
  const [editing, setEditing] = useState<Coupon | null | undefined>(undefined); const [form, setForm] = useState<CouponInput>(empty()); const [busy, setBusy] = useState(false); const submitting = useRef(false)
  const requestSequence = useRef(0)
  const currentQuery = useRef({ filters, page })
  currentQuery.current = { filters, page }
  const load = () => {
    const requestId = ++requestSequence.current
    const query = currentQuery.current
    setLoading(true); setError('')
    api.coupons({ ...query.filters, page: query.page, pageSize: 20 })
      .then(value => { if (requestId === requestSequence.current) setResult(value) })
      .catch(value => { if (requestId === requestSequence.current) setError(errorText(value)) })
      .finally(() => { if (requestId === requestSequence.current) setLoading(false) })
  }
  useEffect(load, [page, filters.search, filters.status, filters.type, filters.validity])
  const change = <K extends keyof CouponInput>(key: K, value: CouponInput[K]) => setForm(current => ({ ...current, [key]: value }))
  const save = async (event: FormEvent) => { event.preventDefault(); if (submitting.current) return; submitting.current = true; setBusy(true); setError(''); try { if (editing) await api.updateCoupon(editing.id, form); else await api.createCoupon(form); setNotice(`Cupón ${editing ? 'actualizado' : 'creado'} correctamente.`); setEditing(undefined); setForm(empty()); load() } catch (value) { setError(errorText(value)) } finally { submitting.current = false; setBusy(false) } }
  const toggle = async (coupon: Coupon) => { if (!confirm(`¿${coupon.isActive ? 'Desactivar' : 'Activar'} ${coupon.code}?`)) return; setBusy(true); try { await api.setCouponStatus(coupon.id, !coupon.isActive); setNotice('Estado actualizado.'); load() } catch (value) { setError(errorText(value)) } finally { setBusy(false) } }
  const numeric = (value: string) => value === '' ? null : Number(value)
  return <section className="admin-page"><div className="admin-page-heading"><div><p className="eyebrow">PROMOCIONES</p><h1>Cupones</h1><p>Gestioná descuentos generales y sus límites de uso.</p></div><button className="primary" onClick={() => { setEditing(null); setForm(empty()) }}>Nuevo cupón</button></div><AdminFeedback error={error} success={notice} />
    <div className="admin-filters"><label>Buscar<input value={filters.search} onChange={e => { setPage(1); setFilters(x => ({ ...x, search: e.target.value })) }} /></label><label>Estado<select value={filters.status} onChange={e => { setPage(1); setFilters(x => ({ ...x, status: e.target.value })) }}><option value="">Todos</option><option value="true">Activos</option><option value="false">Inactivos</option></select></label><label>Tipo<select value={filters.type} onChange={e => setFilters(x => ({ ...x, type: e.target.value }))}><option value="">Todos</option><option value="Percentage">Porcentaje</option><option value="FixedAmount">Monto fijo</option></select></label><label>Vigencia<select value={filters.validity} onChange={e => setFilters(x => ({ ...x, validity: e.target.value }))}><option value="">Todas</option><option value="current">Vigentes</option><option value="future">Futuros</option><option value="expired">Vencidos</option></select></label></div>
    {loading && result.items.length === 0 ? <AdminLoading label="Cargando cupones…" /> : result.items.length === 0 ? <AdminEmpty>No hay cupones para estos filtros.</AdminEmpty> : <div className="admin-product-list">{result.items.map(c => <article className="admin-product-row" key={c.id}><div><strong>{c.code}</strong><small>{c.name}</small></div><span>{c.type === 'Percentage' ? `${c.value}%` : money(c.value)}</span><span>{c.reservedUses + c.consumedUses}{c.totalUsageLimit ? ` / ${c.totalUsageLimit}` : ''} usos</span><span className={`admin-pill ${c.isActive ? 'active' : 'inactive'}`}>{c.isActive ? 'Activo' : 'Inactivo'}</span><div className="admin-product-actions"><button disabled={busy} onClick={() => { setEditing(c); setForm(input(c)) }}>Editar</button><button disabled={busy} onClick={() => void toggle(c)}>{c.isActive ? 'Desactivar' : 'Activar'}</button></div></article>)}</div>}
    {result.totalPages > 1 && <div className="admin-pagination"><button disabled={loading || page === 1} onClick={() => setPage(x => x - 1)}>Anterior</button><span>Página {page} de {result.totalPages}</span><button disabled={loading || page === result.totalPages} onClick={() => setPage(x => x + 1)}>Siguiente</button></div>}
    {editing !== undefined && <form className="admin-form" onSubmit={save}><h2>{editing ? `Editar ${editing.code}` : 'Nuevo cupón'}</h2><label>Código<input required maxLength={50} value={form.code} onChange={e => change('code', e.target.value.toUpperCase())} /></label><label>Nombre<input required maxLength={200} value={form.name} onChange={e => change('name', e.target.value)} /></label><label>Tipo<select value={form.type} onChange={e => change('type', e.target.value as CouponType)}><option value="Percentage">Porcentaje</option><option value="FixedAmount">Monto fijo</option></select></label><label>Valor<input required type="number" min="0.01" step="0.01" value={form.value || ''} onChange={e => change('value', Number(e.target.value))} /></label><label>Compra mínima<input type="number" min="0" step="0.01" value={form.minimumPurchase ?? ''} onChange={e => change('minimumPurchase', numeric(e.target.value))} /></label>{form.type === 'Percentage' && <label>Descuento máximo<input type="number" min="0.01" step="0.01" value={form.maximumDiscount ?? ''} onChange={e => change('maximumDiscount', numeric(e.target.value))} /></label>}<label>Inicio (hora local)<input type="datetime-local" value={utcIsoToLocalDateTimeInput(form.startsAtUtc)} onChange={e => change('startsAtUtc', localDateTimeInputToUtcIso(e.target.value))} /></label><label>Fin (hora local)<input type="datetime-local" value={utcIsoToLocalDateTimeInput(form.endsAtUtc)} onChange={e => change('endsAtUtc', localDateTimeInputToUtcIso(e.target.value))} /></label><label>Límite total<input type="number" min="1" value={form.totalUsageLimit ?? ''} onChange={e => change('totalUsageLimit', numeric(e.target.value))} /></label><label>Límite por usuario<input type="number" min="1" value={form.usageLimitPerUser ?? ''} onChange={e => change('usageLimitPerUser', numeric(e.target.value))} /></label><div><button type="button" disabled={busy} onClick={() => setEditing(undefined)}>Cancelar</button><button className="primary" disabled={busy}>{busy ? 'Guardando…' : 'Guardar'}</button></div></form>}
  </section>
}
