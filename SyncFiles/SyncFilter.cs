using System;
using System.IO;
using System.Management.Automation;

namespace net.ninebroadcast
{

    public class SyncFilter
	{
		private string[] includeList;


		public bool include(string file, string[] includeList)
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
				}
			}
		}


	}
}
