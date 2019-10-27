using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using DSharpPlus;
using DSharpPlus.EventArgs;
using WikiClientLibrary;
using WikiClientLibrary.Client;
using WikiClientLibrary.Sites;
using Newtonsoft.Json;
using DSharpPlus.Entities;

namespace DiscordWikiBot
{
	/// <summary>
	/// Linking class.
	/// <para>Adds methods for formatting wiki links and reacting to messages.</para>
	/// </summary>
	class Linking
	{
		/// <summary>
		/// Link pattern: [[]], [[|, {{}} or {{|
		/// </summary>
		private static readonly string pattern = string.Format("(?:{0}|{1})",
			"(\\[{2})([^\\[\\]{}\\|\n]+)(?:\\|[^\\[\\]{}\\|\n]*)?]{2}",
			"({{2})([^#][^\\[\\]{}\\|\n]*)(?:\\|*[^\\[\\]{}\n]*)?}{2}");

		/// <summary>
		/// Key of default configuration.
		/// </summary>
		private static readonly string LANG_DEFAULT = "default";

		/// <summary>
		/// Replacement string for long messages.
		/// </summary>
		private static readonly string TOO_LONG = "TOO_LONG";

		/// <summary>
		/// Dictionary extension class to store a specified number of items.
		/// </summary>
		public class Buffer<TKey, TValue> : Dictionary<TKey, TValue>
		{
			public int MaxItems { get; set; }

			private Queue<TKey> orderedKeys = new Queue<TKey>();

			public new void Add(TKey key, TValue value)
			{
				orderedKeys.Enqueue(key);
				if (this.MaxItems != 0 && this.Count >= MaxItems)
				{
					this.Remove(orderedKeys.Dequeue());
				}

				base.Add(key, value);
			}
		}

		/// <summary>
		/// Message cache length.
		/// </summary>
		private static readonly int CACHE_LENGTH = 500;

		/// <summary>
		/// Cache for messages IDs for which edits and deletions are tracked.
		/// </summary>
		private static Buffer<ulong, ulong> Cache = new Buffer<ulong, ulong>
		{
			MaxItems = CACHE_LENGTH
		};

		/// <summary>
		/// Class to store needed wiki site information.
		/// </summary>
		public class SiteInfo
		{
			public InterwikiMap iw;
			public NamespaceCollection ns;
			public bool isCaseSensitive;
		}

		// Permanent site information for main wikis
		private static Dictionary<string, InterwikiMap> IWList = new Dictionary<string, InterwikiMap>();
		private static Dictionary<string, NamespaceCollection> NSList = new Dictionary<string, NamespaceCollection>();
		private static Dictionary<string, bool> IsCaseSensitive = new Dictionary<string, bool>();

		/// <summary>
		/// Initialise the default settings and setup things for overrides.
		/// </summary>
		/// <param name="goal">Discord server ID.</param>
		static public void Init(string goal = "")
		{
			string wiki = Config.GetWiki(goal);
			if (goal != "" && wiki == Config.GetWiki()) return;

			// Fetch values for the goal
			if (!IWList.ContainsKey(goal))
			{
				// Fetch site information for default wiki
				SiteInfo data = FetchSiteInfo(wiki).Result;
				if (goal == "" || goal == null)
				{
					goal = LANG_DEFAULT;
				}

				IWList.Add(goal, data.iw);
				NSList.Add(goal, data.ns);
				IsCaseSensitive.Add(goal, data.isCaseSensitive);
			}
		}

		/// <summary>
		/// Remove wiki site information for a specified server.
		/// </summary>
		/// <param name="goal">Discord server ID.</param>
		static public void Remove(string goal = "")
		{
			if (goal == "" || goal == null) return;

			IWList.Remove(goal);
			NSList.Remove(goal);
			IsCaseSensitive.Remove(goal);
		}

