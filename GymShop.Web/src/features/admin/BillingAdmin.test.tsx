import { render, screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { afterEach, describe, expect, it, vi } from 'vitest'
import { BillingAdmin } from './BillingAdmin'

const profile = {
  mode: 'ElectronicInvoice', taxCondition: 'Monotributo', businessName: 'GymShop', cuit: '20-12345678-9',
  fiscalAddress: 'Rosario', grossIncomeNumber: 'Exento', activityStartDate: '2026-01-01', pointOfSale: 4,
  arcaEnabled: true, electronicInvoicingReady: true,
}
const status = {
  environment: 'Homologation', configurationReady: true, wsaaAuthenticated: true, wsfeReachable: true,
  pointsOfSale: [4, 12], errorCode: null, message: 'Conexion de homologacion validada correctamente.', checkedAtUtc: '2026-10-03T15:00:00Z',
}
const response = (body: unknown) => Promise.resolve(new Response(JSON.stringify(body), { status: 200, headers: { 'Content-Type': 'application/json' } }))

describe('BillingAdmin', () => {
  afterEach(() => vi.restoreAllMocks())

  it('muestra el perfil y ejecuta el diagnostico solo cuando se solicita', async () => {
    const fetchMock = vi.spyOn(globalThis, 'fetch').mockImplementation(input =>
      response(String(input).endsWith('/arca/status') ? status : profile))

    render(<BillingAdmin />)

    expect(await screen.findByText('GymShop')).toBeInTheDocument()
    expect(fetchMock).toHaveBeenCalledTimes(1)
    await userEvent.click(screen.getByRole('button', { name: 'Probar conexión de homologación' }))
    expect(await screen.findByText('Autenticado')).toBeInTheDocument()
    expect(screen.getByText('4, 12')).toBeInTheDocument()
    await waitFor(() => expect(fetchMock).toHaveBeenCalledTimes(2))
  })
})
