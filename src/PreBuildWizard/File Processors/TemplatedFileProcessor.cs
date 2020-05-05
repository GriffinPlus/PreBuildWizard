///////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// This file is part of the Griffin+ PreBuildWizard (https://github.com/griffinplus/PreBuildWizard).
//
// Copyright 2019-2020 Sascha Falk <sascha@falk-online.eu>
// Copyright 2019      Sebastian Piel <sebastianpiel@outlook.de>
//
// Licensed under the Apache License, Version 2.0 (the "License"); you may not use this file except in compliance
// with the License. You may obtain a copy of the License at http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software distributed under the License is distributed
// on an "AS IS" BASIS, WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied. See the License for
// the specific language governing permissions and limitations under the License.
///////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

using GriffinPlus.Lib.Logging;
using RazorLight;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace GriffinPlus.PreBuildWizard
{
	/// <summary>
	/// Processes '.pbwtempl' files by interpreting them as Razor templates.
	/// A file 'MyFile.ext.pbwtempl' is rendered and the result is written to 'MyFile.ext'.
	/// The template can use the following data in the model:
	/// - Environment Variables: @Model.Env['MyVar']
	/// - ... more to come ...
	/// </summary>
	public class TemplatedFileProcessor : IFileProcessor
	{
		private static LogWriter sLog = Log.GetWriter<TemplatedFileProcessor>();
		private const string ProcessorName = "PreBuildWizard Template (Razor)";
		private const bool DebugRazor = false;
		private static readonly Regex sFileNameRegex = new Regex(@"^.*\.pbwtempl$", RegexOptions.Compiled | RegexOptions.IgnoreCase);

		/// <summary>
		/// Rendering context used by the Razor template engine.
		/// </summary>
		public class RenderingContext
		{
			/// <summary>
			/// Environment variables that can be used from within a template.
			/// </summary>
			public Dictionary<string,string> Env { get; } = new Dictionary<string, string>();
		}

		/// <summary>
		/// Initializes a new instance of the <see cref="TemplatedFileProcessor"/> class.
		/// </summary>
		public TemplatedFileProcessor()
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
			return sFileNameRegex.IsMatch(fileName);
		}

		/// <summary>
		/// Processes the specified file.
		/// </summary>
		/// <param name="appCore">App core that runs the file processor.</param>
		/// <param name="path">Path of the file to process.</param>
		public async Task ProcessAsync(AppCore appCore, string path)
		{
			string templateKey = path = Path.GetFullPath(path);
			System.Text.Encoding encoding;

			RazorLightEngine engine = new RazorLightEngineBuilder()
				.UseEmbeddedResourcesProject(typeof(TemplatedFileProcessor))
				.UseMemoryCachingProvider()
				.DisableEncoding()
				.Build();

			// prepare rendering context
			RenderingContext context = new RenderingContext();
			foreach (DictionaryEntry kvp in Environment.GetEnvironmentVariables())
			{
				context.Env[(string)kvp.Key] = kvp.Value.ToString();
			}

			// compile and render the template
			string renderedTemplate;
			try
			{
				using (FileStream fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read))
				using (StreamReader reader = new StreamReader(fs, true))
				{
					string template = await reader.ReadToEndAsync().ConfigureAwait(false);
					encoding = reader.CurrentEncoding;
					renderedTemplate = await engine
						.CompileRenderStringAsync(templateKey, template, context)
						.ConfigureAwait(false);
				}
			}
			catch (Exception ex)
			{
				throw new FileProcessingException(ex, "Rendering template ({0}) failed.", path);
			}

			// write rendered file
			try
			{
				string renderedFilePath = Path.Combine(Path.GetDirectoryName(path), Path.GetFileNameWithoutExtension(path));
				using (FileStream fs = new FileStream(renderedFilePath, FileMode.Create, FileAccess.Write, FileShare.None))
				using (StreamWriter writer = new StreamWriter(fs, encoding))
				{
					await writer.WriteAsync(renderedTemplate).ConfigureAwait(false);
				}
			}
			catch (Exception ex)
			{
				throw new FileProcessingException(ex, "Writing rendered template ({0}) failed.", path);
			}
		}
	}
}
