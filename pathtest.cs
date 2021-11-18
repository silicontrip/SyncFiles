using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Management.Automation;
using System.Management.Automation.Host;
using System.Management.Automation.Runspaces;

namespace net.ninebroadcast {
 
 // Get-ChildItem ...  Get-Item ... Get-Content  
 // I really do think that they went overboard with the verb-noun paradigm
    [Cmdlet(VerbsDiagnostic.Test, "Sync")]
    public class testSync : PSCmdlet
    {

        public
        testSync()
        {
            // empty, provided per design guidelines.
        }

        [Alias("FullName")]
        [Parameter(Position = 0, ValueFromPipelineByPropertyName = true)]
        public string[] Path
        {
            get { return path; }
            set { path = value; }
        }
        private string[] path = {"."};

        [Parameter()]
        public PSSession PSSession
        {
            get { return session; }
            set { session = value; }
        }
        private PSSession session;

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

		//	Console.WriteLine ("expanded from path: {0} -> {1} ",p,String.Join(", ",expath));

                // expath should all be absolute
                foreach (string ep in expath)
                {
                    if (session != null)
                    {
		//				Console.WriteLine("Remote add: {0}",ep);
                        src.Add( new RemoteIO(session,ep));
                    }
                    else
                    {
		//				Console.WriteLine("local add: {0}",ep);

                        src.Add( new LocalIO(this.SessionState,ep));
                    }
                }

            }
            return new Collection<IO> (src);
        }

		protected override void BeginProcessing()
		{

            
			Console.WriteLine ("command line path: {0}",path);

            Collection<IO> src = IOFactory(session,path);

            foreach (IO cdir in src)
            {
                Console.WriteLine("abs: {0}", cdir.AbsPath());
                Collection<string> dd = cdir.ReadDir();

                foreach (string p in dd)
                    Console.WriteLine(p);
            }                                                                                                                                                                                                                                                      
		}
	}

}