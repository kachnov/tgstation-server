﻿using System;

namespace Tgstation.Server.Api.Models.Internal
{
	/// <summary>
	/// Represents a run of <see cref="DreamMaker"/>
	/// </summary>
	public class CompileJob
	{
		/// <summary>
		/// The ID of the job
		/// </summary>
		public long Id { get; set; }

		/// <summary>
		/// The .dme file used for compilation
		/// </summary>
		public string DmeName { get; set; }

		/// <summary>
		/// Textual output of DM
		/// </summary>
		public string Output { get; set; }

		/// <summary>
		/// The Game folder the results were compiled into
		/// </summary>
		public Guid? DirectoryName { get; set; }
	}
}
