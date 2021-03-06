﻿using System;
using System.Threading;
using System.Threading.Tasks;
using Tgstation.Server.Api;
using Tgstation.Server.Api.Models;

namespace Tgstation.Server.Client
{
	/// <inheritdoc />
	sealed class UsersClient : IUsersClient
	{
		/// <summary>
		/// The <see cref="apiClient"/> for the <see cref="UsersClient"/>
		/// </summary>
		readonly IApiClient apiClient;

		/// <summary>
		/// Construct an <see cref="UsersClient"/>
		/// </summary>
		/// <param name="apiClient">The value of <see cref="apiClient"/></param>
		public UsersClient(IApiClient apiClient)
		{
			this.apiClient = apiClient ?? throw new ArgumentNullException(nameof(apiClient));
		}

		/// <inheritdoc />
		public Task<User> Create(UserUpdate user, CancellationToken cancellationToken) => apiClient.Create<UserUpdate, User>(Routes.User, user, cancellationToken);

		/// <inheritdoc />
		public Task<User> Read(CancellationToken cancellationToken) => apiClient.Read<User>(Routes.User, cancellationToken);

		/// <inheritdoc />
		public Task<User> Update(UserUpdate user, CancellationToken cancellationToken) => apiClient.Update<UserUpdate, User>(Routes.User, user, cancellationToken);
	}
}