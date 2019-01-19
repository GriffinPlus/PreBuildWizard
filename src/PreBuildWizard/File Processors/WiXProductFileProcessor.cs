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
using System.Text;
using System.Text.RegularExpressions;
using System.Xml;

namespace GriffinPlus.PreBuildWizard
{
	/// <summary>
	/// A class that provides functionality to patch version number and product code to WiX projects.
	/// </summary>
	public class WiXProductFileProcessor : IFileProcessor
	{
		private static LogWriter sLog = Log.GetWriter<WiXProductFileProcessor>();
		private const string ProcessorName = "New WiX File";
		private const string XmlWiXNamespace = "http://schemas.microsoft.com/wix/2006/wi";
		private static readonly Regex sFileNameRegex = new Regex(@"^.*\.wxs$", RegexOptions.Compiled | RegexOptions.IgnoreCase);
		private static readonly Regex sGuidRegex = new Regex(@"^[{]?[0-9A-F]{8}[-]?(?:[0-9A-F]{4}[-]?){3}[0-9A-F]{12}[}]?$", RegexOptions.Compiled | RegexOptions.IgnoreCase);
		
		/// <summary>
		/// Initializes the <see cref="WiXProductFileProcessor"/> class.
		/// </summary>
		static WiXProductFileProcessor()
		{

		}

		/// <summary>
		/// Initializes a new instance of the <see cref="WiXProductFileProcessor"/> class.
		/// </summary>
		public WiXProductFileProcessor()
		{

		}

		/// <summary>
		///  Gets the name of the file processor.
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
				var nsmgr = new XmlNamespaceManager(doc.NameTable);
				nsmgr.AddNamespace("wix", XmlWiXNamespace);
				XmlNode productNode = doc.DocumentElement.SelectSingleNode("//wix:Product", nsmgr);
				XmlNode bundleNode = doc.DocumentElement.SelectSingleNode("//wix:Bundle", nsmgr);
				return (productNode != null || bundleNode != null);
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
			bool isCompleted = false;
			XmlDocument doc = new XmlDocument
			{
				PreserveWhitespace = true
			};
			doc.Load(path);
			var nsmgr = new XmlNamespaceManager(doc.NameTable);
			nsmgr.AddNamespace("wix", XmlWiXNamespace);
			XmlNode productNode = doc.DocumentElement.SelectSingleNode("//wix:Product", nsmgr);
			XmlNode bundleNode = doc.DocumentElement.SelectSingleNode("//wix:Bundle", nsmgr);

