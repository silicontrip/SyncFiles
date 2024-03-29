using System.Management.Automation;  // Windows PowerShell assembly.
using System.Management.Automation.Runspaces;
//using System.Management.Automation.Runspaces.Pipeline;
using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Security;
using System.Text.RegularExpressions;
// using Microsoft.VisualBasic.CompilerServices;

namespace net.ninebroadcast
{
    // Declare the class as a cmdlet and specify the
    // appropriate verb and noun for the cmdlet name.

    [Cmdlet(VerbsData.Sync, "ChildItem")]
    public class SyncPathCommand : PSCmdlet
    {

        Int32 Count = 0;

        public
        SyncPathCommand()
        {
        }

        // Declare the parameters for the cmdlet.
        [Alias("FullName")]
        [Parameter(Mandatory = true, Position = 0, ValueFromPipelineByPropertyName = true)]
        public string[] Path
        {
            get { return path; }
            set { path = value; }
        }
        private string[] path = {"."};

        [Alias("Destination")]
        [Parameter(Mandatory = true, Position = 1)]
        public string Target
        {
            get { return target; }
            set { target = value; }
        }
        private string target;

        [Parameter()]
        public SwitchParameter Checksum
        {
            get { return checksum; }
            set { checksum = value; }
        }
        private Boolean checksum;

        [Parameter()]
        public SwitchParameter Update
        {
            get { return update; }
            set { update = value; }
        }
        private Boolean update;

        [Parameter()]
        public SwitchParameter SizeOnly
        {
            get { return sizeonly; }
            set { sizeonly = value; }
        }
        private Boolean sizeonly;

        [Parameter()]
        public SwitchParameter Whole
        {
            get { return whole; }
            set { whole = value; }
        }
        private Boolean whole;

       [Parameter()]
        public SwitchParameter Times
        {
            get { return times; }
            set { times = value; }
        }
        private Boolean times;

       [Parameter()]
        public SwitchParameter Permissions
        {
            get { return permissions; }
            set { permissions = value; }
        }
        private Boolean permissions;

       [Parameter()]
        public SwitchParameter Acls
        {
            get { return acls; }
            set { acls = value; }
        }
        private Boolean acls;

      [Parameter()]
        public SwitchParameter Owner
        {
            get { return owner; }
            set { owner = value; }
        }
        private Boolean owner;

      [Parameter()]
        public SwitchParameter Group
        {
            get { return group; }
            set { group = value; }
        }
        private Boolean group;

        [Parameter()]
        public SwitchParameter Progress
        {
            get { return progress; }
            set { progress = value; }
        }
        private Boolean progress;


        [Parameter()]
        public SwitchParameter WhatIf
        {
            get { return whatif; }
            set { whatif = value; }
        }
        private Boolean whatif;

        [Parameter()]
        public SwitchParameter Delete
        {
            get { return delete; }
            set { delete = value; }
        }
        private Boolean delete;

        [Parameter()]
        public PSSession ToSession
        {
            get { return tosession; }
            set { tosession = value; }
        }
        private PSSession tosession;

        [Parameter()]
        public PSSession FromSession
        {
            get { return fromsession; }
            set { fromsession = value; }
        }
        private PSSession fromsession;

        [Parameter()]
        public PSCredential Credential
        {
            get { return credential; }
            set { credential = value; }
        }
        private PSCredential credential;

        [Parameter()]
        public string[] Exclude
        {
            get { return excludeList; }
            set { excludeList = value; }
        }
        private string[] excludeList;

        [Parameter()]
        public string[] Include
        {
            get { return includeList; }
            set { includeList = value; }
        }
        private string[] includeList;

        private IO DestinationIO (PSSession pss, string dp)
        {
            IO destio;
            if (pss != null)
            {
                destio = new RemoteIO (pss,dp);
            } else {
                destio = new LocalIO(this.SessionState,dp);
            }

			// destio abs = (cwd() + dp) or (dp) if rooted(dp)
            WriteDebug("syncfiles::DestinationIO AbsPath " + destio.AbsPath());  

			// is there a reason we are expanding the path ?
			// a long time ago this also performed absolute path expansion
			// now I'm just not sure why it's here

			FileAttributes destfa = destio.GetAttributes("");

			WriteDebug("syncfiles::DestinationIO DestAttributes: " + destfa);

			if (destio.IsDir()) 
				return destio;

            Collection<string> expandpath = destio.ExpandPath("");  // have to work out what this returns under different scenarios.

            if (expandpath.Count == 0)
            {
                destio.MakeDir("");
                // destio.SetPath(abspath);
            } else if (expandpath.Count == 1) {
               // destio.SetPath(abspath);
            } else {
				// WriteDebug("abs: " + abspath);
				foreach (string expanded in expandpath) {
					WriteDebug("syncfiles::DestinationIO Destination Error: " + expanded);
				}
                // will need to handle \. expanding to .\*
                throw new ArgumentException ("Ambigious destination");
            }

            return destio;

            //Collection<string> expandpath = destio.ExpandPath(abspath);
        }

