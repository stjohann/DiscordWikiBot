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
	class Linking
	{
		// Link pattern: [[]], [[|, {{}} or {{|
		private static string pattern = string.Format("(?:{0}|{1})",
			"(\\[{2})([^\\[\\]{}\\|\n]+)(?:\\|[^\\[\\]{}\\|\n]*)?]{2}",
			"({{2})([^#][^\\[\\]{}\\|\n]*)(?:\\|*[^\\[\\]{}\n]*)?}{2}");

		// Name of default configuration key
		private static string LANG_DEFAULT = "default";

		// Error for long messages
		private static string TOO_LONG = "TOO_LONG";

		// Messages ID storage
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

		private static Buffer<ulong, ulong> Cache;

		// Maximum cache length
		private static int CACHE_LENGTH = 500;

		// Site information storage
		public class SiteInfo
		{
			public InterwikiMap iw;
			public NamespaceCollection ns;
			public bool isCaseSensitive;
		}

		// Permanent site information for main wikis
		private static Dictionary<string, InterwikiMap> IWList;
		private static Dictionary<string, NamespaceCollection> NSList;
		private static Dictionary<string, bool> IsCaseSensitive;

		static public void Init(string goal = "")
		{
			string wiki = Config.GetWiki(goal);
			if (goal != "" && wiki == Config.GetWiki()) return;

			// Set defaults for first fetch
			if (IWList == null)
			{
				IWList = new Dictionary<string, InterwikiMap>();
				NSList = new Dictionary<string, NamespaceCollection>();
				IsCaseSensitive = new Dictionary<string, bool>();

				// Create cache
				Cache = new Buffer<ulong, ulong>();
				Cache.MaxItems = CACHE_LENGTH;
			}

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

		static public void Remove(string goal = "")
		{
			if (goal == "" || goal == null) return;

			IWList.Remove(goal);
			NSList.Remove(goal);
			IsCaseSensitive.Remove(goal);
		}

		static public async Task Answer(MessageCreateEventArgs e)
		{
			// Ignore bots
			if (e.Message.Author.IsBot) return;

			// Determine our goal (default for DMs)
			string goal = (e.Guild != null ? e.Guild.Id.ToString() : LANG_DEFAULT);
			string lang = Config.GetLang(goal);
			Init(goal);

			// Send message
			string msg = PrepareMessage(e.Message.Content, goal);
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
			}
		}

		static public async Task Edit(MessageUpdateEventArgs e)
		{
			// Ignore bots
			if (e.Message.Author.IsBot) return;

			// Only update known messages
			if (!Cache.ContainsKey(e.Message.Id)) return;
			ulong id = e.Message.Id;

			// Determine our goal (default for DMs)
			string goal = (e.Guild != null ? e.Guild.Id.ToString() : LANG_DEFAULT);
			string lang = Config.GetLang(goal);
			Init(goal);

			// Update message
			string msg = PrepareMessage(e.Message.Content, goal);
			if (msg != "")
			{
				bool isTooLong = (msg == TOO_LONG);
				if (isTooLong) return;

				DiscordMessage response = await e.Channel.GetMessageAsync(Cache[id]);
				await response.ModifyAsync(msg);
			}
		}

		static public async Task Delete(MessageDeleteEventArgs e)
		{
			// Ignore bots
			if (e.Message.Author.IsBot) return;

			// Only update known messages
			if (!Cache.ContainsKey(e.Message.Id)) return;
			ulong id = e.Message.Id;

			// Delete message
			DiscordMessage response = await e.Channel.GetMessageAsync(Cache[id]);
			Cache.Remove(id);
			await response.DeleteAsync();
		}

		static public string PrepareMessage(string content, string goal)
		{
			// Remove code from the message
			content = Regex.Replace(content, "```(.|\n)*?```", string.Empty);
			content = Regex.Replace(content, "`.*?`", string.Empty);

			// Start digging for links
			string msg = "";
			MatchCollection matches = Regex.Matches(content, pattern);
			List<string> links = new List<string>();

			// Get language from the goal
			string lang = Config.GetLang(goal);

			if (matches.Count > 0)
			{
				// Add a unique link for each match into the list
				foreach (Match link in matches)
				{
					string str = AddLink(link, goal);
					if (str.Length > 0 && !links.Contains(str))
					{
						msg += str;
						links.Add(str);
					}
				}

				if (msg != "")
				{
					msg = (links.Count > 1 ? Locale.GetMessage("linking-links", lang) + "\n" : Locale.GetMessage("linking-link", lang) + " ") + msg;

					if (msg.Length > 2000)
					{
						msg = TOO_LONG;
					}
				}

				return msg;
			}

			return "";
		}

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
					InterwikiMap latestIWList = (tempIWList != null ? tempIWList : defaultIWList);
					NamespaceCollection latestNSList = (tempNSList != null ? tempNSList : defaultNSList);
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
				return string.Format("<{0}>\n", GetLink(str, linkFormat));
			}
			return "";
		}

		public static async Task<SiteInfo> FetchSiteInfo(string url)
		{
			string urlWiki = "/wiki/$1";
			SiteInfo result = new SiteInfo();
			if (url.Contains(urlWiki))
			{
				// Connect with API if it is a wiki site
				WikiClient wikiClient = new WikiClient
				{
					ClientUserAgent = "DiscordWikiBot/1.0",
				};
				WikiSite site = new WikiSite(wikiClient, url.Replace(urlWiki, "/w/api.php"));
				await site.Initialization;

				// Generate and return the info needed
				result.iw = site.InterwikiMap;
				result.ns = site.Namespaces;

				result.isCaseSensitive = site.SiteInfo.IsTitleCaseSensitive;
			} else
			{
				result.isCaseSensitive = true;
			}

			await Task.FromResult(0);
			return result;
		}

		public static bool IsInvalid(string str)
		{
			var anchor = str.Split('#');
			if (anchor.Length > 1)
			{
				str = anchor[0];
			}

			// Check if page title length is more than 255 bytes
			if (Encoding.UTF8.GetByteCount(str) > 255) return true;

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

		public static string GetLink(string title, string format = null, bool escapePar = false)
		{
			if (format == null)
			{
				format = Config.GetWiki();
			}

			title = EncodePageTitle(title, escapePar);
			return format.Replace("$1", title);
		}

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
