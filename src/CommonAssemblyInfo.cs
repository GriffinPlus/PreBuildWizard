using System.Reflection;

[assembly: AssemblyCompany("Griffin+")]
[assembly: AssemblyProduct("Griffin+ PreBuildWizard")]
[assembly: AssemblyCopyright("Copyright (c) Sascha Falk, Sebastian Piel 2021")]

[assembly: AssemblyVersionAttribute("1.0.0.0")];
[assembly: AssemblyFileVersionAttribute("1.0.0.0")];
[assembly: AssemblyInformationalVersionAttribute("1.0.0")];

#if DEBUG
[assembly: AssemblyConfigurationAttribute("Debug")];
#else
[assembly: AssemblyConfigurationAttribute("Release")];
#endif