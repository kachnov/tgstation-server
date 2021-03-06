﻿using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Tgstation.Server.Host.IO
{
	/// <summary>
	/// <see cref="IIOManager"/> that resolves paths to <see cref="Environment.CurrentDirectory"/>
	/// </summary>
	class DefaultIOManager : IIOManager
	{
		/// <summary>
		/// Default <see cref="FileStream"/> buffer size used by .NET
		/// </summary>
		public const int DefaultBufferSize = 4096;

		/// <summary>
		/// Recursively empty a directory
		/// </summary>
		/// <param name="dir"><see cref="DirectoryInfo"/> of the directory to empty</param>
		/// <param name="cancellationToken">The <see cref="CancellationToken"/> for the operation</param>
		/// <returns>A <see cref="Task"/> representing the running operation</returns>
		static async Task NormalizeAndDelete(DirectoryInfo dir, CancellationToken cancellationToken)
		{
			var tasks = new List<Task>();

			foreach (var subDir in dir.EnumerateDirectories())
			{
				cancellationToken.ThrowIfCancellationRequested();
				if (!subDir.Attributes.HasFlag(FileAttributes.Directory) || subDir.Attributes.HasFlag(FileAttributes.ReparsePoint))
					//this is probably a symlink
					subDir.Delete();
				else
					tasks.Add(NormalizeAndDelete(subDir, cancellationToken));
			}
			foreach (var file in dir.EnumerateFiles())
			{
				cancellationToken.ThrowIfCancellationRequested();
				file.Attributes = FileAttributes.Normal;
				file.Delete();
			}
			await Task.WhenAll(tasks).ConfigureAwait(false);
			cancellationToken.ThrowIfCancellationRequested();
			dir.Delete(true);
		}

		/// <summary>
		/// Opens a <see cref="FileStream"/> for async writing at a given <paramref name="path"/>
		/// </summary>
		/// <param name="path">The path to open the <see cref="FileStream"/> at</param>
		/// <returns>A new <see cref="FileStream"/> ready for async writing</returns>
		static FileStream OpenWriteStream(string path) => new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.ReadWrite, DefaultBufferSize, true);

		/// <summary>
		/// Copies a directory from <paramref name="src"/> to <paramref name="dest"/>
		/// </summary>
		/// <param name="src">The source directory path</param>
		/// <param name="dest">The destination directory path</param>
		/// <param name="ignore">Files and folders to ignore at the root level</param>
		/// <param name="cancellationToken">The <see cref="CancellationToken"/> for the operation</param>
		/// <returns>A <see cref="IEnumerable{T}"/> of <see cref="Task"/>s representing the running operation</returns>
		IEnumerable<Task> CopyDirectoryImpl(string src, string dest, IEnumerable<string> ignore, CancellationToken cancellationToken)
		{
			var dir = new DirectoryInfo(src);
			var atLeastOneSubDir = false;
			foreach (var I in dir.EnumerateDirectories())
			{
				if (ignore != null && ignore.Contains(I.Name))
					continue;
				foreach (var J in CopyDirectoryImpl(I.FullName, Path.Combine(dest, I.Name), null, cancellationToken))
				{
					atLeastOneSubDir = true;
					yield return J;
				}
			}

			async Task CopyThisDirectory()
			{
				if (!atLeastOneSubDir)
					await CreateDirectory(dest, cancellationToken).ConfigureAwait(false);	//save on createdir calls

				var tasks = new List<Task>();

				await dir.EnumerateFiles().ToAsyncEnumerable().ForEachAsync(I =>
				{
					if (ignore != null && ignore.Contains(I.Name))
						return;
					tasks.Add(CopyFile(I.FullName, Path.Combine(dest, I.Name), cancellationToken));
				}).ConfigureAwait(false);

				await Task.WhenAll(tasks).ConfigureAwait(false);
			};

			yield return CopyThisDirectory();
		}

		/// <inheritdoc />
		public async Task CopyDirectory(string src, string dest, IEnumerable<string> ignore, CancellationToken cancellationToken)
		{
			if (dest == null)
				throw new ArgumentNullException(nameof(src));
			if (dest == null)
				throw new ArgumentNullException(nameof(src));
			
			src = ResolvePath(src);
			dest = ResolvePath(dest);
			foreach (var directoryCopy in CopyDirectoryImpl(src, dest, ignore, cancellationToken))
				await directoryCopy.ConfigureAwait(false);
		}

		/// <inheritdoc />
		public async Task AppendAllText(string path, string additional_contents, CancellationToken cancellationToken)
		{
			if (additional_contents == null)
				throw new ArgumentNullException(nameof(additional_contents));
			using (var destStream = new FileStream(ResolvePath(path), FileMode.Append, FileAccess.Write, FileShare.ReadWrite | FileShare.Delete, DefaultBufferSize, true))
			{
				var buf = Encoding.UTF8.GetBytes(additional_contents);
				await destStream.WriteAsync(buf, 0, buf.Length, cancellationToken).ConfigureAwait(false);
			}
		}

		/// <inheritdoc />
		public string ConcatPath(params string[] paths)
		{
			if (paths == null)
				throw new ArgumentNullException(nameof(paths));
			return Path.Combine(paths);
		}

		/// <inheritdoc />
		public async Task CopyFile(string src, string dest, CancellationToken cancellationToken)
		{
			if (src == null)
				throw new ArgumentNullException(nameof(src));
			if (dest == null)
				throw new ArgumentNullException(nameof(dest));
			using (var srcStream = new FileStream(ResolvePath(src), FileMode.Open, FileAccess.Read, FileShare.Read | FileShare.Delete, DefaultBufferSize, true))
			using (var destStream = new FileStream(ResolvePath(dest), FileMode.Create, FileAccess.Write, FileShare.ReadWrite | FileShare.Delete, DefaultBufferSize, true))
				await srcStream.CopyToAsync(destStream, 81920, cancellationToken).ConfigureAwait(false);
		}

		/// <inheritdoc />
		public Task CreateDirectory(string path, CancellationToken cancellationToken) => Task.Factory.StartNew(() => Directory.CreateDirectory(ResolvePath(path)), cancellationToken, TaskCreationOptions.LongRunning, TaskScheduler.Current);

		/// <inheritdoc />
		public async Task DeleteDirectory(string path, CancellationToken cancellationToken)
		{
			path = ResolvePath(path);
			var di = new DirectoryInfo(path);
			if (!di.Exists)
				return;
			await NormalizeAndDelete(di, cancellationToken).ConfigureAwait(false);
		}

		/// <inheritdoc />
		public Task DeleteFile(string path, CancellationToken cancellationToken) => Task.Factory.StartNew(() => File.Delete(ResolvePath(path)), cancellationToken, TaskCreationOptions.LongRunning, TaskScheduler.Current);

		/// <inheritdoc />
		public Task<bool> FileExists(string path, CancellationToken cancellationToken) => Task.Factory.StartNew(() => File.Exists(ResolvePath(path)), cancellationToken, TaskCreationOptions.LongRunning, TaskScheduler.Current);

		/// <inheritdoc />
		public Task<bool> DirectoryExists(string path, CancellationToken cancellationToken) => Task.Factory.StartNew(() => Directory.Exists(ResolvePath(path)), cancellationToken, TaskCreationOptions.LongRunning, TaskScheduler.Current);

		/// <inheritdoc />
		public string GetDirectoryName(string path) => Path.GetDirectoryName(path ?? throw new ArgumentNullException(nameof(path)));

		/// <inheritdoc />
		public string GetFileName(string path) => Path.GetFileName(path ?? throw new ArgumentNullException(nameof(path)));

		/// <inheritdoc />
		public string GetFileNameWithoutExtension(string path) => Path.GetFileNameWithoutExtension(path ?? throw new ArgumentNullException(nameof(path)));

		/// <inheritdoc />
		public Task<List<string>> GetFilesWithExtension(string path, string extension, CancellationToken cancellationToken) => Task.Factory.StartNew(() =>
		{
			path = ResolvePath(path);
			if (extension == null)
				throw new ArgumentNullException(extension);
			var results = new List<string>();
			foreach (var I in Directory.EnumerateFiles(path, String.Format(CultureInfo.InvariantCulture, "*.{0}", extension), SearchOption.TopDirectoryOnly))
			{
				cancellationToken.ThrowIfCancellationRequested();
				results.Add(I);
			}
			return results;
		}, cancellationToken, TaskCreationOptions.LongRunning, TaskScheduler.Current);

		/// <inheritdoc />
		public Task MoveFile(string source, string destination, CancellationToken cancellationToken) => Task.Factory.StartNew(() =>
		{
			if (destination == null)
				throw new ArgumentNullException(nameof(destination));
			source = ResolvePath(source ?? throw new ArgumentNullException(nameof(source)));
			destination = ResolvePath(destination);
			File.Move(source, destination);
		}, cancellationToken, TaskCreationOptions.LongRunning, TaskScheduler.Current);

		/// <inheritdoc />
		public Task MoveDirectory(string source, string destination, CancellationToken cancellationToken) => Task.Factory.StartNew(() =>
		{
			if (destination == null)
				throw new ArgumentNullException(nameof(destination));
			source = ResolvePath(source ?? throw new ArgumentNullException(nameof(source)));
			destination = ResolvePath(destination);
			Directory.Move(source, destination);
		}, cancellationToken, TaskCreationOptions.LongRunning, TaskScheduler.Current);

		/// <inheritdoc />
		public async Task<byte[]> ReadAllBytes(string path, CancellationToken cancellationToken)
		{
			path = ResolvePath(path);
			using (var file = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read | FileShare.Delete, DefaultBufferSize, true))
			{
				byte[] buf;
				buf = new byte[file.Length];
				await file.ReadAsync(buf, 0, (int)file.Length, cancellationToken).ConfigureAwait(false);
				return buf;
			}
		}

		/// <inheritdoc />
		public virtual string ResolvePath(string path) => Path.GetFullPath(path ?? throw new ArgumentNullException(nameof(path)));

		/// <inheritdoc />
		public async Task WriteAllBytes(string path, byte[] contents, CancellationToken cancellationToken)
		{
			path = ResolvePath(path);
			using (var file = OpenWriteStream(path))
				await file.WriteAsync(contents, 0, contents.Length, cancellationToken).ConfigureAwait(false);
		}

		/// <inheritdoc />
		public Task<IReadOnlyList<string>> GetDirectories(string path, CancellationToken cancellationToken) => Task.Factory.StartNew(() =>
		{
			path = ResolvePath(path);
			var results = new List<string>();
			cancellationToken.ThrowIfCancellationRequested();
			foreach (var I in Directory.EnumerateDirectories(path))
			{
				results.Add(I);
				cancellationToken.ThrowIfCancellationRequested();
			}
			return (IReadOnlyList<string>)results;
		}, cancellationToken, TaskCreationOptions.LongRunning, TaskScheduler.Current);

		/// <inheritdoc />
		public Task<IReadOnlyList<string>> GetFiles(string path, CancellationToken cancellationToken) => Task.Factory.StartNew(() =>
		{
			path = ResolvePath(path);
			var results = new List<string>();
			cancellationToken.ThrowIfCancellationRequested();
			foreach (var I in Directory.EnumerateFiles(path))
			{
				results.Add(I);
				cancellationToken.ThrowIfCancellationRequested();
			}
			return (IReadOnlyList<string>)results;
		}, cancellationToken, TaskCreationOptions.LongRunning, TaskScheduler.Current);

		/// <inheritdoc />
		public Task CreateSymlink(string target, string link, CancellationToken cancellationToken) => Task.Factory.StartNew(() =>
		{
			target = ResolvePath(target);
			link = ResolvePath(link);

		}, cancellationToken, TaskCreationOptions.LongRunning, TaskScheduler.Current);

		/// <inheritdoc />
		public async Task<byte[]> DownloadFile(Uri url, CancellationToken cancellationToken)
		{
			//DownloadDataTaskAsync can't be cancelled and is shittily written, don't use it
			using (var wc = new WebClient())
			{
				var tcs = new TaskCompletionSource<byte[]>();
				wc.DownloadDataCompleted += (a, b) =>
				{
					if (b.Error != null)
						tcs.TrySetException(b.Error);
					else if (b.Cancelled)
						tcs.TrySetCanceled();
					else
						tcs.TrySetResult(b.Result);
				};
				wc.DownloadDataAsync(url);
				using (cancellationToken.Register(() =>
				{
					wc.CancelAsync();	//cancelasync alone doesnt do it either! who wrote this!!
					tcs.SetCanceled();
				}))
					return await tcs.Task.ConfigureAwait(false);
			}	//ITS STILL FUCKING DOWNLOADING!!!
		}

		/// <inheritdoc />
		public async Task ZipToDirectory(string path, byte[] zipFileBytes, CancellationToken cancellationToken)
		{
			path = ResolvePath(path);
			if (zipFileBytes == null)
				throw new ArgumentNullException(nameof(zipFileBytes));

			using (var ms = new MemoryStream(zipFileBytes))
			{
				zipFileBytes = null;
				using (var archive = new ZipArchive(ms))
				{
					string GetEntryName(ZipArchiveEntry entry) => ConcatPath(path, entry.FullName);
					//create directories first
					await Task.WhenAll(CreateDirectory(path, cancellationToken), Task.WhenAll(archive.Entries.Where(x => x.Name.Length == 0).Select(x => CreateDirectory(GetEntryName(x), cancellationToken)))).ConfigureAwait(false);
					//extract files
					await Task.WhenAll(archive.Entries.Where(x => x.Name.Length > 0).Select(async x =>
					{
						var entryPath = GetEntryName(x);
						using (var stream = x.Open())
						using (var file = OpenWriteStream(entryPath))
							await stream.CopyToAsync(file).ConfigureAwait(false);
					})).ConfigureAwait(false);
				}
			}
		}
	}
}
