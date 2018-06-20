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

##### Update/Rehydrate config declarations #####
$config = ReadConfigFile
$rehydratedCount = RehydrateOldDeclarations $config $environmentConfigBuilder
$updatedCount = UpdateDeclarations $config $environmentConfigBuilder
if ($updatedCount -le 0) { AddDefaultDeclaration $config $environmentConfigBuilder }
SaveConfigFile $config
