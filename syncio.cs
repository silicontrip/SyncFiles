using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Management.Automation;
using System.Management.Automation.Runspaces;
using System.Runtime.InteropServices;
using System.Security;


namespace net.ninebroadcast
{
	public interface IO
	{

		// all paths should be relative to abspath.
		Collection<string> ReadDir(string p);
		Collection<string> ReadDir();
		DirectoryInfo MakeDir(string p);
		DirectoryInfo MakeAbsDir(string p);

		SyncStat GetInfo(string p);
		void SetInfo(string p, SyncStat f);
		byte[] ReadBlock(string p, Int64 block);  // this might need a file handle, windows open and close is quite expensive
		void WriteBlock(string p, Int64 block, byte[] data); // file handle?
		string HashBlock(string p, Int64 block); // file handle
		string HashTotal(string p);
		void Delete(string p);
		string GetCwd();
		string SourceCombine(string p);
        string DestinationCombine(string p);

		void SetPath(string p);
		string AbsPath();
		bool IsDir();
		Collection<string> GetDirs(string p);
		Collection<string> GetFiles(string p);
		Collection<string> ExpandPath(string p);

	};

	public class LocalIO : IO
	{
		public readonly static int g_blocksize = 1048576;
		private SessionState session;
		protected string abspath;  // absolute, all path operations are relative to this.

        protected string element;

	   // private String remoteUNC = null;

		public LocalIO (SessionState ss) { this.session = ss; this.SetPath(@"./"); }
		public LocalIO (SessionState ss, String pp) { this.session=ss; this.SetPath(pp); }

		public string AbsPath() { return Path.Combine(this.abspath,this.element); }

		public string GetCwd() { return this.session.Path.CurrentFileSystemLocation.ToString(); } 

		public string SourceCombine(string p) { 
			// Console.WriteLine("Combining: {0} + {1}",this.abspath,p);
			if (this.IsDir())
				return Path.Combine(this.abspath, p);
			else
			// this should be the same as Combine (this.abspath,p)
				return this.AbsPath();  // it doesn't make sense combining a file path and subpath
		}

        public string DestinationCombine(string p) {
            return Path.Combine(this.abspath, this.element, p);
        }

		/// <summary>
		/// sets the IO base path.  works with relative or absolute paths. (stored as absolute)
		/// </summary>
		/// <param>Path relative or absolute</param>
		public void SetPath(string p) {
            string apath = Path.Combine(session.Path.CurrentFileSystemLocation.ToString(), p);
            this.abspath = Path.GetDirectoryName(apath);
            this.element = Path.GetFileName(apath);
		}

		public bool IsDir() { 
            return IsDir(this.AbsPath());
		}

        private static bool IsDir(string p)
        {
            FileAttributes fa = File.GetAttributes(p);
			return (( fa & FileAttributes.Directory) != 0); 
        }

		///<summary>gets list of files for IO basepath</summary>
		public Collection<String> ReadDir() { return this.ReadDir(""); }

		// because "get all directories recursively" may explode if it encounters a permission denied and return NOTHING.
		/// <summary>Returns a list of files made by recursively reading from path</summary>
		/// <param>path to get</param>
		/// <returns>Collection of files relative<returns>

		private static string makerel( Uri fromRoot, string toPath)
		{
			Uri feUri = new Uri (toPath);
			
			Uri rel = fromRoot.MakeRelativeUri(feUri);
			string relPath = Uri.UnescapeDataString(rel.ToString());
			return relPath.Replace(System.IO.Path.AltDirectorySeparatorChar, System.IO.Path.DirectorySeparatorChar);
		}

