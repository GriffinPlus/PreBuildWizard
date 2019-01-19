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
using RazorEngine;
using RazorEngine.Configuration;
using RazorEngine.Templating;
using RazorEngine.Text;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;

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
		private static readonly IRazorEngineService sRazorEngineText;

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
		/// Initializes the <see cref="TemplatedFileProcessor"/> class.
		/// </summary>
		static TemplatedFileProcessor()
		{
			var textRazorConfig = new TemplateServiceConfiguration();
			textRazorConfig.Language = Language.CSharp;
			textRazorConfig.EncodedStringFactory = new RawStringFactory();
			textRazorConfig.Debug = DebugRazor;
			sRazorEngineText = RazorEngineService.Create(textRazorConfig);
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
		public void Process(AppCore appCore, string path)
		{
			string templateKey = path = Path.GetFullPath(path);
			IRazorEngineService razor = sRazorEngineText;
			System.Text.Encoding encoding;
			string renderedText;

			try
			{
				// compile template
				using (FileStream fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read))
				using (StreamReader reader = new StreamReader(fs, true))
				{
					string template = reader.ReadToEnd();
					encoding = reader.CurrentEncoding;
					if (!razor.IsTemplateCached(templateKey, null))
					{
						ITemplateSource templateSource = new LoadedTemplateSource(template, null);
						razor.AddTemplate(templateKey, templateSource);
						using (TimingLogger.Measure(sLog, string.Format("Compiling {0}", path)))
						{
							razor.Compile(templateSource, templateKey, typeof(RenderingContext));
						}
					}
				}
			}
			catch (Exception ex)
			{
				throw new FileProcessingException(ex, "Compiling template ({0}) failed.", path);
			}

			// prepare rendering context
			RenderingContext context = new RenderingContext();
			foreach (DictionaryEntry kvp in Environment.GetEnvironmentVariables())
			{
				context.Env[(string)kvp.Key] = kvp.Value.ToString();
			}

			try
			{
				// render template
				using (TimingLogger.Measure(sLog, string.Format("Rendering {0}", path)))
				{
					renderedText = razor.Run(templateKey, typeof(RenderingContext), context);
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
					writer.Write(renderedText);
				}
			}
			catch (Exception ex)
			{
				throw new FileProcessingException(ex, "Writing rendered template ({0}) failed.", path);
			}
		}
	}
}
