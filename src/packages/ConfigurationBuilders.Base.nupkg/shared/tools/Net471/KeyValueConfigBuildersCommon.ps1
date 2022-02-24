# Copyright (c) .NET Foundation. All rights reserved.
# Licensed under the MIT license. See LICENSE file in the project root for full license information.


##
## Assigning a "DefaultValue" to a ParameterDescription will result in emitting this parameter when
## writing out a default builder declaration.
##
## Setting IsRequired to $true will require the attribute to be set on all declarations in config.
##
## Parameters that are not in the allowed list will be dropped from config declarations.
##
Add-Type @"
	using System;

	public class ValueMap {
		public string OriginalValue;
		public string NewValue;
	}

	public class ParameterDescription {
		public string Name;
		public string DefaultValue;
		public bool IsRequired;
		public string MigrateTo;
		public ValueMap[] ValueMigration;
		public bool IsObsolete;
		public string[] ObsoleteValues;
	}

	public class BuilderDescription {
		public string TypeName;
		public string Assembly;
		public string Version;
		public string DefaultName;
		public ParameterDescription[] AllowedParameters;
	}
"@

$keyValueCommonParameters = @(
		[ParameterDescription]@{ Name="mode"; IsRequired=$false; MigrateTo="mode";
			ValueMigration=@(
				[ValueMap]@{ OriginalValue="Expand"; NewValue="RawToken"}
			) },
		[ParameterDescription]@{ Name="prefix"; IsRequired=$false },
		[ParameterDescription]@{ Name="stripPrefix"; IsRequired=$false },
		[ParameterDescription]@{ Name="tokenPattern"; IsRequired=$false },
		[ParameterDescription]@{ Name="escapeExpandedValues"; IsRequired=$false },
		[ParameterDescription]@{ Name="charMap"; IsRequired=$false },
		[ParameterDescription]@{ Name="enabled"; IsRequired=$false },
		[ParameterDescription]@{ Name="optional"; IsRequired=$false; MigrateTo="enabled";
			ValueMigration=@(
				[ValueMap]@{ OriginalValue="true"; NewValue="optional"},
				[ValueMap]@{ OriginalValue="false"; NewValue="enabled"}
			) }
		);

function CommonInstall($builderDescription) {
	##### Update/Rehydrate config declarations #####
	$config = ReadConfigFile
	$rehydratedCount = RehydrateOldDeclarations $config $builderDescription
	$updatedCount = UpdateDeclarations $config $builderDescription
	if ($rehydratedCount -le 0) { AddDefaultDeclaration $config $builderDescription }
	SaveConfigFile $config
}

function CommonUninstall($builderType) {
	##### Dehydrate config declarations #####
	$config = ReadConfigFile
	DehydrateDeclarations $config $builderType | Out-Null
	SaveConfigFile $config
}

function GetConfigFileName() {
	# Try web.config first. Then fall back to app.config.
	$configFile = $project.ProjectItems | where { $_.Name -ieq "web.config" }
	if ($configFile -eq $null) { $configFile = $project.ProjectItems | where { $_.Name -ieq "app.config" } }
	$configPath = $configFile.Properties | where { $_.Name -ieq "LocalPath" }
    if ($configPath -eq $null) { $configPath = $configFile.Properties | where { $_.Name -ieq "FullPath" } }
	return $configPath.Value
}

function GetTempFileName() {
	$uname = $project.UniqueName
	if ([io.path]::IsPathRooted($uname)) { $uname = $project.Name }
	return [io.path]::Combine($env:TEMP, "Microsoft.Configuration.ConfigurationBuilders.KeyValueConfigBuilders.Temp", $uname + ".xml")
}

function ReadConfigFile() {
	$configFile = GetConfigFileName
	$configObj = @{ fileName = $configFile; xml = (Select-Xml -Path "$configFile" -XPath /).Node }
	$configObj.xml.PreserveWhitespace = $true
	return $configObj
}

