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
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;

using CommandLine;

using GriffinPlus.Lib;
using GriffinPlus.Lib.Logging;

namespace GriffinPlus.PreBuildWizard
{

	class Program
	{
		private static readonly LogWriter sLog = LogWriter.Get<Program>();

		/// <summary>
		/// Command line argument mapping class
		/// (see https://github.com/commandlineparser/commandline for details).
		/// </summary>
		private class Options
		{
			[Option('v', Default = false)]
			public bool Verbose { get; set; }

			[Option('b', "baseIntermediateOutputPath")]
			public string BaseIntermediateOutputPath { get; set; }

			[Option("skipNugetConsistencyCheckPattern")]
			public IEnumerable<string> SkipNugetConsistencyCheckPatterns { get; set; }

			[Value(0, Min = 1)]
			public IEnumerable<string> Paths { get; set; }
		}

		/// <summary>
		/// Exit codes returned by the the application.
		/// </summary>
		internal enum ExitCode
		{
			Success       = 0,
			ArgumentError = 1,
			GeneralError  = 2,
			FileNotFound  = 3
		}

		private static int Main(string[] args)
		{
			// configure command line parser
			var parser = new Parser(
				with =>
				{
					with.CaseInsensitiveEnumValues = true;
					with.CaseSensitive = false;
					with.EnableDashDash = true;
					with.IgnoreUnknownArguments = false;
					with.ParsingCulture = CultureInfo.InvariantCulture;
					with.HelpWriter = null;
				});

			// process command line
			int exitCode = parser.ParseArguments<Options>(args)
				.MapResult(
					options => (int)RunOptionsAndReturnExitCode(options),
					errors => (int)HandleParseError(errors));

			// shut down the logging subsystem
			Log.Shutdown();

			return exitCode;
		}

		#region Command Line Processing

		/// <summary>
		/// Is called, if specified command line arguments have successfully been validated.
		/// </summary>
		/// <param name="options">Command line options.</param>
		/// <returns>Exit code the application should return.</returns>
		private static ExitCode RunOptionsAndReturnExitCode(Options options)
		{
			// configure the log
			Log.Initialize<VolatileLogConfiguration>(
				builder =>
				{
					builder.AddLogWriterDefault(x => x.WithBaseLevel(options.Verbose ? LogLevel.All : LogLevel.Notice));
				},
				builder =>
				{
					builder.Add<ConsoleWriterPipelineStage>(
						"Console",
						stage =>
						{
							var formatter = new TableMessageFormatter();
							formatter.AddTimestampColumn();
							formatter.AddLogLevelColumn();
							formatter.AddTextColumn();

							stage.Formatter = formatter;
						});
				});

			// initialize the application core
			var processor = new AppCore
			{
				Version = Environment.GetEnvironmentVariable("GitVersion_MajorMinorPatch"),
				AssemblyVersion = Environment.GetEnvironmentVariable("GitVersion_AssemblySemVer"),
				FileVersion = Environment.GetEnvironmentVariable("GitVersion_AssemblySemFileVer"),
				PackageVersion = Environment.GetEnvironmentVariable("GitVersion_NuGetVersionV2"),
				InformationalVersion = Environment.GetEnvironmentVariable("GitVersion_InformationalVersion")
			};

			try
			{
				// scan for files to process
				foreach (string path in options.Paths)
				{
					if (Directory.Exists(path))
					{
						sLog.Write(LogLevel.Notice, "The specified path ({0}) is a directory. Scanning for files to process...", path);

						try
						{
							processor.ScanDirectory(path);
						}
						catch (Exception ex)
						{
							sLog.Write(LogLevel.Error, "Scanning for files to process failed. Exception: {0}", ex.ToString());
							return ExitCode.GeneralError;
						}
					}
					else if (File.Exists(path))
					{
						processor.AddFile(path);
					}
					else
					{
						sLog.Write(LogLevel.Notice, "The specified file or directory ({0}) does not exist.", path);
						return ExitCode.FileNotFound;
					}
				}

				// convert patterns to match names of projects to skip in the nuget package consistency scan
				var skipProjectNameRegexes = new List<Regex>();
				if (options.SkipNugetConsistencyCheckPatterns != null)
				{
					foreach (string pattern in options.SkipNugetConsistencyCheckPatterns)
					{
						try
						{
							skipProjectNameRegexes.Add(RegexHelpers.FromWildcardExpression(pattern, RegexOptions.Singleline));
						}
						catch (Exception ex)
						{
							sLog.Write(LogLevel.Notice, "The specified pattern ({0}) is malformed. Expecting a wildcard pattern.", pattern);
							return ExitCode.ArgumentError;
						}
					}
				}

				// scan for NuGet project assets when option is set
				if (!string.IsNullOrEmpty(options.BaseIntermediateOutputPath))
				{
					if (Directory.Exists(options.BaseIntermediateOutputPath))
					{
						try
						{
							processor.ScanNugetProjectAssets(options.BaseIntermediateOutputPath, skipProjectNameRegexes);
						}
						catch (Exception ex)
						{
							sLog.Write(LogLevel.Error, "Scanning for NuGet project assets failed. Exception: {0}", ex.ToString());
							return ExitCode.GeneralError;
						}
					}
					else
					{
						sLog.Write(LogLevel.Notice, "The specified directory ({0}) does not exist.", options.BaseIntermediateOutputPath);
						return ExitCode.ArgumentError;
					}
				}

				// process files
				try
				{
					processor.ProcessAsync().Wait();
				}
				catch (Exception ex)
				{
					sLog.Write(LogLevel.Error, "Processing files failed. Exception: {0}", ex.ToString());
					return ExitCode.GeneralError;
				}

				// process NuGet consistency check
				try
				{
					processor.CheckNuGetConsistency();
				}
				catch (Exception ex)
				{
					sLog.Write(LogLevel.Error, "Processing NuGet consistency failed. Exception: {0}", ex.ToString());
					return ExitCode.GeneralError;
				}
			}
			catch (Exception ex)
			{
				sLog.Write(LogLevel.Error, "Unhandled Exception: {0}", ex.ToString());
				return ExitCode.GeneralError;
			}

			return ExitCode.Success;
		}

