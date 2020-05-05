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
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace GriffinPlus.PreBuildWizard
{
	/// <summary>
	/// Processes .rc files and patches product version and file version information.
	/// </summary>
	public class CppResourceFileProcessor : IFileProcessor
	{
		private static LogWriter sLog = Log.GetWriter<CppResourceFileProcessor>();
		private const string ProcessorName = "C/C++ Resource";
		private static readonly Regex sFileNameRegex = new Regex(@"^.*\.rc$", RegexOptions.Compiled | RegexOptions.IgnoreCase);

		private static readonly Regex sContainsVersionInfoRegex = new Regex(
			@"^\s*VS_VERSION_INFO\s+VERSIONINFO.*$",
			RegexOptions.Compiled | RegexOptions.Multiline);

		private const string VersionInfo_ValueRegexFormat = @"(?<=^\s*{0}\s+)(\d,\d,\d,\d)(?=.*\r?$)"; // matches the version string only!

		private static readonly Regex sVersionInfo_FileVersionRegex = new Regex(
			string.Format(VersionInfo_ValueRegexFormat, "FILEVERSION"),
			RegexOptions.Compiled | RegexOptions.Multiline);

		private static readonly Regex sVersionInfo_ProductVersionRegex = new Regex(
			string.Format(VersionInfo_ValueRegexFormat, "PRODUCTVERSION"),
			RegexOptions.Compiled | RegexOptions.Multiline);

		private const string StringInfo_ValueRegexFormat = @"(?<=^\s*VALUE\s+""{0}\""\s*,\s*\"")(.*)(?="".*\r?$)"; // matches the version string only!

		private static readonly Regex sStringFileInfo_FileVersionRegex = new Regex(
			string.Format(StringInfo_ValueRegexFormat, "FileVersion"),
			RegexOptions.Compiled | RegexOptions.Multiline);

		private static readonly Regex sStringFileInfo_ProductVersionRegex = new Regex(
			string.Format(StringInfo_ValueRegexFormat, "ProductVersion"),
			RegexOptions.Compiled | RegexOptions.Multiline);

		private static readonly Regex sStringInfo_CommentsRegex = new Regex(
			string.Format(StringInfo_ValueRegexFormat, "Comments"),
			RegexOptions.Compiled | RegexOptions.Multiline);

		/// <summary>
		/// Initializes the <see cref="CppResourceFileProcessor"/> class.
		/// </summary>
		static CppResourceFileProcessor()
		{

		}

		/// <summary>
		/// Initializes a new instance of the <see cref="CppResourceFileProcessor"/> class.
		/// </summary>
		public CppResourceFileProcessor()
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

			// check whether the filename extension is ok
			if (!sFileNameRegex.IsMatch(fileName)) {
				return false;
			}

			// check whether the resource file contains a VERSIONINFO block to skip resource files
			// that contain other stuff...
			using (FileStream fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read))
			using (StreamReader reader = new StreamReader(fs, true))
			{
				string content = reader.ReadToEnd();
				if (!sContainsVersionInfoRegex.IsMatch(content)) {
					return false;
				}
			}

			return true;
		}

		/// <summary>
		/// Processes the specified file.
		/// </summary>
		/// <param name="appCore">App core that runs the file processor.</param>
		/// <param name="path">Path of the file to process.</param>
		public async Task ProcessAsync(AppCore appCore, string path)
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
					content = await reader.ReadToEndAsync().ConfigureAwait(false);
					encoding = reader.CurrentEncoding;
				}
			}
			catch (Exception ex)
			{
				throw new FileProcessingException(ex, "Loading file ({0}) failed.", path);
			}

			// patching:
			// VERSIONINFO => FILEVERSION
			// VERSIONINFO => StringFileInfo => FileVersion
			if (appCore.FileVersion != null)
			{
				// VERSIONINFO: FILEVERSION
				if (sVersionInfo_FileVersionRegex.IsMatch(content))
				{
					string version = string.Join(",", appCore.FileVersion.Split('.').Select(x => x.Trim()));
					sLog.Write(LogLevel.Note, "Patching VERSIONINFO field 'FILEVERSION' to '{0}'.", version);
					content = sVersionInfo_FileVersionRegex.Replace(content, version);
					modified = true;
				}
				else
				{
					sLog.Write(LogLevel.Error, "The file does not contain the VERSIONINFO field 'FILEVERSION'.");
				}

				// StringFileInfo: FileVersion
				if (sStringFileInfo_FileVersionRegex.IsMatch(content))
				{
					sLog.Write(LogLevel.Note, "Patching StringFileInfo field 'FileVersion' to '{0}'.", appCore.FileVersion);
					content = sStringFileInfo_FileVersionRegex.Replace(content, appCore.FileVersion);
					modified = true;
				}
				else
				{
					sLog.Write(LogLevel.Error, "The file does not contain the StringFileInfo field 'FileVersion'.");
				}
			}

			// patching:
			// VERSIONINFO => PRODUCTVERSION
			if (appCore.AssemblyVersion != null)
			{
				// VERSIONINFO: PRODUCTVERSION
				if (sVersionInfo_ProductVersionRegex.IsMatch(content))
				{
					// split up version number
					string version = string.Join(",", appCore.AssemblyVersion.Split('.').Select(x => x.Trim()));
					sLog.Write(LogLevel.Note, "Patching VERSIONINFO field 'PRODUCTVERSION' to '{0}'.", version);
					content = sVersionInfo_ProductVersionRegex.Replace(content, version);
					modified = true;
				}
				else
				{
					sLog.Write(LogLevel.Error, "The file does not contain the VERSIONINFO field 'PRODUCTVERSION'.");
				}
			}

			// patching:
			// VERSIONINFO => StringFileInfo => ProductVersion
			if (appCore.InformationalVersion != null)
			{
				// StringFileInfo: ProductVersion
				if (sStringFileInfo_ProductVersionRegex.IsMatch(content))
				{
					sLog.Write(LogLevel.Note, "Patching StringFileInfo field 'ProductVersion' to '{0}'.", appCore.InformationalVersion);
					content = sStringFileInfo_ProductVersionRegex.Replace(content, appCore.InformationalVersion);
					modified = true;
				}
				else
				{
					sLog.Write(LogLevel.Error, "The file does not contain the StringFileInfo field 'ProductVersion'.");
				}
			}

			if (modified)
			{
				if (encoding != System.Text.Encoding.ASCII)
				{
					sLog.Write(
						LogLevel.Warning,
						"The file seems to be using '{0}' encoding, but resource files need to be ASCII encoded. Enforcing ASCII encoding...",
						encoding.EncodingName);
				}

				try
				{
					using (FileStream fs = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.None))
					using (StreamWriter writer = new StreamWriter(fs, System.Text.Encoding.ASCII))
					{
						await writer.WriteAsync(content).ConfigureAwait(false);
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
