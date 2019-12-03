using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Management.Automation;
using System.Management.Automation.Runspaces;
using System.Runtime.InteropServices;
using System.Security;

namespace SyncPath
{
    public interface IO
    {

        Collection<string> ReadDir(string p);
        void MakeDir(string p);
        SyncStat GetInfo(string p);
        void SetInfo(string p, SyncStat f);
        byte[] ReadBlock(string p, Int64 block);  // this might need a file handle, windows open and close is quite expensive
        void WriteBlock(string p, Int64 block, byte[] data); // file handle?
        string HashBlock(string p, Int64 block); // file handle
        string HashTotal(string p);
        void Delete(string p);
        string GetCwd();

        Collection<string> GetDirs(string p);
        Collection<string> GetFiles(string p);
        Collection<string> ExpandPath(string p);

    };


    public class LocalIO : IO
    {
        public readonly static int g_blocksize = 1048576;
        private SessionState session;
       // private String remoteUNC = null;

        public LocalIO (SessionState ss) { this.session = ss; }
        public LocalIO (SessionState ss, PSCredential pc, String unc) { 
            this.session = ss;
           // this.remoteUNC = unc;
            
           // ConnectToRemote(unc, pc.UserName, SecureStringToString(pc.Password));
        }

        ~LocalIO ()
        {
          //  if (remoteUNC != null)
          //  {
          //      DisconnectRemote(remoteUNC);
          //  }
        }

        /*
        [DllImport("Mpr.dll")]
        private static extern int WNetUseConnection(
            IntPtr hwndOwner,
            NETRESOURCE lpNetResource,
            string lpPassword,
            string lpUserID,
            int dwFlags,
            string lpAccessName,
            string lpBufferSize,
            string lpResult
        );

        [DllImport("Mpr.dll")]
        private static extern int WNetCancelConnection2(
            string lpName,
            int dwFlags,
            bool fForce
        );

        [StructLayout(LayoutKind.Sequential)]
        private class NETRESOURCE
        {
            public int dwScope = 0;
            public int dwType = 0;
            public int dwDisplayType = 0;
            public int dwUsage = 0;
            public string lpLocalName = "";
            public string lpRemoteName = "";
            public string lpComment = "";
            public string lpProvider = "";
        }
        */
        private static String SecureStringToString(SecureString value)
        {
            IntPtr valuePtr = IntPtr.Zero;
            try
            {
                valuePtr = Marshal.SecureStringToGlobalAllocUnicode(value);
                return Marshal.PtrToStringUni(valuePtr);
            }
            finally
            {
                Marshal.ZeroFreeGlobalAllocUnicode(valuePtr);
            }
        }
        /*
        private static void ConnectToRemote(string remoteUNC, string username, string password)
        {
            NETRESOURCE nr = new NETRESOURCE();
            nr.dwType = 1; // DISK RESOURCE
            nr.lpRemoteName = remoteUNC;
            //			nr.lpLocalName = "F:";

            int ret;
          
                ret = WNetUseConnection(IntPtr.Zero, nr, password, username, 0, null, null, null);

            if (ret != 0)
                throw new Win32Exception(ret);
        }
        */
        /*
        private static void DisconnectRemote(string remoteUNC)
        {
            int ret = WNetCancelConnection2(remoteUNC, 1, false); // CONNECT_UPDATE_PROFILE
            if (ret != 0)
                throw new Win32Exception(ret);
        }
        */
        public string GetCwd() { return session.Path.CurrentFileSystemLocation.ToString(); } 

        // because get all directories recursively may explode if it encounters a permission denied and return NOTHING.
        public Collection<string> ReadDir(string p)
        {
            List<string> dl = new List<string>();
            List<string> fl = new List<string>();
            dl.Add(p);
            while (dl.Count > 0)
            {

                try
                {
                    string[] tfl = Directory.GetFileSystemEntries(dl[0]);
                    dl.RemoveAt(0);

                    foreach (string fe in tfl)
                    {
                        FileAttributes fa = File.GetAttributes(fe);
                        if ((fa & FileAttributes.Directory) != 0 && (fa & FileAttributes.ReparsePoint) == 0)
                            dl.Add(fe);
                        fl.Add(fe);
                    }
                } catch
                {
                    dl.RemoveAt(0);
                }
            }
            return new Collection<string>(fl);
        }

        public Collection<string> GetDirs(string p)
        {
            string[] fl = Directory.GetDirectories(p);
            return new Collection<string>(fl);
        }