		/// <summary>
		/// Is called, if the specified command line arguments have failed validation.
		/// </summary>
		/// <param name="errors">Errors detected by the command line parser.</param>
		/// <returns>Exit code the application should return.</returns>
		private static ExitCode HandleParseError(IEnumerable<Error> errors)
		{
			IEnumerable<Error> errorArray = errors as Error[] ?? errors.ToArray();

			if (errorArray.Any(x => x.Tag == ErrorType.HelpRequestedError))
			{
				PrintUsage(null, Console.Out);
				return ExitCode.Success;
			}
			if (errorArray.Any(x => x.Tag == ErrorType.VersionRequestedError))
			{
				PrintVersion(Console.Out);
				return ExitCode.Success;
			}

			PrintUsage(errorArray, Console.Error);
			return ExitCode.ArgumentError;
		}

		#endregion

		#region Usage Information / Error Reporting

		/// <summary>
		/// Writes usage text (with an optional error section).
		/// </summary>
		/// <param name="errors">Command line parsing errors (null, if no error occurred).</param>
		/// <param name="writer">Text writer to use.</param>
		private static void PrintUsage(IEnumerable<Error> errors, TextWriter writer)
		{
			Version version = Assembly.GetExecutingAssembly().GetName().Version;
			writer.WriteLine("  PreBuildWizard v{0}", version);
			writer.WriteLine("--------------------------------------------------------------------------------");

			IEnumerable<Error> errorArray = errors as Error[] ?? errors?.ToArray();
			if (errors != null && errorArray.Any())
			{
				writer.WriteLine();
				writer.WriteLine("  ERRORS:");
				writer.WriteLine();

				foreach (Error error in errorArray)
				{
					switch (error.Tag)
					{
						case ErrorType.UnknownOptionError:
						{
							var err = (UnknownOptionError)error;
							writer.WriteLine("    - Unknown option: {0}.", err.Token);
							break;
						}

						case ErrorType.RepeatedOptionError:
						{
							var err = (RepeatedOptionError)error;
							writer.WriteLine("    - Repeated option: -{0}, --{1}.", err.NameInfo.ShortName, err.NameInfo.LongName);
							break;
						}

						default:
						{
							writer.WriteLine("    - Unspecified command line error");
							break;
						}
					}
				}

				writer.WriteLine();
				writer.WriteLine("--------------------------------------------------------------------------------");
			}

			writer.WriteLine();
			writer.WriteLine("  USAGE:");
			writer.WriteLine();
			writer.WriteLine("    PreBuildWizard.exe [-v] [-b|--baseIntermediateOutputPath <path>] [--skipNugetConsistencyCheckPattern <pattern>] <path>");
			writer.WriteLine();
			writer.WriteLine("    [-v]");
			writer.WriteLine("      Sets output to verbose.");
			writer.WriteLine();
			writer.WriteLine("    [-b|--baseIntermediateOutputPath <path>]");
			writer.WriteLine("      BaseIntermediateOutputPath of msbuild to check for consistency of NuGet packages.");
			writer.WriteLine();
			writer.WriteLine("    [--skipNugetConsistencyCheckPattern <pattern>]");
			writer.WriteLine("      Wildcard pattern of projects to skip when checking the consistency of NuGet packages.");
			writer.WriteLine();
			writer.WriteLine("    <path>");
			writer.WriteLine("      One or more paths where files to patch can be found.");
			writer.WriteLine();
			writer.WriteLine("--------------------------------------------------------------------------------");
		}

		/// <summary>
		/// Writes version information.
		/// </summary>
		/// <param name="writer">Text writer to use.</param>
		private static void PrintVersion(TextWriter writer)
		{
			Version version = Assembly.GetExecutingAssembly().GetName().Version;
			writer.WriteLine("PreBuildWizard v{0}", version);
		}

		#endregion
	}

}
