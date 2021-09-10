function IsDir()
{
	param (
		[Parameter()][string]$Path
	)

	(([io.file]::GetAttributes($path) -bAnd [io.fileattributes]::Directory) -ne 0)

}

function ReadDir()
{
	param (
		[Parameter()][string]$Path,
		[Parameter()][string]$BasePath
	)

	set-location $BasePath
	$dl=new-object System.Collections.ArrayList
	$dl.Add($Path) > $null
	while ($dl.Count -gt 0) {
		$tfl=[IO.Directory]::GetFileSystemEntries($dl[0])
		$dl.removeAt(0)
		foreach ($fe in $tfl)
		{
			$fa =[io.file]::GetAttributes($fe)
			$rp=(resolve-path -Relative $fe)

			if (($fa -bAnd [io.fileattributes]::Directory) -ne 0)
			{
				$dl.Add($fe) > $null
			}
			$rp
		}
	}

}


function HashTotal()
{
	param (
		[Parameter()][string]$Path
	)

	$fs=[System.IO.file]::OpenRead($path)
	$sha=[system.security.cryptography.sha256]::Create()
	$h=$sha.computehash($fs)
	$sha.Dispose()
	$fs.Close()
	$h
}