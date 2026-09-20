import { useEffect, useState } from 'react'
import { api } from '../../api/gymshop'
import type { OrderSummary, Product } from '../../api/types'
import { LOW_STOCK_THRESHOLD } from './adminConfig'
import { describeAdminError } from './adminErrors'
import { AdminFeedback, AdminLoading } from './adminUi'

export function AdminDashboard() {
  const [data, setData] = useState<{ products: Product[]; orders: OrderSummary[] } | null>(null)
  const [error, setError] = useState('')
  useEffect(() => {
    let active = true
    Promise.all([api.products(true), api.orders()])
      .then(([products, orders]) => { if (active) setData({ products, orders }) })
      .catch(value => { if (active) setError(describeAdminError(value)) })
    return () => { active = false }
  }, [])
  const stats = data ? [
    ['Total de productos', data.products.length],
    ['Productos activos', data.products.filter(product => product.isActive).length],
    ['Productos inactivos', data.products.filter(product => !product.isActive).length],
    ['Sin stock', data.products.filter(product => product.stock === 0).length],
    [`Stock bajo (1–${LOW_STOCK_THRESHOLD})`, data.products.filter(product => product.stock > 0 && product.stock <= LOW_STOCK_THRESHOLD).length],
    ['Total de pedidos', data.orders.length],
  ] : []
  return <section className="admin-page">
    <div className="admin-page-heading"><div><p className="eyebrow">PANEL</p><h1>Resumen</h1><p>Estado actual de la operación con datos disponibles en el sistema.</p></div></div>
    <AdminFeedback error={error} />
    {!data && !error && <AdminLoading label="Cargando resumen…" />}
    {data && <div className="admin-stats">{stats.map(([label, value]) => <article key={label}><span>{label}</span><strong>{value}</strong></article>)}</div>}
    <p className="admin-footnote">Stock bajo se considera entre 1 y {LOW_STOCK_THRESHOLD} unidades. Los totales reflejan la lista completa que entrega actualmente la API.</p>
  </section>
}
