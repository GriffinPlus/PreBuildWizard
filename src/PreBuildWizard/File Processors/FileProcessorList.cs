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

using System.Collections.Generic;

namespace GriffinPlus.PreBuildWizard
{

	static class FileProcessorList
	{
		/// <summary>
		/// All available file processors.
		/// </summary>
		private static readonly IFileProcessor[] sFileProcessors =
		{
			new TemplatedFileProcessor(),
			new CsprojFileProcessor(),
			new AssemblyInfoFileProcessor(),
			new CppResourceFileProcessor(),
			new WiXProductFileProcessor()
		};

		/// <summary>
		/// Gets the file processors that are applicable on the specified file.
		/// </summary>
		/// <param name="appCore"></param>
		/// <param name="path"></param>
		/// <returns></returns>
		public static IEnumerable<IFileProcessor> GetApplicableProcessors(AppCore appCore, string path)
		{
			foreach (IFileProcessor processor in sFileProcessors)
			{
				if (processor.IsApplicable(appCore, path))
					yield return processor;
			}
		}
	}

}
