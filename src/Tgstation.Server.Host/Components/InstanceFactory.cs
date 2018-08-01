﻿using Byond.TopicSender;
using Microsoft.Extensions.Logging;
using System;
using Tgstation.Server.Host.Components.Byond;
using Tgstation.Server.Host.Components.Chat;
using Tgstation.Server.Host.Components.Chat.Commands;
using Tgstation.Server.Host.Components.Compiler;
using Tgstation.Server.Host.Components.Repository;
using Tgstation.Server.Host.Components.StaticFiles;
using Tgstation.Server.Host.Components.Watchdog;
using Tgstation.Server.Host.Core;
using Tgstation.Server.Host.IO;
using Tgstation.Server.Host.Security;

namespace Tgstation.Server.Host.Components
{
	/// <inheritdoc />
	sealed class InstanceFactory : IInstanceFactory
	{
		/// <summary>
		/// The <see cref="IIOManager"/> for the <see cref="InstanceFactory"/>
		/// </summary>
		readonly IIOManager ioManager;

		/// <summary>
		/// The <see cref="IDatabaseContextFactory"/> for the <see cref="InstanceFactory"/>
		/// </summary>
		readonly IDatabaseContextFactory databaseContextFactory;

		/// <summary>
		/// The <see cref="IApplication"/> for the <see cref="InstanceFactory"/>
		/// </summary>
		readonly IApplication application;

		/// <summary>
		/// The <see cref="ILoggerFactory"/> for the <see cref="InstanceFactory"/>
		/// </summary>
		readonly ILoggerFactory loggerFactory;

		/// <summary>
		/// The <see cref="IByondTopicSender"/> for the <see cref="InstanceFactory"/>
		/// </summary>
		readonly IByondTopicSender byondTopicSender;

		/// <summary>
		/// The <see cref="IServerControl"/> for the <see cref="InstanceFactory"/>
		/// </summary>
		readonly IServerControl serverUpdater;

		/// <summary>
		/// The <see cref="ICryptographySuite"/> for the <see cref="InstanceFactory"/>
		/// </summary>
		readonly ICryptographySuite cryptographySuite;

		/// <summary>
		/// The <see cref="IExecutor"/> for the <see cref="InstanceFactory"/>
		/// </summary>
		readonly IExecutor executor;

		/// <summary>
		/// The <see cref="ICommandFactory"/> for the <see cref="InstanceFactory"/>
		/// </summary>
		readonly ICommandFactory commandFactory;

		/// <summary>
		/// The <see cref="ISynchronousIOManager"/> for the <see cref="InstanceFactory"/>
		/// </summary>
		readonly ISynchronousIOManager synchronousIOManager;

		/// <summary>
		/// The <see cref="ISymlinkFactory"/> for the <see cref="InstanceFactory"/>
		/// </summary>
		readonly ISymlinkFactory symlinkFactory;

		/// <summary>
		/// The <see cref="IByondInstaller"/> for the <see cref="InstanceFactory"/>
		/// </summary>
		readonly IByondInstaller byondInstaller;

		/// <summary>
		/// The <see cref="IProviderFactory"/> for the <see cref="InstanceFactory"/>
		/// </summary>
		readonly IProviderFactory providerFactory;

		/// <summary>
		/// The <see cref="IScriptExecutor"/> for the <see cref="InstanceFactory"/>
		/// </summary>
		readonly IScriptExecutor scriptExecutor;

