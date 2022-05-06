using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Management.Automation;
using System.Management.Automation.Runspaces;
using System.Runtime.InteropServices;
using System.Security;
using System.Security.AccessControl;


namespace net.ninebroadcast
{
	public interface IO
	{

		// all paths should be relative to abspath.

		//SyncStat GetInfo(string p);
		//void SetInfo(string p, SyncStat f);
		long GetLength(string lp);
		DateTime GetModificationTime(string lp);
		void SetModificationTime(string lp, DateTime dt);
		FileAttributes GetAttributes(string lp);
		void SetAttributes(string lp, FileAttributes fa);
		Boolean Exists(string lp);

		System.Security.AccessControl.FileSecurity GetAcl(string lp);
		void SetAcl(string lp, FileSecurity fa);

		void SetPath(string b);
		void Parent();

		byte[] ReadBlock(string p, Int64 block);  // this might need a file handle, windows open and close is quite expensive
		void WriteBlock(string p, Int64 block, byte[] data); // file handle?
		string HashBlock(string p, Int64 block); // file handle
		string HashTotal(string p);

		void Delete(string p);
		void MakeDir(string p);

		string AbsPath();
		string AbsPath(string p);
		string GetCwd();
		string GetRelative(string toPath);


		bool IsDir();
        bool IsDir(string p);

		Collection<string> ReadDir(string p);
		//Collection<string> GetDirs(string p);
		//Collection<string> GetFiles(string p);
		Collection<string> ExpandPath(string p);

	};

	public class LocalIO : IO
	{
		public readonly static int g_blocksize = 1048576;
		private SessionState session;
		protected string abspath;  // absolute, all path operations are relative to this.

       // protected string element;

		public LocalIO (SessionState ss) 
		{ 
			this.session = ss; 
			this.SetPath(GetCwd()); 
		}

		public LocalIO (SessionState ss, string pp) 
		{ 
			this.session=ss; 

			this.SetPath(
				Path.Combine(this.GetCwd(),pp)  // pp can be relative or absolute
			);

		}

		public void SetPath(string b) { this.abspath = b; }
		public string GetCwd() { return this.session.Path.CurrentFileSystemLocation.ToString(); } 

		public string AbsPath() { return abspath; }
		public string AbsPath(string p) { return Path.Combine(this.abspath,p); }
		public void Parent() { this.abspath = Path.GetDirectoryName(this.abspath); }

/*
		public FileAttributes GetAttributes(string p) { 
			return File.GetAttributes(this.AbsPath(p));
		}
*/
		public bool IsDir() { return IsDir(""); }
        public bool IsDir(string lp)
        {
			string p = this.AbsPath(lp);
			FileAttributes fa = File.GetAttributes(p);
			return (( fa & FileAttributes.Directory) != 0); 
        }

		///<summary>gets list of files for IO basepath</summary>
		// public Collection<String> ReadDir() { return this.ReadDir(""); }

		public string GetRelative(string toPath)
		{
			// return Path.GetRelativePath(this.abspath, toPath);
			char ps = System.IO.Path.DirectorySeparatorChar;
			return toPath.Replace(abspath.TrimEnd(ps) + ps, "");
		}

		// because "get all directories recursively" may explode if it encounters a permission denied and return NOTHING.
		/// <summary>Returns a list of files made by recursively reading from path</summary>
		/// <param>path to get</param>
		/// <returns>Collection of files relative<returns>

