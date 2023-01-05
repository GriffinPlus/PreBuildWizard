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
using System.Threading.Tasks;

using GriffinPlus.Lib.Logging;

using Newtonsoft.Json.Linq;

namespace GriffinPlus.PreBuildWizard
{

	/// <summary>
	/// The application's core.
	/// </summary>
	public class AppCore
	{
		private static readonly LogWriter                  sLog                  = LogWriter.Get<AppCore>();
		private readonly        List<string>               mFilesToProcess       = new();
		private readonly        Dictionary<string, string> mProjectAssetsToCheck = new();

		private const string cBuildFolderName   = "_build";
		private const string cObjectFolderName  = ".obj";
		private const string cProjectAssetsName = "project.assets.json";

		/// <summary>
		/// Gets or sets the version to patch into the &lt;Version&gt; element of .NET projects
		/// (base version property, no pre-release tag).
		/// </summary>
		public string Version { get; set; }

		/// <summary>
		/// Gets or sets the assembly version to patch into .NET projects.
		/// </summary>
		public string AssemblyVersion { get; set; }

		/// <summary>
		/// Gets or sets the assembly file version to patch into .NET projects.
		/// </summary>
		public string FileVersion { get; set; }

		/// <summary>
		/// Gets or sets the package version to patch into *.nuspec files.
		/// </summary>
		public string PackageVersion { get; set; }

		/// <summary>
		/// Gets or sets the informational version to patch into .NET projects.
		/// </summary>
		public string InformationalVersion { get; set; }

		/// <summary>
		/// Adds a single file to process.
		/// </summary>
		/// <param name="path">Path of the file to process.</param>
		public void AddFile(string path)
		{
			string fullFilePath = Path.GetFullPath(path);
			sLog.Write(LogLevel.Trace, "Checking {0}...", fullFilePath);

			IFileProcessor[] processors = FileProcessorList.GetApplicableProcessors(this, fullFilePath).ToArray();
			if (processors.Length == 0)
			{
				throw new FileProcessingException("No applicable processor found for file {0}.", fullFilePath);
			}

			foreach (IFileProcessor processor in processors)
			{
				sLog.Write(LogLevel.Notice, "Found applicable processor '{0}' for {1}.", processor.Name, fullFilePath);
			}

			mFilesToProcess.Add(fullFilePath);
		}

		/// <summary>
		/// Scans the specified directory recursively for files to process and files to check for consistency
		/// </summary>
		/// <param name="directory">Path of the directory to scan.</param>
		public void ScanDirectory(string directory)
		{
			foreach (string filePath in Directory.GetFiles(directory, "*.*", SearchOption.AllDirectories))
			{
				string fullFilePath = Path.GetFullPath(filePath);
				sLog.Write(LogLevel.Trace, "Checking {0}...", fullFilePath);
				foreach (IFileProcessor processor in FileProcessorList.GetApplicableProcessors(this, fullFilePath))
				{
					sLog.Write(LogLevel.Notice, "Found applicable processor '{0}' for {1}.", processor.Name, fullFilePath);
					mFilesToProcess.Add(fullFilePath);
				}
			}
		}

		/// <summary>
		/// Processes the files determined by scans.
		/// </summary>
		public async Task ProcessAsync()
		{
			sLog.Write(LogLevel.Notice, "Processing started...");

			foreach (string path in mFilesToProcess)
			{
				foreach (IFileProcessor processor in FileProcessorList.GetApplicableProcessors(this, path))
				{
					sLog.Write(LogLevel.Notice, "Processing {0} using processor '{1}'...", path, processor.Name);
					await processor.ProcessAsync(this, path).ConfigureAwait(false);
					sLog.Write(LogLevel.Notice, "Processing {0} completed.", path);
				}
			}

			sLog.Write(LogLevel.Notice, "Processing completed.");
		}

		#region Checking NuGet package consistency