		/// <summary>
		/// React to a Discord message containing wiki links.
		/// </summary>
		/// <param name="e">Discord message information.</param>
		static public async Task Answer(MessageCreateEventArgs e)
		{
			// Ignore empty messages / bots
			if (e.Message?.Content == null || e.Message?.Author?.IsBot == true) return;

			// Determine our goal (default for DMs)
			bool isServerMessage = (e.Guild != null);
			string goal = LANG_DEFAULT;
			string lang = Config.GetLang();

			if (isServerMessage)
			{
				string guildID = e.Guild.Id.ToString();
				goal = GetGoal(guildID, e.Channel.Id.ToString());
				lang = Config.GetLang(guildID);

				Init(goal);
			}

			// Send message
			string msg = PrepareMessage(e.Message.Content, lang, goal);
			if (msg != "")
			{
				bool isTooLong = (msg == TOO_LONG);
				if (isTooLong)
				{
					msg = Locale.GetMessage("linking-toolong", lang);
				}

				DiscordMessage response = await e.Message.RespondAsync(msg);
				if (isServerMessage && !isTooLong)
				{
					Cache.Add(e.Message.Id, response.Id);
				}
			}
		}

		/// <summary>
		/// Edit or delete the bot’s message if one of the messages in cache was edited.
		/// </summary>
		/// <param name="e">Discord message information.</param>
		static public async Task Edit(MessageUpdateEventArgs e)
		{
			// Ignore empty messages / bots / DMs
			if (e.Message?.Content == null || e.Message?.Author?.IsBot == true || e.Guild == null) return;
			ulong id = e.Message.Id;
			bool isLastMessage = (e.Message.Id == e.Channel.LastMessageId);

			// Only update known messages
			if (!Cache.ContainsKey(id) && !isLastMessage) return;

			// Determine our goal (default for DMs)
			bool isServerMessage = (e.Guild != null);
			string goal = LANG_DEFAULT;
			string lang = Config.GetLang();

			if (isServerMessage)
			{
				string guildID = e.Guild.Id.ToString();
				goal = GetGoal(guildID, e.Channel.Id.ToString());
				lang = Config.GetLang(guildID);

				Init(goal);
			}

			// Get a message
			string msg = PrepareMessage(e.Message.Content, lang, goal);

			// Post a message if links were added in last one
			if (isLastMessage)
			{
				if (msg != "")
				{
					bool isTooLong = (msg == TOO_LONG);
					if (isTooLong)
					{
						msg = Locale.GetMessage("linking-toolong", lang);
					}

					DiscordMessage response = await e.Message.RespondAsync(msg);
					if (!isTooLong)
					{
						Cache.Add(e.Message.Id, response.Id);
					}
					return;
				}

				if (!Cache.ContainsKey(id))
				{
					return;
				}
			}

			// Update message
			if (msg != "")
			{
				bool isTooLong = (msg == TOO_LONG);
				if (isTooLong) return;

				DiscordMessage response = await e.Channel.GetMessageAsync(Cache[id]);
				if (response.Content != msg) await response.ModifyAsync(msg);
			} else
			{
				DiscordMessage response = await e.Channel.GetMessageAsync(Cache[id]);
				Cache.Remove(id);
				await response.DeleteAsync();
			}
		}

		/// <summary>
		/// Delete the bot’s message if one of the messages in cache was deleted.
		/// </summary>
		/// <param name="e">Discord message information.</param>
		static public async Task Delete(MessageDeleteEventArgs e)
		{
			// Ignore other bots / DMs
			bool isBot = (e.Message?.Author?.IsBot == true);
			bool isOurBot = (isBot && e.Message?.Author == Program.Client.CurrentUser);
			if (isBot && !isOurBot || e.Guild == null) return;
			ulong id = e.Message.Id;

			// Clear cache if bot’s message was deleted
			if (isOurBot && Cache.ContainsValue(id))
			{
				ulong key = Cache.FirstOrDefault(x => x.Value == id).Key;
				Cache.Remove(key);
				return;
			}

			// Ignore unknown messages / messages from our bot
			if (isOurBot || !Cache.ContainsKey(id)) return;

			// Delete message
			DiscordMessage response = await e.Channel.GetMessageAsync(Cache[id]);
			Cache.Remove(id);
			await response.DeleteAsync();
		}

