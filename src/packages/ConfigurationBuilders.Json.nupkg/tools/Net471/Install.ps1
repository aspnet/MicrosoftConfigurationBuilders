# Copyright (c) .NET Foundation. All rights reserved.
# Licensed under the MIT license. See LICENSE file in the project root for full license information.

param($installPath, $toolsPath, $package, $project)

. "$PSScriptRoot\KeyValueConfigBuildersCommon.ps1"

##### Describe the SimpleJson config builder #####
$jsonConfigBuilder = [BuilderDescription]@{
	TypeName="$typeName$";
	Assembly="$assemblyName$";
	Version="$assemblyVersion$";
	DefaultName="SimpleJson";
	AllowedParameters=@( $keyValueCommonParameters +
		[ParameterDescription]@{ Name="jsonFile"; IsRequired=$true; DefaultValue="~/App_Data/settings.json" },
		[ParameterDescription]@{ Name="jsonMode"; IsRequired=$false },
		[ParameterDescription]@{ Name="optional"; IsRequired=$false });
}

##### Update/Rehydrate config declarations #####
$config = ReadConfigFile
$rehydratedCount = RehydrateOldDeclarations $config $jsonConfigBuilder
$updatedCount = UpdateDeclarations $config $jsonConfigBuilder
if ($updatedCount -le 0) { AddDefaultDeclaration $config $jsonConfigBuilder }
SaveConfigFile $config
