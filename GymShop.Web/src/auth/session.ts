import type { User } from '../api/types'

const TOKEN_KEY = 'gymshop.token'
const USER_KEY = 'gymshop.user'

export const session = {
  token: () => localStorage.getItem(TOKEN_KEY),
  user: (): User | null => {
    const value = localStorage.getItem(USER_KEY)
    if (!value) return null
    try { return JSON.parse(value) as User } catch { return null }
  },
  save: (token: string, user: User) => {
    localStorage.setItem(TOKEN_KEY, token)
    localStorage.setItem(USER_KEY, JSON.stringify(user))
    window.dispatchEvent(new Event('gymshop:session'))
  },
  clear: () => {
    localStorage.removeItem(TOKEN_KEY)
    localStorage.removeItem(USER_KEY)
    window.dispatchEvent(new Event('gymshop:session'))
  },
}
