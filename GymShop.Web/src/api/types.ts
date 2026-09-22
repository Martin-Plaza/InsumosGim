export type Role = 'User' | 'Admin' | 'SuperAdmin'
export type OrderStatus = 'Pending' | 'Paid' | 'Preparing' | 'Shipped' | 'Delivered' | 'Canceled' | 'Refunded'
export type PaymentStatus = 'Creating' | 'Pending' | 'CreationFailed' | 'Approved' | 'Rejected' | 'Canceled' | 'Expired' | 'Refunded'

export interface User { id: number; email: string; name: string; lastName?: string | null; role: Role }
export interface RegistrationPending { email: string; expiresInSeconds: number; developmentCode: string | null }
export interface PasswordResetPending { message: string; expiresInSeconds: number; developmentCode: string | null }
export interface PasswordResetCompleted { message: string }
export interface AdminUser extends User { isActive: boolean; createdAt: string }
export interface AdminUserPage { items: AdminUser[]; page: number; pageSize: number; totalItems: number; totalPages: number }
export interface AdminUserFilters { page?: number; pageSize?: number; search?: string; role?: Role; isActive?: boolean }
export interface UserOrderSummary { id: number; createdAt: string; total: number; status: OrderStatus }
export interface AdminUserDetail extends AdminUser { orderCount: number; totalPurchased: number; lastOrderAt: string | null; ordersTotal: number; ordersPageSize: number; recentOrders: UserOrderSummary[] }
export interface AuthResponse { token: string; user: User }
export interface CategorySummary { id: number; name: string; slug: string }
export interface Category extends CategorySummary { description: string | null; displayOrder: number }
export interface AdminCategory extends Category { isActive: boolean; productCount: number }
export interface CategoryInput { name: string; slug: string; description: string | null; displayOrder: number }
export interface Product { id: number; name: string; description: string | null; price: number; stock: number; imageUrl: string | null; isActive: boolean; category: CategorySummary | null }
export interface CreateProductInput { name: string; description: string | null; price: number; stock: number; imageUrl: string | null; categoryId: number | null }
export interface UpdateProductInput { name: string; description: string | null; price: number; imageUrl: string | null; isActive: boolean; categoryId: number | null }
export type StockMovementType = 'InitialStock' | 'Sale' | 'CancellationReturn' | 'ManualEntry' | 'ManualCorrection' | 'LossDamage'
export interface StockMovement { id: number; productId: number; productName: string; type: StockMovementType; quantity: number; previousStock: number; resultingStock: number; reason: string; actorUserId: number | null; actorName: string | null; orderId: number | null; createdAtUtc: string }
export interface StockMovementPage { items: StockMovement[]; page: number; pageSize: number; totalItems: number; totalPages: number }
export interface StockAdjustment { productId: number; previousStock: number; resultingStock: number; movement: StockMovement }
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
export interface DashboardStatusCount { status: OrderStatus; count: number }
export interface DashboardDailySales { date: string; amount: number; orders: number }
export interface DashboardTopProduct { productId: number; productName: string; quantity: number; amount: number }
export interface DashboardStockProduct { productId: number; productName: string; stock: number; isActive: boolean }
export interface DashboardStatistics {
  fromUtc: string; toUtc: string; timeZoneId: string; totalSales: number; paidOrders: number; averageTicket: number
  ordersByStatus: DashboardStatusCount[]; salesByDay: DashboardDailySales[]; topProducts: DashboardTopProduct[]
  outOfStockProducts: DashboardStockProduct[]; lowStockProducts: DashboardStockProduct[]; lowStockThreshold: number
}
export interface DashboardFilters { period?: '7d' | '30d' | 'month'; from?: string; to?: string }

export interface ApiErrorShape { status: number; message: string; traceId?: string; retryAfter?: number; validationErrors?: Record<string, string[]> }
