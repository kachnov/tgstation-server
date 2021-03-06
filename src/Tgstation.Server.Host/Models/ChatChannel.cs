﻿using Tgstation.Server.Api.Models;

namespace Tgstation.Server.Host.Models
{
	/// <inheritdoc />
	public sealed class ChatChannel : Api.Models.ChatChannel
	{
		/// <summary>
		/// The row Id
		/// </summary>
		public long Id { get; set; }

		/// <summary>
		/// The <see cref="Api.Models.Internal.ChatBot.Id"/>
		/// </summary>
		public long ChatSettingsId { get; set; }

		/// <summary>
		/// The <see cref="Models.ChatBot"/>
		/// </summary>
		public ChatBot ChatSettings { get; set; }

		/// <summary>
		/// Convert the <see cref="ChatChannel"/> to it's API form
		/// </summary>
		/// <returns>A new <see cref="Api.Models.ChatChannel"/></returns>
		public Api.Models.ChatChannel ToApi() => new Api.Models.ChatChannel
		{
			DiscordChannelId = DiscordChannelId,
			IsAdminChannel = IsAdminChannel,
			IsWatchdogChannel = IsWatchdogChannel,
			IrcChannel = IrcChannel
		};
	}
}
