using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using DiscordWikiBot.Schemas;
using DSharpPlus;
using DSharpPlus.EventArgs;
using WikiClientLibrary.Sites;
using DSharpPlus.Entities;
using WikiClientLibrary.Pages;

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
			@"(\[{2})([^\[\]{}\|\n]+)(?:\|[^\[\]{}\|\n]*)?]{2}",
			@"({{2})([^#][^\[\]{}\|\n]*)(?:\|*[^\[\]{}\n]*)?}{2}");

		/// <summary>
		/// Key of default configuration.
		/// </summary>
		private static readonly string LANG_DEFAULT = "default";

		/// <summary>
		/// Replacement string for long messages.
		/// </summary>
		private static readonly string TOO_LONG = "TOO_LONG";

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

		// Permanent site information for main wikis
		private static Dictionary<string, WikiSite> WikiSiteInfo = new Dictionary<string, WikiSite>();

		/// <summary>
		/// Initialise the default settings and setup things for overrides.
		/// </summary>
		/// <param name="goal">Discord server ID.</param>
		static public void Init(string goal = "")
		{
			string wiki = Config.GetWiki(goal);

			// Fetch values for the goal’s wiki
			if (!WikiSiteInfo.ContainsKey(wiki))
			{
				WikiSite data = FetchSiteInfo(wiki).Result;
				WikiSiteInfo.Add(wiki, data);
			}
		}

		/// <summary>
		/// Remove wiki site information for a specified server.
		/// </summary>
		/// <param name="goal">Discord server ID.</param>
		static public void Remove(string goal = "")
		{
			//if (goal == "" || goal == null) return;
			return;

			// TODO: Reimplement removal mechanism for wiki info
		}

		/// <summary>
		/// React to a Discord message containing wiki links.
		/// </summary>
		/// <param name="e">Discord message information.</param>
		static public async Task Answer(MessageCreateEventArgs e)
		{
			// Ignore empty messages / bots
			if (e.Message?.Content == null || e.Message?.Author?.IsBot == true) return;
			string content = e.Message.Content;

			// Ignore messages without wiki syntax
			if (!content.Contains("[[") && !content.Contains("{{"))
			{
				return;
			}

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
			string msg = PrepareMessage(content, lang, goal);
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
		/// Respond to bulk deletion by bulk deleting the bot’s messages.
		/// </summary>
		/// <param name="e">Discord information.</param>
		static public async Task BulkDelete(MessageBulkDeleteEventArgs e)
		{
			// Ignore bots / DMs
			if (e.Messages?[0]?.Author?.IsBot == true || e.Channel?.Guild == null) return;

			foreach (var item in e.Messages)
			{
				// Ignore messages not in cache
				if (!Cache.ContainsKey(item.Id))
				{
					continue;
				}
				ulong id = item.Id;

				// Delete bot’s message if possible
				try
				{
					DiscordMessage message = await e.Channel.GetMessageAsync(Cache[id]);
					Cache.Remove(id);
					await message.DeleteAsync();
				} catch(Exception ex)
				{
					Program.LogMessage($"Deleting the bot’s message {Cache[id]} returned an exception: {ex}");
				}
			}
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
			content = Regex.Replace(content, @"(`{1,3}).*?\1", string.Empty, RegexOptions.Singleline);

			// Remove links from the message
			content = Regex.Replace(content, @"https?://[^\s/$.?#].[^\s\[\]\{\}]*", string.Empty);

			// Remove quotes from the message
			content = Regex.Replace(content, @"^>>> [^$]+$", string.Empty, RegexOptions.Multiline);
			content = Regex.Replace(content, @"^> .+", string.Empty, RegexOptions.Multiline);

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
			WikiSite defaultSiteInfo = WikiSiteInfo[Config.GetWiki(goal)];

			// Temporary site info storage for other wikis
			WikiSite tempSiteInfo = null;

			// Remove escaping symbols before Markdown syntax in Discord
			// (it converts \ to / anyway)
			str = str.Replace(@"\", "");

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
					if (!str.StartsWith(':'))
					{
						ns = defaultSiteInfo.Namespaces["template"].CustomName;
						str = Regex.Replace(str, "^:?(?:subst|подст):", "");

						// MediaWiki page transclusion
						if (str.StartsWith("int:"))
						{
							ns = defaultSiteInfo.Namespaces["mediawiki"].CustomName;
							str = Regex.Replace(str, "^int:", "");
						}
					}
				}

				WikiSite latestSiteInfo = defaultSiteInfo;

				// Check if link contains interwikis
				Match iwMatch = Regex.Match(str, "^:?([A-Za-z-]+):");
				while (type == "[[" && iwMatch.Length > 0)
				{
					string prefix = iwMatch.Groups[1].Value.ToLower();
					latestSiteInfo = tempSiteInfo ?? defaultSiteInfo;
					InterwikiMap latestIWList = latestSiteInfo.InterwikiMap;
					NamespaceCollection latestNSList = latestSiteInfo.Namespaces;
					if (latestIWList.Contains(prefix) && !latestNSList.Contains(prefix))
					{
						string oldLinkFormat = linkFormat;
						linkFormat = latestIWList[prefix].Url;

						// Fetch temporary site information if necessary and store new prefix
						if (iw != "" || oldLinkFormat.Replace(iw, prefix) != linkFormat)
						{
							WikiSite data = FetchSiteInfo(linkFormat).Result;
							tempSiteInfo = data;
							latestSiteInfo = tempSiteInfo;
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
					NamespaceCollection latestNSList = latestSiteInfo.Namespaces;
					if (latestNSList.Contains(prefix))
					{
						var namespaceInfo = latestNSList[prefix];
						if ((namespaceInfo.Id == 2 || namespaceInfo.Id == 3) && namespaceInfo.Aliases.Count > 0)
						{
							// Get title according to gender for User namespaces
							str = GetNormalisedTitle(str, linkFormat).Result;
						}
						else
						{
							ns = namespaceInfo.CustomName;
							Regex only = new Regex($":?{prefix}:", RegexOptions.IgnoreCase);
							str = only.Replace(str, "", 1).Trim();
						}
					}
				}

				// If there is only namespace, return nothing
				if (ns != "" && str.Length == 0) return "";

				// Check if it’s a parser function
				if (type == "{{" && str.StartsWith("#")) return "";

				// Check for invalid page title length
				if (IsInvalid(str, true)) return "";

				// Rewrite other text
				if (str.Length > 0)
				{
					// Trim : from the start (nuisance)
					str = str.TrimStart(':');

					// Capitalise first letter if wiki does not allow lowercase titles
					if (latestSiteInfo?.SiteInfo?.IsTitleCaseSensitive == false)
					{
						str = str[0].ToString().ToUpper() + str.Substring(1);
					}

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
		public static async Task<WikiSite> FetchSiteInfo(string url)
		{
			string wikiUrlPattern = "/wiki/$1";
			if (!url.EndsWith(wikiUrlPattern))
			{
				return null;
			}

			string apiUrl = url.Replace(wikiUrlPattern, "/w/api.php");
			WikiSite result = new WikiSite(Program.WikiClient, apiUrl);
			try
			{
				await result.Initialization;
			}
			catch (Exception ex)
			{
				Program.LogMessage($"Wiki ({url}) can’t be reached: {ex.InnerException}", "Linking", LogLevel.Warning);
			}

			await Task.FromResult(0);
			return result;
		}

		/// <summary>
		/// Get normalised page title from API.
		/// </summary>
		/// <param name="title">Page title.</param>
		/// <param name="url">URL string in <code>https://ru.wikipedia.org/wiki/$1</code> format.</param>
		public static async Task<string> GetNormalisedTitle(string title, string url)
		{
			string pageTitle = null;
			string wikiUrlPattern = "/wiki/$1";
			string apiUrl = url.Replace(wikiUrlPattern, "/w/api.php");

			WikiSite site = null;
			bool siteWasInitialised = WikiSiteInfo.ContainsKey(url);
			if (siteWasInitialised)
			{
				site = WikiSiteInfo[url];
			} else
			{
				site = new WikiSite(Program.WikiClient, apiUrl);
			}

			try
			{
				if (!siteWasInitialised) await site.Initialization;

				var page = new WikiPage(site, title);
				await page.RefreshAsync();
				pageTitle = page.Title;
			}
			catch (Exception ex)
			{
				Program.LogMessage($"Wiki ({url}) can’t be reached: {ex.InnerException}", "Linking", LogLevel.Warning);
			}

			// Restore the anchor from original title
			if (title.Contains('#'))
			{
				pageTitle += "#" + EncodePageTitle(title.Split('#')?[1], false);
			}

			await Task.FromResult(0);
			return pageTitle;
		}

		/// <summary>
		/// Check if a page title is invalid according to MediaWiki restrictions.
		/// </summary>
		/// <param name="str">Page title.</param>
		/// <param name="checkLength">Whether to check page title length.</param>
		/// <returns>Is page title invalid.</returns>
		public static bool IsInvalid(string str, bool checkLength = false)
		{
			string[] anchor = str.Split('#');
			if (anchor.Length > 1)
			{
				str = anchor[0];
			}

			// Check if page title length is more than 255 bytes
			if (checkLength && Encoding.UTF8.GetByteCount(str) > 255) return true;

			// Check if it is a MediaWiki-valid URL
			// https://www.mediawiki.org/wiki/Manual:$wgUrlProtocols
			string[] uriProtocols = {
				"bitcoin:", "ftp://", "ftps://", "geo:", "git://", "gopher://", "http://",
				"https://", "irc://", "ircs://", "magnet:", "mailto:", "mms://", "news:",
				"nntp://", "redis://", "sftp://", "sip:", "sips:", "sms:", "ssh://",
				"svn://", "tel:", "telnet://", "urn:", "worldwind://", "xmpp:", "//"
			};
			if (uriProtocols.Any(str.StartsWith)) return true;

			// Check if it has two : or more
			if (Regex.IsMatch(str, "^:{2,}")) return true;

			// Following checks are based on MediaWiki page title restrictions:
			// https://www.mediawiki.org/wiki/Manual:Page_title
			string[] illegalExprs =
			{
				@"\<", @"\>",
				@"\[", @"\]",
				@"\{", @"\}",
				@"\|",
				@"~{3,}",
				@"&(?:[a-z]+|#x?\d+);"
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

			// TODO: Remove the hack when Discord fixes its Android client
			if (!escapePar && title.EndsWith(")") && !title.Contains("#"))
			{
				title += "_";
			}
			return format.Replace("$1", title.Trim());
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
				'~',
				// Added: Causes problems in anchors
				'<',
				'>'
			};
			
			// Replace all spaces to underscores
			str = Regex.Replace(str, @"\s{1,}", "_");

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
				str = str.Replace(")", @"\)");
			}

			return str;
		}
	}
}
