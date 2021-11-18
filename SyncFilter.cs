using System;
using System.IO;
using System.Management.Automation;
using System.Text.RegularExpressions;

namespace net.ninebroadcast
{

    public class SyncFilter
	{
		// private string[] includeList;

		// update
		// size
		// checksum

		// some sort of observer or strategy pattern.
		// add these to an array and evaluate the array.

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
			return include;
		}

		public bool exclude (string file, string[] excludeList)
		{
			bool include = true;
			if (excludeList != null)
			{
				foreach (string m in excludeList)
				{
					Match me = Regex.Match(file,m);
					if (me.Success) { include = false; }
					// if (LikeOperator.LikeString(file, m, Microsoft.VisualBasic.CompareMethod.Text)) { include = false; }
				}
			}
			return include;
		}
	}
}
