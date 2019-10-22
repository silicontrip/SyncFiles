using System;
using System.IO;
using System.Management.Automation;

namespace SyncPath
{

    public class SyncStat
    {
        public DateTime CreationTimeUtc;
        public DateTime LastWriteTimeUtc;
        public FileAttributes Attributes;
        public bool Exists;
        public long Length;

        public SyncStat()
        {
            this.Exists = false;
        }
        public SyncStat(FileSystemInfo p)
        {
            if ((p.Attributes & FileAttributes.Directory) == FileAttributes.Directory)
                this.Length = 0;
            else
                this.Length = ((FileInfo)p).Length;

            this.CreationTimeUtc = p.CreationTimeUtc;
            this.LastWriteTimeUtc = p.LastWriteTimeUtc;
            this.Attributes = p.Attributes;
            this.Exists = p.Exists;
        }

        public SyncStat(PSObject p)
        {
            this.Length = 0;
            foreach (PSMemberInfo pmi in p.Members)
            {
                // for my next trick, genericise this entire loop
                // so any object can be converted.
                if (pmi.Name.Equals("CreationTimeUtc"))
                    this.CreationTimeUtc = (DateTime)pmi.Value;
                if (pmi.Name.Equals("LastWriteTimeUtc"))
                    this.LastWriteTimeUtc = (DateTime)pmi.Value;
                if (pmi.Name.Equals("Length"))
                    this.Length = (long)pmi.Value;
                if (pmi.Name.Equals("Exists"))
                    this.Exists = (bool)pmi.Value;
                if (pmi.Name.Equals("Attributes"))
                    this.Attributes = (FileAttributes)Enum.Parse(typeof(FileAttributes), (string)pmi.Value, true);
            }

        }

        public bool isDir() { return ((this.Attributes & FileAttributes.Directory) == FileAttributes.Directory); }
    }
}
