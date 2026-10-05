namespace GymShop.Domain.Enums;

public enum TransactionalNotificationType
{
    OrderCreated,
    PaymentApproved,
    PaymentRejected,
    OrderPreparing,
    OrderShipped,
    OrderReadyForPickup,
    PaymentRefunded,
    BillingDocumentAvailable
}

public enum NotificationDeliveryStatus
{
    Pending,
    Processing,
    Sent,
    DeadLetter
}
