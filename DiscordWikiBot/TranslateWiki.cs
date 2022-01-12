using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using DSharpPlus;
using DSharpPlus.Entities;
using WikiClientLibrary.Client;
using WikiClientLibrary.Sites;
using System.Threading;
using Newtonsoft.Json.Linq;
using System.Text.RegularExpressions;

namespace DiscordWikiBot
{
	/// <summary>
	/// TranslateWiki notifications class.
	/// <para>Adds methods for reacting to new messages, and adding and removing notifications from channels.</para>
	/// </summary>
	class TranslateWiki
	{
		/// <summary>
		/// List of MediaWiki/Wikimedia-related project namespaces.
		/// </summary>
		static private readonly Dictionary<string, string> Projects = new Dictionary<string, string>()
		{
			{ "8", "MediaWiki" },
			{ "1206", "Wikimedia" },
			{ "1238", "Pywikibot" },
			{ "1244", "Kiwix" },
			{ "1248", "Huggle" },
			{ "1274", "Phabricator" },
		};

		private static readonly Dictionary<string, TranslateWiki> Wikis = new Dictionary<string, TranslateWiki>();

		/// <summary>
		/// Maximum embed length allowed by Discord.
		/// </summary>
		static private readonly int MAX_EMBED_LENGTH = 1024;

		// List of used channels
		private List<string> Channels = new List<string>();

		// Latest fetch revision
		private string LatestFetchKey;

		private readonly CancellationTokenSource _cancellation = new CancellationTokenSource();
		private readonly string _lang;

		// Update deadline and whether info about it was already sent
		private static bool UpdateDeadline = false;
		private static bool UpdateDeadlineDone = false;

		private TranslateWiki(string lang)
		{
			_lang = lang;
		}

		/// <summary>
		/// Initialise the default settings and setup things for overrides.
		/// </summary>
		/// <param name="channel">Discord channel ID for recent changes notifications.</param>
		/// <param name="lang">Language code in ISO 639 format.</param>
		public static void Init(string channel = "", string lang = "")
		{
			var run = false;
			if (!Wikis.TryGetValue(lang, out var wiki))
			{
				if (lang == "" || lang == null)
				{
					channel = Config.GetTWChannel();
					lang = Config.GetTWLang();
				}

				Program.LogMessage($"Watching changes for {lang.ToUpper()}", "TranslateWiki");

				Wikis[lang] = wiki = new TranslateWiki(lang);
				run = true;
			}

			wiki.Channels.Add(channel);
			wiki.LatestFetchKey = GetLegacyKey(channel);
			if (run)
			{
				wiki.Run();
			}
		}

		/// <summary>
		/// Run task to fetch new Translatewiki changes.
		/// </summary>
		private void Run()
		{
			Task.Run(FetchPeriodically);
		}

		/// <summary>
		/// Stop notifying about TranslateWiki recent changes in a specified channel.
		/// </summary>
		/// <param name="channel">Discord channel ID for recent changes notifications.</param>
		/// <param name="lang">Language code in ISO 639 format.</param>
		public static void Remove(string channel = "", string lang = "")
		{
			Config.SetChannelOverride(channel, "translatewiki-key", null);

			if (!Wikis.TryGetValue(lang, out var wiki))
				return;

			wiki.RemoveChannel(channel);
		}

		/// <summary>
		/// Remove channel data from all storage.
		/// </summary>
		/// <param name="channel">Discord channel ID.</param>
		private void RemoveChannel(string channel)
		{
			Channels.Remove(channel);
			if (Channels.Count == 0)
			{
				Wikis.Remove(_lang);
				_cancellation.Cancel();
			}
		}

