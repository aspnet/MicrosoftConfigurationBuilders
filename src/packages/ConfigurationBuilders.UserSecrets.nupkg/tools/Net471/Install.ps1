# Copyright (c) .NET Foundation. All rights reserved.
# Licensed under the MIT license. See LICENSE file in the project root for full license information.

param($installPath, $toolsPath, $package, $project)

. "$PSScriptRoot\KeyValueConfigBuildersCommon.ps1"

##### Describe the UserSecrets config builder #####
$userSecretsId=[Guid]::NewGuid()
$secretsConfigBuilder = [BuilderDescription]@{
	TypeName="$typeName$";
	Assembly="$assemblyName$";
	Version="$assemblyVersion$";
	DefaultName="Secrets";
	AllowedParameters=@( $keyValueCommonParameters +
		[ParameterDescription]@{ Name="userSecretsId"; IsRequired=$false; DefaultValue=$userSecretsId },
		[ParameterDescription]@{ Name="userSecretsFile"; IsRequired=$false });
}

CommonInstall $secretsConfigBuilder