function DehydrateDeclarations($config, $typeName) {
	$tempFile = GetTempFileName
	$count = 0

	if ([io.file]::Exists($tempFile)) {
		$xml = (Select-Xml -Path "$tempFile" -XPath /).Node
	} else {
		$xml = New-Object System.Xml.XmlDocument
		$xml.PreserveWhitespace = $true
		$xml.AppendChild($xml.CreateElement("driedDeclarations")) | Out-Null
	}

	foreach ($rec in $config.xml.configuration.configBuilders.builders.add  | where { IsSameType $_.type $typeName }) {
		# Remove records from config.
		$config.xml.configuration.configBuilders.builders.RemoveChild($rec) | Out-Null

		# Add the record to the temp stash. Don't worry about duplicates.
		AppendBuilderNode $xml.ImportNode($rec, $true) $xml.DocumentElement | Out-Null
		$count++
	}

	# Save the dehydrated declarations
	$tmpFolder = Split-Path $tempFile
	md -Force $tmpFolder | Out-Null
	$xml.Save($tempFile) | Out-Null
	return $count
}

function RehydrateOldDeclarations($config, $builderDescription) {
	$tempFile = GetTempFileName
	if (![io.file]::Exists($tempFile)) { return 0 }

	$count = 0
	$xml = (Select-Xml -Path "$tempFile" -XPath /).Node
	$xml.PreserveWhitespace = $true

	foreach($rec in $xml.driedDeclarations.add | where { IsSameType $_.type ($builderDescription.TypeName + "," + $builderDescription.Assembly) }) {
		# Remove records that match type, even if we don't end up rehydrating them.
		$xml.driedDeclarations.RemoveChild($rec) | Out-Null

		# Skip if an existing record of the same name already exists.
		$existingRecord = $config.xml.configuration.configBuilders.builders.add | where { $_.name -eq $rec.name }
		if ($existingRecord -ne $null) { continue }

		# Bring the record back to life
		AppendBuilderNode $config.xml.ImportNode($rec, $true) $config.xml.configuration.configBuilders.builders | Out-Null
		$count++
		Write-Host "Restored configBuilder '$($rec.name)'."
	}

	# Make dried record removal permanent
	$xml.Save($tempFile) | Out-Null

	return $count
}

function UpdateDeclarations($config, $builderDescription) {
	$count = 0

	foreach ($builder in $config.xml.configuration.configBuilders.builders.add | where { IsSameType $_.type ($builderDescription.TypeName + "," + $builderDescription.Assembly) }) {

		$failed = $false

		# Migrate parameters that have changed
		foreach ($p in $builderDescription.AllowedParameters | where { $_.MigrateTo -ne $null }) {
			if ($builder.($p.Name) -ne $null) {
				$oldvalue = $builder.($p.Name)
				$newvalue = ($p.ValueMigration | where { $_.OriginalValue -eq $oldvalue } | select -First 1 ).NewValue
				if ($newvalue -eq $null) { $newvalue = $oldvalue }
				$builder.RemoveAttribute($p.Name) | Out-Null
				$builder.SetAttribute($p.MigrateTo, $newvalue) | Out-Null
				Write-Host "Migrated '$($p.Name):$($oldvalue)' to '$($p.MigrateTo):$($newvalue)' for configBuilder '$($builder.name)'"
			}
		}

		# Add default parameters if they are required and not already present
		foreach ($p in $builderDescription.AllowedParameters | where { $_.IsRequired -eq $true }) {
			if ($builder.($p.Name) -eq $null) {
				if ($p.DefaultValue -eq $null) {
					Write-Warning "Failed to add parameter to '$($builder.name)' configBuilder: '$($p.Name)' is required, but does not have a default value."
					$failed = $true
				}
				$builder.SetAttribute($p.Name, $p.DefaultValue) | Out-Null
				Write-Host "Added default value for parameter '$($p.Name)' to configBuilder '$($builder.name)'"
			}
		}

		# Finally, update type. And do so with remove/add so the 'type' parameter get put at the end
		$builder.RemoveAttribute("type") | Out-Null
		$builder.SetAttribute("type", "$($builderDescription.TypeName), $($builderDescription.Assembly), Version=$($builderDescription.Version), Culture=neutral, PublicKeyToken=31bf3856ad364e35") | Out-Null

		# Check for unknown parameters
		foreach ($attr in $builder.Attributes | where { ($_.Name -ne "name") -and ($_.Name -ne "type") }) {
			if (($builderDescription.AllowedParameters | where { $_.Name -ceq $attr.Name }) -eq $null) {
				# Leave it alone, but spit out a warning?
				Write-Warning "The parameter '$($attr.Name)' on configBuilder '$($builder.name)' is unknown and may cause errors at runtime."
			}
		}

		# Warn about any obsolete settings
		foreach ($p in $builderDescription.AllowedParameters | where { $_.IsObsolete -eq $true }) {
			if ($builder.($p.Name) -ne $null) {
				Write-Warning "The parameter '$($p.Name)' on '$($builder.name)' configBuilder is obsolete. Please consider alternative configurations."
			}
		}
		foreach ($p in $builderDescription.AllowedParameters | where { $_.ObsoleteValues -ne $null -and $_.ObsoleteValues.Count -gt 0 }) {
			if ($builder.($p.Name) -ne $null -and $p.ObsoleteValues.Contains($builder.($p.Name))) {
				Write-Warning "The value of parameter '$($p.Name)' on '$($builder.name)' configBuilder is obsolete. Please consider alternative configurations."
			}
		}

		# Count the existing declaration as updated
		if ($failed -ne $true) { $count++ }
	}

	return $count
}