		public Collection<string> ReadDir(string lp) // I really don't think there needs to be an argument based readdir
		{
			// this needs make relative
			//string p = this.SourceCombine(lp);

		   // Console.WriteLine("READING REL: {0}",lp);
		   // Console.WriteLine("READING ABS: {0}",p);
			List<string> dl = new List<string>();
			List<string> fl = new List<string>();

			if (!this.IsDir())
			{
			   // Console.WriteLine("returning file.");
				fl.Add(Path.GetFileName(this.AbsPath()));
				return new Collection<string>(fl);
			}

            // string parent = Path.GetDirectoryName(this.abspath);
		    // Uri fromRoot = new Uri (parent  + "/");

			Uri fromRoot = new Uri (this.abspath + "/");

            // as I want the last element included in the relative path.
            string searchRoot = this.DestinationCombine(lp);
			dl.Add(searchRoot);
			fl.Add(makerel(fromRoot,searchRoot));

			while (dl.Count > 0)
			{
				try
				{
					string[] tfl = Directory.GetFileSystemEntries(dl[0]);
					dl.RemoveAt(0);

					foreach (string fe in tfl)
					{
						FileAttributes fa = File.GetAttributes(fe);
						if ((fa & FileAttributes.Directory) != 0)
							dl.Add(fe);

						fl.Add(makerel(fromRoot,fe));
					}
				} catch {
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

// should move from session to cwd
		public Collection<string> ExpandPath (string pp) { return ExpandPath(pp,session); }
		
		public static Collection<string> ExpandPath (string pp, SessionState sess) 
		{

			string cur = sess.Path.CurrentFileSystemLocation.ToString();
			// Console.WriteLine(String.Format("LocalIO ExpandPath current: {0}",cur));
			string p2 = Path.Combine(cur, pp);
			//Console.WriteLine(String.Format("LocalIO ExpandPath combine: {0}",p2));

			string path = Path.GetDirectoryName(p2);
			//Console.WriteLine(String.Format("LocalIO ExpandPath basedir: {0}",path));

			string card = Path.GetFileName(p2);
			//Console.WriteLine(String.Format("LocalIO ExpandPath file: {0}",card));

         // Console.WriteLine(String.Format("path: {0} card: {1}",path,card));
			string[] fse = {p2};
			try {
	            fse = Directory.GetFileSystemEntries(path,card);  // have to work out what this returns under different scenarios.

				if (fse.Length == 0)
				{
					Directory.CreateDirectory(path);
					fse = Directory.GetFileSystemEntries(path,card);
					//Console.WriteLine(String.Format("make dir fse size: {0}",fse.Length));
				}

			} catch (DirectoryNotFoundException e) {

				Directory.CreateDirectory(path);
				fse = Directory.GetFileSystemEntries(path,card);

				// Console.WriteLine(String.Format("LOCALIO GetFileSystemEntries Exception: {0}",e.Message));
				//Console.WriteLine(String.Format("LocalIO ExpandPath GetFileSystemEntries: {0}",fse));

			} catch (Exception e) {
				//Console.WriteLine(String.Format("LocalIO ExpandPath Exception."));
				//Console.WriteLine(String.Format("LocalIO ExpandPath Exception message: {0}",e.Message));
				//Console.WriteLine(String.Format("LocalIO ExpandPath Exception trace: {0}",e.StackTrace));

				throw e;
			}

			// Console.WriteLine(String.Format("LocalIO ExpandPath GetFileSystemEntries: {0}",fse));

            return new Collection<string>(fse);

		}

// I don't think splitting these 2 was required
// I don't think this is called anymore
		public static Collection<string> ExpandPath(string card, string path, SessionState sess)
		{
		   // Console.WriteLine("EXPANDING: {0} in {1}",card,path);

			Collection<string> pathList = new Collection<string>();
			foreach (string pt in Directory.EnumerateFileSystemEntries(path, card))
			{
				pathList.Add(pt);
			}

			if (card.Length == 0)
				pathList.Add(path);

			return pathList;
		}

// Should return stuff
		public DirectoryInfo MakeDir(string p)
		{
            // as the leaf element is removed from abspath, 
            // all destination operations must add it.
           // string apath = Path.Combine(this.AbsPath(), p);
           // System.IO.Directory.CreateDirectory(apath);
			return System.IO.Directory.CreateDirectory(this.DestinationCombine(p));
		}

		public  DirectoryInfo MakeAbsDir(string p)
		{
			return System.IO.Directory.CreateDirectory(p);
		}

		public SyncStat GetInfo(string lp)
		{
		   // try
		  //  {

				string p = this.SourceCombine(lp);  //read based Combine

               // Console.WriteLine("GetInfo: {0}",p);
			//	Console.WriteLine(" abs: {0} + {1}",this.abspath,lp);
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

		public void SetInfo(string lp, SyncStat f)
		{
			string p = this.DestinationCombine(lp);  // write based combine

			FileSystemInfo pfi = new FileInfo(p);
			if ((f.Attributes & FileAttributes.Directory) == FileAttributes.Directory)
				pfi = new DirectoryInfo(p);

			pfi.Attributes = f.Attributes;
			pfi.CreationTimeUtc = f.CreationTimeUtc;
			pfi.LastWriteTimeUtc = f.LastWriteTimeUtc;

		}

		public string HashTotal(string lp)
		{
			string p = this.SourceCombine(lp);

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
			string p = this.SourceCombine(lp);

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
			string p = this.DestinationCombine(lp);

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
			string p = this.SourceCombine(lp);

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

		public void Delete (string lp)
		{
			string p = this.DestinationCombine(lp);

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
		private string abspath;
		private string cwd;
        private string element;

		public RemoteIO(PSSession s)
		{
			this.session = s;
			this.cwd = GetRemoteCWD(s);
            this.SetPath("");

		}

		public RemoteIO(PSSession s, string pp)
		{
			this.session = s;
			this.cwd = GetRemoteCWD(s);
			this.SetPath(pp);

		}

		private static string GetRemoteCWD(PSSession s)
		{
			Pipeline pipe = s.Runspace.CreatePipeline();
			//pipe.Commands.AddScript("resolve-path ~");
			pipe.Commands.AddScript("get-location");
			Collection<PSObject> rv = pipe.Invoke();
			pipe.Dispose();
			foreach (PSObject ps in rv)
			{
				return ps.ToString();
			}
			throw new System.IO.DirectoryNotFoundException(); 
		}

		public string GetCwd() { return this.cwd; }
		public string SourceCombine(string p) { return System.IO.Path.Combine(this.abspath,p); }
        public string DestinationCombine(string p) { return System.IO.Path.Combine(this.abspath,this.element,p); }

		public void SetPath (string pp) { 
            string apath = Path.Combine(this.cwd,pp); 
            this.abspath = Path.GetDirectoryName(apath);
            this.element = Path.GetFileName(apath);
        }
		public string AbsPath() { return Path.Combine(this.abspath,this.element); }
		public Collection<String> ReadDir() { return this.ReadDir(""); }

        public bool IsDir() { return IsDir(this.AbsPath()); }

		private bool IsDir(string p) {
			string format = @"(([io.file]::GetAttributes(""{0}"") -bAnd [io.fileattributes]::Directory) -ne 0)";

			string command = string.Format(format, p);
			Pipeline pipe = session.Runspace.CreatePipeline();
			pipe.Commands.AddScript(command);
			Collection<PSObject> rv = pipe.Invoke();

			bool ret=false;
			foreach (PSObject ps in rv) { 
				Console.WriteLine(ps.ToString());
				ret= (ps.ToString().Equals("True"));
			   // ret.Add(ps.ToString()); 
			}
			pipe.Dispose();
			return ret;   
		}

		public Collection<string> ReadDir(string lp)
		{
			//string p = this.Combine(lp);
            string searchRoot = this.DestinationCombine(lp);
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

			string command = string.Format(format, searchRoot, this.abspath);

			Pipeline pipe = session.Runspace.CreatePipeline();
			pipe.Commands.AddScript(command);
			Collection<PSObject> rv = pipe.Invoke();
			Collection<string> ret = new Collection<string>();

			foreach (PSObject ps in rv) { ret.Add(ps.ToString()); }
			pipe.Dispose();
			return ret;
		}

		public Collection<string> GetDirs(string lp)
		{
			string p = this.SourceCombine(lp);
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

		public Collection<string> GetFiles(string lp)
		{
			string p = this.SourceCombine(lp);
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

		public DirectoryInfo MakeDir(string lp)
		{
			string p = this.DestinationCombine(lp);
			return this.MakeAbsDir(p);
		}

		public DirectoryInfo MakeAbsDir(string p)
		{
			string format = @"[System.IO.Directory]::CreateDirectory(""{0}"")";
			string command = string.Format(format, p);
			Pipeline pipe = session.Runspace.CreatePipeline();

			pipe.Commands.AddScript(command);
			Collection<PSObject> ol = pipe.Invoke();

			pipe.Dispose();
			/*
			if (ol.Count == 1)
			{
				//Console.WriteLine(ol[0].GetType)
				foreach (PSMemberInfo pmi in ol[0].Members)
					Console.WriteLine("(" + pmi.GetType() + ") " +pmi.Name + " -> "+pmi.Value);

				DirectoryInfo di = (DirectoryInfo)ol[0].BaseObject;
				return di;
			}
			*/
			// this should never happen
			// well I give up unwrapping the object for the moment.
			// until then, this will always happen.
			// do we even use directory info anyway?
			return null;
		}

		public Collection<string> ExpandPath (string p) { return ExpandPath(p,session); }

// this behaves differently to the LocalIO version
// Expand path only takes absolute paths.

		public static Collection<string> ExpandPath (string p, PSSession sess)
		{
			string f = @"resolve-path ""{0}""";
			string command = string.Format(f, p);
			Pipeline pipe = sess.Runspace.CreatePipeline();
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
			string p = this.SourceCombine(lp);
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
			string p = this.DestinationCombine(lp);
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

		public string HashTotal(string lp)
		{
			string p = this.SourceCombine(lp);
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
			string p = this.SourceCombine(lp);
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
			string p = this.DestinationCombine(lp);

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

		public void OldWriteBlock(string lp, Int64 block, byte[] data)
		{
			string p = this.SourceCombine(lp);
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

		public void Delete(string lp)
		{
			string p = this.DestinationCombine(lp);
			Pipeline pipe = session.Runspace.CreatePipeline();

			string format = "remove-item -force \"{0}\""; // Force for hidden files
			string command = string.Format(format, p);
			pipe.Commands.AddScript(command);

			pipe.Invoke();
			pipe.Dispose();
		}

		public string OldHashBlock(string lp, Int64 block)
		{
			string p = this.SourceCombine(lp);
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

		public string HashBlock(string lp, Int64 block)
		{
			string p = this.SourceCombine(lp);
			// p, 
			string format = @"
			$fs=[System.IO.file]::Open(""{0}"",[System.IO.FileMode]::Open)
			$r=$fs.Seek({1},[System.IO.SeekOrigin]::Begin)
			$b=[System.byte[]]::new({2})
			$r=$fs.read($b,0,{2})
			if ($r -eq 0) {{ throw ""EndOfFile"" }}
			$sha=[system.security.cryptography.sha256]::Create()
			$h=$sha.computehash($b,0,$r)
			$sha.dispose()
			$fs.close()
			$h
			";

			Int64 bloffset = block * g_blocksize;

			string command = string.Format(format, 
				p,
				bloffset,
				g_blocksize
			);
			Pipeline pipe = session.Runspace.CreatePipeline();

			pipe.Commands.AddScript(command);

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
