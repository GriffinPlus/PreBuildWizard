///////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// This file is part of the Griffin+ PreBuildWizard (https://github.com/griffinplus/PreBuildWizard).
//
// Copyright 2019 Sascha Falk <sascha@falk-online.eu>
// Copyright 2019 Sebastian Piel <sebastianpiel@outlook.de>
//
// Licensed under the Apache License, Version 2.0 (the "License"); you may not use this file except in compliance
// with the License. You may obtain a copy of the License at http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software distributed under the License is distributed
// on an "AS IS" BASIS, WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied. See the License for
// the specific language governing permissions and limitations under the License.
///////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

using GriffinPlus.Lib.Logging;

namespace GriffinPlus.PreBuildWizard
{

	/// <summary>
	/// Base class for file processors.
	/// </summary>
	public abstract class FileProcessorBase : IFileProcessor
	{
		/// <summary>
		/// Log writer the file processor uses.
		/// </summary>
		protected readonly LogWriter mLog;

		/// <summary>
		/// Regular expression matching variables to expand (format: '{{ MyVar }}').
		/// </summary>
		protected static readonly Regex sExpandedVariableRegex = new(@"{{\s*(.+?)\s*}}", RegexOptions.Compiled);

		/// <summary>
		/// List of file name patterns the file processor should process.
		/// </summary>
		protected readonly List<Regex> mFileNamePatterns;

		/// <summary>
		/// Initializes a new instance of the <see cref="FileProcessorBase"/> class.
		/// </summary>
		/// <param name="name">Name of the file processor.</param>
		/// <param name="patterns">File name patterns the file processor is responsible for.</param>
		protected FileProcessorBase(string name, params Regex[] patterns)
		{
			mLog = LogWriter.Get(GetType()); // use more specific type, instead of FileProcessorBase
			Name = name;

			mFileNamePatterns = new();
			foreach (Regex pattern in patterns)
			{
				if (pattern != null)
				{
					mFileNamePatterns.Add(pattern);
				}
			}
		}

		/// <summary>
		/// Gets the name of the file processor.
		/// </summary>
		public string Name { get; }

		/// <summary>
		/// Determines whether the file processor is applicable on the specified file.
		/// </summary>
		/// <param name="appCore">App core that runs the file processor.</param>
		/// <param name="path">Path of the file to check.</param>
		/// <returns>true, if the file processor is applicable; otherwise false.</returns>
		public bool IsApplicable(AppCore appCore, string path)
		{
			string fileName = Path.GetFileName(path);
			return mFileNamePatterns.Any(x => x.IsMatch(fileName));
		}

		/// <summary>
		/// Processes the specified file.
		/// </summary>
		/// <param name="appCore">App core that runs the file processor.</param>
		/// <param name="path">Path of the file to process.</param>
		public virtual async Task ProcessAsync(AppCore appCore, string path)
		{
			await using var fs = new FileStream(path, FileMode.Open, FileAccess.ReadWrite, FileShare.None);

			// read file into memory
			string data;
			Encoding encoding;
			mLog.Write(LogLevel.Trace, "Loading file {0}.", path);
			using (var reader = new StreamReader(fs))
			{
				data = await reader.ReadToEndAsync().ConfigureAwait(false);
				encoding = reader.CurrentEncoding;
			}

			// replace all occurrences of environment variables
			bool modified = false;
			foreach (Match match in sExpandedVariableRegex.Matches(data))
			{
				string variableName = match.Groups[1].Value;
				string replacement = Environment.GetEnvironmentVariable(variableName);

				if (replacement != null)
				{
					mLog.Write(LogLevel.Trace, "Replacing environment variable '{0}' with '{1}' in file {2}.", variableName, replacement, path);
					data = sExpandedVariableRegex.Replace(data, replacement);
					modified = true;
				}
				else
				{
					throw new FileProcessingException("Processing {0} failed, expected environment variable '{1}' is not set.", path, variableName);
				}
			}

			// write changed file
			if (modified)
			{
				mLog.Write(LogLevel.Trace, "Writing file {0}.", path);
				fs.SetLength(0);
				await using var writer = new StreamWriter(fs, encoding);
				await writer.WriteAsync(data).ConfigureAwait(false);
			}
			else
			{
				mLog.Write(LogLevel.Trace, "File {0} was not modified.", path);
			}
		}
	}

}
