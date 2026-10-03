import { useEffect, useState } from 'react'
import { api } from '../../api/gymshop'
import type { ArcaConnectionStatus, BillingProfile } from '../../api/types'
import { describeAdminError } from './adminErrors'
import { AdminFeedback, AdminLoading } from './adminUi'

export function BillingAdmin() {
  const [profile, setProfile] = useState<BillingProfile | null>(null)
  const [status, setStatus] = useState<ArcaConnectionStatus | null>(null)
  const [loading, setLoading] = useState(true)
  const [checking, setChecking] = useState(false)
  const [error, setError] = useState('')

  useEffect(() => {
    api.billingProfile().then(setProfile).catch(value => setError(describeAdminError(value))).finally(() => setLoading(false))
  }, [])

  const checkConnection = async () => {
    if (checking) return
    setChecking(true); setError('')
    try { setStatus(await api.arcaStatus()) }
    catch (value) { setError(describeAdminError(value)) }
    finally { setChecking(false) }
  }

  return <section className="admin-page billing-admin">
    <div className="admin-page-heading"><div><p className="eyebrow">CONFIGURACIÓN PROTEGIDA</p><h1>Facturación</h1><p>Estado del perfil fiscal y de la conexión de homologación con ARCA.</p></div></div>
    <AdminFeedback error={error} />
    {loading && <AdminLoading label="Cargando configuración fiscal…" />}
    {profile && <div className="dashboard-grid">
      <section className="dashboard-panel"><h2>Perfil fiscal</h2><dl className="billing-status-list">
        <div><dt>Modo</dt><dd>{profile.mode}</dd></div><div><dt>Condición</dt><dd>{profile.taxCondition}</dd></div><div><dt>Razón social</dt><dd>{profile.businessName || 'Sin configurar'}</dd></div><div><dt>CUIT</dt><dd>{profile.cuit || 'Sin configurar'}</dd></div><div><dt>Punto de venta</dt><dd>{profile.pointOfSale ?? 'Sin configurar'}</dd></div>
      </dl></section>
      <section className="dashboard-panel"><h2>ARCA</h2><p><strong>{profile.arcaEnabled ? 'Homologación habilitada' : 'Conector deshabilitado'}</strong></p><p>{profile.electronicInvoicingReady ? 'El perfil fiscal está completo.' : 'El perfil fiscal todavía no está listo para facturación electrónica.'}</p><button className="primary" type="button" disabled={checking} onClick={() => void checkConnection()}>{checking ? 'Verificando…' : 'Probar conexión de homologación'}</button></section>
    </div>}
    {status && <section className="dashboard-panel arca-diagnostic" aria-live="polite"><h2>Último diagnóstico</h2><dl className="billing-status-list">
      <div><dt>Ambiente</dt><dd>{status.environment}</dd></div><div><dt>Configuración</dt><dd>{status.configurationReady ? 'Completa' : 'Incompleta'}</dd></div><div><dt>WSFE</dt><dd>{status.wsfeReachable ? 'Disponible' : 'No disponible'}</dd></div><div><dt>WSAA</dt><dd>{status.wsaaAuthenticated ? 'Autenticado' : 'Sin autenticar'}</dd></div><div><dt>Puntos de venta</dt><dd>{status.pointsOfSale.length ? status.pointsOfSale.join(', ') : 'Ninguno informado'}</dd></div>
    </dl><p className={status.errorCode ? 'error' : 'notice'}>{status.message}</p>{status.errorCode && <small>Código técnico: {status.errorCode}</small>}</section>}
    <p className="admin-footnote">Esta pantalla no permite cargar secretos ni cambiar proveedores. La configuración se administra únicamente mediante Railway.</p>
  </section>
}