		/// <summary>
		/// Notify about recent changes every hour.
		/// </summary>
		/// <param name="source"></param>
		/// <param name="lang">Language code in ISO 639 format.</param>
		private async Task FetchPeriodically()
		{
			while (!_cancellation.Token.IsCancellationRequested)
			{
				var now = DateTime.UtcNow;
				// Inform about update deadline after Sunday 6:00 UTC
				if (now.DayOfWeek == DayOfWeek.Sunday && now.Hour >= 6)
				{
					if (!UpdateDeadlineDone) UpdateDeadline = true;
				}
				else
				{
					UpdateDeadline = false;
					UpdateDeadlineDone = false;
				}

				// React to all channels about any new changes
				var msgresult = await Fetch(_lang);
				if (msgresult != null)
				{
					JToken[] msgs = msgresult.ToArray();
					if (msgs != null)
					{
						await React(msgs);
					}
				}

				now = DateTime.UtcNow;
				var nextTime = now
					.AddMilliseconds(-now.Millisecond)
					.AddSeconds(-now.Second)
					.AddMinutes(-now.Minute)
					.AddHours(1)
					.AddSeconds(3 * Array.IndexOf(Wikis.Keys.ToArray(), _lang));

				await Task.Delay(nextTime - now, _cancellation.Token);
			}
		}

		/// <summary>
		/// React if there are new messages in a specified language.
		/// </summary>
		/// <param name="list">List of all fetched messages.</param>
		/// <param name="lang">Language code in ISO 639 format.</param>
		private async Task React(JToken[] list)
		{
			// Filter only translations from projects above
			bool gotToLatest = false;
			List<JToken> query = list.Where(jt => jt.Type == JTokenType.Object).Select(item =>
			{
				string key = item["key"].ToString();
				if (Projects.Keys.Where(x => key.StartsWith(x + ":")).ToList().Count > 0)
				{
					// Check if matches with latest fetch
					if (key == LatestFetchKey)
					{
						gotToLatest = true;
					}

					// Return if no there is still no match
					if (gotToLatest == false)
					{
						return item;
					}
				}

				return null;
			}).Where(i => i != null).ToList();
			if (query.Count == 0) return;

			// Sort authors and messages by groups
			int count = query.Count;
			Dictionary<string, Dictionary<string, List<string>>> groups = new Dictionary<string, Dictionary<string, List<string>>>();
			HashSet<string> authors = new HashSet<string>();

			foreach (var item in query)
			{
				string key = item["key"].ToString();
				string ns = Regex.Match(key, @"^\d+:").ToString().TrimEnd(':');
				string translator = item["properties"]["last-translator-text"].ToString();
				key = key.Replace(ns + ":", string.Empty);

				if (!groups.ContainsKey(ns))
				{
					groups[ns] = new Dictionary<string, List<string>>();
				}
				if (!groups[ns].ContainsKey(translator))
				{
					groups[ns][translator] = new List<string>();
				}

				groups[ns][translator].Add(key);
				authors.Add(translator);
			}

			// Send Discord messages to guilds
			Dictionary<string, string> badServers = new Dictionary<string, string>();
			DiscordClient client = Program.Client;
			foreach (string chan in Channels)
			{
				DiscordEmbedBuilder embed = new DiscordEmbedBuilder()
					.WithTimestamp(DateTime.UtcNow)
					.WithColor(new DiscordColor(0x013467))
					.WithFooter(
						"translatewiki.net • " + Locale.GetLanguageName(_lang, "{0} ({1})"),
						"https://upload.wikimedia.org/wikipedia/commons/thumb/5/51/Translatewiki.net_logo.svg/512px-Translatewiki.net_logo.svg.png"
					);

				// Fetch info about channel
				DiscordChannel channel = null;
				try
				{
					ulong chanId = ulong.Parse(chan);
					channel = await client.GetChannelAsync(chanId);
				}
				catch (Exception ex)
				{
					Program.LogMessage($"Channel can’t be reached: {ex}", "TranslateWiki", "warning");

					// Remove data if channel is deleted or unavailable
					if (Program.IsChannelInvalid(ex))
					{
						badServers.Add(chan, _lang);
					}
				}

				// Stop if channel is not assigned
				if (channel == null)
				{
					continue;
				}

				string guildLang = Config.GetLang(channel.GuildId.ToString());

				// Inform about deadline if needed
				string deadlineInfo = null;
				if (UpdateDeadline)
				{
					deadlineInfo = Locale.GetMessage("translatewiki-deadline", guildLang);
					UpdateDeadline = false;
					UpdateDeadlineDone = true;
				}

				// Build messages
				string headerCount = (gotToLatest ? count.ToString() : count.ToString() + "+");
				string header = Locale.GetMessage("translatewiki-header", guildLang, headerCount, count, authors.Count);
				embed.WithTitle(header)
					.WithUrl(string.Format("https://translatewiki.net/wiki/Special:RecentChanges?translations=only&namespace={0}&limit=500&trailer=/{1}", string.Join("%3B", Projects.Keys), _lang));

				// Form message lists for groups
				foreach (var groupList in groups)
				{
					// Do not display message IDs for Phabricator messages
					string desc = FormDescription(groupList.Value, (groupList.Key != "1274"));
					embed.AddField(Projects[groupList.Key], desc);
				}

				try
				{
					await client.SendMessageAsync(channel, deadlineInfo, embed: embed);

					// Remember the key of first message
					Config.SetChannelOverride(channel.Id.ToString(), "translatewiki-key", query[0]["key"].ToString());
				}
				catch (Exception ex)
				{
					Program.LogMessage($"Message in channel #{channel.Name} (ID {chan}) could not be posted: {ex}", "TranslateWiki", "warning");

					// Remove data if channel is deleted or unavailable
					if (Program.IsChannelInvalid(ex))
					{
						badServers.Add(chan, _lang);
					}
				}
			}

			// Remove language from bad servers
			foreach (KeyValuePair<string, string> entry in badServers)
			{
				Remove(entry.Key, entry.Value);
			}

			// Write down new first key
			LatestFetchKey = query.First()["key"].ToString();
		}

