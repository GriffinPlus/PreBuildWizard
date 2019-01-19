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

using GriffinPlus.Lib.Logging;
using System;
using System.IO;
using System.Text.RegularExpressions;

namespace GriffinPlus.PreBuildWizard
{
	/// <summary>
	/// Processes AssemblyInfo.cs files and patches version information specified in these files.
	/// </summary>
	public class AssemblyInfoFileProcessor : IFileProcessor
	{
		private static LogWriter sLog = Log.GetWriter<AssemblyInfoFileProcessor>();
		private const string ProcessorName = "Assembly Info";
		private static readonly Regex sFileNameRegex = new Regex(@"^(.*AssemblyInfo.*)\.(cs|cpp|mcpp)$", RegexOptions.Compiled | RegexOptions.IgnoreCase);

		private const string versionRegexFormat = @"(?<=\[\s*assembly\s*:\s*{0}(?:Attribute)?\s*\(\s*"")(.*)(?=""\s*\)\s*\])"; // matches the version string only!

		private static readonly Regex sAssemblyVersionRegex = new Regex(
			string.Format(versionRegexFormat, "AssemblyVersion"),
			RegexOptions.Compiled | RegexOptions.IgnoreCase);

		private static readonly Regex sAssemblyFileVersionRegex = new Regex(
			string.Format(versionRegexFormat, "AssemblyFileVersion"),
			RegexOptions.Compiled | RegexOptions.IgnoreCase);

		private static readonly Regex sAssemblyInformationalVersionRegex = new Regex(
			string.Format(versionRegexFormat, "AssemblyInformationalVersion"),
			RegexOptions.Compiled | RegexOptions.IgnoreCase);

		/// <summary>
		/// Initializes the <see cref="AssemblyInfoFileProcessor"/> class.
		/// </summary>
		static AssemblyInfoFileProcessor()
		{

		}

		/// <summary>
		/// Initializes a new instance of the <see cref="AssemblyInfoFileProcessor"/> class.
		/// </summary>
		public AssemblyInfoFileProcessor()
		{

		}

		/// <summary>
		/// Gets the name of the file processor.
		/// </summary>
		public string Name
		{
			get { return ProcessorName; }
		}

		/// <summary>
		/// Determines whether the file processor is applicable on the specified file.
		/// </summary>
		/// <param name="appCore">App core that runs the file processor.</param>
		/// <param name="path">Path of the file to check.</param>
		/// <returns>true, if the file processor is applicable; otherwise false.</returns>
		public bool IsApplicable(AppCore appCore, string path)
		{
			string fileName = Path.GetFileName(path);
			if (sFileNameRegex.IsMatch(fileName))
			{
				return true;
			}

			return false;
		}

		/// <summary>
		/// Processes the specified file.
		/// </summary>
		/// <param name="appCore">App core that runs the file processor.</param>
		/// <param name="path">Path of the file to process.</param>
		public void Process(AppCore appCore, string path)
		{
			System.Text.Encoding encoding;
			string content;
			bool modified = false;

			try
			{
				// replace occurrences of version strings
				using (FileStream fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read))
				using (StreamReader reader = new StreamReader(fs, true))
				{
					content = reader.ReadToEnd();
					encoding = reader.CurrentEncoding;
				}
			}
			catch (Exception ex)
			{
				throw new FileProcessingException(ex, "Loading file ({0}) failed.", path);
			}

			// replace occurences of version strings

			// AssemblyVersion
			if (appCore.AssemblyVersion != null)
			{
				Match match = sAssemblyVersionRegex.Match(content);
				if (match.Success)
				{
					sLog.Write(LogLevel.Note, "Patching [AssemblyVersion] attribute to '{0}'.", appCore.AssemblyVersion);
					content = sAssemblyVersionRegex.Replace(content, appCore.AssemblyVersion);
					modified = true;
				}
			}

			// AssemblyFileVersion
			if (appCore.FileVersion != null)
			{
				Match match = sAssemblyFileVersionRegex.Match(content);
				if (match.Success)
				{
					sLog.Write(LogLevel.Note, "Patching [AssemblyFileVersion] attribute to '{0}'.", appCore.FileVersion);
					content = sAssemblyFileVersionRegex.Replace(content, appCore.FileVersion);
					modified = true;
				}
			}

			// AssemblyInformationalVersion
			if (appCore.InformationalVersion != null)
			{
				Match match = sAssemblyInformationalVersionRegex.Match(content);
				if (match.Success)
				{
					sLog.Write(LogLevel.Note, "Patching [AssemblyInformationalVersion] attribute to '{0}'.", appCore.InformationalVersion);
					content = sAssemblyInformationalVersionRegex.Replace(content, appCore.InformationalVersion);
					modified = true;
				}
			}

			if (modified)
			{
				try
				{
					using (FileStream fs = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.None))
					using (StreamWriter writer = new StreamWriter(fs, encoding))
					{
						writer.Write(content);
					}
				}
				catch (Exception ex)
				{
					throw new FileProcessingException(ex, "Saving file ({0}) failed.", path);
				}
			}

		}

	}
}
