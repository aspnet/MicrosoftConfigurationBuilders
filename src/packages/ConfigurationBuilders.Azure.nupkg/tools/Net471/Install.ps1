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
		[ParameterDescription]@{ Name="connectionString"; IsRequired=$false }, # Obsolete, but don't complain about it here. Still preserve it so people can revert back to the version that allows this.
		[ParameterDescription]@{ Name="version"; IsRequired=$false },
		[ParameterDescription]@{ Name="preloadSecretNames"; IsRequired=$false });
}

CommonInstall $keyVaultConfigBuilder