		/// <summary>
		/// Parse a Discord message.
		/// </summary>
		/// <param name="content">Discord message content.</param>
		/// <param name="lang">MediaWiki-compatible language code.</param>
		/// <param name="goal">Discord server or channel ID.</param>
		/// <returns>A message with parsed wiki links or a response code.</returns>
		static public string PrepareMessage(string content, string lang, string goal)
		{
			if (content == "" || content == null)
			{
				return "";
			}

			// Remove code from the message
			content = Regex.Replace(content, "```(.|\n)*?```", string.Empty);
			content = Regex.Replace(content, "`{1,2}.*?`{1,2}", string.Empty);

			// Remove links from the message
			content = Regex.Replace(content, "https?://[^\\s/$.?#].[^\\s]*", string.Empty);

			// Start digging for links
			MatchCollection matches = Regex.Matches(content, pattern);
			List<string> links = new List<string>();

			if (matches.Count > 0)
			{
				// Add a unique link for each match into the list
				foreach (Match link in matches)
				{
					string str = AddLink(link, goal);
					if (str.Length > 0 && !links.Contains(str))
					{
						links.Add(str);
					}
				}

				// Reject if there are no links
				if (links.Count == 0) return "";

				// Choose label and separator
				string label = "linking-link";
				string separator = " ";
				if (links.Count > 1)
				{
					label = "linking-links";
					separator = "\n";
				}

				// Compose message
				string msg = Locale.GetMessage(label, lang) + separator;
				msg += string.Join("\n", links);

				// Reject if the message is too long
				if (msg.Length > 2000) return TOO_LONG;

				return msg;
			}

			return "";
		}

		/// <summary>
		/// Parse a matched wiki link syntax.
		/// </summary>
		/// <param name="link">Regular expression match.</param>
		/// <param name="goal">Discord server ID.</param>
		/// <returns>A parsed URL from the match.</returns>
		static public string AddLink(Match link, string goal)
		{
			string linkFormat = Config.GetWiki(goal);
			GroupCollection groups = link.Groups;
			string type = ( groups[1].Value.Length == 0 ? groups[3].Value : groups[1].Value).Trim();
			string str = ( groups[2].Value.Length == 0 ? groups[4].Value : groups[2].Value ).Trim();

			// Default site info
			InterwikiMap defaultIWList = GetList(goal, "iw");
			NamespaceCollection defaultNSList = GetList(goal, "ns");

			// Temporary site info for other wikis
			InterwikiMap tempIWList = null;
			NamespaceCollection tempNSList = null;
			bool tempIsCaseSensitive = true;

			// Remove escaping symbols before Markdown syntax in Discord
			// (it converts \ to / anyway)
			str = str.Replace("\\", "");

			// Check for invalid page titles
			if (IsInvalid(str)) return "";

			// Storages for prefix and namespace data
			string iw = "%%%%%";
			string ns = "";

			if (str.Length > 0)
			{
				// Add template namespace for template links and remove substitution
				if (type == "{{")
				{
					ns = defaultNSList["template"].CustomName;
					str = Regex.Replace(str, "^(?:subst|подст):", "");
				}

				// Check if link contains interwikis
				Match iwMatch = Regex.Match(str, "^:?([A-Za-z-]+):");
				while (type == "[[" && iwMatch.Length > 0)
				{
					string prefix = iwMatch.Groups[1].Value.ToLower();
					InterwikiMap latestIWList = (tempIWList ?? defaultIWList);
					NamespaceCollection latestNSList = (tempNSList ?? defaultNSList);
					if (latestIWList.Contains(prefix) && !latestNSList.Contains(prefix))
					{
						string oldLinkFormat = linkFormat;
						linkFormat = latestIWList[prefix].Url;

						// Fetch temporary site information if necessary and store new prefix
						if (iw != "" || oldLinkFormat.Replace(iw, prefix) != linkFormat)
						{
							SiteInfo data = FetchSiteInfo(linkFormat).Result;
							tempIWList = data.iw;
							tempNSList = data.ns;
							tempIsCaseSensitive = data.isCaseSensitive;
						}
						iw = prefix;

						Regex only = new Regex($":?{prefix}:", RegexOptions.IgnoreCase);
						str = only.Replace(str, "", 1).Trim();

						iwMatch = Regex.Match(str, "^:?([A-Za-z-]+):");
					} else
					{
						// Return the regex that can’t be matched
						iwMatch = Regex.Match(str, "^\b$");
					}
				}

				// Check if link contains namespace
				Match nsMatch = Regex.Match(str, "^:?([^:]+):");
				if (nsMatch.Length > 0)
				{
					string prefix = nsMatch.Groups[1].Value.ToUpper();
					NamespaceCollection latestNSList = defaultNSList;
					if (linkFormat != Config.GetWiki(goal) && tempNSList != null)
					{
						latestNSList = tempNSList;
					}

					if (latestNSList.Contains(prefix))
					{
						ns = latestNSList[prefix].CustomName;
						Regex only = new Regex($":?{prefix}:", RegexOptions.IgnoreCase);
						str = only.Replace(str, "", 1).Trim();
					}
				}

				// If there is only namespace, return nothing
				if (ns != "" && str.Length == 0) return "";

				// Check if it’s a parser function
				if (type == "{{" && str.StartsWith("#")) return "";

				// Check if page title length is more than 255 bytes
				if (Encoding.UTF8.GetByteCount(str) > 255) return "";

				// Rewrite other text
				if (str.Length > 0)
				{
					// Capitalise first letter if wiki does not allow lowercase titles
					if ((linkFormat == Config.GetWiki(goal) && !GetList(goal)) || (linkFormat != Config.GetWiki(goal) && !tempIsCaseSensitive))
					{
						str = str[0].ToString().ToUpper() + str.Substring(1);
					}

					// Clear temporary site info
					tempIWList = null;
					tempNSList = null;
					tempIsCaseSensitive = false;

					// Add namespace before any transformations
					if (ns != "")
					{
						str = string.Join(":", new[] { ns, str });
					}
				}
				return string.Format("<{0}>", GetLink(str, linkFormat));
			}

			return "";
		}

