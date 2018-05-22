using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;
using DSharpPlus;
using Newtonsoft.Json.Linq;

namespace DiscordWikiBot
{
	class Config
	{
		public const int RESULT_STRANGE = -2;
		public const int RESULT_SAME = -1;
		public const int RESULT_RESET = 0;
		public const int RESULT_CHANGE = 1;

		// Data storage
		private static JObject Default;
		public static JObject Overrides;

		// JSON file paths
		private const string cfgPath = @"config.json";
		private const string overridesPath = @"overrides.json";

		static public void Init()
		{
			// Get a file with default config
			if (!File.Exists(cfgPath))
			{
				Console.WriteLine("Please create a JSON file called \"config.json\" before running the bot!");
				Console.WriteLine("[Press any key to exit...]");
				Console.ReadKey();
				Environment.Exit(0);
			}
			Default = LoadConfig(cfgPath);

			// Set default prefix if there’s none
			if (Default["prefix"] == null)
			{
				Default["prefix"] = "!";
			}

			// Get a file with overrides
			if (!File.Exists(overridesPath))
			{
				Console.WriteLine("Please create a JSON file called \"overrides.json\" before running the bot!");
				Console.WriteLine("[Press any key to exit...]");
				Console.ReadKey();
				Environment.Exit(0);
			}
			Overrides = LoadConfig(overridesPath);
		}

		static public JObject LoadConfig(string path)
		{
			string json = "{}";
			if (!File.Exists(path))
			{
				return JObject.Parse(json);
			}
			json = File.ReadAllText(path);

			return JObject.Parse(json);
		}

		static public string GetValue(string key, string goal = "")
		{
			JObject source;
			JToken value;
			
			// Return an override if it exists
			if (goal != "" || goal != null)
			{
				source = (JObject)Overrides[goal];
				if (source != null && source.TryGetValue(key, out value))
				{
					return value.ToString();
				}
			}

			// Return default value
			value = Default.GetValue(key).ToString();
			return value.ToString();
		}

		static public string GetDomain(string goal = "")
		{
			return GetValue("domain", goal);
		}

		static public string GetLang(string goal = "")
		{
			return GetValue("lang", goal);
		}

		static public string GetWiki(string goal = "")
		{
			return GetValue("wiki", goal);
		}

		static public bool IsValuePresent(string key, string value)
		{
			bool isPresent = false;
			foreach(var guild in Overrides)
			{
				JToken cfg = guild.Value;

				JToken current;
				JObject obj = cfg.ToObject<JObject>();
				if (obj != null && obj.TryGetValue(key, out current))
				{
					if (current.ToString() == value)
					{
						isPresent = true;
					}
				}
			}

			return isPresent;
		}

		static public int SetOverride(string goal, string key, string value)
		{
			if (Overrides == null) return RESULT_STRANGE;
			if (goal == null || goal == "") return RESULT_STRANGE;
			Program.Client.DebugLogger.LogMessage(LogLevel.Info, "Config", $"Changing server overrides after a command was fired", DateTime.Now);

			// Change current data
			JObject goalObj = (JObject)Overrides[goal];
			if (goalObj == null)
			{
				goalObj = new JObject();
				Overrides.Add(goal, goalObj);
			}

			// Check if value is the same
			JToken current;
			if (goalObj.TryGetValue(key, out current))
			{
				if (current.ToString() == value)
				{
					return RESULT_SAME;
				}
			}

			// Set the new value
			Overrides[goal][key] = value;

			// Reset data if it matches defaults
			int code = RESULT_CHANGE;
			if (value == GetValue(key))
			{
				(Overrides.Property(goal).Value as JObject).Property(key).Remove();
				code = RESULT_RESET;
			}

			// Write it to JSON file
			File.WriteAllText(overridesPath, Overrides.ToString());
			return code;
		}
	}
}
