using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Timers;
using System.Threading.Tasks;
using DSharpPlus;
using DSharpPlus.Entities;
using WikiClientLibrary;
using WikiClientLibrary.Client;
using WikiClientLibrary.Generators;
using WikiClientLibrary.Sites;
using System.Threading;
using Newtonsoft.Json.Linq;

namespace DiscordWikiBot
{
	/// <summary>
	/// TranslateWiki notifications class.
	/// <para>Adds methods for reacting to new messages, and adding and removing notifications from channels.</para>
	/// </summary>
	class TranslateWiki
	{
		/// <summary>
		/// List of MediaWiki/Wikimedia-related projects.
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

		// List of used languages and guilds
		private static List<string> Languages = new List<string>();
		private static Dictionary<string, List<string>> Channels = new Dictionary<string, List<string>>();
		private static Dictionary<string, System.Timers.Timer> Timers = new Dictionary<string, System.Timers.Timer>();

		// Latest fetch time and revision
		private static Dictionary<string, int> LatestFetchTime = new Dictionary<string, int>();
		private static Dictionary<string, string> LatestFetchKey = new Dictionary<string, string>();

		// Update deadline and whether info about it was already sent
		private static bool UpdateDeadline = false;
		private static bool UpdateDeadlineDone = false;

		/// <summary>
		/// Initialise the default settings and setup things for overrides.
		/// </summary>
		/// <param name="channel">Discord channel ID for recent changes notifications.</param>
		/// <param name="lang">Language code in ISO 639 format.</param>
		public static void Init(string channel = "", string lang = "")
		{
			// Start updates for new language
			if (!Languages.Contains(lang))
			{
				if (lang == "" || lang == null)
				{
					channel = Config.GetTWChannel();
					lang = Config.GetTWLang();
				}

				Program.LogMessage($"Watching changes for {lang.ToUpper()}", "TranslateWiki");
				Languages.Add(lang);
				if (!Channels.ContainsKey(lang))
				{
					Channels[lang] = new List<string>();
				}
				Channels[lang].Add(channel);

				// To start fetching ASAP
				LatestFetchTime[lang] = -1;
				LatestFetchKey[lang] = Config.GetInternal("translatewiki-key", channel);
				
				Timers[lang] = new System.Timers.Timer(20000);
				Timers[lang].Elapsed += (sender, args) => RequestOnTimer(sender, lang);
				Timers[lang].Enabled = true;
			} else
			{
				// Only add new channel into the list
				if (!Channels.ContainsKey(lang))
				{
					Channels[lang].Add(channel);
				}
			}
		}

		/// <summary>
		/// Stop notifying about TranslateWiki recent changes in a specified channel.
		/// </summary>
		/// <param name="channel">Discord channel ID for recent changes notifications.</param>
		/// <param name="lang">Language code in ISO 639 format.</param>
		public static void Remove(string channel = "", string lang = "")
		{
			Config.SetInternal(channel, "translatewiki-key", null);
			Channels[lang].Remove(channel);
			if (Channels[lang].Count == 0)
			{
				Languages.Remove(lang);
				Channels.Remove(lang);
				Timers[lang].Stop();
			}
		}

		/// <summary>
		/// Notify about recent changes every hour.
		/// </summary>
		/// <param name="source"></param>
		/// <param name="lang">Language code in ISO 639 format.</param>
		private static void RequestOnTimer(object source, string lang)
		{
			DateTime now = DateTime.UtcNow;
			if (LatestFetchTime[lang] < now.Hour || (LatestFetchTime[lang] == 23 && now.Hour == 0))
			{
				// Inform about update deadline after Wednesday 6:00 UTC
				if (now.DayOfWeek == DayOfWeek.Wednesday && now.Hour > 6) {
					if (!UpdateDeadlineDone) UpdateDeadline = true;
				} else
				{
					UpdateDeadline = false;
					UpdateDeadlineDone = false;
				}

				// Remember new hour
				LatestFetchTime[lang] = now.Hour;

				// React to all channels about any new changes
				var msgresult = Fetch(lang).Result;
				if (msgresult != null)
				{
					JToken[] msgs = msgresult.ToArray();
					if (msgs != null)
					{
						React(msgs, lang).Wait();
					}
				}
			}
		}

