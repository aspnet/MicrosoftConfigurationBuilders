# Copyright (c) .NET Foundation. All rights reserved.
# Licensed under the MIT license. See LICENSE file in the project root for full license information.

param($installPath, $toolsPath, $package, $project)

. "$PSScriptRoot\KeyValueConfigBuildersCommon.ps1"

##### Dehydrate config declarations #####
$config = ReadConfigFile
DehydrateDeclarations $config, "$typeName$, $assemblyName$""
SaveConfigFile $config
