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
	/// <summary>
	/// Configuration class.
	/// <para>Adds methods for reading saved configuration and changing it per server.</para>
	/// </summary>
	class Config
	{
		/// <summary>
		/// Response code: Bad parameters or errors.
		/// </summary>
		public const int RESULT_STRANGE = -2;

		/// <summary>
		/// Response code: Configuration wasn’t changed.
		/// </summary>
		public const int RESULT_SAME = -1;

		/// <summary>
		/// Response code: Variable was reset back to default.
		/// </summary>
		public const int RESULT_RESET = 0;

		/// <summary>
		/// Response code: Variable was changed.
		/// </summary>
		public const int RESULT_CHANGE = 1;

		// Data storage
		private static JObject Default;
		private static JObject Overrides;

		// JSON file paths
		private const string cfgPath = @"config.json";
		private const string overridesPath = @"overrides.json";

		/// <summary>
		/// Initialise the default settings and setup things for overrides.
		/// </summary>
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

		/// <summary>
		/// Load a configuration file for specified path.
		/// </summary>
		/// <param name="path">File path.</param>
		/// <returns>List of configuration variables.</returns>
		static public JObject LoadConfig(string path)
		{
			string json = "{}";
			if (!File.Exists(path))
			{
				return JObject.Parse(json);
			}
			json = File.ReadAllText(path, Encoding.Default);

			return JObject.Parse(json);
		}

		/// <summary>
		/// Generic function to return a variable from configuration.
		/// <para>Use more specific functions if possible.</para>
		/// </summary>
		/// <param name="key">Configuration key.</param>
		/// <param name="goal">Discord channel or Discord guild ID.</param>
		/// <returns>A value of a configuration variable.</returns>
		static public string GetValue(string key, string goal = "")
		{
			JObject source;
			JToken value;
			string str = null;

			// Return an override if it exists
			if (goal != "" && goal != null)
			{
				source = (JObject)Overrides[goal];
				if (source != null && source.TryGetValue(key, out value))
				{
					str = value.ToString();
					return str;
				}
			}

			// Return default value
			value = Default.GetValue(key);
			if (value != null) {
				string val = value.ToString();
				if (val != "")
				{
					str = val;
				}
			}
			return str;
		}

		/// <summary>
		/// Get EventStreams domain from configuration.
		/// </summary>
		/// <param name="goal">Discord server ID.</param>
		/// <returns>EventStreams domain.</returns>
		static public string GetDomain(string goal = "")
		{
			return GetValue("domain", goal);
		}

		/// <summary>
		/// Get an internal value by key from configuration.
		/// </summary>
		/// <param name="key">Internal configuration key.</param>
		/// <param name="goal">Discord channel or Discord server ID.</param>
		/// <returns>An internal value of a key.</returns>
		static public string GetInternal(string key, string goal = "")
		{
			if (key == null) return "";
			return GetValue("_" + key, goal);
		}

		/// <summary>
		/// Get language of the bot in a server.
		/// </summary>
		/// <param name="goal">Discord server ID.</param>
		/// <returns>Language code in ISO 639 format.</returns>
		static public string GetLang(string goal = "")
		{
			return GetValue("lang", goal);
		}

		/// <summary>
		/// Get TranslateWiki notifications channel in a server.
		/// </summary>
		/// <param name="goal">Discord server ID.</param>
		/// <returns>Discord channel ID.</returns>
		static public string GetTWChannel(string goal = "")
		{
			return GetValue("translatewiki-channel", goal);
		}

		/// <summary>
		/// Get TranslateWiki notifications language in a server.
		/// </summary>
		/// <param name="goal">Discord server ID.</param>
		/// <returns>Language code in ISO 639 format.</returns>
		static public string GetTWLang(string goal = "")
		{
			return GetValue("translatewiki-lang", goal);
		}

		/// <summary>
		/// Get a standard wiki link in a server.
		/// </summary>
		/// <param name="goal">Discord server ID.</param>
		/// <returns>Wiki URL.</returns>
		static public string GetWiki(string goal = "")
		{
			return GetValue("wiki", goal);
		}

		/// <summary>
		/// Check if a specified value is present in keys with same name.
		/// </summary>
		/// <param name="key">Configuration key.</param>
		/// <param name="value">Configuration value.</param>
		/// <returns>A boolean value for whether a value is present or not.</returns>
		static public bool IsValuePresent(string key, string value)
		{
			bool isPresent = false;
			foreach(var guild in Overrides)
			{
				JToken cfg = guild.Value;

				JObject obj = cfg.ToObject<JObject>();
				if (obj != null && obj.TryGetValue(key, out JToken current))
				{
					if (current.ToString() == value)
					{
						isPresent = true;
					}
				}
			}

			return isPresent;
		}

		/// <summary>
		/// Set an override of default configuration on a specified goal.
		/// </summary>
		/// <param name="goal">Discord channel or Discord server ID.</param>
		/// <param name="key">Configuration key.</param>
		/// <param name="value">Override value.</param>
		/// <returns>Response code with a specified result.</returns>
		static public int SetOverride(string goal, string key, string value)
		{
			if (Overrides == null) return RESULT_STRANGE;
			if (goal == null || goal == "") return RESULT_STRANGE;
			Program.Client.DebugLogger.LogMessage(LogLevel.Info, "Config", $"Changing a server override ({key}) after a command was fired.", DateTime.Now);

			// Change current data
			JObject goalObj = (JObject)Overrides[goal];
			if (goalObj == null)
			{
				goalObj = new JObject();
				Overrides.Add(goal, goalObj);
			}

			// Check if value is the same
			if (goalObj.TryGetValue(key, out JToken current))
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
			if (value == "-" || value == GetValue(key))
			{
				(Overrides.Property(goal).Value as JObject).Property(key).Remove();
				code = RESULT_RESET;
			}

			// Write it to JSON file
			File.WriteAllText(overridesPath, Overrides.ToString(), Encoding.Default);
			return code;
		}

		/// <summary>
		/// Set an internal variable on a specified goal.
		/// </summary>
		/// <param name="goal">Discord channel or Discord server ID.</param>
		/// <param name="key">Internal configuration key.</param>
		/// <param name="value">Internal value.</param>
		/// <returns>Response code with a specified result.</returns>
		static public int SetInternal(string goal, string key, string value)
		{
			if (key == null) return RESULT_STRANGE;
			return SetOverride(goal, "_" + key, value);
		}
	}
}