        public Collection<string> GetFiles(string p)
        {
            string[] fl = Directory.GetFiles(p);
            return new Collection<string>(fl);
        }

        public Collection<string> ExpandPath(string p)
        {
            // determine if relative or absolute path
            if (!Path.IsPathRooted(p))
            {
                string cur = session.Path.CurrentFileSystemLocation.ToString();
                p = Path.Combine(cur, p);
            }

          //  Console.WriteLine("path: " + p);

            string path = Path.GetDirectoryName(p);
            string card = Path.GetFileName(p);


            
           // Console.WriteLine("dir: " + path + " card: " + card);

            Collection<string> pathList = new Collection<string>();
            foreach (string pt in Directory.EnumerateFileSystemEntries(path, card))
            {
                pathList.Add(pt);
            }

            if (card.Length == 0)
                pathList.Add(p);

            return pathList;
        }

        public void MakeDir(string p)
        {
            System.IO.Directory.CreateDirectory(p);
        }

        public SyncStat GetInfo(string p)
        {
           // try
          //  {
                FileAttributes fa = System.IO.File.GetAttributes(p);

                if ((fa & FileAttributes.Directory) == FileAttributes.Directory)
                    return new SyncStat(new DirectoryInfo(p));

                //  are there more types?
                return new SyncStat(new FileInfo(p));
          //  }
          //  catch (Exception)
           // {
          //      return new SyncStat();
           // }
        }

        public void SetInfo(string p, SyncStat f)
        {

            FileSystemInfo pfi = new FileInfo(p);
            if ((f.Attributes & FileAttributes.Directory) == FileAttributes.Directory)
                pfi = new DirectoryInfo(p);
           
                pfi.Attributes = f.Attributes;
                pfi.CreationTimeUtc = f.CreationTimeUtc;
            try { 
                pfi.LastWriteTimeUtc = f.LastWriteTimeUtc;
            }
            catch (Exception e)
            {
                // ocassionally we get the incorrect time from f.LastWriteTimeUtc
                // which causes set lastWriteTime to fail
                // hopefully this is not too important
                // May want to print a warning...?
              //  throw new IOException("Cannot set LastWriteTime");
                /*
                Console.WriteLine(e.StackTrace);
                Console.WriteLine("Attributes: " + f.Attributes);
                Console.WriteLine("Create: " + f.CreationTimeUtc);
                Console.WriteLine("Modify: " + f.LastWriteTimeUtc);
                */
            }

        }

        public string HashTotal(string p)
        {
            using (FileStream stream = System.IO.File.OpenRead(p))
            {
                System.Security.Cryptography.SHA256 sha = System.Security.Cryptography.SHA256.Create();
                byte[] bytehash;
                bytehash = sha.ComputeHash(stream);
                string result = "";
                foreach (byte b in bytehash) result += b.ToString("x2");
                sha.Dispose();
                return result;
            }
        }

