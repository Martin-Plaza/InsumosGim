import { useCallback, useEffect, useRef, useState } from 'react'
import { api } from '../../api/gymshop'
import type { AuthResponse } from '../../api/types'
import { authErrorMessage } from './authMessages'
import {
  clearPendingRegistration,
  loadPendingRegistration,
  remainingSeconds,
  savePendingRegistration,
  type PendingRegistration,
} from './pendingRegistration'

interface AuthPanelProps {
  onDone(auth: AuthResponse): void
}

let initializedGoogleClientId: string | null = null
let googleCredentialHandler: ((credential: string) => void) | null = null

export function AuthPanel({ onDone }: AuthPanelProps) {
  const [register, setRegister] = useState(false)
  const [showPassword, setShowPassword] = useState(false)
  const [pending, setPending] = useState<PendingRegistration | null>(() => loadPendingRegistration())
  const [developmentCode, setDevelopmentCode] = useState<string | null>(null)
  const [seconds, setSeconds] = useState(() => pending ? remainingSeconds(pending) : 0)
  const [busy, setBusy] = useState(false)
  const busyRef = useRef(false)
  const [error, setError] = useState('')
  const [notice, setNotice] = useState(() => pending ? 'Retomamos tu verificación pendiente.' : '')
  const googleButton = useRef<HTMLDivElement>(null)
  const googleConfigured = Boolean(import.meta.env.VITE_GOOGLE_CLIENT_ID && !import.meta.env.VITE_GOOGLE_CLIENT_ID.startsWith('<'))

  const complete = useCallback((auth: AuthResponse) => {
    clearPendingRegistration()
    onDone(auth)
  }, [onDone])

  const execute = useCallback(async (action: () => Promise<void>) => {
    if (busyRef.current) return
    busyRef.current = true
    setBusy(true)
    setError('')
    setNotice('')
    try {
      await action()
    } catch (value) {
      setError(authErrorMessage(value))
    } finally {
      busyRef.current = false
      setBusy(false)
    }
  }, [])

  useEffect(() => {
    if (!pending) return
    setSeconds(remainingSeconds(pending))
    const timer = window.setInterval(() => setSeconds(remainingSeconds(pending)), 1000)
    return () => window.clearInterval(timer)
  }, [pending])

  useEffect(() => {
    const clientId = import.meta.env.VITE_GOOGLE_CLIENT_ID
    if (!googleConfigured || !clientId) return
    let attempts = 0
    let cancelled = false
    let retryTimer: number | undefined
    const render = () => {
      if (cancelled) return
      if (window.google && googleButton.current) {
        googleButton.current.replaceChildren()
        googleCredentialHandler = credential => void execute(async () => complete(await api.googleLogin(credential)))
        if (initializedGoogleClientId !== clientId) {
          window.google.accounts.id.initialize({
            client_id: clientId,
            callback: ({ credential }) => googleCredentialHandler?.(credential),
          })
          initializedGoogleClientId = clientId
        }
        window.google.accounts.id.renderButton(googleButton.current, { theme: 'outline', size: 'large', width: 320 })
      } else if (attempts++ < 20) {
        retryTimer = window.setTimeout(render, 250)
      } else {
        setError('No se pudo cargar el acceso con Google. Revisá la conexión e intentá nuevamente.')
      }
    }
    render()
    return () => {
      cancelled = true
      if (retryTimer !== undefined) window.clearTimeout(retryTimer)
    }
  }, [complete, execute, googleConfigured])

  if (pending) {
    return <section className="auth-card">
      <div>
        <p className="eyebrow">VERIFICÁ TU EMAIL</p>
        <h1>Ingresá los 6 números</h1>
        <p>Generamos un código de verificación para <strong>{pending.email}</strong>.</p>
        <p>{seconds > 0 ? `Vence en ${seconds} segundos.` : 'El código venció. Ya podés solicitar uno nuevo.'}</p>
        {developmentCode && <p className="mock-code">Código Mock local: <strong>{developmentCode}</strong></p>}
      </div>
      <form onSubmit={event => {
        event.preventDefault()
        const code = String(new FormData(event.currentTarget).get('code'))
        void execute(async () => complete(await api.verifyEmail({ email: pending.email, code })))
      }}>
        {notice && <div className="notice" role="status">{notice}</div>}
        {error && <div className="error" role="alert">{error}</div>}
        <label>Código de verificación
          <input name="code" inputMode="numeric" autoComplete="one-time-code" pattern="[0-9]{6}" minLength={6} maxLength={6} required autoFocus disabled={busy || seconds === 0} />
        </label>
        <button className="primary" type="submit" disabled={busy || seconds === 0}>{busy ? 'Verificando…' : 'Verificar e ingresar'}</button>
        <button type="button" disabled={busy || seconds > 0} onClick={() => void execute(async () => {
          const result = await api.resendVerification(pending.email)
          setPending(savePendingRegistration(result.email, result.expiresInSeconds))
          setDevelopmentCode(result.developmentCode)
          setNotice('Te enviamos un código nuevo.')
        })}>{busy ? 'Enviando…' : 'Reenviar código'}</button>
        <button type="button" className="link" disabled={busy} onClick={() => {
          clearPendingRegistration()
          setPending(null)
          setDevelopmentCode(null)
          setError('')
          setNotice('')
        }}>Cambiar email</button>
      </form>
    </section>
  }

  return <section className="auth-card">
    <div>
      <p className="eyebrow">TU CUENTA</p>
      <h1>{register ? 'Empezá a entrenar' : 'Bienvenido de nuevo'}</h1>
      <p>Accedé a tu carrito, pagos y seguimiento de órdenes.</p>
    </div>
    <form onSubmit={event => {
      event.preventDefault()
      const data = new FormData(event.currentTarget)
      void execute(async () => {
        const email = String(data.get('email')).trim().toLowerCase()
        if (register) {
          const result = await api.register({
            name: String(data.get('name')),
            lastName: String(data.get('lastName')),
            email,
            password: String(data.get('password')),
          })
          setPending(savePendingRegistration(result.email, result.expiresInSeconds))
          setDevelopmentCode(result.developmentCode)
          setNotice('Cuenta creada. Ingresá el código para activar tu cuenta.')
        } else {
          complete(await api.login({ email, password: String(data.get('password')) }))
        }
      })
    }}>
      {notice && <div className="notice" role="status">{notice}</div>}
      {error && <div className="error" role="alert">{error}</div>}
      <fieldset disabled={busy}>
        {register && <div className="name-fields">
          <label>Nombre<input name="name" required maxLength={100} autoComplete="given-name" /></label>
          <label>Apellido<input name="lastName" required maxLength={100} autoComplete="family-name" /></label>
        </div>}
        <label>Email<input name="email" type="email" required maxLength={256} autoComplete="email" /></label>
        <label>Contraseña
          <div className="password-field">
            <input name="password" type={showPassword ? 'text' : 'password'} required minLength={register ? 8 : undefined} maxLength={128} autoComplete={register ? 'new-password' : 'current-password'} />
            <button type="button" aria-label={showPassword ? 'Ocultar contraseña' : 'Mostrar contraseña'} onClick={() => setShowPassword(value => !value)}>{showPassword ? 'Ocultar' : 'Ver'}</button>
          </div>
        </label>
      </fieldset>
      <button className="primary" type="submit" disabled={busy}>{busy ? (register ? 'Creando cuenta…' : 'Ingresando…') : (register ? 'Crear cuenta' : 'Ingresar')}</button>
      <button type="button" className="link" disabled={busy} onClick={() => { setRegister(value => !value); setError(''); setNotice('') }}>{register ? 'Ya tengo cuenta' : 'Quiero registrarme'}</button>
      <div className="auth-divider"><span>o</span></div>
      <div ref={googleButton} className="google-button">{!googleConfigured && <small>Google no está configurado en este ambiente.</small>}</div>
    </form>
  </section>
}
