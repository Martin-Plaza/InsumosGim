import { render, screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import App from '../../App'

const json = (body: unknown, status = 200) => Promise.resolve(new Response(JSON.stringify(body), {
  status,
  headers: { 'Content-Type': 'application/json' },
}))

function installGoogle(clientId: string) {
  vi.stubEnv('VITE_GOOGLE_CLIENT_ID', clientId)
  let callback: ((response: { credential?: string }) => void) | undefined
  window.google = { accounts: { id: {
    initialize: vi.fn(options => { callback = options.callback }),
    renderButton: vi.fn(element => {
      const button = document.createElement('button')
      button.textContent = 'Continuar con Google'
      element.append(button)
    }),
  } } }
  return (response: { credential?: string }) => callback?.(response)
}

async function openLogin() {
  render(<App />)
  await userEvent.click(screen.getByRole('link', { name: 'Ingresar' }))
  await screen.findByLabelText('Acceso con Google')
}

describe('inicio de sesión con Google', () => {
  beforeEach(() => {
    localStorage.clear()
    window.history.replaceState(null, '', '/')
    vi.restoreAllMocks()
  })

  afterEach(() => {
    vi.unstubAllEnvs()
    delete window.google
  })

  it('completa el acceso y conserva el rol devuelto por la API', async () => {
    const respond = installGoogle('success-client.apps.googleusercontent.com')
    vi.spyOn(globalThis, 'fetch').mockImplementation(input => String(input).includes('/auth/google')
      ? json({ token: 'google-jwt', user: { id: 10, email: 'new@test.com', name: 'Nueva', role: 'User' } })
      : json([]))
    await openLogin()

    respond({ credential: 'valid-google-id-token' })

    await waitFor(() => expect(localStorage.getItem('gymshop.token')).toBe('google-jwt'))
    expect(JSON.parse(localStorage.getItem('gymshop.user')!).role).toBe('User')
  })

  it('informa una cancelación sin crear sesión', async () => {
    const respond = installGoogle('cancel-client.apps.googleusercontent.com')
    vi.spyOn(globalThis, 'fetch').mockImplementation(() => json([]))
    await openLogin()

    respond({})

    expect(await screen.findByRole('status')).toHaveTextContent('Cancelaste el acceso con Google')
    expect(localStorage.getItem('gymshop.token')).toBeNull()
  })

  it('muestra el error seguro entregado por la API', async () => {
    const respond = installGoogle('error-client.apps.googleusercontent.com')
    vi.spyOn(globalThis, 'fetch').mockImplementation(input => String(input).includes('/auth/google')
      ? json({ message: 'La credencial de Google no es valida.' }, 401)
      : json([]))
    await openLogin()

    respond({ credential: 'invalid-google-id-token' })

    expect(await screen.findByRole('alert')).toHaveTextContent('La credencial de Google no es valida.')
    expect(localStorage.getItem('gymshop.token')).toBeNull()
  })

  it('explica cómo habilitar Google cuando falta la configuración', async () => {
    vi.stubEnv('VITE_GOOGLE_CLIENT_ID', '')
    vi.spyOn(globalThis, 'fetch').mockImplementation(() => json([]))
    await openLogin()

    expect(screen.getByText(/Definí VITE_GOOGLE_CLIENT_ID/)).toBeInTheDocument()
  })
})
