using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using Microsoft.IdentityModel.Tokens;
using Snapp.Shared.Constants;

namespace Snapp.Service.Auth.Services;

public class JwtTokenService
{
    private readonly RSA _rsa;
    private readonly string _issuer;
    private readonly string _audience;

    public JwtTokenService(IConfiguration configuration)
    {
        var pemPath = configuration["Auth:SigningKeyPath"]
            ?? throw new InvalidOperationException("Auth:SigningKeyPath is not configured");
        _issuer = configuration["Auth:Issuer"] ?? "snapp-auth";
        _audience = configuration["Auth:Audience"] ?? "snapp-api";

        var pemText = File.ReadAllText(pemPath);
        _rsa = RSA.Create();
        _rsa.ImportFromPem(pemText);
    }

    public string GenerateAccessToken(string userId, string? emailHash)
    {
        var key = new RsaSecurityKey(_rsa);
        var credentials = new SigningCredentials(key, SecurityAlgorithms.RsaSha256);

        var claims = new List<Claim>
        {
            new(Snapp.Shared.Auth.ClaimTypes.UserId, userId),
        };

        if (emailHash is not null)
            claims.Add(new(Snapp.Shared.Auth.ClaimTypes.Email, emailHash));

        var token = new JwtSecurityToken(
            issuer: _issuer,
            audience: _audience,
            claims: claims,
            expires: DateTime.UtcNow.AddMinutes(Limits.AccessTokenTtlMinutes),
            signingCredentials: credentials);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    public static string GenerateRefreshToken()
    {
        var bytes = RandomNumberGenerator.GetBytes(48);
        return Convert.ToBase64String(bytes)
            .Replace("+", "-")
            .Replace("/", "_")
            .TrimEnd('=');
    }

    public static string GenerateMagicLinkCode()
    {
        var bytes = RandomNumberGenerator.GetBytes(48);
        return Convert.ToBase64String(bytes)
            .Replace("+", "-")
            .Replace("/", "_")
            .TrimEnd('=');
    }
}