		public Collection<string> ReadDir(string lp) // I really don't think there needs to be an argument based readdir, as this performs a recursive search
		{
			// the argument is for wild card expansion.  The abspath argument expands to elements with the same base path

			List<string> dl = new List<string>();  // these are absolute
			List<string> fl = new List<string>();  // these are relative 

			// path is file so only return this item
			if (!this.IsDir(lp))
			{
				fl.Add(lp);
				return new Collection<string>(fl);
			}

            // as I want the last element included in the relative path.

			dl.Add(this.AbsPath(lp));
			fl.Add(lp);

			while (dl.Count > 0)
			{
				try
				{
					string[] tfl = Directory.GetFileSystemEntries(dl[0]);
					dl.RemoveAt(0);

					foreach (string fe in tfl)
					{
						string fr = this.GetRelative(fe);
						//Console.WriteLine("root: " + abspath + ". File Entry: "+fr);

						FileAttributes fa = File.GetAttributes(fe);
						if ((fa & FileAttributes.Directory) != 0)
							dl.Add(fe);

						fl.Add(GetRelative(fe));
					}
				} catch {
					dl.RemoveAt(0);
				}
			}
			return new Collection<string>(fl);
		}

// should move from session to cwd
		public Collection<string> ExpandPath (string pp) 
		{ 
			//string cur = sess.Path.CurrentFileSystemLocation.ToString();
			// Console.WriteLine(String.Format("LocalIO ExpandPath current: {0}",cur));
			string p2 = this.AbsPath(pp);
			string path = Path.GetDirectoryName(p2);
			string card = Path.GetFileName(p2);
			//Console.WriteLine(String.Format("LocalIO ExpandPath file: {0}",card));

			string[] fse; //  = {p2};

	        fse = Directory.GetFileSystemEntries(path,card);  // have to work out what this returns under different scenarios.
			// Console.WriteLine(String.Format("LocalIO ExpandPath GetFileSystemEntries: {0}",fse));

            return new Collection<string>(fse);

		}

// Should return stuff,  nah too hard
		public void MakeDir(string p)
		{
            // as the leaf element is removed from abspath, 
            // all destination operations must add it.
           // string apath = Path.Combine(this.AbsPath(), p);
           // System.IO.Directory.CreateDirectory(apath);
			System.IO.Directory.CreateDirectory(this.AbsPath(p));
		}
		private FileSystemInfo getfileinfo (string lp)
		{
			if (IsDir(lp))
				return new DirectoryInfo(this.AbsPath(lp));
			else
				return new FileInfo(this.AbsPath(lp));
		}

		public DateTime GetModificationTime(string lp)
		{
			FileSystemInfo fi = getfileinfo(lp);
			return fi.LastWriteTimeUtc;
		}

		public void SetModificationTime(string lp, DateTime dt)
		{
			FileSystemInfo fi = getfileinfo(lp);
			fi.LastWriteTimeUtc = dt;
		}

		public long GetLength(string lp)
		{
			FileInfo fi = (FileInfo)getfileinfo(lp);
			return fi.Length;
		}

		public FileAttributes GetAttributes(string lp)
		{
			FileSystemInfo fi = getfileinfo(lp);
			return fi.Attributes;
		}

		//void SetAttributes(string lp, FileAttributes fa);
 
		public void SetAttributes(string lp, FileAttributes fa)
		{
			FileSystemInfo fi = getfileinfo(lp);
			fi.Attributes = fa;
		}

		public Boolean Exists(string lp)
		{
			string p = this.AbsPath(lp);

			return System.IO.File.Exists(p);
			// why did I do this?
			//return getfileinfo(lp).Exists;
		}

		public FileSecurity GetAcl(string lp)
		{
			string p = this.AbsPath(lp); 
			return new FileSecurity(p,AccessControlSections.All);
		}

		public void SetAcl(string lp, FileSecurity fa)
		{
			string p = this.AbsPath(lp);
			//System.IO.FileSystem.FileSystemAclExtensions.SetAccessControl()

			if (fa != null)
				File.SetAccessControl(p,fa);
		}


// might see if I can remove SyncStat entirely
		public SyncStat GetInfo(string lp)
		{
			string p = this.AbsPath(lp);  //read based Combine

			FileAttributes fa = System.IO.File.GetAttributes(p);

			if ((fa & FileAttributes.Directory) == FileAttributes.Directory)
				return new SyncStat(new DirectoryInfo(p));

			//  are there more types?
			return new SyncStat(new FileInfo(p));
		}

		public void SetInfo(string lp, SyncStat f)
		{
			string p = this.AbsPath(lp);  // write based combine

			FileSystemInfo pfi = new FileInfo(p);
			if ((f.Attributes & FileAttributes.Directory) == FileAttributes.Directory)
				pfi = new DirectoryInfo(p);

			pfi.Attributes = f.Attributes;
			pfi.CreationTimeUtc = f.CreationTimeUtc;
			pfi.LastWriteTimeUtc = f.LastWriteTimeUtc;

		}

