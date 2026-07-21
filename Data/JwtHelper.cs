using System;
using System.Collections.Generic;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.IdentityModel.Tokens;
using QLStudy.API.Models;

namespace QLStudy.API.Data
{
    public static class JwtHelper
    {
        // 256-bit secret key fallback for JWT signing
        private const string DefaultSecretKey = "QLStudy_Super_Secure_Secret_Key_For_JWT_Signing_1234567890_Please_Change_In_Production";

        public static string GenerateToken(User user, string? configSecretKey)
        {
            var keyStr = string.IsNullOrWhiteSpace(configSecretKey) ? DefaultSecretKey : configSecretKey;
            var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(keyStr));
            var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

            var claims = new List<Claim>
            {
                new Claim(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
                new Claim(JwtRegisteredClaimNames.Email, user.Email),
                new Claim(ClaimTypes.Role, user.Role),
                new Claim("name", user.FullName)
            };

            var token = new JwtSecurityToken(
                issuer: "QLStudy.API",
                audience: "QLStudy.Frontend",
                claims: claims,
                expires: DateTime.UtcNow.AddDays(7),
                signingCredentials: credentials);

            return new JwtSecurityTokenHandler().WriteToken(token);
        }

        public static ClaimsPrincipal? ValidateToken(string token, string? configSecretKey)
        {
            if (string.IsNullOrEmpty(token)) return null;

            var keyStr = string.IsNullOrWhiteSpace(configSecretKey) ? DefaultSecretKey : configSecretKey;
            var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(keyStr));

            var tokenHandler = new JwtSecurityTokenHandler();
            try
            {
                var validationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidIssuer = "QLStudy.API",
                    ValidateAudience = true,
                    ValidAudience = "QLStudy.Frontend",
                    ValidateLifetime = true,
                    ClockSkew = TimeSpan.Zero,
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey = key
                };

                var principal = tokenHandler.ValidateToken(token, validationParameters, out SecurityToken validatedToken);
                return principal;
            }
            catch
            {
                return null;
            }
        }
    }
}
