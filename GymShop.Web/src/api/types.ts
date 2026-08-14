export type Role = 'User' | 'Admin' | 'SuperAdmin'
export type OrderStatus = 'Pending' | 'Paid' | 'Shipped' | 'Canceled' | 'Refunded'
export type PaymentStatus = 'Creating' | 'Pending' | 'CreationFailed' | 'Approved' | 'Rejected' | 'Canceled' | 'Expired' | 'Refunded'

export interface User { id: number; email: string; name: string; lastName?: string | null; role: Role }
export interface RegistrationPending { email: string; expiresInSeconds: number; developmentCode: string | null }
export interface PasswordResetPending { message: string; expiresInSeconds: number; developmentCode: string | null }
export interface PasswordResetCompleted { message: string }
export interface AdminUser extends User { isActive: boolean; createdAt: string }
export interface AuthResponse { token: string; user: User }
export interface Product { id: number; name: string; description: string | null; price: number; stock: number; imageUrl: string | null; isActive: boolean }
export interface CartItem { productId: number; productName: string; unitPrice: number; quantity: number; subtotal: number; stock: number; imageUrl: string | null }
export interface Cart { id: number; userId: number; total: number; items: CartItem[] }
export interface OrderItem { productId: number; productName: string; unitPrice: number; quantity: number; subtotal: number }
export interface OrderPayment { id: number; provider: string; amount: number; currency: string; status: PaymentStatus; createdAt: string; paidAt: string | null }
export interface Order { id: number; userId: number; userEmail: string | null; createdAt: string; total: number; status: OrderStatus; shippingAddress: string; cancellationReason: string | null; items: OrderItem[]; payments: OrderPayment[] }
export interface OrderSummary { id: number; userId: number; userEmail: string | null; createdAt: string; total: number; status: OrderStatus; lastPaymentStatus: PaymentStatus | null; lastPaymentId: number | null }
export interface Payment { id: number; orderId: number; provider: string; externalReference: string; providerPreferenceId: string | null; providerPaymentId: string | null; idempotencyKey: string | null; amount: number; currency: string; status: PaymentStatus; checkoutUrl: string | null; failureReason: string | null; createdAt: string; updatedAt: string | null; paidAt: string | null }
export interface AuditPage { items: AuditEntry[]; page: number; pageSize: number; totalItems: number; totalPages: number }
export interface AuditEntry { id: number; actorUserId: number | null; action: string; entityType: string; entityId: string; reason: string | null; createdAtUtc: string; correlationId: string }

export interface ApiErrorShape { status: number; message: string; traceId?: string; retryAfter?: number; validationErrors?: Record<string, string[]> }
