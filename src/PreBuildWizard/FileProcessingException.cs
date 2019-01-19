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

namespace GriffinPlus.PreBuildWizard
{
	/// <summary>
	/// Exception that is thrown in case of file processing errors.
	/// </summary>
	public class FileProcessingException : Exception
	{
		/// <summary>
		/// Initializes a new instance of the <see cref="FileProcessingException"/> class.
		/// </summary>
		public FileProcessingException()
		{

		}

		/// <summary>
		/// Initializes a new instance of the <see cref="FileProcessingException"/> class.
		/// </summary>
		/// <param name="message">Message describing the reason why the exception is thrown.</param>
		public FileProcessingException(string message) : base(message)
		{

		}

		/// <summary>
		/// Initializes a new instance of the <see cref="FileProcessingException"/> class.
		/// </summary>
		/// <param name="format">String that is used to format the final message describing the reason why the exception is thrown.</param>
		/// <param name="args">Arguments used to format the final exception message.</param>
		public FileProcessingException(string format, params object[] args) :
			base(string.Format(format, args))
		{

		}

		/// <summary>
		/// Initializes a new instance of the <see cref="FileProcessingException"/> class.
		/// </summary>
		/// <param name="innerException">Exception that caused this exception to be thrown.</param>
		/// <param name="format">String that is used to format the final message describing the reason why the exception is thrown.</param>
		/// <param name="args">Arguments used to format the final exception message.</param>
		public FileProcessingException(Exception innerException, string format, params object[] args) :
			base(string.Format(format, args), innerException)
		{

		}

		/// <summary>
		/// Initializes a new instance of the <see cref="FileProcessingException"/> class.
		/// </summary>
		/// <param name="message">Message describing the reason why the exception is thrown.</param>
		/// <param name="ex">Some other exception that caused the exception to be thrown.</param>
		public FileProcessingException(string message, Exception ex) : base(message, ex)
		{

		}
	}
}
