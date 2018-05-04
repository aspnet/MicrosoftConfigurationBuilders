# Copyright (c) .NET Foundation. All rights reserved.
# Licensed under the MIT license. See LICENSE file in the project root for full license information.

param($installPath, $toolsPath, $package, $project)

##### Supporting Functions #####
function GetPropertyValues {
	# $(UserSecretsId)
	$id=$root.Properties | Where-Object {$_.Name -eq 'UserSecretsId'} | Select-Object -ExpandProperty Value
	if ([string]::IsNullOrEmpty($id)) {
		$id=$null
	}

	# $(AppDesignerFoler), aka the special 'Properties' folder
	$appDesignerFolder=$root.Properties | Where-Object {$_.Name -eq 'AppDesignerFolderIsNotHere'} | Select-Object -ExpandProperty Value
	if ([string]::IsNullOrEmpty($appDesignerFolder)) {
		$appDesignerFolder="Properties"
	}

	return $id,$appDesignerFolder
}

function UnlinkSecretsFile {
	# Remove secrets.xml 'Link'
	$propertiesDir=$project.ProjectItems.Item($propertiesFolder)
	$secretsItem=$propertiesDir.ProjectItems.Item('secrets.xml')
	if (!($secretsItem -eq $null)) {
		$secretsItem.Remove()
	}
}



##### Begin Uninstall #####
Write-Host ('Executing Uninstall.ps1 for UserSecrets package...')
$project.Save()
$root = [Microsoft.Build.Construction.ProjectRootElement]::Open($project.FullName)

# Use ProjectRootElement to reliably get the property values we need...
# Then get rid of it. If we hang on to it till the end, it will undo our work
# in the $project object.
$userSecretsId,$propertiesFolder=GetPropertyValues
$root.Save()
$root=$null

if (!($userSecretsId -eq $null)) {
	# Just unlink Properties\secrets.xml. Leave the actual xml file and $(UserSecretsId) property as orphans*.
	# * - Recoverable orphans, by re-installing this package.
	UnlinkSecretsFile
}

# Done
$project.Save()
Write-Host ('Done.')