/// <reference types="vite/client" />

interface Window {
  google?: {
    accounts: { id: {
      initialize(options: { client_id: string; callback(response: { credential?: string; select_by?: string }): void }): void
      renderButton(element: HTMLElement, options: Record<string, unknown>): void
    } }
  }
}
