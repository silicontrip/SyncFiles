using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Management.Automation;
using System.Management.Automation.Runspaces;

using SyncPath;

namespace DirectoryUsage
{
    [Cmdlet(VerbsCommon.Get, "DirectoryUsage")]
    public class GetDiskUsageCommand : PSCmdlet
    {

        IO src;
        Dictionary<string, int> fileResults;

        [Alias("FullName")]
        [Parameter(Position = 0, ValueFromPipelineByPropertyName = true)]
        public string Path
        {
            get { return path; }
            set { path = value; }
        }
        private string path=".\\";

        [Parameter()]
        public PSSession PSSession
        {
            get { return pssession; }
            set { pssession = value; }
        }
        private PSSession pssession;

        [Parameter()]
        public int BlockSize
        {
            get { return blocksize; }
            set { blocksize = value; }
        }
       // private int blocksize = 512; // probably doesn't make sense in windows
        private int blocksize = 1; 


        [Parameter()]
        public SwitchParameter All
        {
            get { return all; }
            set { all = value; }
        }
        private Boolean all;

        [Parameter()]
        public int MaxDepth
        {
            get { return maxdepth; }
            set { maxdepth = value; }
        }
        private int maxdepth;

        [Parameter()]
        public SwitchParameter Total
        {
            get { return total; }
            set { total = value; }
        }
        private Boolean total;

        [Parameter()]
        public SwitchParameter SeperateDir
        {
            get { return seperatedir; }
            set { seperatedir = value; }
        }
        private Boolean seperatedir;

        [Parameter()]
        public SwitchParameter Summarize
        {
            get { return summarize; }
            set { summarize = value; }
        }
        private Boolean summarize;

        [Parameter()]
        public SwitchParameter Mega
        {
            get { return true; }
            set { blocksize = 1048576; }
        }

        [Parameter()]
        public SwitchParameter Kilo
        {
            get { return true; }
            set { blocksize = 1024; }
        }

        [Parameter()]
        public SwitchParameter Bytes
        {
            get { return true; }
            set { blocksize = 1; }
        }

        void WriteResult (ulong sz, string p)
        {
            PSObject o = new PSObject();


            string[] props = { "Length", "FullName" };
            PSPropertySet ps = new PSPropertySet("DefaultDisplayPropertySet", props);
            PSMemberSet pm = new PSMemberSet("PSStandardMembers", new PSMemberInfo[] { ps });
           // pm.Members.Add();

            o.Members.Add(pm);

            o.Properties.Add(new PSNoteProperty("Length",sz));
            o.Properties.Add(new PSNoteProperty("FullName",p));
            WriteObject(o);
        }

        ulong TotalFiles (Collection<string> f)
        {
            ulong tt = 0;
            foreach (string fl in f)
            {
                ulong fsz=0;
                try
                {
                    SyncStat fi = src.GetInfo(fl);
                    double sz = fi.Length * 1.0 / blocksize;
                    fsz = (ulong)Math.Ceiling(sz);
                    tt += fsz;
                } catch (Exception e)
                {
                    WriteWarning(e.Message);
                    WriteDebug(e.StackTrace);
                }
                if (all) 
                {
                    WriteResult(fsz,fl);
                    // fileResults.Add(fl, fsz);
                }
            }
            return tt;
        }

        ulong ProcessDir(string p)
        {
            ulong dsize = 0;

            // check for symlink
            //Console.Write("{0} -> {1}\n",p,ss.Attributes);
           
                SyncStat ss = src.GetInfo(p);

                if ((ss.Attributes & System.IO.FileAttributes.ReparsePoint) == 0)
                {
                    if ((ss.Attributes & System.IO.FileAttributes.Directory) != 0)
                    {

                        Collection<string> listDirectories = src.GetDirs(p);
                        if (SeperateDir)
                        {
                            // don't add dirs
                            foreach (string d in listDirectories)
                            {
                                try
                                {
                                    ProcessDir(d);
                                }
                                catch (Exception e)
                                {
                                    WriteWarning(e.Message);
                                    WriteDebug(e.StackTrace);
                                }
                            }
                        }
                        else
                        {
                            foreach (string d in listDirectories)
                            {
                                try
                                {
                                    dsize += ProcessDir(d);
                                }
                                catch (Exception e)
                                {
                                    WriteWarning(e.Message);
                                    WriteDebug(e.StackTrace);
                                }
                            }
                        }

                        Collection<string> listFiles = src.GetFiles(p);
                        dsize += TotalFiles(listFiles);
                    } else
                    {
                        double sz = ss.Length * 1.0 / blocksize;
                        ulong fsz = (ulong)Math.Ceiling(sz);
                        dsize += fsz;
                    }
                    if (!summarize)
                    {
                        // fileResults.Add(p, dsize);
                        WriteResult(dsize, p);
                    }
                }
           
            return dsize;
            
        }


        protected override void ProcessRecord()
        {
            try
            {
                if (pssession != null)
                {
                    src = new RemoteIO(pssession);
                }
                else
                {
                    src = new LocalIO(this.SessionState);
                }

                fileResults = new Dictionary<string,int>();

                // perform path/wildcard expansion
                // (harder than I expected) 

                ulong ltotal = 0;
               // ProviderInfo pi;

                Collection<string> resolvedPaths = src.ExpandPath(path);

                foreach (string p in resolvedPaths)
                {
                    //string pattern = Path.GetFileName(p);

                    WriteDebug("path: " + p);

                    ulong summ = ProcessDir(p);
                    ltotal += summ;
                    if (summarize)
                        WriteResult(summ, p);
                }

                if (total)
                    WriteResult (ltotal,"Total");



            } catch (Exception e)
            {
                WriteWarning("Fatal Error");
                WriteWarning(e.Message);
                WriteDebug(e.StackTrace);
                ErrorRecord er = new ErrorRecord(e, "TopLevel", ErrorCategory.ObjectNotFound, null);
                WriteError(er);
            }

        }
    }
}
