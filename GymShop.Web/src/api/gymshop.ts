import { json, request } from './client'
import type { AdminUser, AuditPage, AuthResponse, Cart, Order, OrderSummary, Payment, Product, RegistrationPending, Role, User } from './types'

export const api = {
  register: (data: { name: string; lastName: string; email: string; password: string }) => request<RegistrationPending>('/api/auth/register', json('POST', data)),
  verifyEmail: (data: { email: string; code: string }) => request<AuthResponse>('/api/auth/verify-email', json('POST', data)),
  resendVerification: (email: string) => request<RegistrationPending>('/api/auth/resend-verification', json('POST', { email })),
  googleLogin: (credential: string) => request<AuthResponse>('/api/auth/google', json('POST', { credential })),
  login: (data: { email: string; password: string }) => request<AuthResponse>('/api/auth/login', json('POST', data)),
  me: () => request<User>('/api/auth/me'),
  products: (includeInactive = false) => request<Product[]>(`/api/products${includeInactive ? '?includeInactive=true' : ''}`),
  product: (id: number) => request<Product>(`/api/products/${id}`),
  createProduct: (data: Omit<Product, 'id'>) => request<Product>('/api/products', json('POST', data)),
  updateProduct: (product: Product) => request<Product>(`/api/products/${product.id}`, json('PUT', product)),
  setProductStock: (id: number, stock: number) => request<void>(`/api/products/${id}/stock`, json('PATCH', { stock })),
  setProductStatus: (id: number, isActive: boolean) => request<void>(`/api/products/${id}/status`, json('PATCH', { isActive })),
  cart: () => request<Cart>('/api/cart'),
  addCartItem: (productId: number, quantity: number) => request<Cart>('/api/cart/items', json('POST', { productId, quantity })),
  updateCartItem: (productId: number, quantity: number) => request<Cart>(`/api/cart/items/${productId}`, json('PUT', { quantity })),
  removeCartItem: (productId: number) => request<Cart>(`/api/cart/items/${productId}`, json('DELETE')),
  clearCart: () => request<void>('/api/cart', json('DELETE')),
  checkout: (shippingAddress: string) => request<Order>('/api/cart/checkout', json('POST', { shippingAddress })),
  myOrders: () => request<OrderSummary[]>('/api/orders/my'),
  orders: (email = '') => request<OrderSummary[]>(`/api/orders${email ? `?userEmail=${encodeURIComponent(email)}` : ''}`),
  order: (id: number) => request<Order>(`/api/orders/${id}`),
  cancelOrder: (id: number, reason?: string) => request<Order>(`/api/orders/${id}/cancel`, json('POST', { reason: reason || null })),
  setOrderStatus: (id: number, status: string) => request<void>(`/api/orders/${id}/status`, json('PATCH', { status })),
  createPayment: (orderId: number, idempotencyKey: string) => request<Payment>(`/api/orders/${orderId}/payments`, json('POST', { provider: 'Mock', idempotencyKey })),
  payment: (id: number) => request<Payment>(`/api/payments/${id}`),
  orderPayments: (orderId: number) => request<Payment[]>(`/api/payments/orders/${orderId}`),
  setPaymentStatus: (id: number, status: string, failureReason?: string) => request<Payment>(`/api/payments/${id}/status`, json('POST', { status, failureReason: failureReason || null, providerPaymentId: null })),
  users: () => request<AdminUser[]>('/api/users'),
  createUser: (data: { name: string; email: string; password: string; role: Role }) => request<AdminUser>('/api/users', json('POST', data)),
  setUserRole: (id: number, role: Role) => request<void>(`/api/users/${id}/role`, json('PATCH', { role })),
  setUserStatus: (id: number, isActive: boolean) => request<void>(`/api/users/${id}/status`, json('PATCH', { isActive })),
  audit: () => request<AuditPage>('/api/audit?page=1&pageSize=50'),
}

export function paymentKey(orderId: number) {
  const key = `gymshop.payment-key.${orderId}`
  let value = localStorage.getItem(key)
  if (!value) {
    value = crypto.randomUUID()
    localStorage.setItem(key, value)
  }
  return value
}
