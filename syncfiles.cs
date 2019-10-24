using System.Management.Automation;  // Windows PowerShell assembly.
using System.Management.Automation.Runspaces;
//using System.Management.Automation.Runspaces.Pipeline;
using System;
using System.IO;
using System.Linq;
using System.Collections.ObjectModel;
using System.Security;
using Microsoft.VisualBasic.CompilerServices;

namespace SyncPath
{
    // Declare the class as a cmdlet and specify the
    // appropriate verb and noun for the cmdlet name.

    [Cmdlet(VerbsData.Sync, "ChildItems")]
    public class SyncPathCommand : PSCmdlet
    {
        IO src;
        IO dst;

        // .net standard 2.0 does not have this function
        // taken from  https://stackoverflow.com/questions/275689/how-to-get-relative-path-from-absolute-path/340454#340454
        public static String MakeRelativePath(String fromPath, String toPath)
        {

            if (String.IsNullOrEmpty(fromPath)) throw new ArgumentNullException("fromPath");
            if (String.IsNullOrEmpty(toPath)) throw new ArgumentNullException("toPath");

            Uri fromUri = new Uri(fromPath);
            Uri toUri = new Uri(toPath);

            if (fromUri.Scheme != toUri.Scheme) { return toPath; } // path can't be made relative.

            Uri relativeUri = fromUri.MakeRelativeUri(toUri);
            String relativePath = Uri.UnescapeDataString(relativeUri.ToString());

            if (toUri.Scheme.Equals("file", StringComparison.InvariantCultureIgnoreCase))
            {
                relativePath = relativePath.Replace(System.IO.Path.AltDirectorySeparatorChar, System.IO.Path.DirectorySeparatorChar);
            }
            // WriteDebug(String.Format("MakeRelativePath {0} -> {1} = {2}\n", fromPath, toPath, relativePath));

            return relativePath;
        }


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
        public string Path
        {
            get { return path; }
            set { path = value; }
        }
        private string path;

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