			if (productNode != null)
			{
				if (appCore.AssemblyVersion != null)
				{
					// set product version to assembly version
					XmlNode productVersionNode = doc.SelectSingleNode("//wix:WixVariable[@Id='ProductVersion']", nsmgr);
					if (productVersionNode != null)
					{
						XmlElement element = (XmlElement)productVersionNode;
						sLog.Write(LogLevel.Note, "Patching WiXInstaller produt version variable to '{0}'", appCore.AssemblyVersion);
						element.SetAttribute("Value", appCore.AssemblyVersion);
					}
					else
					{
						sLog.Write(LogLevel.Warning, "Cannot patch assembly version to wxs file. Missing <WixVariable Id='ProductVersion'/> tag.");
					}

					// generate Guid for installer Id:
					// generate random byte sequence from a seed based on the assembly version
					// XOR with the upgradecode for specific products
					XmlNode productCodeNode = doc.SelectSingleNode("//wix:WixVariable[@Id='ProductCode']", nsmgr);
					XmlNode upgradeCodeNode = doc.SelectSingleNode("//wix:WixVariable[@Id='UpgradeCode']", nsmgr);
					if (productCodeNode != null && upgradeCodeNode != null)
					{
						XmlElement productCode = (XmlElement)productCodeNode;
						string upgradeCode = ((XmlElement)upgradeCodeNode).GetAttribute("Value");

						if (sGuidRegex.IsMatch(upgradeCode))
						{
							// convert hexadecimal representation of upgradecode to byte array
							upgradeCode = upgradeCode.Replace("{", "");
							upgradeCode = upgradeCode.Replace("}", "");
							upgradeCode = upgradeCode.Replace("-", "");
							byte[] upgradeCodeArray = Enumerable.Range(0, upgradeCode.Length)
																.Where(x => x % 2 == 0)
																.Select(x => Convert.ToByte(upgradeCode.Substring(x, 2), 16))
																.ToArray();
							// convert version number to integer where each number represents 8 bit
							string[] versionNumbers = appCore.AssemblyVersion.Split(new string[] { "." }, StringSplitOptions.RemoveEmptyEntries);
							byte[] bVersionNumbers = new byte[versionNumbers.Length];
							for (int i = 0; i < bVersionNumbers.Length; i++)
							{
								bVersionNumbers[i] = Byte.Parse(versionNumbers[i]);
							}
							// generate random byte sequence from version number
							Random rand = new Random(BitConverter.ToInt32(bVersionNumbers, 0));
							byte[] guid = new byte[upgradeCodeArray.Length];
							rand.NextBytes(guid);
							// xor with upgrade code to make a specific guid for each product
							for (int i = 0; i < guid.Length; i++)
							{
								guid[i] = (byte)(guid[i] ^ upgradeCodeArray[i]);
							}
							// set version and variant of Guid format version 4
							guid[6] = (byte)((guid[6] & 0x0f) + 0x40);
							guid[8] = (byte)((guid[8] & 0x3f) + 0x80);
							// convert result to hexadecimal string representation
							StringBuilder productCodeString = new StringBuilder(BitConverter.ToString(guid).Replace("-", ""));
							productCodeString.Append("}");
							productCodeString.Insert(20, "-");
							productCodeString.Insert(16, "-");
							productCodeString.Insert(12, "-");
							productCodeString.Insert(8, "-");
							productCodeString.Insert(0, "{");
							// write value to attribute
							sLog.Write(LogLevel.Note, "Patching WiXInstaller product code variable to '{0}'", productCodeString.ToString());
							productCode.SetAttribute("Value", productCodeString.ToString());
						}
						else
						{
							sLog.Write(LogLevel.Error, "Cannot patch product code to wxs file. The <WixVariable Id='UpgradeCode'/> has not the correct guid format {XXXXXXXX-XXXX-XXXX-XXXX-XXXXXXXXXXXX}.");
						}
					}
					else
					{
						sLog.Write(LogLevel.Warning, "Cannot patch product code to wxs file. Either <WixVariable Id='ProductCode'/> or <WixVariable Id='UpgradeCode'/> are missing.");
					}
				}
				else
				{
					sLog.Write(LogLevel.Warning, "Missing assembly version.");
				}

				doc.Save(path);
				// processing completed...
				isCompleted = true;
			}
			if (bundleNode != null)
			{
				if (appCore.AssemblyVersion != null)
				{
					// set product version to assembly version
					// set product version to assembly version
					XmlNode productVersionNode = doc.SelectSingleNode("//wix:WixVariable[@Id='ProductVersion']", nsmgr);
					if (productVersionNode != null)
					{
						XmlElement element = (XmlElement)productVersionNode;
						sLog.Write(LogLevel.Note, "Patching WiXInstaller produt version variable to '{0}'", appCore.AssemblyVersion);
						element.SetAttribute("Value", appCore.AssemblyVersion);
					}
					else
					{
						sLog.Write(LogLevel.Warning, "Cannot patch assembly version to wxs file. Missing <WixVariable Id='ProductVersion'/> tag.");
					}
				}
				else
				{
					sLog.Write(LogLevel.Warning, "Missing assembly version.");
				}

				doc.Save(path);
				// processing completed...
				isCompleted = true;
			}


			if (isCompleted)
			{
				return;
			}
			//should never get here...
			throw new NotSupportedException("The file format is not supported.");
		}
	}
}
