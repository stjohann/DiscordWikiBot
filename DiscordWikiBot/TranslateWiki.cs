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

		// Latest fetch time and revision
		private static Dictionary<string, int> LatestFetchTime;
		private static Dictionary<string, string> LatestFetchKey;

		public static void Init(string channel = "", string lang = "")
		{
			// Set defaults for first fetch 
			if (Languages == null)
			{
				Languages = new List<string>();
				Channels = new Dictionary<string, List<string>>();

				LatestFetchTime = new Dictionary<string, int>();
				LatestFetchKey = new Dictionary<string, string>();
			}

			// Start updates for new language
			if (!Languages.Contains(lang))
			{
				Languages.Add(lang);
				if (!Channels.ContainsKey(lang))
				{
					Channels[lang] = new List<string>();
				}
				Channels[lang].Add(channel);

				// To start fetching ASAP
				LatestFetchTime[lang] = -1;
				LatestFetchKey[lang] = null;
				
				System.Timers.Timer timer = new System.Timers.Timer(20000);
				timer.Elapsed += (sender, args) => RequestOnTimer(sender, lang);
				timer.Enabled = true;
			} else
			{
				// Only add new channel into the list
				Channels[lang].Add(channel);
			}
		}

		private static void RequestOnTimer(object source, string lang)
		{
			int now = DateTime.Now.Hour;
			if (LatestFetchTime[lang] < now || (LatestFetchTime[lang] == 23 && now == 0))
			{
				LatestFetchTime[lang] = now;

				var msgresult = Fetch(lang).Result;
				if (msgresult != null)
				{
					JToken[] msgs = msgresult.ToArray();
					React(msgs, lang).Wait();
				}
			}
		}

		public static async Task React(JToken[] list, string lang)
		{
			bool gotToLatest = false;
			List<JToken> query = list.Where(jt => jt.Type == JTokenType.Object).Select(item =>
			{
				// Filter only MediaWiki:/Wikimedia: translations
				string key = item["key"].ToString();
				if (key.StartsWith("8:") || key.StartsWith("1206:"))
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
			List<string> authors = new List<string>();
			foreach (var item in query)
			{
				string translator = item["properties"]["last-translator-text"].ToString();
				if (!authors.Contains(translator)) authors.Add(translator);
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

				// Build messages
				string headerCount = (gotToLatest ? count.ToString() : count.ToString() + "+");
				string header = Locale.GetMessage("translatewiki-header", guildLang, headerCount, count, authors.Count);
				embed.WithAuthor(
					header,
					"https://translatewiki.net/wiki/Special:RecentChanges?translations=only&namespace=8%3B1206&limit=500&trailer=/ru",
					"https://upload.wikimedia.org/wikipedia/commons/thumb/5/51/Translatewiki.net_logo.svg/512px-Translatewiki.net_logo.svg.png"
				);

				// Check if authors list doesn’t exceed
				string desc = string.Join(", ", authors.Select(author => {
					return string.Format("[{0}]({1})", author, Linking.GetLink(author, "https://translatewiki.net/wiki/Special:Contribs/$1"));
				}));
				if (desc.Length > 2000)
				{
					desc = string.Join(", ", authors);
				}
				embed.WithDescription(desc);

				await client.SendMessageAsync(channel, embed: embed);
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
