﻿using System;
using Tgstation.Server.Host.Models;

namespace Tgstation.Server.Host.Components.Compiler
{
	/// <summary>
	/// Temporary <see cref="IDmbProvider"/>
	/// </summary>
	sealed class TemporaryDmbProvider : IDmbProvider
	{
		/// <inheritdoc />
		public string DmbName { get; }

		/// <inheritdoc />
		public string PrimaryDirectory { get; }

		/// <inheritdoc />
		public string SecondaryDirectory => throw new NotSupportedException();

		/// <inheritdoc />
		public CompileJob CompileJob { get; }

		/// <summary>
		/// Construct a <see cref="TemporaryDmbProvider"/>
		/// </summary>
		/// <param name="directory">The value of <see cref="PrimaryDirectory"/></param>
		/// <param name="dmb">The value of <see cref="DmbName"/></param>
		/// <param name="compileJob">The value of <see cref="CompileJob"/></param>
		public TemporaryDmbProvider(string directory, string dmb, CompileJob compileJob)
		{
			DmbName = dmb ?? throw new ArgumentNullException(nameof(dmb));
			PrimaryDirectory = directory ?? throw new ArgumentNullException(nameof(directory));
			CompileJob = compileJob ?? throw new ArgumentNullException(nameof(compileJob));
		}

		/// <inheritdoc />
		public void Dispose() { }

		public void KeepAlive() => throw new NotSupportedException();
	}
}