        // Override the ProcessRecord method to process
        // the supplied user name and write out a
        // greeting to the user by calling the WriteObject
        // method.
        protected override void ProcessRecord()
        {

            Collection<string> filelist;
            try
            {
                if (fromsession != null)
                {
                    src = new RemoteIO(fromsession);
                }
                else
                {
                    src = new LocalIO(this.SessionState);
                }

                if (tosession != null)
                {
                    dst = new RemoteIO(tosession);
                }
                else
                {
                    dst = new LocalIO(this.SessionState);
                }

               // string curPath = this.SessionState.Path.CurrentFileSystemLocation.ToString(); //System.IO.Directory.GetCurrentDirectory();

                // Determine if path and target are rel or abs
                string abssrc;
                string absdst;

                // also check for UNC paths
                if (!path.Contains(":\\") && !(path.StartsWith("\\\\")))
                {
                    abssrc = System.IO.Path.Combine(src.GetCwd(), path);
                }
                else
                {
                    abssrc = path;
                }

                if (!target.Contains(":\\") && !(target.StartsWith("\\\\")))
                {
                    string dstcwd = dst.GetCwd();
                    WriteDebug("Dst cwd:" + dstcwd);
                    absdst = System.IO.Path.Combine(dstcwd, target);
                }
                else
                {
                    absdst = target;
                }

                SyncStat srcType = src.GetInfo(abssrc);
                if (!srcType.Exists)
                    throw (new System.IO.FileNotFoundException()); // fatal error, goodbye

                if (srcType.isDir())
                {
                    if (!abssrc.EndsWith("\\")) abssrc += "\\";

                    try
                    {
                        SyncStat ss = dst.GetInfo(absdst); // throws a cast exception
                        if (!ss.Exists)
                        {
                            WriteVerbose(absdst);
                            dst.MakeDir(absdst);
                        }
                        else
                        {
                            WriteVerbose("SKIPPING " + absdst);
                            WriteDebug("Directory exists");
                        }
                    }
                    catch (Exception e) // remote throws different exception to local
                    {
                        WriteDebug(e.Message);
                        WriteDebug(e.StackTrace);
                        WriteDebug(String.Format("exception MakeDir {0}", absdst));

                        WriteVerbose(absdst);
                        dst.MakeDir(absdst);
                    }

                    filelist = src.ReadDir(abssrc);
                }
                else
                {
                    filelist = new Collection<string> { abssrc };
                }
                // Determine if src is file or dir

                // is target absolute or relative
                // get dst cwd
                if (!absdst.EndsWith("\\")) absdst += "\\";

                WriteDebug(String.Format("Arguments {0} -> {1}\n", abssrc, absdst));


                ProgressRecord prog=null;
                if (progress)
                    prog = new ProgressRecord(1, abssrc, "Copying");

                int count = 0;
                foreach (string file in filelist)
                {
                    bool include = true;

                    // add include / exclude
                    if (includeList != null)
                    {
                        include = false;
                        foreach (string m in includeList)
                        {
                            if (LikeOperator.LikeString(file, m, Microsoft.VisualBasic.CompareMethod.Text)) { include = true; }
                        }
                    }

                    if (excludeList != null)
                    {
                        foreach (string m in excludeList)
                        {
                            if (LikeOperator.LikeString(file, m, Microsoft.VisualBasic.CompareMethod.Text)) { include = false; }
                        }
                    }
                    count++;
                    if (include)
                    {
                        if (progress)
                            prog = new ProgressRecord(1, file, "Copying");

                        try
                        {
                            string relfile = MakeRelativePath(abssrc, file);
                            string dstfile = System.IO.Path.Combine(absdst, relfile);

                            SyncStat srcInfo = src.GetInfo(file);  // may throw
                            // sometimes getting nulls
                            if (srcInfo.isDir())
                            {
                                WriteDebug(String.Format("src isDir: {0}", file));
                                try
                                {
                                    SyncStat ss = dst.GetInfo(dstfile);
                                    if (!ss.Exists)
                                    {
                                        WriteVerbose(relfile);
                                        dst.MakeDir(dstfile);
                                    }
                                    else
                                    {
                                        WriteVerbose(String.Format("{0}/{1} SKIPPING {2}", count, filelist.Count, relfile));
                                        WriteDebug("Directory Exists");
                                    }
                                }
                                catch (Exception)
                                {
                                    WriteVerbose(relfile);
                                    dst.MakeDir(dstfile);
                                }
                            }
                            else
                            {
                                //  FileInfo srcfInfo = (FileInfo)srcInfo;

                                WriteDebug(String.Format("Compare and Copy from {1} to {0}", dstfile, file));
                                // check if dst exists
                                // compare relfile	
                                SyncStat dstInfo;
                                try
                                {

                                    dstInfo = dst.GetInfo(dstfile); // don't care if throws
                                    
                                    if (!dstInfo.Exists)
                                    {
                                        WriteVerbose(relfile);
                                        WriteDebug("COPY NEW");

                                        if (progress)
                                        {
                                            prog = new ProgressRecord(1, file, "Copying");
                                            prog.StatusDescription = String.Format("Copying {0}/{1}", count, filelist.Count);
                                        }

                                         copy(file, dstfile,  prog);

                                    }
                                    else if  (srcInfo.Length != dstInfo.Length)
                                    {
                                        WriteVerbose(relfile);
                                        WriteDebug("COPY UPDATE (length)");

                                        if (progress)
                                        {
                                            prog = new ProgressRecord(1, file, "Copying");
                                            prog.StatusDescription = String.Format("Copying {0}/{1}", count, filelist.Count);
                                        }


                                        copy(file, dstfile, prog);
                                    }

                                    else if (Checksum) // if checksum specified check on chksum rather than length and date. but different length should be a dead giveaway.
                                    {
                                       // WriteVerbose(relfile);
                                        WriteDebug("UPDATE (checksum)");

                                        if (progress)
                                        {
                                            prog = new ProgressRecord(1, file, "Checking");
                                            prog.StatusDescription = String.Format("Checking {0}/{1}", count, filelist.Count);
                                            prog.PercentComplete = 0;

                                            WriteProgress(prog);
                                        }
                                        // these can be done simultaneously... especially if one or both are on a remote machine.
                                        string srcHash = src.HashTotal(file);
                                        if (progress)
                                        {
                                            prog.PercentComplete = 50;
                                            WriteProgress(prog);
                                        }
                                        string dstHash = dst.HashTotal(dstfile);
                                        if (progress)
                                        {
                                            prog.PercentComplete = 100;
                                            WriteProgress(prog);
                                        }

                                        if (!srcHash.Equals(dstHash))
                                        {
                                            WriteVerbose(relfile);
                                            WriteDebug(String.Format("COPY UPDATE. file hash mismatch: {0} <-> {1}", srcHash, dstHash));
                                            // WriteDebug("COPY update hash");

                                            if (progress) {
                                                prog = new ProgressRecord(1, file, "Copying");
                                                prog.StatusDescription = String.Format("Copying {0}/{1}", count, filelist.Count);
                                            }
                                            copy(file, dstfile, prog);

                                        }
                                        else
                                        {
                                            WriteVerbose(String.Format("{0}/{1} SKIPPING {2}", count, filelist.Count, relfile));
                                            WriteDebug("File hash match");
                                        }
                                    }
                                    else if (srcInfo.LastWriteTimeUtc > dstInfo.LastWriteTimeUtc)
                                    {
                                        WriteVerbose(relfile);
                                        WriteDebug("COPY UPDATE (date)");

                                        if (progress)
                                        {
                                            prog = new ProgressRecord(1, file, "Copying");
                                            prog.StatusDescription = String.Format("Copying {0}/{1}", count, filelist.Count);
                                        }
                    
                                            copy(file, dstfile, prog);

                                    }
                                    else
                                    {
                                        if (progress && this.MyInvocation.BoundParameters.ContainsKey("Verbose"))
                                        {
                                            prog.RecordType = ProgressRecordType.Completed;
                                        }

                                            WriteVerbose(String.Format ("{0}/{1} SKIPPING {2}" ,count,filelist.Count, relfile));
                                        WriteDebug("Date-length match");
                                    }
                                }
                                catch (Exception e)
                                {

                                    WriteDebug("CHECK : " + file + " : " + e.GetType().ToString() +" : " + e.Message);

                                    // could be a destination not exist

                                    WriteVerbose(relfile);
                                    WriteDebug("COPY NEW");

                                    if (progress)
                                    {
                                        WriteDebug("Setup Progress");
                                        prog = new ProgressRecord(1, file, "Copying");
                                        prog.StatusDescription = String.Format("Copying {0}/{1}", count, filelist.Count);
                                    }

                                    copy(file, dstfile, prog);

                                    /*
                                    WriteVerbose(String.Format("{0} -> {1}\n", relfile, absdst));
                                    WriteVerbose(e.Message);
                                    WriteVerbose(e.StackTrace);

                                    ErrorRecord er = new ErrorRecord(e, "CopyNew", ErrorCategory.InvalidOperation, null);
                                    WriteError(er);

                                    WriteDebug(e.Message);
                                    WriteDebug(e.StackTrace);
                                    WriteDebug("COPY NEW Exception");
                                    */
                                }
                            }
                            //optional sync attributes and dates
                            dst.SetInfo(dstfile, srcInfo);
                            // copy acl 
                        }
                        catch (Exception e)
                        {
                            //WriteWarning("FAILED : " + file + " : " + e.Message);
                            // WriteVerbose(e.StackTrace);
                            ErrorRecord er = new ErrorRecord(e, "FileCopy", ErrorCategory.ReadError, null);
                            WriteError(er);
                        }
                        if (progress && !this.MyInvocation.BoundParameters.ContainsKey("Verbose"))
                        {
                            prog.PercentComplete = 100;
                            prog.StatusDescription = String.Format("Copying {0}/{1}", count, filelist.Count);
                            WriteProgress(prog);
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
