using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using System.Threading.Tasks;
using DiscordWikiBot.Schemas;
using DSharpPlus.Entities;

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

		/// <summary>
		/// Default config for all servers.
		/// </summary>
		private static ConfigData Default;

		/// <summary>
		/// Config overrides for different servers.
		/// </summary>
		private static Dictionary<string, ConfigData> Overrides;

		/// <summary>
		/// JSON serialiser options.
		/// </summary>
		private static JsonSerializerOptions serializerOptions = new JsonSerializerOptions()
		{
			TypeInfoResolver = new DefaultJsonTypeInfoResolver(),
			WriteIndented = true,
			// IndentCharacter = "\t",
			// IndentSize = 1,
		};

		/// <summary>
		/// Relative path to main config file.
		/// </summary>
		private const string configPath = @"config.json";
		
		/// <summary>
		/// Relative path to overrides file.
		/// </summary>
		private const string overridesPath = @"overrides.json";

		/// <summary>
		/// Initialise the default settings and setup things for overrides.
		/// </summary>
		public static void Init()
		{
			// Get a file with default config
			if (!File.Exists(configPath))
			{
				Console.WriteLine("Please create a JSON file called \"config.json\" before running the bot!");
				Console.WriteLine("[Press any key to exit...]");
				Console.ReadKey();
				Environment.Exit(0);
			}
			Default = LoadConfig<ConfigData>(configPath);

			// Set default prefix if there’s none
			if (!Default.TryGetValue("prefix", out _))
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
			Overrides = LoadConfig<Dictionary<string, ConfigData>>(overridesPath);
		}

		/// <summary>
		/// Load a configuration file for specified path.
		/// </summary>
		/// <param name="path">File path.</param>
		/// <returns>List of configuration variables.</returns>
		public static T LoadConfig<T>(string path)
		{
			string json = "{}";
			if (File.Exists(path))
			{
				json = File.ReadAllText(path, Encoding.Default);
			}

			return JsonSerializer.Deserialize<T>(json);
		}

		/// <summary>
		/// Generic function to return a variable from configuration.
		/// <para>Use more specific functions if possible.</para>
		/// </summary>
		/// <param name="key">Configuration key.</param>
		/// <param name="goal">Discord channel or Discord guild ID.</param>
		/// <param name="useDefault">Use default value if none exists for the goal.</param>
		/// <returns>A value of a configuration variable.</returns>
		public static object GetValue(string key, string goal = "", bool useDefault = true)
		{
			// Return an override if it exists
			if (goal != "" && goal != null)
			{
				if (Overrides.TryGetValue(goal, out _) && Overrides[goal].TryGetValue(key, out object oValue))
				{
					return oValue;
				}

				if (!useDefault) return null;
			}

			// Return default value
			var hasDefault = Default.TryGetValue(key, out object value);
			if (!hasDefault)
			{
				return null;
			}

			var valType = value.GetType();
			if (valType == typeof(string) && value.ToString() == "")
			{
				return null;
			}

			return value;
		}

		/// <param name="channel">Discord channel instance.</param>
		/// <inheritdoc cref="GetValue" />
		public static object GetValue(string key, DiscordChannel channel, bool useDefault = true)
		{
			var goal = GetChannelId(channel);

			var result = GetValue(key, goal, false);
			if (result != null || !useDefault)
			{
				return result;
			}

			return GetValue(key, channel.GuildId.ToString(), useDefault);
		}

		/// <summary>
		/// Get an ID for current channel in config.
		/// </summary>
		/// <param name="channel">Discord channel instance.</param>
		private static string GetChannelId(DiscordChannel channel)
		{
			var goal = "#" + channel.Id;
			if (channel.IsThread)
			{
				goal = "#" + channel.ParentId;
			}

			return goal;
		}

		/// <summary>
		/// Get EventStreams domain from configuration.
		/// </summary>
		/// <param name="server">Discord server instance.</param>
		/// <returns>EventStreams domain.</returns>
		public static string GetDomain(DiscordGuild server = null)
		{
			return GetValue("domain", server?.Id.ToString())?.ToString();
		}

		/// <summary>
		/// Get language of the bot in a server.
		/// </summary>
		/// <param name="server">Discord server instance.</param>
		/// <returns>Language code in ISO 639 format.</returns>
		public static string GetLang(DiscordGuild server = null)
		{
			return GetValue("lang", server?.Id.ToString())?.ToString();
		}

		/// <summary>
		/// Get whether a specific server allows to respond to bots.
		/// </summary>
		/// <param name="server">Discord server instance.</param>
		public static bool GetAnswerBots(DiscordGuild server = null)
		{
			return GetValue("answerBots", server?.Id.ToString()) != null;
		}

		/// <summary>
		/// Get TranslateWiki notifications channel in a server.
		/// </summary>
		/// <param name="server">Discord server instance.</param>
		/// <returns>Discord channel ID.</returns>
		public static string GetTWChannel(DiscordGuild server = null)
		{
			return GetValue("translatewiki-channel", server?.Id.ToString())?.ToString();
		}

		/// <summary>
		/// Get TranslateWiki notifications language in a server.
		/// </summary>
		/// <param name="server">Discord server ID.</param>
		/// <returns>Language code in ISO 639 format.</returns>
		public static string GetTWLang(DiscordGuild server = null)
		{
			return GetValue("translatewiki-lang", server?.Id.ToString())?.ToString();
		}

		/// <summary>
		/// Get a standard wiki link in a goal (server/channel).
		/// </summary>
		/// <param name="goal">Discord server/channel ID.</param>
		/// <param name="useDefault">Use default value if none exists for the goal.</param>
		/// <returns>Wiki URL.</returns>
		public static string GetWiki(string goal = "", bool useDefault = true)
		{
			return GetValue("wiki", goal, useDefault)?.ToString();
		}

		/// <param name="server">Discord server instance.</param>
		/// <inheritdoc cref="GetWiki" />
		public static string GetWiki(DiscordGuild server, bool useDefault = true)
		{
			return GetWiki(server.Id.ToString(), useDefault);
		}

		/// <param name="channel">Discord channel instance.</param>
		/// <inheritdoc cref="GetWiki" />
		public static string GetWiki(DiscordChannel channel, bool useDefault = true)
		{
			return GetValue("wiki", channel, useDefault)?.ToString();
		}

		/// <summary>
		/// Set an override of default configuration on a specified goal.
		/// </summary>
		/// <param name="goal">Discord channel or Discord server ID.</param>
		/// <param name="key">Configuration key.</param>
		/// <param name="value">Override value.</param>
		/// <returns>Response code with a specified result.</returns>
		public static async Task<int> SetOverride(string goal, string key, object value)
		{
			if (Overrides == null) return RESULT_STRANGE;
			if (goal == null || goal == "") return RESULT_STRANGE;
			if (key == null || key == "") return RESULT_STRANGE;

			// Change current data
			var goalObj = Overrides.GetValueOrDefault(goal);
			if (goalObj == null)
			{
				Overrides[goal] = new ConfigData();
				goalObj = Overrides[goal];
			}

			// Check if value is the same
			if (goalObj.TryGetValue(key, out object current))
			{
				if (current == value)
				{
					return RESULT_SAME;
				}
			}

			Program.LogMessage($"Changing an override ({goal}: {key}) after a command was fired.");
			int code = RESULT_CHANGE;

			// Reset value if value equals - or false
			var valueType = value?.GetType();
			var defaultVal = GetValue(key);
			if (valueType != null)
			{
				if (valueType == typeof(string) && (value.ToString() == "" || value?.ToString() == "-"))
				{
					value = null;
				}
				else if (valueType == typeof(bool))
				{
					if (defaultVal == null)
					{
						value = (bool)value == false ? null : value;
					}
					else
					{
						value = (bool)value == (bool)defaultVal ? null : value;
					}
				}
			}

			// Check if the new value is the same as default
			if (defaultVal != null && value == defaultVal)
			{
				return RESULT_SAME;
			}

			// Set the new value
			if (value == null)
			{
				Overrides[goal].Remove(key);
				code = RESULT_RESET;
			}
			else
			{
				Overrides[goal][key] = value;
			}

			// Clean up the key without any values
			if (Overrides?[goal].Count == 0)
			{
				Overrides.Remove(goal);
			}

			// Write it to JSON file
			await using FileStream createStream = File.Create(overridesPath);
			await JsonSerializer.SerializeAsync(createStream, Overrides, serializerOptions);
			return code;
		}

		/// <summary>
		/// Set an override for a server.
		/// </summary>
		/// <param name="server">Discord server instance.</param>
		/// <inheritdoc cref="SetOverride" />
		public static async Task<int> SetOverride(DiscordGuild server, string key, object value)
		{
			return await SetOverride(server.Id.ToString(), key, value);
		}

		/// <summary>
		/// Set an override for a channel.
		/// </summary>
		/// <param name="channel">Discord channel instance.</param>
		/// <inheritdoc cref="SetOverride" />
		public static async Task<int> SetOverride(DiscordChannel channel, string key, object value)
		{
			return await SetOverride(GetChannelId(channel), key, value);
		}

		/// <summary>
		/// Parse yes/no in specified language and English as true/false.
		/// </summary>
		/// <param name="value">Boolean-like string.</param>
		/// <param name="lang">Language code in ISO 639 format.</param>
		public static bool ParseBool(string value, string lang)
		{
			var result = false;
			if (Boolean.TryParse(value, out result))
			{
				return result;
			}

			value = value.ToLower();
			var yes = Locale.GetMessage("yes", lang).ToLower();
			var enYes = Locale.GetMessage("yes", "en").ToLower();
			if (value == yes || value == enYes)
			{
				return true;
			}

			return result;
		}
	}
}
