# Copyright (c) .NET Foundation. All rights reserved.
# Licensed under the MIT license. See LICENSE file in the project root for full license information.

param($installPath, $toolsPath, $package, $project)

. "$PSScriptRoot\KeyValueConfigBuildersCommon.ps1"

##### Describe the AzureAppConfig config builder #####
$keyVaultConfigBuilder = [BuilderDescription]@{
	TypeName="$typeName$";
	Assembly="$assemblyName$";
	Version="$assemblyVersion$";
	DefaultName="AzureAppConfig";
	AllowedParameters=@( $keyValueCommonParameters +
		[ParameterDescription]@{ Name="endpoint"; IsRequired=$false; DefaultValue="[Config_Store_Endpoint_Url]" },
		[ParameterDescription]@{ Name="connectionString"; IsRequired=$false },
		[ParameterDescription]@{ Name="keyFilter"; IsRequired=$false },
		[ParameterDescription]@{ Name="labelFilter"; IsRequired=$false },
		[ParameterDescription]@{ Name="acceptDateTime"; IsRequired=$false };
	)
}

CommonInstall $keyVaultConfigBuilder
