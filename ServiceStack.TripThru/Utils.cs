using System;
using System.Collections.Generic;
using System.Collections;
using System.Linq;
using System.Text;
using System.Diagnostics;
using System.Web;
using System.Xml;
using System.Xml.XPath;

namespace ServiceStack.Utils
{
	class GeoLocation
	{
		// http://code.google.com/apis/maps/documentation/geocoding/#ReverseGeocoding
		public static string ReverseGeoLoc(string longitude, string latitude)
		{
			XmlDocument doc = new XmlDocument();
			{
				doc.Load("http://maps.googleapis.com/maps/api/geocode/xml?latlng=" + latitude + "," + longitude + "&sensor=false");
				XmlNode element = doc.SelectSingleNode("//GeocodeResponse/status");
				if (element.InnerText == "ZERO_RESULTS")
					return null;
				else
				{
					element = doc.SelectSingleNode("//GeocodeResponse/result/formatted_address");
					return element.InnerText;
				}
			}
		}
	}

	public class Logger
	{
		static int tab;
		public static int logLine;
		public static bool enabled { get; set; }
		public static bool forceOn { get; set; }
		public static string filePath = "c:\\";
		static int on;
		static System.IO.StreamWriter logFile;
		//        [Conditional("DEBUG")]
		static public void Tab() { tab++; }
		//        [Conditional("DEBUG")]
		static public void Untab() { tab--; }
		static public string GetTab()
		{
			string tabs = "";
			for (int n = 0; n <= tab; n++)
				tabs += '\t';
			//                tabs += "-----";
			return tabs;
		}
		static public void Off()
		{
			on--;
		}
		static public void On()
		{
			on++;
		}

		static public void Log(string msg, string filename)
		{
			if (filename.Length == 0)
			{
				Log(msg);
				return;
			}
			OpenLog(filename, true, true);
			logFile.WriteLine(GetTab() + msg);
			logFile.Flush();
			CloseLog();
		}
		[Conditional("DEBUG")]
		static public void Log(string msg)
		{
			{
				if (logFile == null)
					return;
				if (!enabled && !forceOn)
					return;
			}
			logFile.WriteLine(GetTab() + msg);
			logFile.Flush();
		}
		static public void OpenLog(string filename, bool enabled_, bool append = false)
		{
			if (!append)
			{
				tab = 0;
				logLine = 0;
				forceOn = false;
				enabled = enabled_;
			}
			enabledMethods = new LinkedList<string>();
			logFile = new System.IO.StreamWriter("c:\\Users\\DanielErnesto\\" + filename);
			//logFile = new System.IO.StreamWriter(filePath + filename, append);
		}

		static public void CloseLog()
		{
			logFile.Close();
			logFile = null;
		}
		static public LinkedList<string> enabledMethods;
	}
}