		/// <summary>
		/// Get wiki site information.
		/// </summary>
		/// <param name="url">URL string in <code>https://ru.wikipedia.org/wiki/$1</code> format.</param>
		/// <returns>Site information from the wiki site.</returns>
		public static async Task<SiteInfo> FetchSiteInfo(string url)
		{
			string urlWiki = "/wiki/$1";
			SiteInfo result = new SiteInfo();
			if (url.Contains(urlWiki))
			{
				// Connect with API if it is a wiki site
				WikiClient wikiClient = new WikiClient
				{
					ClientUserAgent = Program.UserAgent,
				};
				WikiSite site = new WikiSite(wikiClient, url.Replace(urlWiki, "/w/api.php"));
				try
				{
					await site.Initialization;

					// Generate and return the info needed
					result.iw = site.InterwikiMap;
					result.ns = site.Namespaces;
					result.isCaseSensitive = site.SiteInfo.IsTitleCaseSensitive;
				} catch (Exception ex)
				{
					Program.LogMessage($"Wiki ({url}) can’t be reached: {ex.InnerException}", "Linking", LogLevel.Warning);

					result.isCaseSensitive = true;
				}
			} else
			{
				result.isCaseSensitive = true;
			}

			await Task.FromResult(0);
			return result;
		}

		/// <summary>
		/// Check if a page title is invalid according to MediaWiki restrictions.
		/// </summary>
		/// <param name="str">Page title.</param>
		/// <returns>Is page title invalid.</returns>
		public static bool IsInvalid(string str)
		{
			string[] anchor = str.Split('#');
			if (anchor.Length > 1)
			{
				str = anchor[0];
			}

			// Check if it is a MediaWiki-valid URL
			// https://www.mediawiki.org/wiki/Manual:$wgUrlProtocols
			string[] uriProtocols = {
				"bitcoin:", "ftp://", "ftps://", "geo:", "git://", "gopher://", "http://",
				"https://", "irc://", "ircs://", "magnet:", "mailto:", "mms://", "news:",
				"nntp://", "redis://", "sftp://", "sip:", "sips:", "sms:", "ssh://",
				"svn://", "tel:", "telnet://", "urn:", "worldwind://", "xmpp:", "//"
			};
			if (uriProtocols.Any(str.StartsWith)) return true;

			// Following checks are based on MediaWiki page title restrictions:
			// https://www.mediawiki.org/wiki/Manual:Page_title
			string[] illegalExprs =
			{
				"\\<", "\\>",
				"\\[", "\\]",
				"\\{", "\\}",
				"\\|",
				"~{3,}",
				"&(?:[a-z]+|#x?\\d+);"
			};

			foreach(string expr in illegalExprs)
			{
				if (Regex.Match(str, expr, RegexOptions.IgnoreCase).Success) return true;
			}

			return false;
		}

