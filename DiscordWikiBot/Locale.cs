using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text.RegularExpressions;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using DSharpPlus.Entities;
using Newtonsoft.Json;
using SmartFormat;
using DSharpPlus;

namespace DiscordWikiBot
{
	class Locale
	{
		// Storage for loaded localisations
		private static Dictionary<string, string> Default;
		private static Dictionary<string, Dictionary<string, string>> Custom;

		// Fallback language should always be English
		private static string FALLBACK_LANG = "en";

		public static void Init()
		{
			Default = LoadLocale(Config.GetLang());
			Custom = new Dictionary<string, Dictionary<string, string>>();
		}

		private static Dictionary<string, string> LoadLocale(string lang)
		{
			string json = "";
			string localePath = string.Format(@"i18n/{0}.json", lang);
			if (!File.Exists(localePath))
			{
				Program.Client.DebugLogger.LogMessage(LogLevel.Warning, "DiscordWikiBot", $"Please create a file with {lang.ToUpper()} locale. Reverting to {FALLBACK_LANG.ToUpper()}.", DateTime.Now);
				localePath = @"i18n/en.json";
			}
			json = File.ReadAllText(localePath, Encoding.Default);

			// Remove authors metadata for the dictionary
			json = Regex.Replace(json, "\"@metadata\": {[^}]+},", String.Empty, RegexOptions.Multiline);

			return JsonConvert.DeserializeObject<Dictionary<string, string>>(json);
		}

		public static void LoadCustomLocale(string lang)
		{
			if (lang == Config.GetLang()) return;
			string localePath = string.Format(@"i18n/{0}.json", lang);
			if (!File.Exists(localePath))
			{
				Program.Client.DebugLogger.LogMessage(LogLevel.Info, "DiscordWikiBot", $"Please create a file with {lang.ToUpper()} locale. Reverting to {FALLBACK_LANG.ToUpper()}.", DateTime.Now);
				if (!Custom.ContainsKey(FALLBACK_LANG))
				{
					LoadCustomLocale(FALLBACK_LANG);
					return;
				}
			}

			if (!Custom.ContainsKey(lang))
			{
				Custom.Add(lang, LoadLocale(lang));
			}
		}

		public static string GetMessage(string key, string lang, params dynamic[] args)
		{
			string str = "";
			Dictionary<string, string> list = Default;

			if (lang != Config.GetLang())
			{
				if (!Custom.ContainsKey(lang))
				{
					LoadCustomLocale(lang);
				}
				list = Custom[lang];
			}

			// Return a MediaWiki-styled key-value pair if there is no message
			if (!list.TryGetValue(key, out str))
			{
				// Revert to fallback language first
				if (lang != FALLBACK_LANG)
				{
					return GetMessage(key, FALLBACK_LANG, args);
				}

				string strArgs = string.Join(", ", args);
				if (strArgs != "")
				{
					strArgs = $": {strArgs}";
				}
				str = string.Format("({0}{1})", key, strArgs);
				return str;
			}

			// Replace messages inside messages (works without arguments)
			str = Regex.Replace(str, @"{msg:([^}\d]+)}", (m) =>
			{
				return GetMessage(m.Groups[1].Value, lang, new string[] { "" });
			});

			// Set params
			if (args.Length > 0)
			{
				str = Smart.Format(CultureInfo.GetCultureInfo(lang), str, args);
			}

			return str;
		}
	}
}
