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
	class TranslateWiki
	{
		// List of used languages and guilds
		private static List<string> Languages;
		private static Dictionary<string, List<string>> Channels;
		private static Dictionary<string, System.Timers.Timer> Timers;

		// Latest fetch time and revision
		private static Dictionary<string, int> LatestFetchTime;
		private static Dictionary<string, string> LatestFetchKey;

		// Update deadline and whether info about it was already sent
		private static bool UpdateDeadline = false;
		private static bool UpdateDeadlineDone = false;

		public static void Init(string channel = "", string lang = "")
		{
			// Set defaults for first fetch 
			if (Languages == null)
			{
				Languages = new List<string>();
				Channels = new Dictionary<string, List<string>>();
				Timers = new Dictionary<string, System.Timers.Timer>();

				LatestFetchTime = new Dictionary<string, int>();
				LatestFetchKey = new Dictionary<string, string>();
			}

			// Start updates for new language
			if (!Languages.Contains(lang))
			{
				if (lang == "" || lang == null)
				{
					channel = Config.GetTWChannel();
					lang = Config.GetTWLang();
				}

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

		public static async Task React(JToken[] list, string lang)
		{
			// List of MediaWiki/Wikimedia-related projects
			string[] twProjects = new string[]
			{
				"8", // MediaWiki
				"1206", // Wikimedia
				"1238", // Pywikibot
				"1244", // Kiwix
				"1248", // Huggle
				"1274", // Phabricator
			};

			Dictionary<string, string> twNames = new Dictionary<string, string>()
			{
				{ "8", "MediaWiki" },
				{ "1206", "Wikimedia" },
				{ "1238", "Pywikibot" },
				{ "1244", "Kiwix" },
				{ "1248", "Huggle" },
				{ "1274", "Phabricator" },
			};

			// Filter only translations from projects above
			bool gotToLatest = false;
			List<JToken> query = list.Where(jt => jt.Type == JTokenType.Object).Select(item =>
			{
				string key = item["key"].ToString();
				if (twProjects.Where(x => key.StartsWith(x + ":")).ToList().Count > 0)
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
				string ns = twProjects.Where(x => key.StartsWith(x + ":")).ToList().First().ToString();
				string translator = item["properties"]["last-translator-text"].ToString();

				if (!authors.ContainsKey(ns))
				{
					authors[ns] = new List<string>();
				}
				if (!authors[ns].Contains(translator)) authors[ns].Add(translator);
				if (!allAuthors.Contains(translator)) allAuthors.Add(translator);
			}

			// Send Discord messages to guilds
			DiscordClient client = Program.Client;
			foreach (string chan in Channels[lang])
			{
				DiscordEmbedBuilder embed = new DiscordEmbedBuilder()
					.WithColor(new DiscordColor(0x013467))
					.WithFooter("translatewiki.net");

				// Fetch info about channel
				ulong chanId = ulong.Parse(chan);
				DiscordChannel channel = await client.GetChannelAsync(chanId);
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
					string.Format("https://translatewiki.net/wiki/Special:RecentChanges?translations=only&namespace={0}&limit=500&trailer=/ru", string.Join("%3B", twProjects)),
					"https://upload.wikimedia.org/wikipedia/commons/thumb/5/51/Translatewiki.net_logo.svg/512px-Translatewiki.net_logo.svg.png"
				);

				// Check if authors list doesn’t exceed
				string desc = string.Join("\n", authors.Select(ns => {
					string str = twNames[ns.Key] + ": ";
					str += string.Join(", ", ns.Value.Select(author =>
					{
						return string.Format("[{0}]({1})", author, Linking.GetLink(author, "https://translatewiki.net/wiki/Special:Contribs/$1", true));
					}));

					return str;
				}));
				if (desc.Length > 2000)
				{
					desc = string.Join("\n", authors.Select(ns => {
						string str = twNames[ns.Key] + ": ";
						str += string.Join(", ", ns.Value.Select(author =>
						{
							return author;
						}));

						return str;
					}));
				}
				embed.WithDescription(desc);

				await client.SendMessageAsync(channel, deadlineInfo, embed: embed);
			}

			// Write down new first key
			LatestFetchKey[lang] = query.First()["key"].ToString();
		}

		public static async Task<JToken> Fetch(string lang)
		{
			if (lang == null) return null;

			WikiClient wikiClient = new WikiClient
			{
				ClientUserAgent = "DiscordWikiBot/1.0",
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
			JToken rcmsgs = (result["query"] != null ? result["query"]["messagecollection"] : null);
			return rcmsgs;
		}
	}
}