        private IO SourceIO(PSSession session, string sp)
        {
            List<IO> src = new List<IO>();
            //WriteDebug(String.Format("Expanding: {0}",p));
            //Collection<string> expath;

            // string cpath = Path.Combine(basepath, element);
            IO srcio;
            if (session != null)
            {
                //expath = RemoteIO.ExpandPath(cpath,session);
				WriteDebug("syncfiles::SourceIO new RemoteIO");
                srcio = new RemoteIO(session,sp);
            } else {
                //expath = LocalIO.ExpandPath(cpath,this.SessionState);
				WriteDebug("syncfiles::SourceIO new LocalIO");
                srcio = new LocalIO(this.SessionState,sp);
            }

            Collection<string> expandpath = srcio.ExpandPath(""); 

            if (expandpath.Count == 0)
            {
                throw new FileNotFoundException(sp);
            }

            return srcio;

        }

        // private void copy(IO src,string srcFile, IO dst, string dstFile, ProgressRecord prog)

        private void CopyMetadata(string filename, IO src, IO dst)
        {
                WriteDebug("syncfiles::CopyMetadata");

            if (times)
            {
                DateTime smod = src.GetModificationTime(filename);
                WriteDebug ("syncfiles::copyMetaData times mod time: " + smod);
                dst.SetModificationTime(filename,smod);
            }


            if (permissions)
            {
                WriteDebug("syncfiles::CopyMetadata permissions/attributes");

                dst.SetAttributes(filename,src.GetAttributes(filename));
            }

            if (acls)
            {
                // Admin privs are needed to read ACLS... really MS? just to read?
                WriteDebug("syncfiles::CopyMetadata acl");
                dst.SetAcl(filename,src.GetAcl(filename)); // stuff 

                //FileSecurity srcacl = src.GetAcl(filename);
                //dst.SetAcl(filename,srcacl); // stuff 
                // throw If error. caught in transfer loop == skip file
            }
            if (owner)
            {
                WriteDebug("syncfiles::CopyMetadata owner");

                ; // stuff
            }
            if (group)
            {
                ;
            }
        }

		// Copies a single file to destination
        private void copy(string filename, IO src, IO dst, ProgressRecord prog)
        {

            WriteDebug("copy: " + filename);

            Int64 bytesxfered = 0;
            Int32 block = 0;
            Byte[] b;

            long srcLength = src.GetLength(filename);

            do
            {
                WriteDebug("SyncFiles::copy: do: " + block);
                bool copyBlock = true;
                if (!whole) 
                {
                    if (dst.Exists(filename))
                    {
                        // I don't see what's so special about that
                        // -- I've got a degree in computer science, that's what.
                        // yeah ok. 

                        try {

                            string srcHash = src.HashBlock(filename, block);  // we should worry if this throws an error, it did throw an error.
                            string dstHash = dst.HashBlock(filename, block); 
                            if (srcHash.Equals(dstHash)) copyBlock = false;

                            WriteDebug(String.Format("SyncFiles::copy src hash: {0}",srcHash));
                            WriteDebug(String.Format("SyncFiles::copy dst hash: {0}",dstHash));

                        } catch {
                            copyBlock = true;
                        }
                    }
                }
                WriteDebug("SyncFiles::copy copyBlock: " + copyBlock);
                if (copyBlock)
                {   
                    WriteDebug("SyncFiles::copy ReadBlock: " + filename + ", "+block);

                    b = src.ReadBlock(filename, block); // throw error report file failure
                    if (!whatif)
                        dst.WriteBlock(filename, block, b);
                }
                if (bytesxfered + LocalIO.g_blocksize > srcLength)
                    bytesxfered = srcLength;
                else
                    bytesxfered += LocalIO.g_blocksize;

                // update progress
                if (prog != null)
                {
                    prog.PercentComplete = 100;
                    // Console.WriteLine(String.Format("{0} {1}", bytesxfered, srcInfo.Length));
                    // add b/s and eta

                    if (srcLength != 0)
                        prog.PercentComplete = (int)(100 * bytesxfered / srcLength);
                    else 
                        prog.PercentComplete = 100;

                    WriteProgress(prog);
                }
                block++;
            } while (bytesxfered < srcLength);

			// Set Attributes



			if (prog != null)
			{
				prog.RecordType = ProgressRecordType.Completed;
				WriteProgress(prog);
			}
        }