		public string HashTotal(string lp)
		{
			string p = this.AbsPath(lp);

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

		public byte[] ReadBlock(string lp, Int64 block)
		{
			string p = this.AbsPath(lp);

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

		public void WriteBlock(string lp, Int64 block, byte[] data)
		{
			string p = this.AbsPath(lp);

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
		public string HashBlock(string lp, Int64 block)
		{
			string p = this.AbsPath(lp);

			// Console.WriteLine("file: " + p + " Block: " + block);

			using (FileStream fs = System.IO.File.Open(p, System.IO.FileMode.Open,FileAccess.Read,FileShare.ReadWrite))
			{
				Int64 bloffset = block * g_blocksize;
				//   Console.WriteLine("seek " + bloffset);

				fs.Seek(bloffset, System.IO.SeekOrigin.Begin);  // hopefully throws when seeking beyond end of file. no it doesn't
				Byte[] b = new Byte[g_blocksize];

				//   Console.WriteLine("read");
				int num=0;
				try {
					num = fs.Read(b, 0, g_blocksize);  // this sometimes takes a long time to complete. wtf windows?
					fs.Close();
				} catch (IOException) {
					b = new byte[0]{};
				}

				//if (num == 0)
				//	throw new System.IO.EndOfStreamException();  // port this to remote PS code.
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

		public void Delete (string lp)
		{
			string p = this.AbsPath(lp);

			FileAttributes fa = System.IO.File.GetAttributes(p);

			if ((fa & FileAttributes.Directory) == FileAttributes.Directory)
				System.IO.Directory.Delete(p);
			else
				System.IO.File.Delete(p);
		}
	}

// ***********************************************************************************************************************************************************************************************

	public class RemoteIO : IO
	{
		public readonly static int g_blocksize = 1048576;
		readonly PSSession session;
		private string abspath;

		private ConvertThroughString converter;

		public RemoteIO(PSSession s)
		{
			this.session = s;
			this.converter = new ConvertThroughString();

			this.SetPath(GetCwd()); 
		}

		public RemoteIO(PSSession ss, string pp)
		{
			this.session=ss; 
			this.converter = new ConvertThroughString();

			this.SetPath(
				Path.Combine(GetCwd(),pp) // pp can be relative or absolute
			);

		}

		public string GetCwd()
		{
			Pipeline pipe = this.session.Runspace.CreatePipeline();
			//pipe.Commands.AddScript("resolve-path ~");
			pipe.Commands.AddScript("$cwd=get-location");
			Collection<PSObject> rv = pipe.Invoke();
			pipe.Dispose();

			return session.Runspace.SessionStateProxy.GetVariable("cwd").ToString();

		}

		public void SetPath(string b) { this.abspath = b; }

		public string AbsPath() { return this.abspath; }
		public string AbsPath(string p) { return Path.Combine(this.abspath,p); }
		public void Parent() { this.abspath = Path.GetDirectoryName(this.abspath); }

		//public Collection<String> ReadDir() { return this.ReadDir(""); }


        public bool IsDir() { return IsDir(""); }
		public bool IsDir(string lp) {

			//string p = this.AbsPath(lp);
			//FileAttributes fa = File.GetAttributes(p);
			FileAttributes fa = GetAttributes(lp);
			return ((fa & FileAttributes.Directory) != 0);
		}

		public string GetRelative(string toPath)
		{
			//return System.IO.Path.GetRelativePath(this.abspath, toPath);

			return toPath.Replace(abspath.TrimEnd('\\') + "\\", "");

		}

		public Collection<string> ReadDir(string lp)
		{
			//string ap = this.Combine(lp);
            //string searchRoot = this.DestinationCombine(ap);

			string format = @"
				$pstack = get-location
				set-location ""{1}""
				if (([io.file]::GetAttributes(""{0}"") -bAnd [io.fileattributes]::Directory) -ne 0)
				{{
				$dl=new-object System.Collections.ArrayList
				$dl.Add(""{0}"") > $null
				while ($dl.Count -gt 0) {{
				$tfl=[IO.Directory]::GetFileSystemEntries($dl[0])
				$dl.removeAt(0)
				foreach ($fe in $tfl)
				{{
				$fa =[io.file]::GetAttributes($fe)
				$rp=(resolve-path -Relative $fe)
				if (($fa -bAnd [io.fileattributes]::Directory) -ne 0)
				{{
				$dl.Add($fe) > $null
				}}
				$rp
				}}
				}}
				}} else {{
					(resolve-path -Relative ""{0}"" )
				}}
				set-location $pstack

			";

			// Console.WriteLine("readDir search {0} relative to {1}",searchRoot,this.abspath);

			string command = string.Format(format, lp, this.abspath);

			Pipeline pipe = session.Runspace.CreatePipeline();
			pipe.Commands.AddScript(command);
			Collection<PSObject> rv = pipe.Invoke();
			Collection<string> ret = new Collection<string>();

// probably need to convert to relative path.
			foreach (PSObject ps in rv) { ret.Add(ps.ToString()); }
			pipe.Dispose();
			return ret;
		}

//		public DirectoryInfo MakeDir(string lp)
//		{
//			string p = this.DestinationCombine(lp);
//			return this.MakeAbsDir(p);
//		}

		public void MakeDir(string p)
		{
			string format = @"[System.IO.Directory]::CreateDirectory(""{0}"")";
			string command = string.Format(format, this.AbsPath(p));
			Pipeline pipe = session.Runspace.CreatePipeline();

			pipe.Commands.AddScript(command);
			Collection<PSObject> ol = pipe.Invoke();

			pipe.Dispose();

		}

		public void Delete(string lp)
		{
			string p = this.AbsPath(lp);
			Pipeline pipe = session.Runspace.CreatePipeline();
			session.Runspace.SessionStateProxy.SetVariable("path",p);
			string cmd = @"[System.IO.File]::Delete($path)";
			pipe.Commands.AddScript(cmd);
			Collection<PSObject> res = pipe.Invoke();
			pipe.Dispose();
		}

	//	public Collection<string> ExpandPath (string p) { return ExpandPath(p,session); }

// this behaves differently to the LocalIO version
// Expand path only takes absolute paths.

		public Collection<string> ExpandPath (string p)
		{
			string f = @"resolve-path ""{0}""";
			string command = string.Format(f, this.AbsPath(p));
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

		public SyncStat GetInfo(string lp)
		{
			string p = this.AbsPath(lp);
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

			throw new System.IO.FileLoadException();
		}

		public void SetInfo(string lp, SyncStat f)
		{
			string p = this.AbsPath(lp);
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

		public long GetLength(string lp)
		{
			string p = this.AbsPath(lp);
			Pipeline pipe = session.Runspace.CreatePipeline();
			session.Runspace.SessionStateProxy.SetVariable("path",p);
			string command = @"(New-Object -TypeName System.IO.FileInfo -ArgumentList $path).Length";
			pipe.Commands.AddScript(command);
			Collection<PSObject> rv = pipe.Invoke();

			long len=0;
			foreach (PSObject ps in rv)
			{
				//Console.WriteLine("this is a journey into sound.");
				len = (long)converter.ConvertFrom(ps,len.GetType(),null,false);
			}
			// long len = (long)(session.Runspace.SessionStateProxy.GetVariable("fa") as ValueType);
 			pipe.Dispose();
			return len;
		}

/*		private FileAttributes getattributes(string lp)
		{
			string p = this.AbsPath(lp);
			Pipeline pipe = session.Runspace.CreatePipeline();
			session.Runspace.SessionStateProxy.SetVariable("path",lp);
			string command = "$fa=[io.file]::getattributes($path)";

			pipe.Commands.AddScript(command);
			Collection<PSObject> rv = pipe.Invoke();
			FileAttributes len = (FileAttributes)(session.Runspace.SessionStateProxy.GetVariable("fa") as Enum);
 			pipe.Dispose();
			return len;
		}
*/

		public FileAttributes GetAttributes(string lp)
		{
			string ap = this.AbsPath(lp);

			// string command = string.Format(format, ap);

			Pipeline pipe = session.Runspace.CreatePipeline();
			if ( pipe is null) 
				throw new ArgumentException ("pipe null");
			pipe.Runspace.SessionStateProxy.SetVariable("path",ap);
			string format = @"(New-Object -TypeName System.IO.FileInfo -ArgumentList $path).Attributes";

			pipe.Commands.AddScript(format);
			Collection<PSObject> rv = pipe.Invoke();

			// throw new ArgumentException ("Get Variable");



			FileAttributes ga = 0;
			foreach (PSObject ps in rv)
			{
				ga = (FileAttributes)converter.ConvertFrom(ps,ga.GetType(),null,false);
			}

			Runspace rs = pipe.Runspace;
		//	if ( rs is null) 
		//		throw new ArgumentException ("Runspace null");

			SessionStateProxy ssp = rs.SessionStateProxy;
		//	if (ssp is null) 
		//		throw new ArgumentException ("SessionStateProxy null");			

/*
			Object obj = ssp.GetVariable("fa");
			if ( obj is null) 
				throw new ArgumentException ("GetVariable null");	

			throw new ArgumentException (" get variable: " + (obj.GetType() + " / " + obj));

			Enum en = obj as System.Enum;

			FileAttributes ga=(FileAttributes)en;
				*/
			//throw new ArgumentException ("pipe dispose");

 			pipe.Dispose();

			return (FileAttributes)ga;

		}

		public void SetAttributes(string lp, FileAttributes fa)
		{
			string p = this.AbsPath(lp);
			//Int32 faval = fa.ToInt32(null);
			//string command = string.Format(format, ap,faval);

			Pipeline pipe = session.Runspace.CreatePipeline();
			session.Runspace.SessionStateProxy.SetVariable("path",p);
			session.Runspace.SessionStateProxy.SetVariable("attrib",fa);

			string command = @"(New-Object -TypeName System.IO.FileInfo -ArgumentList $path).Attributes = $attrib";

			pipe.Commands.AddScript(command);
			Collection<PSObject> rv = pipe.Invoke();
 			pipe.Dispose();

/*
			if (rv.Count == 1) {
				PSObject ps = rv[0];
				return (FileAttributes)(ps.BaseObject as System.Enum);
			}
			throw new IOException("Remote SetAttributes Failure");
			*/
		}

		public Boolean Exists(string lp)
		{
			string p = this.AbsPath(lp);
			Pipeline pipe = session.Runspace.CreatePipeline();
			session.Runspace.SessionStateProxy.SetVariable("path",p);
			string command = @"$fe=(New-Object -TypeName System.IO.FileInfo -ArgumentList $path).Exists";
			pipe.Commands.AddScript(command);
			Collection<PSObject> rv = pipe.Invoke();
			Boolean exist = (Boolean)(session.Runspace.SessionStateProxy.GetVariable("fe") as ValueType);

 			pipe.Dispose();
			return exist;
		}

		public DateTime GetModificationTime(string lp)
		{

			string p = this.AbsPath(lp);
			Pipeline pipe = session.Runspace.CreatePipeline();
			session.Runspace.SessionStateProxy.SetVariable("path",p);

			string command = @"$date=(get-item -force $path).LastWriteTimeUtc"; // Force for hidden files
			//string command = string.Format(format, p);

			// Console.WriteLine("getinfo: "+ command);

			pipe.Commands.AddScript(command);

			Collection<PSObject> rv = pipe.Invoke();
			DateTime dt = (DateTime)(session.Runspace.SessionStateProxy.GetVariable("date") as ValueType);
			pipe.Dispose();

			return dt;
		}

		public void SetModificationTime(string lp,DateTime dt)
		{

			string p = this.AbsPath(lp);
			Pipeline pipe = session.Runspace.CreatePipeline();

			session.Runspace.SessionStateProxy.SetVariable("path",p);
			session.Runspace.SessionStateProxy.SetVariable("date",dt);

			string command = @" (get-item -force $path).LastWriteTimeUtc = $date"; // Force for hidden files
			//long ti = dt.Ticks
			//string command = string.Format(format, pdt.Ticks);

			// Console.WriteLine("getinfo: "+ command);

			pipe.Commands.AddScript(command);

			Collection<PSObject> rv = pipe.Invoke();
			pipe.Dispose();

/*
			if (rv.Count == 1)
				return rv as DateTime;

			throw new System.IO.FileLoadException();
			*/
		}

		public FileSecurity GetAcl(string lp)
		{
		// Should be able to use SDDL string for Absolute ACLing

			string p = this.AbsPath(lp);
			Pipeline pipe = session.Runspace.CreatePipeline();
//
			session.Runspace.SessionStateProxy.SetVariable("path",p);
			string command = @"$acl=New-Object -TypeName System.Security.AccessControl.FileSecurity -ArgumentList $path,""All"""; 

			pipe.Commands.AddScript(command);
			Collection<PSObject> rv = pipe.Invoke();

			FileSecurity fcl = session.Runspace.SessionStateProxy.GetVariable("acl") as FileSecurity;
			pipe.Dispose();

			return fcl;

		}
		public void SetAcl(string lp, FileSecurity fa)
		{
			string p = this.AbsPath(lp);
			Pipeline pipe = session.Runspace.CreatePipeline();
//
		// like this method better
			session.Runspace.SessionStateProxy.SetVariable("path",p);
			session.Runspace.SessionStateProxy.SetVariable("acl",fa);
			string command = @"[System.IO.File]::SetAccessControl($file, $acl)"; 

			pipe.Commands.AddScript(command);

			Collection<PSObject> rv = pipe.Invoke();
			pipe.Dispose();

		//	if (rv.Count == 1)
		//		return rv as FileSecurity;

		//	throw new System.IO.FileLoadException();

		}

		public string HashTotal(string lp)
		{
			string p = this.AbsPath(lp);
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

		public byte[] ReadBlock(string lp, Int64 block)
		{
			string p = this.AbsPath(lp);
			// Console.WriteLine("file: " + p + " block: " + block);

			Int64 bloffset = block * g_blocksize;

			string f = @"
				$fs=[System.IO.file]::Open($fullpath,[System.IO.FileMode]::Open,[System.IO.FileAccess]::Read,[System.IO.FileShare]::ReadWrite)
				$fs.Seek($offset,[System.IO.SeekOrigin]::Begin) | out-null
				$b= New-Object System.byte[] -argumentlist $bsize
				$r=$fs.read($b,0,$bsize)
				$bb=New-Object System.byte[] -argumentList $r
				[System.Array]::copy($b,$bb,$r)
				$bs=[Convert]::ToBase64String($bb)
				$fs.close()
				$bs
			";

			Pipeline pipe = session.Runspace.CreatePipeline();

			pipe.Runspace.SessionStateProxy.SetVariable("fullpath",p);
			pipe.Runspace.SessionStateProxy.SetVariable("offset",bloffset);
			pipe.Runspace.SessionStateProxy.SetVariable("bsize",g_blocksize);

/*
			string command = string.Format(f,
				p,
				bloffset,
				g_blocksize);
*/

			pipe.Commands.AddScript(f);

			Collection<PSObject> res = pipe.Invoke();
			foreach (PSObject ps in res)
			{
				// Console.WriteLine("<< " + ps.BaseObject.ToString());
				pipe.Dispose();
				return System.Convert.FromBase64String(ps.BaseObject.ToString());
			}
			//return (byte[])ps.BaseObject;
			pipe.Dispose();
			throw new System.IO.FileLoadException();
			//return null;
		}

		public void WriteBlock(string lp, Int64 block, byte[] data)
		{
			string p = this.AbsPath(lp);

			// p, bloffset, b64data, bloffset + data.Length,
			string format = @"
			$fs=[System.IO.file]::Open(""{0}"",[System.IO.FileMode]::OpenOrCreate)
			$r=$fs.Seek({1},[System.IO.SeekOrigin]::Begin)
			$b=[Convert]::FromBase64String(""{2}"")
			$fs.write($b,0,$b.Length)
			$fs.SetLength({3})
			$fs.close()
			";

			Int64 bloffset = block * g_blocksize;
			string b64data = System.Convert.ToBase64String(data);
			Int64 fslength = bloffset + data.Length;

			string command = string.Format(format,
				p,
				bloffset,
				b64data,
				fslength);

			Pipeline pipe = session.Runspace.CreatePipeline();
			pipe.Commands.AddScript(command);

			pipe.Invoke();
			pipe.Dispose();
		}	

		public string HashBlock(string lp, Int64 block)
		{
			string p = this.AbsPath(lp);
			// p, 
			string format = @"
			$fs=[System.IO.file]::Open($path,[System.IO.FileMode]::Open)
			$r=$fs.Seek($offset,[System.IO.SeekOrigin]::Begin)
			$b=New-Object -typename System.byte[] -ArgumentList $blocksize
			$r=$fs.read($b,0,$blocksize)
			if ($r -eq 0) {{ throw ""EndOfFile"" }}
			$sha=[system.security.cryptography.sha256]::Create()
			$h=$sha.computehash($b,0,$r)
			$fs.close()
			$h
			";

			Int64 bloffset = block * g_blocksize;

/*
			string command = string.Format(format, 
				p,
				bloffset,
				g_blocksize
			);
*/

			Pipeline pipe = session.Runspace.CreatePipeline();
			pipe.Runspace.SessionStateProxy.SetVariable("path",p);
			pipe.Runspace.SessionStateProxy.SetVariable("offset",bloffset);
			pipe.Runspace.SessionStateProxy.SetVariable("blocksize",g_blocksize);

			pipe.Commands.AddScript(format);

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
