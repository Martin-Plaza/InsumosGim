import { useEffect, useState } from 'react'
import { api } from '../../api/gymshop'
import type { AuditEntry } from '../../api/types'
import { storefront } from '../../config/storefront'
import { describeAdminError } from './adminErrors'
import { AdminEmpty, AdminFeedback, AdminLoading } from './adminUi'

const date = (value: string) => new Intl.DateTimeFormat(storefront.market.locale, { dateStyle: 'medium', timeStyle: 'short' }).format(new Date(value))

export function AuditAdmin() {
  const [entries, setEntries] = useState<AuditEntry[]>([]); const [loading, setLoading] = useState(true); const [error, setError] = useState('')
  useEffect(() => { let active = true; api.audit().then(page => { if (active) setEntries(page.items) }).catch(value => { if (active) setError(describeAdminError(value)) }).finally(() => { if (active) setLoading(false) }); return () => { active = false } }, [])
  return <section className="admin-page"><div className="admin-page-heading"><div><p className="eyebrow">SUPERADMIN</p><h1>Auditoría</h1><p>Registro de operaciones sensibles del sistema.</p></div></div><AdminFeedback error={error} />
    {loading ? <AdminLoading label="Cargando auditoría…" /> : entries.length === 0 ? <AdminEmpty>No hay eventos de auditoría para mostrar.</AdminEmpty> : <div className="list">{entries.map(entry => <div className="list-row audit-row" key={entry.id}><div><h3>{entry.action}</h3><p>{entry.entityType} #{entry.entityId} · {date(entry.createdAtUtc)}</p></div><small>{entry.correlationId}</small></div>)}</div>}
  </section>
}