		/// <summary>
		/// Form description of the changes for the project.
		/// </summary>
		/// <param name="authors">List of authors with their messages.</param>
		/// <param name="includeIds">Whether to include message IDs in the result.</param>
		/// <returns>Changes in the project.</returns>
		private static string FormDescription(Dictionary<string, List<string>> authors, bool includeIds = true)
		{
			if (!includeIds)
			{
				return FormSimpleDescription(authors);
			}
			Dictionary<string, string[]> storage = new Dictionary<string, string[]>();

			// Calculate how much links cost
			int authorLinkLength = 0;
			int authorNameLength = 0;
			foreach (var item in authors)
			{
				var arr = new string[3];
				arr[0] = string.Format("[{0}]({1})", item.Key, Linking.GetLink(item.Key, "https://translatewiki.net/wiki/Special:Contribs/$1", true));
				arr[1] = item.Value.Count.ToString();
				arr[2] = "";

				authorLinkLength += arr[0].Length;
				authorNameLength += item.Key.Length;
				storage.Add(item.Key, arr);
			}

			// Add as many message IDs as we can
			int msgLength = 0;
			bool useLinks = (authorLinkLength < (MAX_EMBED_LENGTH / 2));
			Dictionary<string, int> msgNumbers = new Dictionary<string, int>();
			Dictionary<string, bool> finished = new Dictionary<string, bool>();
			while (msgLength < MAX_EMBED_LENGTH)
			{
				foreach (var item in authors)
				{
					if (msgLength > MAX_EMBED_LENGTH || item.Key == "1274") break;
					if (!msgNumbers.ContainsKey(item.Key))
					{
						msgNumbers[item.Key] = 0;
					}

					var num = msgNumbers[item.Key];
					if (finished.ContainsKey(item.Key) || num >= item.Value.Count)
					{
						if (!finished.ContainsKey(item.Key))
						{
							finished[item.Key] = true;
						}
						continue;
					};

					var arr = storage[item.Key];
					if (num == 0)
					{
						arr[0] = string.Format(" ({0})\n", (useLinks ? arr[0] : item.Key));
						msgLength += arr[0].Length;
						msgLength += arr[1].Length;
					}

					string str = (arr[2].Length > 0 ? ", " : "") + item.Value[num].Replace("_", @"\_");
					if (msgLength + str.Length + arr[0].Length <= MAX_EMBED_LENGTH)
					{
						arr[2] += str;
						msgLength += str.Length;
					}
					else
					{
						finished[item.Key] = true;
						continue;
					}

					msgNumbers[item.Key]++;
				}

				// Break if foreach has ended for all elements
				if (finished.Values.Select(x => x).ToList().Count == msgNumbers.Count)
				{
					break;
				}
			}

			// Build the resulting string while accounting for unknown problems
			StringBuilder result = new StringBuilder();
			foreach (var item in storage)
			{
				int trim = (authors[item.Key].Count - msgNumbers[item.Key]);
				string trimmed = (trim > 0 ? $" + {trim}" : "");

				result.Append(item.Value[2]);
				result.Append(trimmed);
				if (useLinks)
				{
					result.Append((result.Length + item.Value[0].Length > MAX_EMBED_LENGTH ? $" ({item.Key})\n" : item.Value[0]));
				}
				else
				{
					result.Append(item.Value[0]);
				}
			}

			return result.ToString().TrimEnd('\n');
		}

