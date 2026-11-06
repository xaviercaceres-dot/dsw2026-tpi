using Dsw2026Tpi.Data.Identity;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;

namespace Dsw2026Tpi.Application.Services;

public class JwtService
{
    /// <summary>
    /// Claim con el sello de seguridad del usuario. Al cerrar sesión el sello cambia y los tokens
    /// emitidos previamente dejan de ser válidos (tokens revocables).
    /// </summary>
    public const string SecurityStampClaimType = "security_stamp";

    private readonly IConfiguration _config;
    public JwtService(IConfiguration config)
    {
        _config = config;
    }

    public string GenerateToken(ApplicationUser user, string role)
    {
        if (_config == null) throw new ArgumentNullException();
        var jwtConfig = _config.GetSection("Jwt");
        var keyText = jwtConfig["Key"] ?? throw new ArgumentNullException("Jwt Key");
        var issuer = jwtConfig["Issuer"] ?? throw new ArgumentNullException("Jwt Issuer");
        var audience = jwtConfig["Audience"] ?? throw new ArgumentNullException("Jwt Audience");
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(keyText));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
        var expiresIn = int.Parse(jwtConfig["ExpiresInMinutes"] ?? "60");

        var claims = new[]
        {
            new Claim(JwtRegisteredClaimNames.Sub, user.Id),
            new Claim(ClaimTypes.Name, user.UserName!),
            new Claim(ClaimTypes.Role, role),
            new Claim(SecurityStampClaimType, user.SecurityStamp ?? string.Empty)
        };

        var token = new JwtSecurityToken(
            issuer: issuer,
            audience: audience,
            claims: claims,
            expires: DateTime.Now.AddMinutes(expiresIn),
            signingCredentials: creds
            );

        var tokenString = new JwtSecurityTokenHandler().WriteToken(token);

        return tokenString;
    }
}
