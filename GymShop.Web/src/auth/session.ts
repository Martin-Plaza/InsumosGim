import type { User } from '../api/types'

const LEGACY_TOKEN_KEY = 'gymshop.token'
const USER_KEY = 'gymshop.user'
const ACCESS_TOKEN_KEY = 'gymshop.access-token'

export const session = {
  user: (): User | null => {
    const value = localStorage.getItem(USER_KEY)
    if (!value) return null
    try { return JSON.parse(value) as User } catch { return null }
  },
  accessToken: () => sessionStorage.getItem(ACCESS_TOKEN_KEY),
  save: (user: User, accessToken?: string) => {
    localStorage.removeItem(LEGACY_TOKEN_KEY)
    localStorage.setItem(USER_KEY, JSON.stringify(user))
    if (accessToken) sessionStorage.setItem(ACCESS_TOKEN_KEY, accessToken)
    else sessionStorage.removeItem(ACCESS_TOKEN_KEY)
    window.dispatchEvent(new Event('gymshop:session'))
  },
  updateAccessToken: (accessToken: string) => sessionStorage.setItem(ACCESS_TOKEN_KEY, accessToken),
  clear: () => {
    localStorage.removeItem(LEGACY_TOKEN_KEY)
    localStorage.removeItem(USER_KEY)
    sessionStorage.removeItem(ACCESS_TOKEN_KEY)
    window.dispatchEvent(new Event('gymshop:session'))
  },
}
