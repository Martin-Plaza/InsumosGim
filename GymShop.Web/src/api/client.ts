import { session } from '../auth/session'
import type { ApiErrorShape } from './types'

export const API_URL = (import.meta.env.VITE_API_URL || 'http://localhost:5093').replace(/\/$/, '')

export class ApiError extends Error implements ApiErrorShape {
  status: number
  traceId?: string
  retryAfter?: number
  validationErrors?: Record<string, string[]>
  constructor(data: ApiErrorShape) {
    super(data.message)
    this.name = 'ApiError'
    this.status = data.status
    this.traceId = data.traceId
    this.retryAfter = data.retryAfter
    this.validationErrors = data.validationErrors
  }
}

function errorMessage(status: number) {
  if (status === 401) return 'Tu sesión no es válida. Iniciá sesión nuevamente.'
  if (status === 403) return 'No tenés permisos para realizar esta acción.'
  if (status === 409) return 'La operación entra en conflicto con el estado actual.'
  if (status === 429) return 'Demasiadas solicitudes. Intentá nuevamente más tarde.'
  if (status >= 500) return 'Ocurrió un error inesperado.'
  return 'No se pudo completar la solicitud.'
}

async function normalizeError(response: Response): Promise<ApiError> {
  let body: Record<string, unknown> = {}
  try { body = await response.json() as Record<string, unknown> } catch { /* empty response */ }
  const errors = body.errors && typeof body.errors === 'object' ? body.errors as Record<string, string[]> : undefined
  const validationMessage = errors ? Object.values(errors).flat().join(' ') : undefined
  const message = validationMessage || (typeof body.message === 'string' && body.message) || (typeof body.detail === 'string' && body.detail) || (typeof body.title === 'string' && body.title) || errorMessage(response.status)
  const retry = Number(response.headers.get('Retry-After'))
  return new ApiError({
    status: response.status,
    message,
    traceId: typeof body.traceId === 'string' ? body.traceId : undefined,
    retryAfter: Number.isFinite(retry) && retry > 0 ? retry : undefined,
    validationErrors: errors,
  })
}

export async function request<T>(path: string, init: RequestInit = {}): Promise<T> {
  const headers = new Headers(init.headers)
  headers.set('Accept', 'application/json')
  if (init.body && !headers.has('Content-Type')) headers.set('Content-Type', 'application/json')
  const token = session.token()
  if (token) headers.set('Authorization', `Bearer ${token}`)
  const response = await fetch(`${API_URL}${path}`, { ...init, headers })
  if (!response.ok) {
    const error = await normalizeError(response)
    if (response.status === 401) session.clear()
    throw error
  }
  if (response.status === 204) return undefined as T
  return response.json() as Promise<T>
}

export const json = (method: string, body?: unknown): RequestInit => ({ method, body: body === undefined ? undefined : JSON.stringify(body) })
