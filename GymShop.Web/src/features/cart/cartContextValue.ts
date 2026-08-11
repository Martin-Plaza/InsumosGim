import { createContext } from 'react'
import type { CartItem, Product } from '../../api/types'

export interface CartContextValue {
  items: CartItem[]
  total: number
  count: number
  loading: boolean
  error: string
  notice: string
  drawerOpen: boolean
  add(product: Product, quantity: number): Promise<void>
  update(productId: number, quantity: number): Promise<void>
  remove(productId: number): Promise<void>
  clear(): Promise<void>
  refresh(): Promise<void>
  openDrawer(): void
  closeDrawer(): void
  dismissMessages(): void
}

export const CartContext = createContext<CartContextValue | null>(null)
