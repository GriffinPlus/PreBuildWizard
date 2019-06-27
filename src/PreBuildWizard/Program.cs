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

using CommandLine;
using GriffinPlus.Lib.Logging;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security;
using System.Security.Permissions;

namespace GriffinPlus.PreBuildWizard
{
	class Program
	{
		private static LogWriter sLog = Log.GetWriter<Program>();

		/// <summary>
		/// Command line argument mapping class
		/// (see https://github.com/commandlineparser/commandline for details).
		/// </summary>
		class Options
		{
			[Option('v', Default = false)]
			public bool Verbose { get; set; }

			[Option('b', "baseIntermediateOutputPath")]
			public string BaseIntermediateOutputPath { get; set; }

			[Value(0, Min = 1)]
			public IEnumerable<string> Paths { get; set; }
		}

		/// <summary>
		/// Exit codes returned by the the application.
		/// </summary>
		internal enum ExitCode
		{
			Success = 0,
			ArgumentError = 1,
			GeneralError = 2,
			FileNotFound = 3,
		}

		static int Main(string[] args)
		{
			// switch to second app domain to enable RazorEngine to clean up properly
			if (AppDomain.CurrentDomain.IsDefaultAppDomain())
			{
				// RazorEngine cannot clean up from the default appdomain...
				AppDomainSetup adSetup = new AppDomainSetup();
				adSetup.ApplicationBase = AppDomain.CurrentDomain.SetupInformation.ApplicationBase;
				var current = AppDomain.CurrentDomain;

				var domain = AppDomain.CreateDomain(
					"MyMainDomain", null,
					current.SetupInformation, new PermissionSet(PermissionState.Unrestricted),
					null);
				return domain.ExecuteAssembly(Assembly.GetExecutingAssembly().Location, args);
			}

			// configure the log
			Log.LogMessageProcessingPipeline = new ConsoleWriterPipelineStage()
				.WithTimestamp()
				// .WithLogWriterName()
				.WithLogLevel()
				.WithText();

			// configure command line parser
			CommandLine.Parser parser = new CommandLine.Parser(with =>
			{
				with.CaseInsensitiveEnumValues = true;
				with.CaseSensitive = false;
				with.EnableDashDash = true;
				with.IgnoreUnknownArguments = false;
				with.ParsingCulture = CultureInfo.InvariantCulture;
				with.HelpWriter = null;
			});

			// process command line
			var exitCode = parser.ParseArguments<Options>(args)
				.MapResult(
					options => (int)RunOptionsAndReturnExitCode(options),
					errors => (int)HandleParseError(errors));

			return exitCode;
		}

		#region Command Line Processing

