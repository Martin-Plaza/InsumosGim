export type Role = 'User' | 'Admin' | 'SuperAdmin'
export type OrderStatus = 'Pending' | 'Paid' | 'Preparing' | 'Shipped' | 'Delivered' | 'Canceled' | 'Refunded'
export type PaymentStatus = 'Creating' | 'Pending' | 'CreationFailed' | 'Approved' | 'Rejected' | 'Canceled' | 'Expired' | 'Refunded'

export interface User { id: number; email: string; name: string; lastName?: string | null; role: Role }
export interface RegistrationPending { email: string; expiresInSeconds: number; developmentCode: string | null }
export interface PasswordResetPending { message: string; expiresInSeconds: number; developmentCode: string | null }
export interface PasswordResetCompleted { message: string }
export interface AdminUser extends User { isActive: boolean; createdAt: string }
export interface AuthResponse { token: string; user: User }
export interface CategorySummary { id: number; name: string; slug: string }
export interface Category extends CategorySummary { description: string | null; displayOrder: number }
export interface AdminCategory extends Category { isActive: boolean; productCount: number }
export interface CategoryInput { name: string; slug: string; description: string | null; displayOrder: number }
export interface Product { id: number; name: string; description: string | null; price: number; stock: number; imageUrl: string | null; isActive: boolean; category: CategorySummary | null }
export interface ProductWrite { name: string; description: string | null; price: number; stock: number; imageUrl: string | null; categoryId: number | null }
export type CreateProductInput = ProductWrite
export interface UpdateProductInput extends ProductWrite { isActive: boolean }
export interface ProductImageUpload { url: string; key: string }
export interface CartItem { productId: number; productName: string; unitPrice: number; quantity: number; subtotal: number; stock: number; imageUrl: string | null }
export interface Cart { id: number; userId: number; total: number; items: CartItem[] }
export interface OrderItem { productId: number; productName: string; unitPrice: number; quantity: number; subtotal: number }
export interface OrderPayment { id: number; provider: string; amount: number; currency: string; status: PaymentStatus; createdAt: string; paidAt: string | null }
export interface Order { id: number; userId: number; userEmail: string | null; userName: string; userPhone: string | null; createdAt: string; total: number; status: OrderStatus; shippingAddress: string; cancellationReason: string | null; updatedAt: string | null; items: OrderItem[]; payments: OrderPayment[] }
export interface OrderSummary { id: number; userId: number; userEmail: string | null; userName: string; createdAt: string; total: number; status: OrderStatus; updatedAt: string | null; lastPaymentStatus: PaymentStatus | null; lastPaymentId: number | null }
export interface OrderPage { items: OrderSummary[]; page: number; pageSize: number; totalItems: number; totalPages: number }
export interface OrderFilters { page?: number; pageSize?: number; search?: string; status?: string; fromUtc?: string; toUtc?: string }
export type OrderHistorySource = 'Manual' | 'Automatic' | 'Provider'
export interface OrderHistoryEvent { id: number; action: string; previousStatus: string | null; newStatus: string | null; reason: string | null; createdAtUtc: string; actorUserId: number | null; actorName: string | null; actorEmail: string | null; source: OrderHistorySource }
export interface Payment { id: number; orderId: number; provider: string; externalReference: string; providerPreferenceId: string | null; providerPaymentId: string | null; idempotencyKey: string | null; amount: number; currency: string; status: PaymentStatus; checkoutUrl: string | null; failureReason: string | null; createdAt: string; updatedAt: string | null; paidAt: string | null }
export interface AuditPage { items: AuditEntry[]; page: number; pageSize: number; totalItems: number; totalPages: number }
export interface AuditEntry { id: number; actorUserId: number | null; action: string; entityType: string; entityId: string; reason: string | null; createdAtUtc: string; correlationId: string }

export interface ApiErrorShape { status: number; message: string; traceId?: string; retryAfter?: number; validationErrors?: Record<string, string[]> }
