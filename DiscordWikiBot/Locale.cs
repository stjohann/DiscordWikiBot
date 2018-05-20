using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;

namespace DiscordWikiBot
{
	class Locale
	{
		//private static string prefix = "discordwikibot-";

		private static Dictionary<string, string> List;

		public static void Init()
		{
			List = LoadLocale(Program.Config.Lang);
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

		public static string GetMessage(string key, params string[] args)
		{
			string str = "";

			// Add a prefix to the key
			//key = prefix + key;

			// Return a message without a key if it doesn’t exist
			if ( !List.TryGetValue(key, out str) )
			{
				str = string.Format("<{0}>", key);
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