		/// <summary>
		/// Scans the specified path for 'project.assets.json' files under the temporary build object folder.
		/// The project.assets.json files are created by the restoration of NuGet packages.
		/// </summary>
		/// <param name="directory">Path of the directory to scan.</param>
		public void ScanNugetProjectAssets(string directory)
		{
			foreach (string projectPath in Directory.GetDirectories(directory))
			{
				string projectId = Path.GetFileName(projectPath);
				string projectAssetsPath = Path.Combine(projectPath, cProjectAssetsName);
				if (File.Exists(projectAssetsPath))
					mProjectAssetsToCheck.Add(projectId, projectAssetsPath);
			}
		}

		/// <summary>
		/// Helping method for checking consistency of NuGet packages. This method scans all detected 'project.assets.json'
		/// files of the solution and detect inconsistencies when two projects reference the same NuGet package
		/// in different versions.
		/// </summary>
		public void CheckNuGetConsistency()
		{
			var targetFrameworks = new Dictionary<string, Dictionary<string, string>>();

			foreach (string projectId in mProjectAssetsToCheck.Keys)
			{
				string projectAssetsPath = mProjectAssetsToCheck[projectId];
				sLog.Write(LogLevel.Notice, $"Checking consistency of '{projectId}'...");

				using (var reader = new StreamReader(projectAssetsPath))
				{
					string json = reader.ReadToEnd();
					JObject jObj = JObject.Parse(json);
					foreach (JProperty property in jObj.Properties())
					{
						// get node 'libraries'
						if (property.Path == "targets")
						{
							// contains all referenced targets (e.g. netstandard v2.0, net461, etc.)
							foreach (JToken target in property.Value)
							{
								// expected string of target is '$target$,Version=$version$/$platform$', but platform is optional
								string[] targetFrameworkVersion = ((JProperty)target).Name.Split('/');
								// skip if target contains platform information, assuming the packages are identical for each platform and it exists a string without platform information
								if (targetFrameworkVersion.Length > 1)
								{
									sLog.Write(LogLevel.Notice, "Skipping target '{0}'", ((JProperty)target).Name);
									continue;
								}
								if (!targetFrameworks.ContainsKey(targetFrameworkVersion[0]))
									targetFrameworks.Add(targetFrameworkVersion[0], new());
								// get dictionary for specified target framework
								Dictionary<string, string> nuGetPackagesWithVersion = targetFrameworks[targetFrameworkVersion[0]];

								// contains information about referenced NuGet packages within this framework for the project
								foreach (JProperty library in ((JObject)jObj["targets"][targetFrameworkVersion[0]]).Properties())
								{
									// ignore references of type project
									if ((string)jObj["targets"][targetFrameworkVersion[0]][library.Name]["type"] != "project")
									{
										string[] packageVersion = library.Name.Split('/');
										if (packageVersion.Length != 2)
										{
											throw new FormatException(
												$"Expected package information from file '{projectAssetsPath}'" +
												$" in the format 'package/version', but not as '{library.Name}'");
										}
										(string package, string version) = (packageVersion[0], packageVersion[1]);
										if (nuGetPackagesWithVersion.ContainsKey(package))
										{
											string previousVersion = nuGetPackagesWithVersion[package];
											if (previousVersion != version)
											{
												throw new FileProcessingException(
													$"Inconsistency with package '{package}' in '{targetFrameworkVersion[0]}' detected. " +
													$"The package is referenced before in version '{previousVersion}' and now in '{version}'.");
											}
										}
										else
											nuGetPackagesWithVersion.Add(package, version);
										sLog.Write(LogLevel.Notice, $"{targetFrameworkVersion[0],-30} : {package,-50} : {version,10}");
									}
								}
							}
						}
					}
				}
				sLog.Write(LogLevel.Notice, $"'{projectId}' is consistent...");
				sLog.Write(LogLevel.Notice, "");
			}
		}

		#endregion
	}

}
