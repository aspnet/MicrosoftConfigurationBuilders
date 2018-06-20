# Copyright (c) .NET Foundation. All rights reserved.
# Licensed under the MIT license. See LICENSE file in the project root for full license information.

param($installPath, $toolsPath, $package, $project)

. "$PSScriptRoot\KeyValueConfigBuildersCommon.ps1"

##### Describe the AzureKeyVault config builder #####
$keyVaultConfigBuilder = [BuilderDescription]@{
	TypeName="$typeName$";
	Assembly="$assemblyName$";
	Version="$assemblyVersion$";
	DefaultName="AzureKeyVault";
	AllowedParameters=@( $keyValueCommonParameters +
		[ParameterDescription]@{ Name="vaultName"; IsRequired=$false; DefaultValue="[VaultName]" },
		[ParameterDescription]@{ Name="uri"; IsRequired=$false },
		[ParameterDescription]@{ Name="connectionString"; IsRequired=$false },
		[ParameterDescription]@{ Name="version"; IsRequired=$false },
		[ParameterDescription]@{ Name="preloadSecretNames"; IsRequired=$false });
}

##### Update/Rehydrate config declarations #####
$config = ReadConfigFile
$rehydratedCount = RehydrateOldDeclarations $config $keyVaultConfigBuilder
$updatedCount = UpdateDeclarations $config $keyVaultConfigBuilder
if ($updatedCount -le 0) { AddDefaultDeclaration $config $keyVaultConfigBuilder }
SaveConfigFile $config
