using System.Security.Cryptography;
using GymShop.Application.Abstractions;

namespace GymShop.Infrastructure.Services;

public class PasswordHasher : IPasswordHasher
{
    private const int SaltSize = 16;
    private const int KeySize = 32;
    private const int Iterations = 100_000;
    private static readonly HashAlgorithmName Algorithm = HashAlgorithmName.SHA256;

    public string Hash(string password)
    {
        var salt = RandomNumberGenerator.GetBytes(SaltSize);
        var key = Rfc2898DeriveBytes.Pbkdf2(password, salt, Iterations, Algorithm, KeySize);

        return string.Join('.', Convert.ToBase64String(salt), Convert.ToBase64String(key), Iterations, Algorithm.Name);
    }

    public bool Verify(string password, string passwordHash)
    {
        var parts = passwordHash.Split('.', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length != 4)
        {
            return false;
        }

        var salt = Convert.FromBase64String(parts[0]);
        var expectedKey = Convert.FromBase64String(parts[1]);
        var iterations = int.Parse(parts[2]);
        var algorithm = new HashAlgorithmName(parts[3]);
        var actualKey = Rfc2898DeriveBytes.Pbkdf2(password, salt, iterations, algorithm, expectedKey.Length);

        return CryptographicOperations.FixedTimeEquals(actualKey, expectedKey);
    }
}
