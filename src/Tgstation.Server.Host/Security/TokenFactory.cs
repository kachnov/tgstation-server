﻿using Microsoft.IdentityModel.Tokens;
using System;
using System.Globalization;
using System.IdentityModel.Tokens.Jwt;
using System.Reflection;
using System.Security.Claims;
using Tgstation.Server.Api.Models;

namespace Tgstation.Server.Host.Security
{
	/// <inheritdoc />
	sealed class TokenFactory : ITokenFactory
	{
		/// <summary>
		/// Amount of minutes until generated <see cref="Token"/>s expire
		/// </summary>
		const int TokenExpiryMinutes = 15;

		public static readonly string TokenAudience = typeof(Token).Assembly.GetName().Name;
		public static readonly string TokenIssuer = Assembly.GetExecutingAssembly().GetName().Name;
		public static readonly byte[] TokenSigningKey = CryptographySuite.GetSecureBytes(256);

		/// <inheritdoc />
		public Token CreateToken(Models.User user)
		{
			if (user == null)
				throw new ArgumentNullException(nameof(user));

			var expiry = DateTimeOffset.Now.AddMinutes(TokenExpiryMinutes);
			var claims = new Claim[]
			{
				new Claim(JwtRegisteredClaimNames.Sub, user.Id.ToString(CultureInfo.InvariantCulture)),
				new Claim(JwtRegisteredClaimNames.Exp, $"{expiry.ToUnixTimeSeconds()}"),
				new Claim(JwtRegisteredClaimNames.Nbf, $"{DateTimeOffset.Now.ToUnixTimeSeconds()}"),
				new Claim(JwtRegisteredClaimNames.Iss, TokenIssuer),
				new Claim(JwtRegisteredClaimNames.Aud, TokenAudience)
			};

			var key = new SymmetricSecurityKey(TokenSigningKey);

			var token = new JwtSecurityToken(new JwtHeader(new SigningCredentials(key, SecurityAlgorithms.HmacSha256)), new JwtPayload(claims));
			return new Token { Bearer = new JwtSecurityTokenHandler().WriteToken(token), ExpiresAt = expiry };
		}
	}
}
