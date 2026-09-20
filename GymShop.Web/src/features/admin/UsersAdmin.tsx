import { useCallback, useEffect, useState } from 'react'
import { api } from '../../api/gymshop'
import type { AdminUser, Role } from '../../api/types'
import { describeAdminError } from './adminErrors'
import { AdminEmpty, AdminFeedback, AdminLoading } from './adminUi'

export function UsersAdmin() {
  const [users, setUsers] = useState<AdminUser[]>([]); const [loading, setLoading] = useState(true); const [pending, setPending] = useState<number | null>(null); const [error, setError] = useState(''); const [success, setSuccess] = useState('')
  const load = useCallback(async () => { setLoading(true); setError(''); try { setUsers(await api.users()) } catch (value) { setError(describeAdminError(value)) } finally { setLoading(false) } }, [])
  useEffect(() => { void load() }, [load])
  const mutate = async (user: AdminUser, action: () => Promise<void>, message: string) => { if (pending !== null) return; setPending(user.id); setError(''); setSuccess(''); try { await action(); await load(); setSuccess(message) } catch (value) { setError(describeAdminError(value)) } finally { setPending(null) } }
  return <section className="admin-page"><div className="admin-page-heading"><div><p className="eyebrow">SUPERADMIN</p><h1>Usuarios y roles</h1><p>Gestión existente de permisos y estado de cuentas.</p></div></div><AdminFeedback error={error} success={success} />
    {loading && users.length === 0 ? <AdminLoading label="Cargando usuarios…" /> : users.length === 0 ? <AdminEmpty>No hay usuarios para mostrar.</AdminEmpty> : <div className="list">{users.map(user => <div className="list-row" key={user.id} aria-busy={pending === user.id}><div><h3>{user.name}</h3><p>{user.email}</p></div><select disabled={pending !== null} aria-label={`Rol de ${user.name}`} value={user.role} onChange={event => void mutate(user, () => api.setUserRole(user.id, event.target.value as Role), `Rol de ${user.name} actualizado.`)}><option>User</option><option>Admin</option><option>SuperAdmin</option></select><button disabled={pending !== null} onClick={() => void mutate(user, () => api.setUserStatus(user.id, !user.isActive), `${user.name} fue ${user.isActive ? 'desactivado' : 'activado'}.`)}>{pending === user.id ? 'Guardando…' : user.isActive ? 'Desactivar' : 'Activar'}</button></div>)}</div>}
  </section>
}
