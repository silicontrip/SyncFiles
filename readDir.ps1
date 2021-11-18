

function ReadDir {
param( [parameter()][string]$p )
$dl=new-object System.Collections.ArrayList
$fl=new-object System.Collections.ArrayList
$dl.Add($p)
while ($dl.Count -gt 0)
{
	$tfl=[IO.Directory]::GetFileSystemEntries($dl[0])
	$dl.removeAt(0)
	foreach ($fe in $tfl)
	{
		$fa =[io.file]::GetAttributes($fe)
		if (($fa -bAnd [io.fileattributes]::Directory) -ne 0)
		{
			$dl.Add($fe) > $null
		}
		$fl.Add($fe) > $null
	}
}
$fl
}
