# Copyright (c) .NET Foundation. All rights reserved.
# Licensed under the MIT license. See LICENSE file in the project root for full license information.

param($installPath, $toolsPath, $package, $project)

. "$PSScriptRoot\KeyValueConfigBuildersCommon.ps1"

##### Describe the KeyPerFile config builder #####
$keyPerFileConfigBuilder = [BuilderDescription]@{
	TypeName="$typeName$";
	Assembly="$assemblyName$";
	Version="$assemblyVersion$";
	DefaultName="KeyPerFile";
	AllowedParameters=@( $keyValueCommonParameters +
		[ParameterDescription]@{ Name="directoryPath"; IsRequired=$true; DefaultValue="[PathToSourceDirectory]" },
		[ParameterDescription]@{ Name="keyDelimiter"; IsRequired=$false },
		[ParameterDescription]@{ Name="ignorePrefix"; IsRequired=$false });
}

CommonInstall $keyPerFileConfigBuilder
