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
using Newtonsoft.Json.Linq;
using WikiClientLibrary.Client;
using WikiClientLibrary.Sites;
using System.Threading;

namespace DiscordWikiBot
{
	/// <summary>
	/// Localisation class.
	/// <para>Adds methods for fetching, adding and removing localisations.</para>
	/// </summary>
	class Locale
	{
		// Storage for loaded localisations
		private static Dictionary<string, string> Default;
		private static Dictionary<string, Dictionary<string, string>> Custom = new Dictionary<string, Dictionary<string, string>>();

		/// <summary>
		/// Cache of usable fallback language chains.
		/// </summary>
		private static Dictionary<string, List<string>> LanguageFallbacks = new Dictionary<string, List<string>>();

		/// <summary>
		/// Storage for MediaWiki’s language data
		/// </summary>
		private static JObject LanguageData;

		/// <summary>
		/// Final fallback language of MediaWiki language chain (English)
		/// </summary>
		private static readonly string FALLBACK_LANG = "en";

		/// <summary>
		/// Initialise the default settings and setup things for overrides.
		/// </summary>
		/// <param name="lang">MediaWiki-compatible language code.</param>
		public static void Init(string lang = null)
		{
			// Initialise default locale
			if (lang == null)
			{
				InitLanguages().Wait();
				lang = Config.GetLang();
				Program.LogMessage($"Loading {lang.ToUpper()} locale");
				Default = LoadLocale(lang);

				// Revert to English if default language does not exist
				if (Default == null)
				{
					Program.LogMessage($"Please create a file with {lang.ToUpper()} locale. Reverting to {FALLBACK_LANG.ToUpper()}.", level: LogLevel.Warning);
					Default = LoadLocale(FALLBACK_LANG);
				}
				return;
			}

			// Do not duplicate the data for default language
			if (lang == Config.GetLang())
			{
				return;
			}

			// Do not load the same data twice
			if (!Custom.ContainsKey(lang))
			{
				Dictionary<string, string> locale = LoadLocale(lang);
				if (locale != null)
				{
					Program.LogMessage($"Loading {lang.ToUpper()} locale");
					Custom.Add(lang, locale);
					return;
				}

				// Get a fallback language if locale does not exist
				List<string> fallbackChain = GetFallbackChain(lang);
				string fallback = fallbackChain.First();
				if (!Custom.ContainsKey(fallback))
				{
					Program.LogMessage($"Loading {fallback.ToUpper()} locale as a fallback for {lang.ToUpper()}");
					Custom.Add(fallback, LoadLocale(fallback));
				}
			}
		}

		/// <summary>
		/// Get the language data.
		/// </summary>
		private static async Task InitLanguages()
		{
			LanguageData = await GetLanguageData();
			Console.WriteLine(LanguageData.Count);
		}

		/// <summary>
		/// Load a localisation file in JSON format.
		/// </summary>
		/// <param name="lang">MediaWiki-compatible language code.</param>
		/// <returns>A parsed dictionary of localised messages.</returns>
		private static Dictionary<string, string> LoadLocale(string lang)
		{
			string localePath = $"i18n/{lang}.json";
			if (!File.Exists(localePath))
			{
				return null;
			}
			string json = File.ReadAllText(localePath, Encoding.Default);

			// Remove authors metadata for the dictionary
			json = Regex.Replace(json, "\"@metadata\": {[^}]+},", String.Empty, RegexOptions.Multiline);

			return JsonConvert.DeserializeObject<Dictionary<string, string>>(json);
		}

		/// <summary>
		/// Get localised message in a specified language.
		/// </summary>
		/// <param name="key">Message key in JSON file.</param>
		/// <param name="lang">Language code in ISO 639.</param>
		/// <param name="args">List of arguments to be substituted in a message.</param>
		/// <returns>A parsed message.</returns>
		public static string GetMessage(string key, string lang, params dynamic[] args)
		{
			Dictionary<string, string> list = Default;

			if (lang != Config.GetLang())
			{
				// Try to load a locale if it doesn’t exist
				if (!Custom.ContainsKey(lang))
				{
					Init(lang);
				}

				// Use loaded locale or provide an empty dictionary
				if (Custom.ContainsKey(lang))
				{
					list = Custom[lang];
				}
				else
				{
					list = new Dictionary<string, string>();
				}
			}

			// Go through a MediaWiki-style language fallback chain
			if (!list.TryGetValue(key, out string str))
			{
				// Revert to nearest fallback language if it exis
				if (lang != FALLBACK_LANG)
				{
					List<string> fallbackChain = GetFallbackChain(lang);
					string fallback = fallbackChain.First();
					return GetMessage(key, fallback, args);
				}

				// Return a MediaWiki-styled key-value pair if there is no message
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
				// Break hyphenated language codes for SmartFormat
				string rootLang = lang.Split('-')[0];
				str = Smart.Format(CultureInfo.GetCultureInfo(rootLang), str, args);
			}

			return str;
		}

