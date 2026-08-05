namespace GymShop.Domain.Entities;

public class User
{
    public int Id { get; set; }
    public string Email { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? LastName { get; set; }
    public string? Phone { get; set; }
    public string? Address { get; set; }
    public int RoleId { get; set; }
    public bool IsActive { get; set; } = true;
    public int TokenVersion { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }

    public Role Role { get; set; } = null!;
    public ICollection<Order> Orders { get; set; } = new List<Order>();
    public Cart? Cart { get; set; }
}


