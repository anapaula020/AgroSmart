using System.Security.Cryptography;
using System.Text;
using Api.Data;
using Api.Models;
using Microsoft.EntityFrameworkCore;

namespace Api.Services;

public class ApiKeyService(AppDbContext db)
{
    public static (string rawKey, string prefix, string hash) GenerateKey()
    {
        var raw    = $"ak_{Convert.ToBase64String(RandomNumberGenerator.GetBytes(32)).Replace("+","").Replace("/","").Replace("=","")[..40]}";
        var prefix = raw[..8];
        var hash   = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(raw))).ToLower();
        return (raw, prefix, hash);
    }

    public async Task<ApiKey?> ValidateAsync(string rawKey)
    {
        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(rawKey))).ToLower();
        var key  = await db.ApiKeys.FirstOrDefaultAsync(k =>
            k.KeyHash == hash && k.IsActive &&
            (k.ExpiresAt == null || k.ExpiresAt > DateTime.UtcNow));

        if (key is not null)
        {
            key.LastUsedAt = DateTime.UtcNow;
            await db.SaveChangesAsync();
        }
        return key;
    }
}
