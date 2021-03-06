﻿namespace Tgstation.Server.Host.Configuration
{
	/// <summary>
	/// Configuration options for the <see cref="Models.DatabaseContext{TParentContext}"/>
	/// </summary>
	sealed class DatabaseConfiguration
	{
		/// <summary>
		/// The key for the <see cref="Microsoft.Extensions.Configuration.IConfigurationSection"/> the <see cref="DatabaseConfiguration"/> resides in
		/// </summary>
		public const string Section = "Database";

		/// <summary>
		/// The <see cref="Configuration.DatabaseType"/> to create
		/// </summary>
		public DatabaseType DatabaseType { get; set; }

		/// <summary>
		/// If the admin user should be enabled and have it's password reset
		/// </summary>
		public bool ResetAdminPassword { get; set; }

		/// <summary>
		/// The connection string for the database
		/// </summary>
		public string ConnectionString { get; set; }

		/// <summary>
		/// If the database should use direct table creation instead of automatic migrations. Should not be used in production!
		/// </summary>
		public bool NoMigrations { get; set; }
	}
}
