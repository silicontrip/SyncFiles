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
            WriteDebug("abs; " + destio.AbsPath());  

			// is there a reason we are expanding the path ?
			// a long time ago this also performed absolute path expansion
			// now I'm just not sure why it's here

			FileAttributes destfa = destio.GetFileAttributes();

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
					WriteDebug("exp: " + expanded);
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
				WriteDebug("using remote IO");
                srcio = new RemoteIO(session,sp);
            } else {
                //expath = LocalIO.ExpandPath(cpath,this.SessionState);
								WriteDebug("using local IO");

                srcio = new LocalIO(this.SessionState,sp);
            }

            //string abspath = srcio.AbsPath();

			//WriteDebug("source IO abspath: " + abspath);


            //string tree = System.IO.Path.GetDirectoryName(abspath);
            //string leaf = System.IO.Path.GetFileName(abspath);

			//WriteDebug("source IO tree/leaf: " + tree +"|"+leaf);

            Collection<string> expandpath = srcio.ExpandPath(""); 

            if (expandpath.Count == 0)
            {
                throw new FileNotFoundException(sp);
            }
			/*
			 else    // if (expandpath.Length == 1) 
            {
                srcio.SetPath(abspath);
               // src.Add(srcio);
            } 
*/
            return srcio;

        }

        // private void copy(IO src,string srcFile, IO dst, string dstFile, ProgressRecord prog)
        private void copy(string filename, IO src, IO dst, ProgressRecord prog)
        {

            Int64 bytesxfered = 0;
            Int32 block = 0;
            Byte[] b;

            SyncStat srcInfo = src.GetInfo(filename);  // rel
            SyncStat dstInfo;
            try
            {
            	dstInfo = dst.GetInfo(filename); 
            } catch {
				dstInfo = new SyncStat();
            }

            // do some clever compare
            // src date newer dst date
            // src size != dst size
            // src chksum != dst chksum

            do
            {
                WriteDebug("Do Block copy: " + block);
                bool copyBlock = true;
                if (dstInfo.Exists)
                {
                    // doesn't look very clever to me
                    string srcHash = src.HashBlock(filename, block);  // we should worry if this throws an error
                    try { 
                        string dstHash = dst.HashBlock(filename, block); 
                        if (srcHash.Equals(dstHash)) copyBlock = false;

						WriteDebug(String.Format("src hash: {0}",srcHash));
						WriteDebug(String.Format("dst hash: {0}",dstHash));

                    } catch {
                        copyBlock = true;
                    }
                }
                WriteDebug("will copy block: " + copyBlock);
                if (copyBlock)
                {
                    b = src.ReadBlock(filename, block); // throw error report file failure
                    if (!whatif)
                        dst.WriteBlock(filename, block, b);
                }
                if (bytesxfered + LocalIO.g_blocksize > srcInfo.Length)
                    bytesxfered = srcInfo.Length;
                else
                    bytesxfered += LocalIO.g_blocksize;

                // update progress
                if (prog != null)
                {
                    prog.PercentComplete = 100;
                    // Console.WriteLine(String.Format("{0} {1}", bytesxfered, srcInfo.Length));
                    // add b/s and eta

                    if (srcInfo.Length != 0)
                        prog.PercentComplete = (int)(100 * bytesxfered / srcInfo.Length);
                    WriteProgress(prog);
                }
                block++;
            } while (bytesxfered < srcInfo.Length);

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

                // basic rsync options (-a) assumed to always be active
                // recursive
                // copy links (maybe difficult for windows, symlinks are privileged)
                // preserve permissions (attributes & acl) or if implementing -Extended attributes only
                // preserve times; System.IO.File.GetAttributes(path) FileSystemInfo
                // preserve group
                // preserve owner 
                // Devices (N/A)

                // TODO: (these are possible options that could be implemented relatively easily)
                // going to put future implementable options here

                // -Checksum copy based on checksum not date/size
                // -Update skip newer files in destination
                // -inplace (normal operation is to write to temporary file and rename)
                // -WhatIf (aka dry run)
                // -Whole don't perform block check
                // - don't cross reparse points
                // checksum block size
                // -Delete (delete files that only exist in the destination)
                // -minSize / -maxsize
                // -compress (maybe)
                // -Extended (acl)


// putting all file matching here
        bool includefile (string file)
        {
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

        void transfer (IO src, IO dst)
        {
            string apath = src.AbsPath();
            // string rel = src.GetDirectoryName();
			ProgressRecord prog = null;
            // wild card expansion only
            Collection<string> expath = src.ExpandPath(apath);

            foreach (string spath in expath)
            {
                Collection<string> allpath = src.ReadDir(spath);
                foreach (string sourcetarget in allpath)
                {
                    // copylist.Add(sourcetarget);
                    if (includefile(sourcetarget))
                    {
                        Count++;
                        string relpath  = src.GetRelative(sourcetarget);

                        if (progress)
                            prog = new ProgressRecord(1, relpath, "Copying");

                        // Console.WriteLine("src: {0}",file);
                        WriteVerbose(relpath);
                        SyncStat srcType = src.GetInfo(sourcetarget);  // relative from src basepath *** this is really really important ***

                        if (srcType.isDir())
                        {
                            WriteDebug(String.Format("MKDIR: {0}",dst.AbsPath(relpath)));
                            dst.MakeDir(relpath);  // relative to the destination path
                        } else {
                            WriteDebug( String.Format("COPY TO: {0}",dst.AbsPath(relpath)));
                            //dst.copyfrom(cdir,file,prog);
                            copy(relpath,src,dst,prog);
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
                    WriteDebug("source fromsession: " + p);
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
