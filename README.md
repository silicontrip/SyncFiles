# SyncFiles
Powershell cmdlet similar to rsync

    NAME
        Sync-ChildItem
        
    SYNOPSIS
        Synchronises items from one location to another.

    SYNTAX
        Sync-ChildItem [-Path] <String> [-Target] <String> [-Checksum] [-Progress] [-ToSession <PSSession>] [-FromSession <PSSession>] [-Exclude <String[]>] [-Include <String[]>] [-verbose] [-debug]

    DESCRIPTION
        The Sync-ChildItems cmdlet copies an item or container from one location to another in the FileSystem provider
        namespace.  It recurses the container and copies all items to the destination as needed.  If the length and 
        Last Write Time, and optionally the SHA256 checksum, match the file is skipped.   If a file needs to be copied,
        the source is compared to the destination in 1 meg blocks and only blocks that do not match are copied.

	As the ToSession and FromSession use base64 for the transfer, speeds can be around 5 megabytes/s even on 
	gigabit ethernet network connections. 
        
        Example 1: Copy a file to a destination folder
        
        PS C:\>Sync-ChildItems c:\Videos\MyLargeVideo.mp4 d:\Videos\ -Progress
        
        Will copy MyLargeVideo.mp4 file in c:\videos to d:\Videos\ and will display the progress as it goes.
        
        Example 2: Copy a folder to a destination folder
        
        PS C:\>Sync-ChildItems c:\Videos\ d:\Videos\ -Verbose
        
        Will copy the contents of c:\videos into d:\videos printing a line for each file copied.
        
        PS C:\>Sync-ChildItems c:\Videos\ d:\videos -Exclude *.avi
        
        Copies all the files in c:\videos excluding any that end in .avi
        
        PS C:\>Sync-ChildItems c:\Videos d:\videos -Include *.mp4
        
        Copies only the files ending in mp4 
        
        PS C:\>Sync-ChildItems c:\Videos d:\videos -Include *.mp4  -Exclude DSC_*
        
        Copies only files ending in mp4 but not those starting with DSC_
        
        PS C:\>$Session = New-PSSession -ComputerName "Server01" -Credential "Silicontrip\mark"
        PS C:\> Sync-ChildItems "D:\Folder001\"  "C:\Folder001_Copy\" -ToSession $Session -progress

	Copies the contents of the local d:\Folder001 to the C drive on Server01 into the folder Folder001_Copy via
        a PSSession connection.  

        PS C:\>$Session1 = New-PSSession -ComputerName "Server01" -Credential "Silicontrip\mark"
        PS C:\>$Session2 = New-PSSession -ComputerName "Server02" -Credential "Silicontrip\mark"
        PS C:\> Sync-ChildItems "D:\Folder001\"  "D:\Folder001\" -ToSession $Session1 -FromSession $session2 -progress

	Copies the contents of the folder d:\Folder001 on the server Server02 to the folder d:\Folder001 on the server
	Server01 via the PSsession protocol.   This will pull the data to your local machine and then send it back to 
        the other server.  This can be usefull if a firewall blocks server02 from getting to server01.