        public byte[] ReadBlock(string p, Int64 block)
        {
            using (FileStream fs = System.IO.File.Open(p, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            {
                Int64 bloffset = block * g_blocksize;

                fs.Seek(bloffset, System.IO.SeekOrigin.Begin);
                Byte[] b = new Byte[g_blocksize];

                Int32 br = fs.Read(b, 0, g_blocksize);
                if (br != g_blocksize)
                    Array.Resize(ref b, br);

                return b;
            }

        }

        public void WriteBlock(string p, Int64 block, byte[] data)
        {
            using (FileStream fs = System.IO.File.Open(p, System.IO.FileMode.OpenOrCreate))
            {
                Int64 bloffset = block * g_blocksize;

                // Int32 bytes = data.Length;
                //  Console.WriteLine("Write block: " + block + " to file " + p + " seeking...");

                fs.Seek(bloffset, System.IO.SeekOrigin.Begin); // cannot seek to data already written
                                                               //  Console.WriteLine("completed");

                fs.Write(data, 0, data.Length);
                if (data.Length != g_blocksize)
                    fs.SetLength(bloffset + data.Length);
            }

        }
        // cater for short blocks
        public string HashBlock(string p, Int64 block)
        {

            // Console.WriteLine("file: " + p + " Block: " + block);

            using (FileStream fs = System.IO.File.Open(p, System.IO.FileMode.Open,FileAccess.Read,FileShare.ReadWrite))
            {
                Int64 bloffset = block * g_blocksize;
                //   Console.WriteLine("seek " + bloffset);

                fs.Seek(bloffset, System.IO.SeekOrigin.Begin);  // hopefully throws when seeking beyond end of file. no it doesn't
                Byte[] b = new Byte[g_blocksize];

                //   Console.WriteLine("read");

                int num = fs.Read(b, 0, g_blocksize);  // this sometimes takes a long time to complete. wtf windows?

                fs.Close();
                if (num == 0)
                    throw new System.IO.EndOfStreamException();  // port this to remote PS code.
                                                                 //   Console.WriteLine("crypt create");

                System.Security.Cryptography.SHA256 sha = System.Security.Cryptography.SHA256.Create();
                byte[] bytehash;
                //   Console.WriteLine("computehash");

                bytehash = sha.ComputeHash(b, 0, num);
                sha.Dispose();
                string result = "";
                //   Console.WriteLine("foreach");

                foreach (byte bb in bytehash) result += bb.ToString("x2");
                return result;
            }
        }

        public void Delete (string p)
        {
            FileAttributes fa = System.IO.File.GetAttributes(p);

            if ((fa & FileAttributes.Directory) == FileAttributes.Directory)
                System.IO.Directory.Delete(p);
            else
                System.IO.File.Delete(p);
        }

    }



    public class RemoteIO : IO
    {
        public readonly static int g_blocksize = 1048576;
        readonly PSSession session;
        public RemoteIO(PSSession s)
        {
            session = s;
        }

        public string GetCwd()
        {
            Pipeline pipe = session.Runspace.CreatePipeline();
            //pipe.Commands.AddScript("resolve-path ~");
            pipe.Commands.AddScript("get-location");
            Collection<PSObject> rv = pipe.Invoke();
            pipe.Dispose();
            foreach (PSObject ps in rv)
            {
               // Console.WriteLine( "cwd: " + ps.ToString());
                return ps.ToString();
            }
            throw new System.IO.DirectoryNotFoundException();
        }

        public Collection<string> ReadDir(string p)
        {
            string format = @"
                $dl=new-object System.Collections.ArrayList
                $dl.Add(""{0}"")
                while ($dl.Count -gt 0) {{
                $tfl=[IO.Directory]::GetFileSystemEntries($dl[0])
                $dl.removeAt(0)
                foreach ($fe in $tfl)
                {{
                $fa =[io.file]::GetAttributes($fe)
                if (($fa -bAnd [io.fileattributes]::Directory) -ne 0 -and ($fa -bAnd [io.FileAttributes]::ReparsePoint) -eq 0)
                {{
                $dl.Add($fe) > $null
                }}
                $fe
                }}
                }}
            ";
            string command = string.Format(format, p);

            Pipeline pipe = session.Runspace.CreatePipeline();
            pipe.Commands.AddScript(command);
            Collection<PSObject> rv = pipe.Invoke();
            Collection<string> ret = new Collection<string>();

            foreach (PSObject ps in rv) { ret.Add(ps.ToString()); }
            pipe.Dispose();
            return ret;
        }

        public Collection<string> GetDirs(string p)
        {
            string format = @"[IO.Directory]::GetDirectories(""{0}"")";

            string command = string.Format(format, p);

            Pipeline pipe = session.Runspace.CreatePipeline();
            pipe.Commands.AddScript(command);
            Collection<PSObject> rv = pipe.Invoke();
            Collection<string> ret = new Collection<string>();

            foreach (PSObject ps in rv) { ret.Add(ps.ToString()); }
            pipe.Dispose();
            return ret;
        }

        public Collection<string> GetFiles(string p)
        {
            string format = @"[IO.Directory]::GetFiles(""{0}"")";

            string command = string.Format(format, p);

            Pipeline pipe = session.Runspace.CreatePipeline();
            pipe.Commands.AddScript(command);
            Collection<PSObject> rv = pipe.Invoke();
            Collection<string> ret = new Collection<string>();

            foreach (PSObject ps in rv) { ret.Add(ps.ToString()); }
            pipe.Dispose();
            return ret;
        }

        public void MakeDir(string p)
        {
            string format = @"[System.IO.Directory]::CreateDirectory(""{0}"")";
            string command = string.Format(format, p);
            Pipeline pipe = session.Runspace.CreatePipeline();

            pipe.Commands.AddScript(command);
            pipe.Invoke();
            pipe.Dispose();

        }

        public Collection<string> ExpandPath (string p)
        {
            string f = @"resolve-path ""{0}""";
            string command = string.Format(f, p);
            Pipeline pipe = session.Runspace.CreatePipeline();
            pipe.Commands.AddScript(command);

            Collection<PSObject> res = pipe.Invoke();
            pipe.Dispose();
            Collection<string> pathList = new Collection<string>();
            foreach (PSObject ps in res)
            {
                pathList.Add(ps.ToString());               
            }

            return pathList;

            // no items
           // throw new System.IO.FileLoadException();
        }

        public SyncStat GetInfo(string p)
        {

            Pipeline pipe = session.Runspace.CreatePipeline();

            string format = @"get-item -force ""{0}"""; // Force for hidden files
            string command = string.Format(format, p);

            // Console.WriteLine("getinfo: "+ command);

            pipe.Commands.AddScript(command);

            Collection<PSObject> res = pipe.Invoke();
            pipe.Dispose();
            foreach (PSObject ps in res)
            {
                return new SyncStat(ps);
            }

            // return (FileInfo)ps1.BaseObject;
            //            pipe.Dispose();

           // Console.WriteLine("Failed: " + p);

            // no items
            throw new System.IO.FileLoadException();
        }

        public void SetInfo(string p, SyncStat f)
        {
            Pipeline pipe = session.Runspace.CreatePipeline();
            // string format;

            string cc = @"
                $f=get-item -force ""{0}"" 
                $f.CreationTimeUtc=[System.DateTime]::FromFileTimeUtc(""{1}"")
                $f.LastWriteTimeUtc=[System.DateTime]::FromFileTimeUtc(""{2}"")
                $f.Attributes=""{3}""
            ";

            string command = string.Format(cc,
                p,
                f.CreationTimeUtc.ToFileTimeUtc(),
                f.LastWriteTimeUtc.ToFileTimeUtc(),
                f.Attributes
            );

            pipe.Commands.AddScript(command);
            pipe.Invoke();
            pipe.Dispose();

        }

        public string HashTotal(string p)
        {
            Pipeline pipe = session.Runspace.CreatePipeline();

            string f = @"
                $fs=[System.IO.file]::OpenRead(""{0}"")
                $sha=[system.security.cryptography.sha256]::Create()
                $h=$sha.computehash($fs)
                $sha.Dispose()
                $fs.Close()
                $h
            ";

            string command = string.Format(f, p);

            pipe.Commands.AddScript(command);

            /*
            string format = "$fs=[System.IO.file]::OpenRead(\"{0}\")";
            string command = string.Format(format, p);

            pipe.Commands.AddScript(command);

            pipe.Commands.AddScript("$sha=[system.security.cryptography.sha256]::Create()");
            pipe.Commands.AddScript("$h=$sha.computehash($fs)");
            pipe.Commands.AddScript("$sha.Dispose()");
            pipe.Commands.AddScript("$fs.Close()");
            pipe.Commands.AddScript("$h");
            */

            Collection<PSObject> res = pipe.Invoke();
            string result = "";
            //Console.Write("hash len: " + res.Count);

            foreach (PSObject ps in res)
            {
                byte bytes = (byte)ps.BaseObject;
                result += bytes.ToString("x2");
            }
            pipe.Dispose();

            return result;
        }

        public byte[] ReadBlock(string p, Int64 block)
        {

            // Console.WriteLine("file: " + p + " block: " + block);

            Int64 bloffset = block * g_blocksize;

            string f = @"
                $fs=[System.IO.file]::Open(""{0}"",[System.IO.FileMode]::Open,[System.IO.FileAccess]::Read,[System.IO.FileShare]::ReadWrite)
                $fs.Seek({1},[System.IO.SeekOrigin]::Begin)
                $b= New-Object System.byte[] {2}
                $r=$fs.read($b,0,{2})
                [System.Array]::Resize([ref]$b,$r)
                $bs=[Convert]::ToBase64String($b)
                $fs.close()
                $bs
            ";

            string command = string.Format(f,
                p,
                bloffset,
                g_blocksize);

            Pipeline pipe = session.Runspace.CreatePipeline();
            pipe.Commands.AddScript(command);

            /*
            string format = @"$fs=[System.IO.file]::Open(""{0}"",[System.IO.FileMode]::Open,[System.IO.FileAccess]::Read,[System.IO.FileShare]::ReadWrite)";
            string command = string.Format(format, p);

            string format2 = "$fs.Seek({0},[System.IO.SeekOrigin]::Begin)";
            string command2 = string.Format(format2, bloffset);
            pipe.Commands.AddScript(command2);

            string format3 = "$b= New-Object System.byte[] {0}";
            string command3 = string.Format(format3, g_blocksize);
            pipe.Commands.AddScript(command3);

            //pipe.Commands.AddScript( "$b=[System.byte[]]::new({0})" );
            string format4 = "$r=$fs.read($b,0,{0})";
            string command4 = string.Format(format4, g_blocksize);
            pipe.Commands.AddScript(command4); // fix
            // truncate to read bytes
            pipe.Commands.AddScript("[System.Array]::Resize([ref]$b,$r)");


            //Collection<PSObject> bytesres = pipe.Invoke();
            pipe.Commands.AddScript("$bs=[Convert]::ToBase64String($b)");
            pipe.Commands.AddScript("$fs.close()");
            pipe.Commands.AddScript("$bs");
            */

            Collection<PSObject> res = pipe.Invoke();
            foreach (PSObject ps in res)
            {
                // Console.WriteLine("<< " + ps.BaseObject.ToString());

                return System.Convert.FromBase64String(ps.BaseObject.ToString());
            }
            //return (byte[])ps.BaseObject;
            pipe.Dispose();
            throw new System.IO.FileLoadException();
            //return null;
        }
        public void WriteBlock(string p, Int64 block, byte[] data)
        {
            Pipeline pipe = session.Runspace.CreatePipeline();

            string format = "$fs=[System.IO.file]::Open(\"{0}\",[System.IO.FileMode]::OpenOrCreate)";
            string command = string.Format(format, p);

            pipe.Commands.AddScript(command);

            Int64 bloffset = block * g_blocksize;

            string format2 = "$r=$fs.Seek({0},[System.IO.SeekOrigin]::Begin)";
            string command2 = string.Format(format2, bloffset);
            pipe.Commands.AddScript(command2);

            string b64data = System.Convert.ToBase64String(data);
            string format3 = "$b=[Convert]::FromBase64String(\"{0}\")";
            string command3 = string.Format(format3, b64data);
            pipe.Commands.AddScript(command3);

            // pipe.Commands.AddScript("$b=[System.byte[]]::new({0})");
            pipe.Commands.AddScript("$fs.write($b,0,$b.Length)");

            if (data.Length != g_blocksize)
            {
                string format4 = "$fs.SetLength(\"{0}\")";
                string command4 = string.Format(format4, bloffset + data.Length);
                pipe.Commands.AddScript(command4);
            }
            pipe.Commands.AddScript("$fs.close()");
            //Collection<PSObject> bytesres = pipe.Invoke();
            //pipe.Commands.AddScript("$b");
            // Collection<PSObject> res = 
            pipe.Invoke();
            pipe.Dispose();

        }

        public void Delete(string p)
        {
            Pipeline pipe = session.Runspace.CreatePipeline();

            string format = "remove-item -force \"{0}\""; // Force for hidden files
            string command = string.Format(format, p);
            pipe.Commands.AddScript(command);

            pipe.Invoke();
            pipe.Dispose();
        }

        public string HashBlock(string p, Int64 block)
        {
            string format = "$fs=[System.IO.file]::Open(\"{0}\",[System.IO.FileMode]::Open)";
            string command = string.Format(format, p);
            Pipeline pipe = session.Runspace.CreatePipeline();

            pipe.Commands.AddScript(command);

            Int64 bloffset = block * g_blocksize;

            string format2 = "$r=$fs.Seek({0},[System.IO.SeekOrigin]::Begin)";
            string command2 = string.Format(format2, bloffset);
            pipe.Commands.AddScript(command2);

            string format3 = "$b=[System.byte[]]::new({0})";
            string command3 = string.Format(format3, g_blocksize);
            pipe.Commands.AddScript(command3);

            string format4 = "$b=[System.byte[]]::new({0})";
            string command4 = string.Format(format4, g_blocksize);
            pipe.Commands.AddScript(command4);

            string format5 = "$r=$fs.read($b,0,{0})";
            string command5 = string.Format(format5, g_blocksize);
            pipe.Commands.AddScript(command5);

            pipe.Commands.AddScript("if ($r -eq 0) { throw \"EndOfFile\"} ");


            pipe.Commands.AddScript("$sha=[system.security.cryptography.sha256]::Create()");
            pipe.Commands.AddScript("$h=$sha.computehash($b,0,$r)");
            pipe.Commands.AddScript("$sha.dispose()");
            pipe.Commands.AddScript("$fs.close()");
            pipe.Commands.AddScript("$h");

            Collection<PSObject> res = pipe.Invoke();
            string result = "";

            foreach (PSObject ps in res)
            {
                byte bytes = (byte)ps.BaseObject;
                result += bytes.ToString("x2");
            }
            pipe.Dispose();

            return result;
        }

    }
}
