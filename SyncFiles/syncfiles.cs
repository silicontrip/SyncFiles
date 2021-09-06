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
        IO src;
        IO dst;

        protected void copy(string srcFile, string dstFile, ProgressRecord prog)
        {

            Int64 bytesxfered = 0;
            Int32 block = 0;
            Byte[] b;

            SyncStat srcInfo = src.GetInfo(srcFile);
            SyncStat dstInfo;
            try
            {
                dstInfo = dst.GetInfo(dstFile);
            } catch
            {
                dstInfo = new SyncStat();
            }
  //          do some clever compare

            do
            {
                WriteDebug("Do Block copy: " + block);
                bool copyBlock = true;
                if (dstInfo.Exists)
                {
                    // doesn't look very clever to me
                    string srcHash = src.HashBlock(srcFile, block);  // we should worry if this throws an error
                    try { 
                        string dstHash = dst.HashBlock(dstFile, block); 
                        if (srcHash.Equals(dstHash)) copyBlock = false;
                    } catch {
                        copyBlock = true;
                    }
                }
                WriteDebug("will copy block: " + copyBlock);
                if (copyBlock)
                {
                    b = src.ReadBlock(srcFile, block); // throw error report file failure
                    dst.WriteBlock(dstFile, block, b);
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

    private Collection<IO> IOFactory(PSSession session,string[] path)
        {
            List<IO> src = new List<IO>();

            foreach (string p in path)
            {
                Collection<string> expath;
                if (session != null)
                {
                    expath = RemoteIO.ExpandPath(p,session);
                } else {
                    expath = LocalIO.ExpandPath(p,this.SessionState);
                }

			// Console.WriteLine ("expanded from path: {0} -> {1} ",p,String.Join(", ",expath));

                // expath should all be absolute
                foreach (string ep in expath)
                {
                    WriteDebug(String.Format("Expanded: {0}",ep));
                    if (session != null)
                    {
		                WriteDebug(String.Format("Remote add: {0}",ep));
                        src.Add( new RemoteIO(session,ep));
                    }
                    else
                    {
		                WriteDebug(String.Format("local add: {0}",ep));
                        src.Add( new LocalIO(this.SessionState,ep));
                    }
                }

            }
            return new Collection<IO> (src);
        }

        private void copy(IO src,string srcFile, IO dst, string dstFile, ProgressRecord prog)
        {

            Int64 bytesxfered = 0;
            Int32 block = 0;
            Byte[] b;

            SyncStat srcInfo = src.GetInfo(srcFile);
            SyncStat dstInfo;
            try
            {
                dstInfo = dst.GetInfo(dstFile);
            } catch
            {
                dstInfo = new SyncStat();
            }
  //          do some clever compare

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
                    string srcHash = src.HashBlock(srcFile, block);  // we should worry if this throws an error
                    try { 
                        string dstHash = dst.HashBlock(dstFile, block); 
                        if (srcHash.Equals(dstHash)) copyBlock = false;
                    } catch {
                        copyBlock = true;
                    }
                }
                WriteDebug("will copy block: " + copyBlock);
                if (copyBlock)
                {
                    b = src.ReadBlock(srcFile, block); // throw error report file failure
                    dst.WriteBlock(dstFile, block, b);
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
        }

        // Override the ProcessRecord method to process

        protected override void ProcessRecord()
        {
            Collection<IO> src;
			Collection<IO> tdst;
            IO dst;

            ProgressRecord prog = null;
            try
            {
               // string curPath = this.SessionState.Path.CurrentFileSystemLocation.ToString(); //System.IO.Directory.GetCurrentDirectory();
                if (fromsession != null)
                {
                    src= IOFactory (fromsession,path);
                }
                else
                {
                   // Console.WriteLine("src local: {0}",target);
                    src = IOFactory(null,path);
                }

            // target should not be expandable
            // well only if it expands into 1 item
                if (tosession != null)
                {
                  //  Console.WriteLine("dst remote: {0}",target);
                    //dst = new RemoteIO(tosession,target);
					tdst = IOFactory(tosession,target);
                }
                else
                {
                  //  Console.WriteLine("dst local: {0}",target);
                    //dst = new LocalIO(this.SessionState,target);
					tdst = IOFactory(null,target);
                }

				if (tdst.Length > 1)
					throw "Ambiguous destination.";

				dst = tdst[0];

                int count = 0;
                foreach (IO cdir in src)
                {
                   // WriteVerbose(cdir.AbsPath());
                    Collection<string> dd = cdir.ReadDir();

                    foreach (string file in dd)
                    {
                        // Move these into match strategy class
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
// -Update, skip newer files in destination
// -inplace (normal operation is to write to temporary file and rename)
// -WhatIf (aka dry run)
// -Whole don't perform block check
// - don't cross reparse points
// checksum block size
// -Delete (delete files that only exist in the destination)
// -minSize / -maxsize
// -compress (maybe)
// -Extended (acl)
// -Links, copy reparse target (https://gist.github.com/LGM-AdrianHum/260bc9ab3c4cd49bc8617a2abe84ca74)

                        count++;
                        if (include)
                        {
                            if (progress)
                                prog = new ProgressRecord(1, file, "Copying");
                            try {
                            // Console.WriteLine("src: {0}",file);
                                WriteVerbose(file);
                                SyncStat srcType = cdir.GetInfo(file);  // relative from src basepath
                                if (srcType.isDir())
                                {
                                // Console.WriteLine ("MKDIR: {0}",dst.DestinationCombine(file));
                                    dst.MakeDir(file);
                                } else {

                                // Console.WriteLine( "COPY TO: {0}",dst.DestinationCombine(file));
                                //dst.copyfrom(cdir,file,prog);
                                    copy(cdir,file,dst,file,prog);
                                    dst.SetInfo(file,cdir.GetInfo(file));
                                }
                            } catch (Exception e) {
                                ErrorRecord er = new ErrorRecord(e, "Copy", ErrorCategory.WriteError, null);
                                WriteError(er);
                            }
                        }
                    }
                }    
            }
            catch (Exception e)
            {
                WriteWarning("Fatal Error");
                WriteDebug(e.Message);
                WriteDebug(e.StackTrace);
                ErrorRecord er = new ErrorRecord(e, "TopLevel", ErrorCategory.ObjectNotFound, null);
                WriteError(er);
            }

        }
    }

}
