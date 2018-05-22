using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using DSharpPlus.Entities;
using Newtonsoft.Json;

namespace DiscordWikiBot
{
	class Locale
	{
		private static Dictionary<string, string> Default;
		private static Dictionary<string, Dictionary<string, string>> Custom;

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
				Console.WriteLine("Please create a file with {0} locale. Reverting to EN.", lang.ToUpper());
				localePath = @"i18n/en.json";
			}
			json = File.ReadAllText(localePath);

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
				Console.WriteLine("Please create a file with {0} locale. Reverting to EN.", lang.ToUpper());
				if (!Custom.ContainsKey("en"))
				{
					LoadCustomLocale("en");
					return;
				}
			}

			if (!Custom.ContainsKey(lang))
			{
				Custom.Add(lang, LoadLocale(lang));
			}
		}

		public static string GetMessage(string key, string lang, params string[] args)
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
			if ( !list.TryGetValue(key, out str) )
			{
				string strArgs = string.Join(", ", args);
				if (strArgs != "")
				{
					strArgs = $": {strArgs}";
				}
				str = string.Format("({0}{1})", key, strArgs);
				return str;
			}

			// Set params
			if (args.Length > 0)
			{
				str = string.Format(str, args);
			}

			return str;
		}
	}
}