				/* ARGUMENT LOGIC

				due to arguments passed in beyond our control, destination file will not be reliable.

				DESTINATION; Direcory or not exist
				(ignore the following allowed matrix)

				we also must check for multiple sources 
				Source: exist single file.  Destination: existing file, existing directory, non existing directory (makedir).

				Source: exist single directory.  Destination: directory.

				Source: exist files & directories
				*/


				// -a archive mode; rlptgoD
				// recurse, links, permissions, times, group, owner, devices
                // archive rsync options (-a) assumed to always be active: NOT IMPLEMENTED
                // recursive (-r implemented by -a)
                // copy links (maybe difficult for windows, symlinks are privileged)
                // preserve permissions (attributes & acl) or if implementing -Extended attributes only
                // preserve times; System.IO.File.GetAttributes(path) FileSystemInfo
                // preserve group
                // preserve owner 
                // Devices (N/A)

                //    -H, --hard-links            preserve hard links
                //    -p, --perms                 preserve permissions
                //    -E, --executability         preserve executability
                //    -A, --acls                  preserve ACLs (implies -p)
                //    -X, --xattrs                preserve extended attributes
                //    -o, --owner                 preserve owner (super-user only)
                //    -g, --group                 preserve group
                //        --devices               preserve device files (super-user only)
                //        --specials              preserve special files
                //    -t, --times                 preserve modification times

                // TODO: (these are possible options that could be implemented relatively easily)
                // going to put future implementable options here

                
                // -Checksum copy based on checksum not date/size IMPLEMENTED
                // -Update skip newer files in destination IMPLEMENTED
                // -inplace (normal operation is to write to temporary file and rename)
                // -WhatIf (aka dry run) IMPLEMENTED
                // -Whole don't perform block check IMPLEMENTED
                // - don't cross reparse points
                // checksum block size  -> syncio
                // -Delete (delete files that only exist in the destination)
                // -minSize / -maxsize
                // -compress (maybe)
                // -Extended (acl)


// putting all file matching here
// files that return false are not shown.
// different from skipping a file based on checksum/ date/ size
        Boolean includefile(string file)
        {
            WriteDebug("syncfiles::includefile() " + file);

            bool include = true;
            if (includeList != null)
            {
                include = false;
                foreach (string m in includeList)
                {
					// string, string, method
					Match me = Regex.Match(file,m);
					if (me.Success) { include = true; }
                    // if (LikeOperator.LikeString(file, m, Microsoft.VisualBasic.CompareMethod.Text)) { include = true; }
                }
            }

            if (excludeList != null)
            {
                foreach (string m in excludeList)
                {
					Match me = Regex.Match(file,m);
					if (me.Success) { include = false; }
                                // if (LikeOperator.LikeString(file, m, Microsoft.VisualBasic.CompareMethod.Text)) { include = false; }
                }
            }
            return include;
        }

        // copy skipping here (file name still shows in verbose)

        Boolean skipfile (IO src, IO dst, string p)
        {
            WriteDebug("syncfiles::skipfile: !exist " + p);
//         -c, --checksum              skip based on checksum, not mod-time & size
            // full path not relative
            if (!dst.Exists(p))
                return false;
            WriteDebug("syncfiles::skipfile: checksum " + p);

            if (checksum)
            {
                string shash = src.HashTotal(p);
                string dhash = dst.HashTotal(p);
                if (dhash == shash)
                    return true;
            }

//         -u, --update                skip files that are newer on the receiver
            WriteDebug("syncfiles::skipfile: update " + p);

            if (update)
            {
                long updatetime = src.GetModificationTime(p).CompareTo(dst.GetModificationTime(p));
                if (updatetime < 0 )
                    return true;
            }
            WriteDebug("syncfiles::skipfile: sizeonly " + p);

            if (sizeonly) 
            {
                if (src.GetLength(p) == dst.GetLength(p))
                    return true;
            } 

            WriteDebug("syncfiles::skipfile: length and time " + p);


            DateTime stime = src.GetModificationTime(p);
            DateTime dtime = dst.GetModificationTime(p);

            long timediff = stime.CompareTo(dtime);
            WriteDebug(String.Format("syncfiles::skipfile: length: {0} srctime: {1} destime: {2} diff: {3}", src.GetLength(p), stime, dtime, timediff));

            if ((src.GetLength(p) == dst.GetLength(p)) && timediff==0)
                return true;

            WriteDebug("syncfiles::skipfile: false " + p);


            return false;
        }