		/// <summary>
		/// Get a URL for a specified title and a wiki URL.
		/// </summary>
		/// <param name="title">Page title.</param>
		/// <param name="format">Wiki URL.</param>
		/// <param name="escapePar">Escape parentheses for Markdown links.</param>
		/// <returns>A page URL in specified format.</returns>
		public static string GetLink(string title, string format = null, bool escapePar = false)
		{
			if (format == null)
			{
				format = Config.GetWiki();
			}

			title = EncodePageTitle(title, escapePar);
			return format.Replace("$1", title);
		}

		/// <summary>
		/// Test if channel override exists for the channel.
		/// </summary>
		/// <param name="guild">Discord server ID.</param>
		/// <param name="channel">Discord guild ID.</param>
		/// <returns>Goal ID compatible with data.</returns>
		private static string GetGoal(string guild, string channel)
		{
			string goal = "#" + channel;
			string channelWiki = Config.GetWiki(goal, false);
			if (channelWiki == null)
			{
				return guild;
			}

			return goal;
		}

		/// <summary>
		/// Get site information for a specified goal.
		/// </summary>
		/// <param name="goal">Discord server ID.</param>
		/// <param name="key">Short key for needed information.</param>
		/// <returns>A list or a boolean value.</returns>
		private static dynamic GetList(string goal, string key = "")
		{
			if (key == "iw")
			{
				if (IWList.ContainsKey(goal))
				{
					return IWList[goal];
				}
				return IWList[LANG_DEFAULT];
			}

			if (key == "ns")
			{
				if (NSList.ContainsKey(goal))
				{
					return NSList[goal];
				}
				return NSList[LANG_DEFAULT];
			}

			if (IsCaseSensitive.ContainsKey(goal))
			{
				return IsCaseSensitive[goal];
			}
			return IsCaseSensitive[LANG_DEFAULT];
		}

		/// <summary>
		/// Encode page title according to the rules of MediaWiki.
		/// </summary>
		/// <param name="str">Page title.</param>
		/// <param name="escapePar">Escape parentheses for Markdown links.</param>
		/// <returns>An encoded page title.</returns>
		private static string EncodePageTitle(string str, bool escapePar)
		{
			// Following character conversions are based on {{PAGENAMEE}} specification:
			// https://www.mediawiki.org/wiki/Manual:PAGENAMEE_encoding
			char[] specialChars =
			{
				// Discord already escapes this character in URLs
				// '"',
				'%',
				'&',
				'+',
				'=',
				'?',
				'\\',
				'^',
				'`',
				'~'
			};
			
			// Replace all spaces to underscores
			str = Regex.Replace(str, "\\s{1,}", "_");

			// Decode percent-encoded symbols before encoding
			if (str.Contains("%")) str = Uri.UnescapeDataString(str);

			// Percent encoding for special characters
			foreach (var ch in specialChars)
			{
				str = str.Replace(ch.ToString(), Uri.EscapeDataString(ch.ToString()));
			}

			// Escape ) in embeds to not break links
			if (escapePar)
			{
				str = str.Replace(")", "\\)");
			}

			return str;
		}
	}
}
