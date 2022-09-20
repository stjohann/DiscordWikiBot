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
		/// Load language data.
		/// </summary>
		public static async Task Load()
		{
			LanguageData = await GetLanguageData();
		}

		/// <summary>
		/// Initialise the default settings and setup things for overrides.
		/// </summary>
		/// <param name="lang">MediaWiki-compatible language code.</param>
		public static void Init(string lang = null)
		{
			// Initialise default locale
			if (lang == null)
			{
				lang = Config.GetLang();
				Program.LogMessage($"Loading {lang.ToUpper()} locale...");
				Default = LoadLocale(lang);

				// Revert to English if default language does not exist
				if (Default == null)
				{
					Program.LogMessage($"No file found for {lang.ToUpper()} locale. {FALLBACK_LANG.ToUpper()} locale will be used for it.", level: "warning");
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
					Program.LogMessage($"Loaded {lang.ToUpper()} locale.");
					Custom.Add(lang, locale);
					return;
				}
			}
		}

		/// <summary>
		/// Load a localisation file in JSON format.
		/// </summary>
		/// <param name="lang">MediaWiki-compatible language code.</param>
		/// <returns>A parsed dictionary of localised messages.</returns>
		private static Dictionary<string, string> LoadLocale(string lang)
		{
			var localePath = GetLocalePath(lang);
			if (localePath == null)
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
			var str = GetFallbackChain(lang)
				.Select(lng => GetMessageCode(key, lng))
				.Where(x => x != null && !x.StartsWith("!!FUZZY!!"))
				.FirstOrDefault();

			// Return a MediaWiki-styled key-value pair if there is no message
			if (str == null)
			{
				string strArgs = string.Join(", ", args);
				if (strArgs != "")
				{
					strArgs = $": {strArgs}";
				}
				return string.Format("({0}{1})", key, strArgs);
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
				try
				{
					str = Smart.Format(CultureInfo.GetCultureInfo(rootLang), str, args);
				} catch(Exception)
				{
					str = Smart.Format(str, args);
				}
			}

			return str;
		}

		/// <summary>
		/// Get the message code in a specified language.
		/// </summary>
		/// <param name="key">Message key in JSON file.</param>
		/// <param name="lang">Language code in ISO 639.</param>
		/// <param name="fallback">Fallback language (for tracking).</param>
		/// <returns>An unparsed message or null.</returns>
		private static string GetMessageCode(string key, string lang)
		{
			Dictionary<string, string> list = Default;

			if (lang != Config.GetLang())
			{
				// Try to load a locale if it doesn’t exist yet
				if (!Custom.ContainsKey(lang))
				{
					Init(lang);
				}

				if (!Custom.ContainsKey(lang))
				{
					return null;
				}

				// Use loaded locale
				list = Custom[lang];
			}

			if (list.TryGetValue(key, out string str))
			{
				return str;
			}

			return null;
		}

		/// <summary>
		/// Get usable fallback chain for languages without a localisation.
		/// </summary>
		/// <param name="lang">MediaWiki-compatible language code.</param>
		/// <returns>List of language fallbacks for a language.</returns>
		private static List<string> GetFallbackChain(string lang)
		{
			// Use cache if it exists
			if (LanguageFallbacks.ContainsKey(lang))
			{
				return LanguageFallbacks[lang];
			}

			// Compile a list of fallbacks with locales
			List<string> result = new List<string>(new[] { lang });
			var langs = new Queue<string>(result);

			while (langs.Count > 0)
			{
				foreach (var fallback in GetFallbackData(langs.Dequeue()))
				{
					if (result.Contains(fallback))
						continue;

					result.Add(fallback);
					langs.Enqueue(fallback);
				}
			}

			// Add English and return the list
			result.Add(FALLBACK_LANG);
			LanguageFallbacks.Add(lang, result);
			return result;
		}

		/// <summary>
		/// Get fallback languages for a specified language.
		/// </summary>
		/// <param name="code">MediaWiki-compatible language code.</param>
		/// <returns>A language chain or null.</returns>
		private static List<string> GetFallbackData(string code)
		{
			var result = LanguageData[code]?["fallbacks"]
				.Select(jt => (string)jt).ToList();

			return result != null ? result : new List<string>();
		}

		/// <summary>
		/// Get supported languages list from MediaWiki API.
		/// </summary>
		/// <returns>List of supported languages.</returns>
		private static async Task<JObject> GetLanguageData()
		{
			string url = Config.GetWiki();
			var site = Linking.GetWikiSite(url).Result;
			if (site == null)
			{
				Program.LogMessage($"Fetching language data from {url} failed. Please set a different default wiki URL.", "Locale", "error");
				return null;
			}

			// Fetch languages using new API (MediaWiki 1.34+)
			try
			{
				JToken newRequest = await site.InvokeMediaWikiApiAsync(
					new MediaWikiFormRequestMessage(new
					{
						action = "query",
						meta = "languageinfo",
						liprop = "code|autonym|name|fallbacks|variants"
					}),
					new CancellationToken()
				);

				if (newRequest?["warnings"] == null)
				{
					JObject languageinfo = (JObject)newRequest["query"]?["languageinfo"];
					return languageinfo;
				}
			} catch (Exception ex)
			{
				Program.LogMessage($"Fetching language list (1.34+) returned an error: {ex}", level: "warning");
			}

			// Fetch languages using old API
			try
			{
				JToken oldRequest = await site.InvokeMediaWikiApiAsync(
					new MediaWikiFormRequestMessage(new
					{
						action = "query",
						meta = "siteinfo",
						siprop = "languages"
					}),
					new CancellationToken()
				);

				// Convert the old format to new one
				JToken temp = oldRequest["query"]?["languages"];
				JObject languages = new JObject(
					temp.Select(jt => new JProperty((string)jt["code"], jt))
				);

				return languages;
			} catch (Exception ex)
			{
				Program.LogMessage($"Fetching language list (<1.34) returned an error: {ex}", level: "error");
			}

			return null;
		}

		/// <summary>
		/// Get locale path if the file for it exists.
		/// </summary>
		/// <param name="lang">MediaWiki-compatible language code.</param>
		private static string GetLocalePath(string lang)
		{
			string localePath = $"i18n/{lang}.json";
			if (File.Exists(localePath))
			{
				return localePath;
			}

			return null;
		}

		/// <summary>
		/// Determines whether the language code is a valid language.
		/// </summary>
		/// <param name="lang">MediaWiki-compatible language code.</param>
		public static bool IsValidLanguage(string lang)
		{
			if (lang == null || lang == "") return false;
			if (LanguageData == null)
			{
				Program.LogMessage($"No language data is present. This means that you need to restart the bot. The assessment of {lang.ToUpper()} can be wrong as a result.", level: "error");
				return CultureInfo.GetCultures(CultureTypes.NeutralCultures)
					.Select(c => c.Name.ToLower())
					.Any(name => name == lang);
			}

			return (LanguageData?[lang] != null);
		}

		/// <summary>
		/// Get a language name in the language.
		/// </summary>
		/// <param name="lang">MediaWiki-compatible language code.</param>
		/// <param name="str">Formatting string.</param>
		public static string GetLanguageName(string lang, string str = "")
		{
			if (LanguageData == null)
			{
				return lang;
			}

			string name = (LanguageData?[lang]?["autonym"] ?? LanguageData?[lang]?["*"] ?? lang).ToString();
			if (name == lang)
			{
				return lang;
			}
			if (str == "")
			{
				return name;
			}

			return string.Format(str, name, lang);
		}
	}
}