		/// <summary>
		/// Get usable fallback chain for languages without a localisation.
		/// </summary>
		/// <param name="lang">MediaWiki-compatible language code.</param>
		/// <returns>List of language fallbacks.</returns>
		private static List<string> GetFallbackChain(string lang)
		{
			// Use cache if it exists
			if (LanguageFallbacks.ContainsKey(lang))
			{
				return LanguageFallbacks[lang];
			}

			// Compile a list of fallbacks with locales
			List<string> result = new List<string>();
			string[] fallbacks = LanguageData[lang]?["fallbacks"].Select(jt => (string)jt).ToArray();
			if (fallbacks != null)
			{
				foreach (var fallbackCode in fallbacks)
				{
					string localePath = $"i18n/{fallbackCode}.json";
					if (File.Exists(localePath))
					{
						result.Add(fallbackCode);
					}
				}

				// Check for other language fallbacks until we hit English
				string lastFallback = fallbacks.LastOrDefault();
				if (lastFallback != null && lastFallback != FALLBACK_LANG)
				{
					result.AddRange(GetFallbackChain(lastFallback));
				}
			}

			// Add English and return the list
			result.Add(FALLBACK_LANG);
			result = result.Distinct().ToList();
			LanguageFallbacks.Add(lang, result);
			return result;
		}

		/// <summary>
		/// Get supported languages list from MediaWiki API.
		/// </summary>
		/// <returns>List of supported languages.</returns>
		private static async Task<JObject> GetLanguageData()
		{
			string url = Config.GetWiki();
			string urlWiki = "/wiki/$1";
			WikiClient wikiClient = new WikiClient
			{
				ClientUserAgent = Program.UserAgent,
			};
			WikiSite site = new WikiSite(wikiClient, url.Replace(urlWiki, "/w/api.php"));
			await site.Initialization;

			// Fetch languages using new API (MediaWiki 1.34+)
			JToken result = await site.InvokeMediaWikiApiAsync(
				new MediaWikiFormRequestMessage(new
				{
					action = "query",
					meta = "languageinfo",
					liprop = "code|autonym|name|fallbacks|variants"
				}),
				new CancellationToken()
			);

			if (result?["warnings"] == null)
			{
				JObject languageinfo = (JObject)result["query"]?["languageinfo"];
				return languageinfo;
			}

			// Fetch languages using old API
			result = await site.InvokeMediaWikiApiAsync(
				new MediaWikiFormRequestMessage(new
				{
					action = "query",
					meta = "siteinfo",
					siprop = "languages"
				}),
				new CancellationToken()
			);

			// Convert the old format to new one
			JToken temp = result["query"]?["languages"];
			JObject languages = new JObject(
				temp.Select(jt => new JProperty((string)jt["code"], jt))
			);

			return languages;
		}

		/// <summary>
		/// Determines whether the language code is a valid language.
		/// </summary>
		/// <param name="lang">MediaWiki-compatible language code.</param>
		public static bool IsValidLanguage(string lang)
		{
			if (LanguageData == null)
			{
				return false;
			}

			return (LanguageData[lang] != null);
		}

		/// <summary>
		/// Get a language name in the language.
		/// </summary>
		/// <param name="lang">MediaWiki-compatible language code.</param>
		public static string GetLanguageName(string lang)
		{
			if (LanguageData == null)
			{
				return lang.ToUpper();
			}

			return (LanguageData[lang]?["autonym"].ToString() ?? LanguageData[lang]?["*"].ToString() ?? lang.ToUpper());
		}
	}
}