		/// <summary>
		/// React if there are new messages in a specified language.
		/// </summary>
		/// <param name="list">List of all fetched messages.</param>
		/// <param name="lang">Language code in ISO 639 format.</param>
		public static async Task React(JToken[] list, string lang)
		{
			// Filter only translations from projects above
			bool gotToLatest = false;
			List<JToken> query = list.Where(jt => jt.Type == JTokenType.Object).Select(item =>
			{
				string key = item["key"].ToString();
				if (Projects.Keys.Where(x => key.StartsWith(x + ":")).ToList().Count > 0)
				{
					// Check if matches with latest fetch
					if (key == LatestFetchKey[lang])
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

			// Fetch every author for future message
			int count = query.Count;
			Dictionary<string, List<string>> authors = new Dictionary<string, List<string>>();
			List<string> allAuthors = new List<string>();
			foreach (var item in query)
			{
				string key = item["key"].ToString();
				string ns = Projects.Keys.Where(x => key.StartsWith(x + ":")).ToList().First().ToString();
				string translator = item["properties"]["last-translator-text"].ToString();

				if (!authors.ContainsKey(ns))
				{
					authors[ns] = new List<string>();
				}
				if (!authors[ns].Contains(translator)) authors[ns].Add(translator);
				if (!allAuthors.Contains(translator)) allAuthors.Add(translator);
			}

			Dictionary<string, string> badServers = new Dictionary<string, string>();

			// Send Discord messages to guilds
			DiscordClient client = Program.Client;
			foreach (string chan in Channels[lang])
			{
				DiscordEmbedBuilder embed = new DiscordEmbedBuilder()
					.WithColor(new DiscordColor(0x013467))
					.WithFooter("translatewiki.net");

				// Fetch info about channel
				DiscordChannel channel = null;
				try
				{
					ulong chanId = ulong.Parse(chan);
					channel = await client.GetChannelAsync(chanId);
				} catch (Exception ex) {
					Program.LogMessage($"Channel can’t be reached: {ex}", "TranslateWiki", LogLevel.Warning);

					// Remove data if channel is deleted or unavailable
					if (ex is DSharpPlus.Exceptions.NotFoundException || ex is DSharpPlus.Exceptions.UnauthorizedException)
					{
						badServers.Add(chan, lang);
					}
				}

				// Stop if channel is not assigned
				if (channel == null)
				{
					continue;
				}

				string guildLang = Config.GetLang(channel.GuildId.ToString());

				// Remember the key of first message
				Config.SetInternal(channel.Id.ToString(), "translatewiki-key", query[0]["key"].ToString());

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
				string header = Locale.GetMessage("translatewiki-header", guildLang, headerCount, count, allAuthors.Count);
				embed.WithAuthor(
					header,
					string.Format("https://translatewiki.net/wiki/Special:RecentChanges?translations=only&namespace={0}&limit=500&trailer=/{1}", string.Join("%3B", Projects.Keys), lang),
					"https://upload.wikimedia.org/wikipedia/commons/thumb/5/51/Translatewiki.net_logo.svg/512px-Translatewiki.net_logo.svg.png"
				);

				// Form authors list
				string desc = FormDescription(authors);
				embed.WithDescription(desc);

				try
				{
					await client.SendMessageAsync(channel, deadlineInfo, embed: embed);
				} catch (Exception ex)
				{
					Program.LogMessage($"Message in channel #{channel.Name} (ID {chan}) could not be posted: {ex}", "TranslateWiki", LogLevel.Warning);

					// Remove data if channel is deleted or unavailable
					if (ex is DSharpPlus.Exceptions.NotFoundException || ex is DSharpPlus.Exceptions.UnauthorizedException)
					{
						badServers.Add(chan, lang);
					}
				}
			}

			// Remove language from bad servers
			foreach (KeyValuePair<string, string> entry in badServers)
			{
				Remove(entry.Key, entry.Value);
			}

			// Write down new first key
			LatestFetchKey[lang] = query.First()["key"].ToString();
		}

		/// <summary>
		/// Form embed description for the message.
		/// </summary>
		/// <param name="authors">List of authors in different namespaces.</param>
		/// <returns>Embed description.</returns>
		private static string FormDescription(Dictionary<string, List<string>> authors)
		{
			var list = authors.Select(ns =>
			{
				string str = Projects[ns.Key] + ": ";
				str += string.Join(", ", ns.Value.Select(author =>
				{
					return string.Format("[{0}]({1})", author, Linking.GetLink(author, "https://translatewiki.net/wiki/Special:Contributions/$1", true));
				}));

				return str;
			});
			string result = string.Join("\n", list);

			// Remove links if their length is too long
			if (result.Length > 2000)
			{
				list = authors.Select(ns => {
					string str = Projects[ns.Key] + ": ";
					str += string.Join(", ", ns.Value.Select(author => author));

					return str;
				});

				result = string.Join("\n", list);
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

			WikiClient wikiClient = new WikiClient
			{
				ClientUserAgent = Program.UserAgent,
			};
			WikiSite site = new WikiSite(wikiClient, "https://translatewiki.net/w/api.php");
			await site.Initialization;

			JToken result = await site.InvokeMediaWikiApiAsync(
				new MediaWikiFormRequestMessage(new
				{
					action = "query",
					list = "messagecollection",
					mcgroup = "!recent",
					mclanguage = lang,
					mclimit = 500,
					mcfilter = "",
					mcprop = "properties|tags"
				} ),
				new CancellationToken()
			);

			// Return a message collection
			JToken rcmsgs = result["query"]?["messagecollection"];
			return rcmsgs;
		}
	}
}
