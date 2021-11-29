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

        private Collection<IO> IOFactory(PSSession session,string basepath, string element)
        {
            List<IO> src = new List<IO>();
                //WriteDebug(String.Format("Expanding: {0}",p));
            Collection<string> expath;

            string cpath = Path.Combine(basepath, element);

            if (session != null)
            {
                expath = RemoteIO.ExpandPath(cpath,session);
            } else {
                expath = LocalIO.ExpandPath(cpath,this.SessionState);
            }

            //WriteDebug (String.Format("expanded path count: {0}",expath.Count));
			// WriteDebug (String.Format("expanded from path: {0} -> {1} ",p,String.Join(", ",expath)));
            // so what causes a 0 length ExpandPath.
            // sould've been obvious, a non existent path
            // We don't know if this is a source or destination path, in order to handle non existant paths
            // expath should all be absolute

            foreach (string ep in expath)
            {
                //WriteDebug(String.Format("Expanded: {0}",ep));
                if (session != null)
                {
                    //WriteDebug(String.Format("Remote add: {0}",ep));
                    src.Add( new RemoteIO(session,ep));
                }
                else
                {
                    //WriteDebug(String.Format("local add: {0}",ep));
                    src.Add( new LocalIO(this.SessionState,ep));
                }
            }

            //WriteDebug("IOFactory exit.");
            return new Collection<IO> (src);
        }

        // private void copy(IO src,string srcFile, IO dst, string dstFile, ProgressRecord prog)
        private void copy(string srcFile, IO src, string dstFile, IO dst, ProgressRecord prog)
        {

            Int64 bytesxfered = 0;
            Int32 block = 0;
            Byte[] b;

            SyncStat srcInfo = src.GetInfo(srcFile);
            SyncStat dstInfo;
            try
            {
            	dstInfo = dst.GetInfo(dstFile);
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
                    string srcHash = src.HashBlock(srcFile, block);  // we should worry if this throws an error
                    try { 
                        string dstHash = dst.HashBlock(dstFile, block); 
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
			if (prog != null)
			{
				prog.RecordType = ProgressRecordType.Completed;
				WriteProgress(prog);
			}

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
                foreach (string p in path)
                {
                    // string curPath = this.SessionState.Path.CurrentFileSystemLocation.ToString(); //System.IO.Directory.GetCurrentDirectory();

                    string fbase = System.IO.Path.GetDirectoryName(p);
                    string felem = System.IO.Path.GetFileName(p);

                    if (fromsession != null)
                    {
                        //WriteDebug("From Session:");
                        src= IOFactory (fromsession,fbase,felem);
                    }
                    else
                    {
                        // Console.WriteLine("src local: {0}",target);
                        // WriteDebug("local path.");
                        src = IOFactory(null,fbase,felem);
                    }
                }
                if (src.Count == 0)
                    throw new FileNotFoundException(path[0]);

                //foreach (IO selm in src)
                //    WriteDebug("source element:" + selm.AbsPath());

                //  WriteDebug("source path: " + path[0] + " :: " + src[0].AbsPath());

                // target should not be expandable
                // well only if it expands into 1 item

				// string[] ta = new string[] {target};
                if (tosession != null)
                {
                    //WriteDebug(String.Format("dst remote: {0}",target));
					tdst = IOFactory(tosession,target,"");

                }
                else
                {
                    //WriteDebug(String.Format("dst local: {0}",target));
					tdst = IOFactory(null,target,"");

                }

                // going to work around the . expanding to .\*


				/* ARGUMENT LOGIC

				due to arguments passed in beyond our control, destination file will not be reliable.

				DESTINATION; Direcory or not exist
				(ignore the following allowed matrix)

				we also must check for multiple sources 
				Source: exist single file.  Destination: existing file, existing directory, non existing directory (makedir).

				Source: exist single directory.  Destination: directory.

				Source: exist files & directories
				*/

				if (tdst.Count == 0)
				{
					// dst = new IO ();
					if (tosession != null)
					{
						//WriteDebug(String.Format("dst remote: {0}",target));
						dst = new RemoteIO(tosession,target);
						dst.MakeDir(target);
					}
					else
					{
						//WriteDebug(String.Format("dst local: {0}",target));
						dst = new LocalIO(this.SessionState,target);
						dst.MakeDir(target);
					}
				}
				else if (tdst.Count == 1)
					dst = tdst[0];
				else if (target == "." && tdst.Count > 1)
                {
                    string pp  = System.IO.Path.GetDirectoryName(tdst[0].AbsPath());

					if (tosession != null)
					{
						//WriteDebug(String.Format("dst remote: {0}",target));
						dst = new RemoteIO(tosession,pp);
					} else {
                        dst = new LocalIO(this.SessionState,pp);
                    }
                } else
					throw new ArgumentException("Ambiguous destination.","Target");


                int count = 0;
                foreach (IO cdir in src)
                {
                    // I wonder if single files also have this issue
                    WriteDebug("foreach source: " + cdir.AbsPath());

                    // this must return relative (to argument) path
                    Collection<string> dd = cdir.ReadDir();

                    if (cdir.IsDir())
                        dd.Add(cdir.AbsPath());

                    // WriteDebug ("read dir" + dd.Join(", "));

                    foreach (string file in dd)
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
                        count++;
                        if (include)
                        {
                            if (progress)
                                prog = new ProgressRecord(1, file, "Copying");

                           // Console.WriteLine("src: {0}",file);
                            WriteVerbose(file);
                            SyncStat srcType = cdir.GetInfo(file);  // relative from src basepath *** this is really really important ***

                            if (srcType.isDir())
                            {
                               WriteDebug(String.Format("MKDIR: {0}",dst.DestinationCombine(file)));
                                dst.MakeDir(file);  // relative to the destination path
                            } else {

                               WriteDebug( String.Format("COPY TO: {0}",dst.DestinationCombine(file)));
                               //dst.copyfrom(cdir,file,prog);
                                copy(file,cdir,file,dst,prog);
                            }
                        }
                    }
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
