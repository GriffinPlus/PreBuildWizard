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
using System.Xml;

namespace GriffinPlus.PreBuildWizard
{
	/// <summary>
	/// Processes .csproj files and patches version information into .NET Core and .NET Standard projects.
	/// </summary>
	public class CsprojFileProcessor : IFileProcessor
	{
		private static LogWriter sLog = Log.GetWriter<CsprojFileProcessor>();
		private const string ProcessorName = "New C# Project";
		private static readonly Regex sFileNameRegex = new Regex(@"^.*\.csproj$", RegexOptions.Compiled | RegexOptions.IgnoreCase);

		/// <summary>
		/// Initializes the <see cref="CsprojFileProcessor"/> class.
		/// </summary>
		static CsprojFileProcessor()
		{

		}

		/// <summary>
		/// Initializes a new instance of the <see cref="CsprojFileProcessor"/> class.
		/// </summary>
		public CsprojFileProcessor()
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
				XmlDocument doc = new XmlDocument();
				doc.Load(path);
				return IsNewCsProj(doc);
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
			XmlDocument doc = new XmlDocument();
			doc.PreserveWhitespace = true;
			doc.Load(path);
			XmlNode node;

			if (IsNewCsProj(doc))
			{
				// determine whether assembly info should automatically be generated out of the project file
				// (the <GenerateAssemblyInfo> element is optional and defaults to 'true')
				bool generateAssemblyInfo = true;
				node = doc.DocumentElement.SelectSingleNode("/Project/PropertyGroup/GenerateAssemblyInfo");
				if (node != null)
				{
					if (node.InnerText == "true")
					{
						generateAssemblyInfo = true;
					}
					else if (node.InnerText == "false")
					{
						generateAssemblyInfo = false;
					}
					else
					{
						sLog.Write(
							LogLevel.Error,
							"Project file contains <GenerateAssemblyInfo>, but the value is invalid ({0}).",
							node.InnerText);

						return;
					}
				}

				if (generateAssemblyInfo)
				{
					sLog.Write(LogLevel.Note, "Project is configured to generate assembly information automatically.");

					if (appCore.Version != null)
					{
						node = doc.DocumentElement.SelectSingleNode("/Project/PropertyGroup/Version");
						if (node != null)
						{
							sLog.Write(LogLevel.Note, "Patching <Version> element to '{0}'.", appCore.Version);
							node.InnerText = appCore.Version;
						}
						else
						{
							sLog.Write(LogLevel.Warning, "Project file does not contain the <Version> to patch.");
						}
					}

					if (appCore.AssemblyVersion != null)
					{
						node = doc.DocumentElement.SelectSingleNode("/Project/PropertyGroup/AssemblyVersion");
						if (node != null)
						{
							sLog.Write(LogLevel.Note, "Patching <AssemblyVersion> element to '{0}'.", appCore.AssemblyVersion);
							node.InnerText = appCore.AssemblyVersion;
						}
						else
						{
							sLog.Write(LogLevel.Warning, "Project file does not contain the <AssemblyVersion> to patch.");
						}
					}

					if (appCore.FileVersion != null)
					{
						node = doc.DocumentElement.SelectSingleNode("/Project/PropertyGroup/FileVersion");
						if (node != null)
						{
							sLog.Write(LogLevel.Note, "Patching <FileVersion> element to '{0}'.", appCore.FileVersion);
							node.InnerText = appCore.FileVersion;
						}
						else
						{
							sLog.Write(LogLevel.Warning, "Project file does not contain the <FileVersion> to patch.");
						}
					}

					if (appCore.PackageVersion != null)
					{
						node = doc.DocumentElement.SelectSingleNode("/Project/PropertyGroup/PackageVersion");
						if (node != null)
						{
							sLog.Write(LogLevel.Note, "Patching <PackageVersion> element to '{0}'.", appCore.PackageVersion);
							node.InnerText = appCore.PackageVersion;
						}
						else
						{
							// <PackageVersion> not present, ok for libraries that are not shipped as Nuget packages
							sLog.Write(LogLevel.Note, "Project file does not contain the <PackageVersion> to patch.");
						}
					}

					if (appCore.InformationalVersion != null)
					{
						node = doc.DocumentElement.SelectSingleNode("/Project/PropertyGroup/InformationalVersion");
						if (node != null)
						{
							sLog.Write(LogLevel.Note, "Patching <InformationalVersion> element to '{0}'.", appCore.InformationalVersion);
							node.InnerText = appCore.InformationalVersion;
						}
						else
						{
							sLog.Write(LogLevel.Warning, "Project file does not contain the <InformationalVersion> to patch.");
						}
					}

					doc.Save(path);
					// processing completed...
					return;
				}
			}

			// should never get here...
			throw new NotSupportedException("The file format is not supported.");
		}

		/// <summary>
		/// Checks whether the specified document is a new .csproj file.
		/// </summary>
		/// <param name="doc">The project file.</param>
		/// <returns>true, if the specified document is a new .csproj file; otherwise false.</returns>
		private static bool IsNewCsProj(XmlDocument doc)
		{
			XmlNode node = doc.DocumentElement.SelectSingleNode("/Project[@Sdk='Microsoft.NET.Sdk']");
			return node != null;
		}
	}
}
