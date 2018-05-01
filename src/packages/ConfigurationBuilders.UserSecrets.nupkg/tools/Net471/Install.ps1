# Copyright (c) .NET Foundation. All rights reserved.
# Licensed under the MIT license. See LICENSE file in the project root for full license information.

param($installPath, $toolsPath, $package, $project)

##### Supporting Functions #####
function EnsureUserSecretsId {
	$id = $null
	foreach ($prop in $root.Properties) {
		if ($prop.Name -eq 'UserSecretsId') {
			$id=$prop.Value
		}
	}
	if ([string]::IsNullOrEmpty($id)) {
		Write-Host ("Generating new UserSecretsId for project " + $project.FullName + "...")
		$newUserSecretsId=New-Guid
		$id=$newUserSecretsId
		$root.AddProperty('UserSecretsId', $newUserSecretsId.ToString())
		Write-Host ("Set UserSecretsId for project to " + $newUserSecretsId)
	}
	return $id
}

function EnsureUserSecretsFile {
	$datadir=$env:APPDATA
	$dirpath=[io.path]::combine($datadir, "Microsoft", "UserSecrets", $userSecretsId)
	$envpath=[io.path]::combine('$(APPDATA)', "Microsoft", "UserSecrets", $userSecretsId, "secrets.xml")
	if ([string]::IsNullOrEmpty($datadir) -or !(Test-Path $datadir)) {
		$datadir=$env:HOME
		$dirpath=[io.path]::combine($datadir, ".microsoft", "usersecrets", $userSecretsId)
		$envpath=[io.path]::combine('~', ".microsoft", "usersecrets", $userSecretsId, "secrets.xml")
	}
	if ([string]::IsNullOrEmpty($datadir) -or !(Test-Path $datadir)) {
		return $null,$null
	}

	$filepath=[io.path]::combine($dirpath, "secrets.xml")
	$sourcepath=[io.path]::combine($installPath, "content", "sample.xml")


	# Don't overwrite an existing secrets file
	if (!(Test-Path $filepath)) {
		New-Item $dirpath -Type Directory -Force
		Copy-Item $sourcepath $filepath -Force
	}

	return $envpath,$filepath
}

function LinkSecretsFile {
	# Make sure there is something to link
	if ($secretsFile -eq $null) {
		return
	}

	# Be smart about the special 'Properties' folder
	$appDesignerFolder=$root.Properties | Where-Object {$_.Name -eq 'AppDesignerFolderIsNotHere'} | Select-Object -ExpandProperty Value
	if ([string]::IsNullOrEmpty($appDesignerFolder)) {
		$appDesignerFolder="Properties"
	}

	# Add secrets.xml as a 'Link'
	$propertiesDir=$project.ProjectItems.Item($appDesignerFolder)
	$item=$propertiesDir.ProjectItems.AddFromFile($expandedSecretsFile)
	if (!($item -eq $null)) {
		# If we successfully added the link, update the location to use $(APPDATA) variable instead of an expanded path.
		$msbuildItem=$root.Items | Where-Object {$_.Metadata | Where-Object {($_.Name -eq 'Link') -and ($_.Value -eq "$appDesignerFolder\secrets.xml")} } | Select-Object
		$msbuildItem.Include=$secretsFile
	}
}



##### Begin Installation #####
Write-Host ('Executing Install.ps1 in UserSecrets package...')
$project.Save()
$root = [Microsoft.Build.Construction.ProjectRootElement]::Open($project.FullName)

# Ensure UserSecretsId exists
$userSecretsId = EnsureUserSecretsId

# Ensure the secrets file exists
$secretsFile, $expandedSecretsFile = EnsureUserSecretsFile

# Ensure the file is linked in AppDesignerFolder, aka 'Properties'
LinkSecretsFile

# Done
$project.Save()
Write-Host ('Done.')