		/// <summary>
		/// Is called, if specified command line arguments have successfully been validated.
		/// </summary>
		/// <param name="options">Command line options.</param>
		/// <returns>Exit code the application should return.</returns>
		static ExitCode RunOptionsAndReturnExitCode(Options options)
		{
			// configure the log, if more verbosity is required
			if (options.Verbose)
			{
				LogConfiguration configuration = new LogConfiguration();
				configuration.SetLogWriterSettings(
					new LogConfiguration.LogWriter(
						new LogConfiguration.WildcardLogWriterPattern("*"),
						"All"));
				Log.Configuration = configuration;
			}

			// initialize the application core
			AppCore processor = new AppCore();
			processor.Version = Environment.GetEnvironmentVariable("GitVersion_MajorMinorPatch");
			processor.AssemblyVersion = Environment.GetEnvironmentVariable("GitVersion_AssemblySemVer");
			processor.FileVersion = Environment.GetEnvironmentVariable("GitVersion_AssemblySemFileVer");
			processor.PackageVersion = Environment.GetEnvironmentVariable("GitVersion_NuGetVersionV2");
			processor.InformationalVersion = Environment.GetEnvironmentVariable("GitVersion_InformationalVersion");

			try
			{
				// scan for files to process
				foreach (string path in options.Paths)
				{
					if (Directory.Exists(path))
					{
						sLog.Write(LogLevel.Note, "The specified path ({0}) is a directory. Scanning for files to process...", path);

						try
						{
							processor.ScanDirectory(path);
						}
						catch (Exception ex)
						{
							sLog.Write(LogLevel.Error, "Scanning for files to process failed. Exception: {0}", ex);
							return ExitCode.GeneralError;
						}
					}
					else if (File.Exists(path))
					{
						processor.AddFile(path);
					}
					else
					{
						sLog.Write(LogLevel.Note, "The specified file or directory ({0}) does not exist.", path);
						return ExitCode.FileNotFound;
					}
				}

				// scan for NuGet project assets when option is set
				if (!string.IsNullOrEmpty(options.BaseIntermediateOutputPath))
				{
					if (Directory.Exists(options.BaseIntermediateOutputPath))
					{
						try
						{
							processor.ScanNugetProjectAssets(options.BaseIntermediateOutputPath);
						}
						catch (Exception ex)
						{
							sLog.Write(LogLevel.Error, "Scanning for NuGet project assets failed. Exception: {0}", ex);
							return ExitCode.GeneralError;
						}
					}
					else
					{
						sLog.Write(LogLevel.Note, "The specified directory ({0}) does not exist.", options.BaseIntermediateOutputPath);
						return ExitCode.ArgumentError;
					}
				}

				// process files
				try
				{
					processor.Process();
				}
				catch (Exception ex)
				{
					sLog.Write(LogLevel.Error, "Processing files failed. Exception: {0}", ex);
					return ExitCode.GeneralError;
				}

				// process NuGet consistency check
				try
				{
					processor.CheckNuGetConsistency();
				}
				catch (Exception ex)
				{
					sLog.Write(LogLevel.Error, "Processing NuGet consistency failed. Exception: {0}", ex);
					return ExitCode.GeneralError;
				}
			}
			catch (Exception ex)
			{
				sLog.Write(LogLevel.Error, "Unhandled Exception: {0}", ex);
				return ExitCode.GeneralError;
			}

			return ExitCode.Success;
		}

		/// <summary>
		/// Is called, if the specified command line arguments have failed validation.
		/// </summary>
		/// <param name="errors">Errors detected by the command line parser.</param>
		/// <returns>Exit code the application should return.</returns>
		static ExitCode HandleParseError(IEnumerable<Error> errors)
		{
			if (errors.Any(x => x.Tag == ErrorType.HelpRequestedError))
			{
				PrintUsage(null, Console.Out);
				return ExitCode.Success;
			}
			else if (errors.Any(x => x.Tag == ErrorType.VersionRequestedError))
			{
				PrintVersion(Console.Out);
				return ExitCode.Success;
			}

			PrintUsage(errors, Console.Error);
			return ExitCode.ArgumentError;
		}

		#endregion

		#region Usage Information / Error Reporting

		/// <summary>
		/// Writes usage text (with an optional error section).
		/// </summary>
		/// <param name="errors">Command line parsing errors (null, if no error occurred).</param>
		/// <param name="writer">Text writer to use.</param>
		static void PrintUsage(IEnumerable<Error> errors, TextWriter writer)
		{
			Version version = Assembly.GetExecutingAssembly().GetName().Version;
			writer.WriteLine(string.Format("  PreBuildWizard v{0}", version));
			writer.WriteLine("--------------------------------------------------------------------------------");

			if (errors != null && errors.Any())
			{
				writer.WriteLine();
				writer.WriteLine("  ERRORS:");
				writer.WriteLine();

				foreach (Error error in errors)
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
			writer.WriteLine("    PreBuildWizard.exe [-v] [-b|--baseIntermediateOutputPath <bpath>] <path>");
			writer.WriteLine();
			writer.WriteLine("    [-v]");
			writer.WriteLine("      Sets output to verbose.");
			writer.WriteLine();
			writer.WriteLine("    [-b|--baseIntermediateOutputPath <bpath>]");
			writer.WriteLine("      BaseIntermediateOutputPath of msbuild to check for consistency of NuGet packages.");
			writer.WriteLine();
			writer.WriteLine("    <path>");
			writer.WriteLine("      One or more paths were files to patch can be found.");
			writer.WriteLine();
			writer.WriteLine("--------------------------------------------------------------------------------");
		}

		/// <summary>
		/// Writes version information.
		/// </summary>
		/// <param name="writer">Text writer to use.</param>
		static void PrintVersion(TextWriter writer)
		{
			Version version = Assembly.GetExecutingAssembly().GetName().Version;
			writer.WriteLine("PreBuildWizard v{0}", version);
		}

		#endregion

	}
}