		/// <summary>
		/// Form description of the changes for the project.
		/// </summary>
		/// <param name="authors">List of authors with their messages.</param>
		/// <returns>Changes in the project.</returns>
		private static string FormSimpleDescription(Dictionary<string, List<string>> authors)
		{
			var keys = authors.Keys.ToList();
			var list = keys.Select(author =>
			{
				return string.Format("{0} ([{1}]({2}))", authors[author].Count, author, Linking.GetLink(author, "https://translatewiki.net/wiki/Special:Contribs/$1", true));
			});
			string result = string.Join(", ", list);

			if (result.Length > MAX_EMBED_LENGTH)
			{
				result = string.Join(", ", keys.Select(author =>
				{
					return $"{authors[author].Count} ({author})";
				}));
				if (result.Length > MAX_EMBED_LENGTH)
				{
					string ellipsis = " […]";
					result = result.Substring(0, MAX_EMBED_LENGTH - ellipsis.Length) + ellipsis;
				}
			}

			return result;
		}

		/// <summary>
		/// Perform an API request for latest messages in a specified language.
		/// </summary>
		/// <param name="lang">Language code in ISO 639 format.</param>
		/// <returns>A list of messages.</returns>
		private static async Task<JToken> Fetch(string lang)
		{
			if (lang == null) return null;

			WikiSite site = Linking.GetWikiSite("https://translatewiki.net/w/api.php").Result;

			JToken result = await site.InvokeMediaWikiApiAsync(
				new MediaWikiFormRequestMessage(new
				{
					action = "query",
					list = "messagecollection",
					mcgroup = "!recent",
					mclanguage = lang,
					mclimit = 500,
					// Ignore FuzzyBot (ID 646)
					mcfilter = "!last-translator:646",
					mcprop = "properties"
				}),
				new CancellationToken()
			);

			// Return a message collection
			JToken rcmsgs = result["query"]?["messagecollection"];
			return rcmsgs;
		}

		/// <summary>
		/// Get key via legacy way and convert it to new format.
		/// </summary>
		/// <param name="channel">Discord channel ID.</param>
		private static string GetLegacyKey(string channel)
		{
			var oldValue = Config.GetValue("_translatewiki-key", channel);
			if (oldValue != null)
			{
				Program.LogMessage("Converting internal key into the new format.", "TranslateWiki");
				Config.SetOverride(channel, "_translatewiki-key", null);
				Config.SetChannelOverride(channel, "translatewiki-key", oldValue);

				return oldValue;
			}

			return Config.GetChannelOverride("translatewiki-key", channel);
		}
	}
}