		/// <summary>
		/// Construct an <see cref="InstanceFactory"/>
		/// </summary>
		/// <param name="ioManager">The value of <see cref="ioManager"/></param>
		/// <param name="databaseContextFactory">The value of <see cref="databaseContextFactory"/></param>
		/// <param name="application">The value of <see cref="application"/></param>
		/// <param name="loggerFactory">The value of <see cref="loggerFactory"/></param>
		/// <param name="byondTopicSender">The value of <see cref="byondTopicSender"/></param>
		/// <param name="serverUpdater">The value of <see cref="serverUpdater"/></param>
		/// <param name="cryptographySuite">The value of <see cref="cryptographySuite"/></param>
		/// <param name="executor">The value of <see cref="executor"/></param>
		/// <param name="commandFactory">The value of <see cref="commandFactory"/></param>
		/// <param name="synchronousIOManager">The value of <see cref="synchronousIOManager"/></param>
		/// <param name="symlinkFactory">The value of <see cref="symlinkFactory"/></param>
		/// <param name="byondInstaller">The value of <see cref="byondInstaller"/></param>
		/// <param name="providerFactory">The value of <see cref="providerFactory"/></param>
		/// <param name="scriptExecutor">The value of <see cref="scriptExecutor"/></param>
		public InstanceFactory(IIOManager ioManager, IDatabaseContextFactory databaseContextFactory, IApplication application, ILoggerFactory loggerFactory, IByondTopicSender byondTopicSender, IServerControl serverUpdater, ICryptographySuite cryptographySuite, IExecutor executor, ICommandFactory commandFactory, ISynchronousIOManager synchronousIOManager, ISymlinkFactory symlinkFactory, IByondInstaller byondInstaller, IProviderFactory providerFactory, IScriptExecutor scriptExecutor)
		{
			this.ioManager = ioManager ?? throw new ArgumentNullException(nameof(ioManager));
			this.databaseContextFactory = databaseContextFactory ?? throw new ArgumentNullException(nameof(databaseContextFactory));
			this.application = application ?? throw new ArgumentNullException(nameof(application));
			this.loggerFactory = loggerFactory ?? throw new ArgumentNullException(nameof(loggerFactory));
			this.byondTopicSender = byondTopicSender ?? throw new ArgumentNullException(nameof(byondTopicSender));
			this.serverUpdater = serverUpdater ?? throw new ArgumentNullException(nameof(serverUpdater));
			this.cryptographySuite = cryptographySuite ?? throw new ArgumentNullException(nameof(cryptographySuite	));
			this.executor = executor ?? throw new ArgumentNullException(nameof(executor));
			this.commandFactory = commandFactory ?? throw new ArgumentNullException(nameof(commandFactory));
			this.synchronousIOManager = synchronousIOManager ?? throw new ArgumentNullException(nameof(synchronousIOManager));
			this.symlinkFactory = symlinkFactory ?? throw new ArgumentNullException(nameof(symlinkFactory));
			this.byondInstaller = byondInstaller ?? throw new ArgumentNullException(nameof(byondInstaller));
			this.providerFactory = providerFactory ?? throw new ArgumentNullException(nameof(providerFactory));
			this.scriptExecutor = scriptExecutor ?? throw new ArgumentNullException(nameof(scriptExecutor));
		}

		/// <inheritdoc />
		public IInstance CreateInstance(Models.Instance metadata, IInteropRegistrar interopRegistrar)
		{
			//Create the ioManager for the instance
			var instanceIoManager = new ResolvingIOManager(ioManager, metadata.Path);

			//various other ioManagers
			var repoIoManager = new ResolvingIOManager(instanceIoManager, "Repository");
			var byondIOManager = new ResolvingIOManager(instanceIoManager, "Byond");
			var gameIoManager = new ResolvingIOManager(instanceIoManager, "Game");
			var configurationIoManager = new ResolvingIOManager(instanceIoManager, "Configuration");

			var configuration = new StaticFiles.Configuration(configurationIoManager, synchronousIOManager, symlinkFactory, scriptExecutor, loggerFactory.CreateLogger<StaticFiles.Configuration>());
			var eventConsumer = new EventConsumer(configuration);

			var dmbFactory = new DmbFactory(databaseContextFactory, gameIoManager, metadata.CloneMetadata());
			try
			{
				var commandFactory = new CommandFactory(application);
				var chatFactory = new ChatFactory(instanceIoManager, loggerFactory, commandFactory, providerFactory);

				var repoManager = new RepositoryManager(metadata.RepositorySettings, repoIoManager, eventConsumer);
				try
				{
					var byond = new ByondManager(byondIOManager, byondInstaller, loggerFactory.CreateLogger<ByondManager>());

					var chat = chatFactory.CreateChat(metadata.ChatSettings);
					try
					{
						var sessionControllerFactory = new SessionControllerFactory(executor, byond, byondTopicSender, interopRegistrar, cryptographySuite, application, gameIoManager, chat, loggerFactory, metadata.CloneMetadata());
						var reattachInfoHandler = new ReattachInfoHandler(databaseContextFactory, dmbFactory, metadata.CloneMetadata());
						var watchdogFactory = new WatchdogFactory(chat, sessionControllerFactory, serverUpdater, loggerFactory, reattachInfoHandler, databaseContextFactory, byondTopicSender, eventConsumer, metadata.CloneMetadata());
						var watchdog = watchdogFactory.CreateWatchdog(dmbFactory, metadata.DreamDaemonSettings);
						eventConsumer.SetWatchdog(watchdog);
						try
						{
							var dreamMaker = new DreamMaker(byond, ioManager, configuration, sessionControllerFactory, dmbFactory, application, eventConsumer, loggerFactory.CreateLogger<DreamMaker>());

							return new Instance(metadata.CloneMetadata(), repoManager, byond, dreamMaker, watchdog, chat, configuration, dmbFactory, databaseContextFactory, dmbFactory, loggerFactory.CreateLogger<Instance>());
						}
						catch
						{
							watchdog.Dispose();
							throw;
						}
					}
					catch
					{
						chat.Dispose();
						throw;
					}
				}
				catch
				{
					repoManager.Dispose();
					throw;
				}
			}
			catch
			{
				dmbFactory.Dispose();
				throw;
			}
		}
	}
}
