# Griffin+ PreBuildWizard

[![Build (master)](https://dev.azure.com/griffinplus/PreBuildWizard/_apis/build/status/Continuous%20Integration?branchName=master)](https://dev.azure.com/griffinplus/PreBuildWizard/_build/latest?definitionId=28&branchName=master)
[![Release](https://img.shields.io/github/release/griffinplus/prebuildwizard.svg?logo=github)](https://github.com/GriffinPlus/PreBuildWizard/releases)


## Overview

This repository contains the Griffin+ *PreBuildWizard*, a tool that is called before building Griffin+ projects
based on Visual Studio.

### Version Patching

The *PreBuildWizard* patches version information into assembly info files of C# projects and C++ .NET projects.
Version information in resource (`.rc`) files usually shipped with native DLLs is patched as well. If the repository
contains an installer project based on the WiX toolkit, the version of the package is also patched appropriately.

### Razor Templates

The *PreBuildWizard* supports rendering Razor templates (`*.pbwtempl`) to customize the build process.

### Checking Nuget Dependencies

The *PreBuildWizard* examines `project.assets.json` files to detect Nuget package dependency issues that can easily
occur, if projects have transitive dependencies to different versions of the same Nuget packages. This condition
can screw up the built solution and is hard to debug.