function AddDefaultDeclaration($config, $builderDescription) {
	$dd = $config.xml.CreateElement("add")

	# name first
	$dd.SetAttribute("name", $builderDescription.DefaultName) | Out-Null

	# everything else in the middle
	foreach ($p in $builderDescription.AllowedParameters) {
		if ($p.IsRequired -and ($p.DefaultValue -eq $null)) {
			Write-Host "Failed to add default declaration for $($builderDescription.TypeName): '$($p.Name)' is required, but does not have a default value."
			return
		}

		if ($p.DefaultValue -ne $null) {
			$dd.SetAttribute($p.Name, $p.DefaultValue) | Out-Null
		}
	}

	# type last
	$dd.SetAttribute("type", "$($builderDescription.TypeName), $($builderDescription.Assembly), Version=$($builderDescription.Version), Culture=neutral, PublicKeyToken=31bf3856ad364e35") | Out-Null

	AppendBuilderNode $dd $config.xml.configuration.configBuilders.builders | Out-Null
	Write-Host "Added default configBuilder '$($dd.name)'."
}

function AppendBuilderNode($builder, $parent, $indentLevel = 3) {
	$lastSibling = $parent.ChildNodes | where { $_ -isnot [System.Xml.XmlWhitespace] } | select -Last 1
	if ($lastSibling -ne $null) {
		# If not the first child, then copy the whitespace convention of the existing child
		$ws = "";
		$prev = $lastSibling.PreviousSibling | where { $_ -is [System.Xml.XmlWhitespace] }
		while ($prev -ne $null) {
			$ws = $prev.data + $ws
			$prev = $prev.PreviousSibling | where { $_ -is [System.Xml.XmlWhitespace] }
		}
		$parent.InsertAfter($builder, $lastSibling) | Out-Null
		if ($ws.length -gt 0) { $parent.InsertAfter($parent.OwnerDocument.CreateWhitespace($ws), $lastSibling) | Out-Null }
		return
	}

	# Add on a new line with indents. Make sure there is no existing whitespace mucking this up.
	foreach ($exws in $parent.ChildNodes | where { $_ -is [System.Xml.XmlWhitespace] }) { $parent.RemoveChild($exws) | Out-Null }
	$parent.AppendChild($parent.OwnerDocument.CreateWhitespace("`r`n")) | Out-Null
	$parent.AppendChild($parent.OwnerDocument.CreateWhitespace("  " * $indentLevel)) | Out-Null
	$parent.AppendChild($builder) | Out-Null
	$parent.AppendChild($parent.OwnerDocument.CreateWhitespace("`r`n")) | Out-Null
	$parent.AppendChild($parent.OwnerDocument.CreateWhitespace("  " * ($indentLevel - 1))) | Out-Null
}

function SaveConfigFile($config) {
	$config.xml.Save($config.fileName) | Out-Null
}

function IsSameType($typeString1, $typeString2) {

	if (($typeString1 -eq $null) -or ($typeString2 -eq $null)) { return $false }

	# First check the type
	$t1 = $typeString1.Split(',')[0].Trim()
	$t2 = $typeString2.Split(',')[0].Trim()
	if ($t1 -cne $t2) { return $false }

	# Then check for assembly match if possible
	$a1 = $typeString1.Split(',')[1]
	$a2 = $typeString2.Split(',')[1]
	if (($a1 -ne $null) -and ($a2 -ne $null)) {
		return ($a1.Trim() -eq $a2.Trim())
	}

	# Don't care about assembly. Match is good.
	return $true
}
