# Copyright (c) .NET Foundation. All rights reserved.
# Licensed under the MIT license. See LICENSE file in the project root for full license information.

param($installPath, $toolsPath, $package, $project)

. "$PSScriptRoot\KeyValueConfigBuildersCommon.ps1"

##### Describe the Environment config builder #####
$environmentConfigBuilder = [BuilderDescription]@{
	TypeName="$typeName$";
	Assembly="$assemblyName$";
	Version="$assemblyVersion$";
	DefaultName="Environment";
	AllowedParameters=$keyValueCommonParameters;
}

CommonInstall $environmentConfigBuilder