        void transfer (IO src, IO dst)
        {
            WriteDebug("syncfiles::transfer starting");
            string apath = src.AbsPath();
            // string rel = src.GetDirectoryName();
			ProgressRecord prog = null;
            // wild card expansion only
            Collection<string> expath = src.ExpandPath(apath);
			src.Parent();
            foreach (string spath in expath)
            {
                WriteDebug("syncfiles::transfer path: " +spath);

                string srpath = src.GetRelative(spath);

                if (src.IsDir(srpath))
                {
                    WriteDebug(String.Format("syncfiles::transfer transfer path MakeDir: {0}",dst.AbsPath(srpath)));
                    dst.MakeDir(srpath);  // relative to the destination path
                    try {
                        CopyMetadata(srpath, src, dst);
                    } catch (Exception e) {
                        WriteWarning("Failed to Set attributes: " + srpath);
                    }
                } 

                Collection<string> allpath = src.ReadDir(spath);
                foreach (string sourcetarget in allpath)
                {
                    WriteDebug("syncfiles::transfer sourcetarget: " +sourcetarget);

                    // copylist.Add(sourcetarget);
                    if (includefile(sourcetarget))
                    {
                        Count++;
                        string relpath  = src.GetRelative(sourcetarget);


                        if (progress)
                            prog = new ProgressRecord(1, relpath, "Copying");

                        // Console.WriteLine("src: {0}",file);
                        WriteVerbose(relpath);
                      //  SyncStat srcType = src.GetInfo(sourcetarget);  // relative from src basepath *** this is really really important ***
						//SyncStat dstType = dst.GetInfo(sourcetarget);

                        WriteDebug("syncfiles::transfer isDir  " + src.AbsPath(relpath));

                       // long l = src.GetLength(relpath);
                        FileAttributes f = src.GetAttributes(relpath);

                        if (src.IsDir(relpath))
                        {
                            WriteDebug(String.Format("syncfiles::transfer MakeDir: {0}",dst.AbsPath(relpath)));
                            dst.MakeDir(relpath);  // relative to the destination path
                            CopyMetadata(relpath, src, dst);

                        } else {

                            WriteDebug( String.Format("syncfiles::transfer skip/copy: {0}",dst.AbsPath(relpath)));
                            //dst.copyfrom(cdir,file,prog);
							if (!skipfile(src,dst,relpath)) { // better name required
                                WriteDebug(String.Format("syncfiles::transfer will Copy file: {0}",dst));

                                try {

                        /* is this the only one, only one, only one, let me go */
	                                copy(relpath,src,dst,prog);
                                    CopyMetadata(relpath, src, dst);
                                } catch (IOException e) {
                                    ErrorRecord er = new ErrorRecord(e,"Transfer",ErrorCategory.OpenError,null);
                                    WriteError (er);
                                    // increase error counter

                                }
                            }
                      //      if (progress)
                       //         prog.close();
                        }
                    }
                }
            }
        }

        // Override the ProcessRecord method to process

        protected override void ProcessRecord()
        {
            IO src;
			IO tdst;
            //IO dst;

            Collection<string> copylist = new Collection<string>();
            //ProgressRecord prog = null;
            Count = 0;
            try
            {
                tdst = DestinationIO(tosession,target);

                foreach (string p in path)
                {
                    WriteDebug("syncfiles::ProcessRecord Path[]: " + p);
                    src= SourceIO (fromsession,p);
                    transfer (src,tdst);
                }

  
            }
            catch (Exception e)
            {
                WriteWarning(String.Format("Fatal Error: {0}",e.Message));
                WriteDebug(e.StackTrace);
                ErrorRecord er = new ErrorRecord(e, "TopLevel", ErrorCategory.ObjectNotFound, null);
                WriteError(er);
            }

        }
    }

}
