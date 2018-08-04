﻿using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Tgstation.Server.Api.Rights;
using Tgstation.Server.Host.Components;
using Tgstation.Server.Host.Core;
using Tgstation.Server.Host.Models;
using Tgstation.Server.Host.Security;

namespace Tgstation.Server.Host.Controllers
{
	/// <summary>
	/// Controller for managing the compiler
	/// </summary>
	[Route("/" + nameof(Api.Models.DreamMaker))]
    public sealed class DreamMakerController : ModelController<Api.Models.DreamMaker>
	{
		/// <summary>
		/// The <see cref="IJobManager"/> for the <see cref="DreamMakerController"/>
		/// </summary>
		readonly IJobManager jobManager;
		/// <summary>
		/// The <see cref="IInstanceManager"/> for the <see cref="DreamMakerController"/>
		/// </summary>
		readonly IInstanceManager instanceManager;

		/// <summary>
		/// Construct a <see cref="DreamMakerController"/>
		/// </summary>
		/// <param name="databaseContext">The <see cref="IDatabaseContext"/> for the <see cref="ApiController"/></param>
		/// <param name="authenticationContextFactory">The <see cref="IAuthenticationContextFactory"/> for the <see cref="ApiController"/></param>
		/// <param name="jobManager">The value of <see cref="jobManager"/></param>
		/// <param name="instanceManager">The value of <see cref="instanceManager"/></param>
		/// <param name="logger">The <see cref="ILogger"/> for the <see cref="ApiController"/></param>
		public DreamMakerController(IDatabaseContext databaseContext, IAuthenticationContextFactory authenticationContextFactory, IJobManager jobManager, IInstanceManager instanceManager, ILogger<DreamMakerController> logger) : base(databaseContext, authenticationContextFactory, logger, true)
		{
			this.jobManager = jobManager ?? throw new ArgumentNullException(nameof(jobManager));
			this.instanceManager = instanceManager ?? throw new ArgumentNullException(nameof(instanceManager));
		}

		/// <inheritdoc />
		[TgsAuthorize(DreamMakerRights.Read)]
		public override async Task<IActionResult> Read(CancellationToken cancellationToken)
		{
			var instance = instanceManager.GetInstance(Instance);
			var projectNameTask = DatabaseContext.DreamMakerSettings.Where(x => x.InstanceId == Instance.Id).Select(x => x.ProjectName).FirstOrDefaultAsync(cancellationToken);
			var job = await DatabaseContext.CompileJobs.OrderByDescending(x => x.Job.StartedAt).Include(x => x.Job).FirstOrDefaultAsync(cancellationToken).ConfigureAwait(false);
			return Json(new Api.Models.DreamMaker
			{
				LastJob = job?.ToApi(),
				ProjectName = await projectNameTask.ConfigureAwait(false),
				Status = instance.DreamMaker.Status
			});
		}

		/// <inheritdoc />
		[TgsAuthorize(DreamMakerRights.Compile)]
		public override async Task<IActionResult> Create([FromBody] Api.Models.DreamMaker model, CancellationToken cancellationToken)
		{
			var job = new Job
			{
				Description = "Compile active repository code",
				StartedBy = AuthenticationContext.User,
				CancelRightsType = RightsType.DreamMaker,
				CancelRight = (int)DreamMakerRights.CancelCompile,
				Instance = Instance
			};
			await jobManager.RegisterOperation(job, (paramJob, serviceProvider, progressReporter, ct) => RunCompile(paramJob, serviceProvider, Instance, ct), cancellationToken).ConfigureAwait(false);
			return Json(job.ToApi());
		}

		/// <inheritdoc />
		[TgsAuthorize(DreamMakerRights.SetDme)]
		public override async Task<IActionResult> Update([FromBody] Api.Models.DreamMaker model, CancellationToken cancellationToken)
		{
			var hostModel = new DreamMakerSettings
			{
				InstanceId = Instance.Id
			};
			DatabaseContext.DreamMakerSettings.Attach(hostModel);
			hostModel.ProjectName = model.ProjectName;
			await DatabaseContext.Save(cancellationToken).ConfigureAwait(false);
			return await Read(cancellationToken).ConfigureAwait(false);
		}

		/// <summary>
		/// Run the compile job and insert it into the database
		/// </summary>
		/// <param name="job">The running <see cref="Job"/></param>
		/// <param name="serviceProvider">The <see cref="IServiceProvider"/> for the operation</param>
		/// <param name="instanceModel">The <see cref="Models.Instance"/> for the operation</param>
		/// <param name="cancellationToken">The <see cref="CancellationToken"/> for the operation</param>
		/// <returns>A <see cref="Task"/> representing the running operation</returns>
		async Task RunCompile(Job job, IServiceProvider serviceProvider, Models.Instance instanceModel, CancellationToken cancellationToken)
		{
			var instanceManager = serviceProvider.GetRequiredService<IInstanceManager>();
			var databaseContext = serviceProvider.GetRequiredService<IDatabaseContext>();

			var timeoutTask = databaseContext.DreamDaemonSettings.Where(x => x.InstanceId == instanceModel.Id).Select(x => x.StartupTimeout).FirstOrDefaultAsync(cancellationToken);
			var projectName = await databaseContext.DreamMakerSettings.Where(x => x.InstanceId == instanceModel.Id).Select(x => x.ProjectName).FirstOrDefaultAsync(cancellationToken).ConfigureAwait(false);
			var timeout = await timeoutTask.ConfigureAwait(false);

			var instance = instanceManager.GetInstance(instanceModel);

			CompileJob compileJob;
			string repoSha = null;
			using (var repo = await instance.RepositoryManager.LoadRepository(cancellationToken).ConfigureAwait(false))
			{
				if (repo == null)
				{
					job.ExceptionDetails = "Missing repository!";
					return;
				}
				repoSha = repo.Head;
				compileJob = await instance.DreamMaker.Compile(projectName, timeout.Value, repo, cancellationToken).ConfigureAwait(false);
			}

			compileJob.Job = job;
			compileJob.RevisionInformation = await databaseContext.RevisionInformations.Where(x => x.CommitSha == repoSha).Select(x => new RevisionInformation { Id = x.Id }).FirstOrDefaultAsync().ConfigureAwait(false);

			if (compileJob.RevisionInformation == default)
			{
				compileJob.RevisionInformation = new RevisionInformation
				{
					CommitSha = repoSha,
					OriginCommitSha = repoSha,
					Instance = new Models.Instance
					{
						Id = Instance.Id
					}
				};
				DatabaseContext.Instances.Attach(compileJob.RevisionInformation.Instance);
			}

			databaseContext.CompileJobs.Add(compileJob);
			//default ct because we don't want to give up after getting this far
			await databaseContext.Save(default).ConfigureAwait(false);
		}
	}
}
