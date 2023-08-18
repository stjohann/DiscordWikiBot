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
using System.Web;

namespace DiscordWikiBot
{
	/// <summary>
	/// Linking class.
	/// <para>Adds methods for formatting wiki links and reacting to messages.</para>
	/// </summary>
	public class Linking
	{
		/// <summary>
		/// Link pattern: [[]], [[|, {{}} or {{| (+ false positives)
		/// </summary>
		private static readonly Regex linkPattern = new Regex(@"
			( \[\[ | \{{2,} )

			( [^\[\]{}\|\n\r]+ )
			(?:
				\|
				(?!\[\[)
				[^{}\n\r]*?
			)?

			( \]\] | \}{2,} )
		", RegexOptions.IgnorePatternWhitespace);

		/// <summary>
		/// See https://www.mediawiki.org/wiki/Manual:$wgCapitalLinks
		/// </summary>
		private static readonly int[] capitalisedNamespaces = {
			// Special
			-1,
			// User
			2, 3,
			// MediaWiki
			8, 9,
		};

		/// <summary>
		/// Replacement string for long messages.
		/// </summary>
		private static readonly string TOO_LONG = "TOO_LONG";

		/// <summary>
		/// Message cache length.
		/// </summary>
		private static readonly int CACHE_LENGTH = 500;

		/// <summary>
		/// Cache for message IDs for which edits and deletions are tracked.
		/// </summary>
		private static Buffer<ulong, ulong> Cache = new Buffer<ulong, ulong>
		{
			MaxItems = CACHE_LENGTH
		};

		/// <summary>
		/// Cache for recently deleted message IDs.
		/// </summary>
		private static Buffer<ulong, bool> DeletedMessageCache = new Buffer<ulong, bool>
		{
			MaxItems = CACHE_LENGTH
		};

		/// <summary>
		/// Permanent site information for main wikis
		/// </summary>
		private static Dictionary<string, WikiSite> WikiSiteInfo = new Dictionary<string, WikiSite>();

		/// <summary>
		/// Initialise the default settings and setup things for overrides.
		/// </summary>
		/// <param name="goal">Discord server ID.</param>
		static public async Task Init(string goal = "")
		{
			string wiki = Config.GetWiki(goal);

			// Fetch values for the goal’s wiki
			if (!WikiSiteInfo.ContainsKey(wiki))
			{
				WikiSite data = await GetWikiSite(wiki);
				if (data != null) WikiSiteInfo.Add(wiki, data);
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
		static public async Task Answer(DiscordClient sender, MessageCreateEventArgs e)
		{
			// Ignore empty messages / bots
			if (e.Message?.Content == null || e.Message?.Author?.IsBot == true) return;
			string content = e.Message.Content;

			// Ignore messages without wiki syntax
			if (!content.Contains("[[") && !content.Contains("{{")) return;

			// Determine our goal (default for DMs)
			bool isServerMessage = (e.Guild != null);
			var wikiUrl = Config.GetWiki();
			var lang = Config.GetLang();

			if (isServerMessage)
			{
				var goal = GetConfigGoal(e.Channel);
				wikiUrl = Config.GetWiki(goal);
				lang = Config.GetLang(e.Guild.Id.ToString());

				await Init(goal);
			}

			// Send message
			string msg = PrepareMessage(content, lang, wikiUrl);
			if (msg != "")
			{
				bool isTooLong = (msg == TOO_LONG);
				if (isTooLong)
				{
					msg = Locale.GetMessage("linking-toolong", lang);
				}
				var messageId = e.Message.Id;

				// Ignore recently deleted messages
				if (DeletedMessageCache.ContainsKey(messageId)) return;

				DiscordMessage response = await e.Message.RespondAsync(msg);
				if (isServerMessage && !isTooLong)
				{
					Cache.Add(messageId, response.Id);
				}
			}
		}

		/// <summary>
		/// Edit or delete the bot’s message if one of the messages in cache was edited.
		/// </summary>
		/// <param name="e">Discord message information.</param>
		static public async Task Edit(DiscordClient sender, MessageUpdateEventArgs e)
		{
			// Ignore empty messages / bots / DMs
			if (e.Message?.Content == null || e.Message?.Author?.IsBot == true || e.Guild == null) return;
			var messageId = e.Message.Id;
			bool isRecentMessage = (DateTime.UtcNow - e.Message.CreationTimestamp).TotalMinutes <= 5;

			// Only update known or recent messages
			if (!Cache.ContainsKey(messageId) && !isRecentMessage) return;

			// Only update on real edits, because new messages with embeds trigger two events
			if (e.Message.Content == e.MessageBefore?.Content) return;

			// Determine our goal
			string goal = GetConfigGoal(e.Channel);
			string lang = Config.GetLang(e.Guild.Id.ToString());
			await Init(goal);

			// Get a message
			string msg = PrepareMessage(e.Message.Content, lang, Config.GetWiki(goal));
			bool isTooLong = (msg == TOO_LONG);

			// Post a reply to a recent message if it was without links
			if (!Cache.ContainsKey(messageId) && isRecentMessage)
			{
				if (msg == "") return;

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

			// Update message
			if (msg != "")
			{
				// Ignore the edit if too many links were added
				if (isTooLong) return;

				DiscordMessage response = await e.Channel.GetMessageAsync(Cache[messageId]);
				if (response.Content != msg) await response.ModifyAsync(msg);
			}
			else
			{
				DiscordMessage response = await e.Channel.GetMessageAsync(Cache[messageId]);
				Cache.Remove(messageId);
				await response.DeleteAsync();
			}
		}

		/// <summary>
		/// Delete the bot’s message if one of the messages in cache was deleted.
		/// </summary>
		/// <param name="e">Discord message information.</param>
		static public async Task Delete(DiscordClient sender, MessageDeleteEventArgs e)
		{
			// Ignore other bots / DMs
			bool isBot = (e.Message?.Author?.IsBot == true);
			bool isOurBot = (isBot && e.Message?.Author == Program.Client.CurrentUser);
			if (isBot && !isOurBot || e.Guild == null) return;
			ulong id = e.Message.Id;
			DeletedMessageCache.Add(id, true);

			// Clear cache if bot’s message was deleted
			if (isOurBot && Cache.ContainsValue(id))
			{
				ulong key = Cache.FirstOrDefault(x => x.Value == id).Key;
				Cache.Remove(key);
				return;
			}

			// Ignore unknown messages / messages from our bot
			if (isOurBot || !Cache.ContainsKey(id)) return;

			// Delete bot’s message if possible
			try
			{
				DiscordMessage response = await e.Message.Channel.GetMessageAsync(Cache[id]);
				Cache.Remove(id);
				await response.DeleteAsync();
			}
			catch (Exception ex)
			{
				Program.LogMessage($"Deleting the bot’s message {Cache[id]} returned an exception: {ex}");
			}
		}

		/// <summary>
		/// Respond to bulk deletion by bulk deleting the bot’s messages.
		/// </summary>
		/// <param name="e">Discord information.</param>
		static public async Task BulkDelete(DiscordClient sender, MessageBulkDeleteEventArgs e)
		{
			// Ignore bots / DMs
			if (e.Messages?[0]?.Author?.IsBot == true || e.Channel?.Guild == null) return;

			foreach (var item in e.Messages)
			{
				ulong id = item.Id;
				DeletedMessageCache.Add(id, true);

				// Ignore messages not in cache
				if (!Cache.ContainsKey(id))
				{
					continue;
				}

				// Delete bot’s message if possible
				try
				{
					DiscordMessage message = await item.Channel.GetMessageAsync(Cache[id]);
					Cache.Remove(id);
					await message.DeleteAsync();
				}
				catch (Exception ex)
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
		/// <param name="linkFormat">Standard link format for the message.</param>
		/// <returns>A message with parsed wiki links or a response code.</returns>
		public static string PrepareMessage(string content, string lang, string linkFormat)
		{
			if (content == "" || content == null)
			{
				return "";
			}

			// Remove code from the message
			content = Regex.Replace(content, @"(`{1,3}).*?\1", string.Empty, RegexOptions.Singleline);

			// Remove quotes from the message
			content = Regex.Replace(content, @"^>>> [^$]+$", string.Empty, RegexOptions.Multiline);
			content = Regex.Replace(content, @"^> .+", string.Empty, RegexOptions.Multiline);

			// Replace emojis (e. g. <:meta:873203055804436513>) in the message
			content = Regex.Replace(content, @"<:([^:]+):[\d]+>", ":$1:", RegexOptions.Multiline);

			// Extract visible content (no spoilers)
			string visibleContent = Regex.Replace(content, @"\|{2}(.+?)\|{2}", "", RegexOptions.Singleline);

			// Start digging for links
			MatchCollection matches = linkPattern.Matches(content);
			List<string> links = new List<string>();

			if (matches.Count > 0)
			{
				// Add a unique link for each match into the list
				foreach (Match link in matches)
				{
					string str = AddLink(link, linkFormat);
					if (str == null || str.Length == 0) continue;

					// Present in content but not visible, therefore it's marked as spoiler
					if (!visibleContent.Contains(link.Value))
					{
						str = $"||{str}||";
					}

					if (!links.Contains(str))
					{
						links.Add(str);
					}
				}

				// Reject if there are no links
				if (links.Count == 0) return "";

				// Compose message
				var msg = Locale.GetMessage("linking-links", lang, links.Count, " ");
				msg += string.Join(Locale.GetMessage("comma", lang, " "), links);

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
		/// <param name="linkFormat">Standard link format for the message.</param>
		/// <returns>A parsed URL from the match.</returns>
		static public string AddLink(Match link, string linkFormat)
		{
			GroupCollection groups = link.Groups;
			var brackets = groups[1].Value.Trim();
			var str = groups[2].Value.Trim();
			var endBrackets = groups[3].Value.Trim();

			bool isLink = brackets == "[[";
			bool isTransclusion = brackets.StartsWith("{{");

			// Check for matching brackets
			if (isLink && !endBrackets.StartsWith("]")) return null;
			if (isTransclusion && !endBrackets.StartsWith("}")) return null;

			// Check for parameter syntax
			if (brackets.StartsWith("{{{") && endBrackets.StartsWith("}}}")) return null;

			// Remove escaping symbols before Markdown syntax in Discord
			// (it converts \ to / anyway)
			str = str.Replace(@"\", "");

			// Check for invalid page titles (without length)
			if (IsInvalid(str, false)) return null;

			// Reject if empty
			if (str.Length == 0) return null;

			// Reject if a regular link contains an anchor
			if (isLink && str.StartsWith('#')) return null;

			// Storage for default and current site data
			var defaultSiteInfo = WikiSiteInfo[linkFormat];
			var currentSiteInfo = defaultSiteInfo;
			var currentLinkFormat = linkFormat;

			// Storage for page data
			NamespaceInfo ns = null;
			string nsName = null;
			bool capitalised = !defaultSiteInfo.SiteInfo.IsTitleCaseSensitive;

			// Storage for interwiki link prefix
			List<string> iwList = new List<string>();

			// Handle transclusion links
			if (isTransclusion)
			{
				var tuple = GetTransclusionInfo(str, defaultSiteInfo);
				if (tuple == null) return null;

				ns = tuple.Item1;
				str = tuple.Item2;
			}

			// Check for interwikis or namespace names if needed
			var prefixRegex = new Regex(@"^ *:? *([^:]+?) *: *");
			var match = prefixRegex.Match(str);
			while (str.Contains(':') && match.Length != 0)
			{
				var prefix = match.Groups[1].Value;
				var nsCollection = currentSiteInfo.Namespaces;
				var iwMap = currentSiteInfo.InterwikiMap;

				// Namespace names/aliases have priority over the interwiki map
				if (nsCollection.Contains(prefix))
				{
					ns = nsCollection[prefix];
					if ((ns.Id == 2 || ns.Id == 3) && ns.Aliases.Count > 0)
					{
						// Get title according to gender for User namespaces
						var normalisedTitle = GetNormalisedTitle(str, currentLinkFormat).Result;
						var tokens = normalisedTitle.Split(':', 2);

						nsName = tokens.Length > 1 ? tokens[0] : null;
						str = tokens[1];
					}
					else
					{
						str = prefixRegex.Replace(str, "", 1).Trim();
					}

					// Normalise Media: to File: since MediaWiki redirects it like this
					if (ns.Id == -2)
					{
						ns = nsCollection["file"];
					}

					// Cannot have an interwiki link after namespace
					break;
				}

				// Check for interwiki links next
				if (!isTransclusion && iwMap.Contains(prefix))
				{
					// Assume not to be capitalised by default
					capitalised = false;

					// Save new link format and try fetching data
					currentLinkFormat = iwMap[prefix].Url;
					var newSiteInfo = GetWikiSite(currentLinkFormat).Result;
					if (newSiteInfo != null)
					{
						currentSiteInfo = newSiteInfo;
						capitalised = !newSiteInfo.SiteInfo.IsTitleCaseSensitive;
					}

					iwList.Add(prefix);
					str = prefixRegex.Replace(str, "", 1).Trim();

					// Cannot resolve future interwiki links after a fetch failed
					if (newSiteInfo == null) break;
				}
				else
				{
					// Cannot be matched with anything, prevent endless loop
					break;
				}

				match = prefixRegex.Match(str);
			}

			// If there is only namespace, return nothing
			if (ns != null && str.Length == 0) return null;

			// Pages in some namespaces are always capitalised
			if (ns != null && capitalisedNamespaces.Contains(ns.Id))
			{
				capitalised = true;
			}

			// Add main page title if needed
			var isDifferentWiki = currentSiteInfo.SiteInfo.BaseUrl != defaultSiteInfo.SiteInfo.BaseUrl;
			if (isDifferentWiki && str.Length == 0)
			{
				str = currentSiteInfo.SiteInfo.MainPage;
			}

			// Check for invalid page title length (namespace number + title)
			var nsTitle = (ns == null || ns.Id == 0) ? str : $"{ns.Id}:{str}";
			if (IsInvalid(nsTitle, true)) return null;

			// Trim : from the start (nuisance)
			str = str.TrimStart(':');

			// Capitalise the title if lowercase titles are not allowed
			if (capitalised)
			{
				str = Capitalise(str);
			}

			// Remember the link string (without namespace for template links)
			var linkStr = str;

			// Add namespace before any transformations
			if (ns != null && ns.Id != 0)
			{
				nsName = nsName == null ? ns.CustomName : nsName;
				str = string.Join(":", new[] { nsName, str });

				if (!(isTransclusion && ns.Id == 10))
				{
					linkStr = str;
				}
			}

			// Add semicolon to mainspace transclusions
			if (isTransclusion && ns != null && ns.Id == 0)
			{
				linkStr = ":" + linkStr;
			}

			// Encode displayed link title for any issues
			linkStr = EncodePageTitle(linkStr, spaceChar: " ");

			// Create link text from the result
			var linkStart = brackets.Substring(0, 2);
			var linkEnd = endBrackets.Substring(0, 2);
			var linkInterwikis = string.Join(":", iwList);
			if (linkInterwikis.Length > 0) linkInterwikis += ":";

			var linkText = $"{linkStart}{linkInterwikis}{linkStr}{linkEnd}";

			return $"[{linkText}](<{GetLink(str, currentLinkFormat)}>)";
		}

		/// <summary>
		/// Get transclusion’s namespace and format its title.
		/// </summary>
		/// <param name="title">Original transclusion title.</param>
		/// <param name="site">Wiki site information.</param>
		/// <returns>Tuple of namespace info and formatted title, or null for parser functions.</returns>
		private static Tuple<NamespaceInfo, string> GetTransclusionInfo(string title, WikiSite site)
		{
			// Guess that it is a mainspace page
			if (title.StartsWith(':'))
			{
				return Tuple.Create(site.Namespaces[""], Regex.Replace(title, "^ *: *", ""));
			}
			var InvariantCultureIgnoreCase = StringComparison.InvariantCultureIgnoreCase;
			var magicWords = site.MagicWords;

			// Remove subst:/safesubst: (substitution) / raw:/msg: (always template) or their localisations
			var substNames = GetMagicWordNames("subst", magicWords);
			var safesubstNames = GetMagicWordNames("safesubst", magicWords);
			var rawNames = GetMagicWordNames("raw", magicWords);
			var msgNames = GetMagicWordNames("msg", magicWords);
			var junkRegex = string.Join('|', new string[] {
				string.Join('|', substNames),
				string.Join('|', safesubstNames),
				string.Join('|', rawNames),
				string.Join('|', msgNames),
			});

			title = Regex.Replace(title, $"^ *(?:{junkRegex}) *", "", RegexOptions.IgnoreCase);

			// Guess that it is a MediaWiki: page
			var intNames = GetMagicWordNames("int", magicWords);
			if (intNames.Any(x => title.StartsWith(x, InvariantCultureIgnoreCase)))
			{
				var intRegex = string.Join('|', intNames);
				title = Regex.Replace(title, $"^ *(?:{intRegex}) *", "", RegexOptions.IgnoreCase);
				return Tuple.Create(site.Namespaces["mediawiki"], title);
			}

			// Guess that it is a Module: page
			var hasModuleNamespace = site.Namespaces["module"] != null;
			if (hasModuleNamespace)
			{
				var invokeNames = GetMagicWordNames("invoke", magicWords);
				if (invokeNames.Any(x => title.StartsWith(x, InvariantCultureIgnoreCase)))
				{
					var invokeRegex = string.Join('|', invokeNames);
					title = Regex.Replace(title, $"^ *(?:{invokeRegex}) *", "", RegexOptions.IgnoreCase);
					return Tuple.Create(site.Namespaces["module"], title);
				}
			}

			// Ignore known magic words of any kind
			if (HasMagicWord(title, magicWords))
			{
				return null;
			}

			return Tuple.Create(site.Namespaces["template"], title);
		}

		/// <summary>
		/// Get localised aliases for a specified magic word.
		/// </summary>
		/// <param name="name">Magic word name.</param>
		/// <param name="data">Magic words collection.</param>
		/// <returns>Array of strings with formatted magic word.</returns>
		private static string[] GetMagicWordNames(string name, MagicWordCollection data)
		{
			var names = data.FirstOrDefault(x => x.Name == name)?.Aliases.ToArray();
			if (names == null)
			{
				return new string[] { name };
			}

			// Format #invoke: manually
			if (name == "invoke")
			{
				names = names.Select(x => $"#{x}:").ToArray();
			}

			return names;
		}

		/// <summary>
		/// Check if a string has a known magic word on a wiki.
		/// See https://www.mediawiki.org/wiki/Help:Magic_words
		/// </summary>
		/// <param name="str">String to check for magic words.</param>
		/// <param name="data">Magic words collection.</param>
		private static bool HasMagicWord(string str, MagicWordCollection data)
		{
			// Assume this is a parser function
			if (str.StartsWith("#"))
			{
				return true;
			}

			// Skip some values
			var magicWords = data.Where(x =>
			{
				return (
					// Behaviour switches
					x.Aliases.FirstOrDefault(xa => xa.StartsWith("__")) == null
					// Params to magic words (img_, timedmedia_, url_ etc.)
					&& !x.Name.Contains("_")
					// |R param to some magic words
					&& x.Name != "rawsuffix"
					// #special: parser function
					&& x.Name != "special"
				);
			});

			// Check for variables
			// Most are case-sensitive ({{PAGENAME}} ≠ {{pagename}}), but some ({{serverpath}}, {{servername}}) are not. Until MediaWiki fixes it, all are expected to be case-insensitive.
			// For simplicity {{PAGENAME|name}} is treated as a variable, even though it is a template.
			var isMagicVariable = magicWords.FirstOrDefault(x =>
			{
				return x.Aliases.FirstOrDefault(xa => xa == str.ToUpper()) != null;
			}) != null;
			if (isMagicVariable) return true;

			// Skip titles that cannot contain parser functions
			if (!str.Contains(':'))
			{
				return false;
			}

			// Parser functions: {{#tag:}} or {{formatnum:}}
			// For simplicity {{if: test}} (for {{#if}} etc.) is treated as a parser function, though it is allowed to create templates with these names.
			static bool IsParserFunction(string value, string str)
			{
				var InvariantCultureIgnoreCase = StringComparison.InvariantCultureIgnoreCase;
				value = value.TrimEnd(':') + ":";
				return str.StartsWith(value, InvariantCultureIgnoreCase);
			}

			return magicWords.FirstOrDefault(x =>
			{
				// Some variables, such as {{servername}}, will be treated like parser functions here.
				// TODO: Come up with a way to actually ignore variables here.
				return x.CaseSensitive == false
					&& x.Aliases.FirstOrDefault(xa => IsParserFunction(xa, str)) != null;
			}) != null;
		}

		/// <summary>
		/// Get wiki site information.
		/// </summary>
		/// <param name="url">URL string in <code>https://ru.wikipedia.org/wiki/$1</code> or <code>https://ru.wikipedia.org/w/api.php</code> format.</param>
		/// <returns>Wiki site information or null.</returns>
		public static async Task<WikiSite> GetWikiSite(string url)
		{
			string wikiUrlPattern = "/wiki/$1";
			string apiUrlPattern = "/api.php";
			if (!url.EndsWith(wikiUrlPattern) && !url.EndsWith(apiUrlPattern))
			{
				return null;
			}

			// Return stored site data if it exists
			if (WikiSiteInfo.ContainsKey(url))
			{
				await Task.CompletedTask;
				return WikiSiteInfo[url];
			}

			// Fetch site data from /w/api.php
			string apiUrl = url.Replace(wikiUrlPattern, "/w/api.php");
			try
			{
				var result = new WikiSite(Program.WikiClient, apiUrl);
				await result.Initialization;
				return result;
			}
			catch (Exception) { }

			// Try fetching from /api.php if it failed before
			apiUrl = apiUrl.Replace("/w/", "/");
			try
			{
				var result = new WikiSite(Program.WikiClient, apiUrl);
				await result.Initialization;
				return result;
			}
			catch (Exception ex)
			{
				Program.LogMessage($"Wiki at {url} can’t be reached: {ex.Message}", "Linking", "warning");
			}

			// Assume this is not a MediaWiki site
			await Task.CompletedTask;
			return null;
		}

		/// <summary>
		/// Get normalised page title from API.
		/// </summary>
		/// <param name="title">Page title.</param>
		/// <param name="url">URL string in <code>https://ru.wikipedia.org/wiki/$1</code> format.</param>
		private static async Task<string> GetNormalisedTitle(string title, string url)
		{
			string pageTitle = title;
			WikiSite site = GetWikiSite(url).Result;
			if (site == null) return pageTitle;

			try
			{
				var page = new WikiPage(site, title);
				await page.RefreshAsync();
				pageTitle = page.Title;
			}
			catch (Exception ex)
			{
				Program.LogMessage($"Wiki at {url} can’t be reached: {ex.InnerException}", "Linking", "warning");
			}

			// Restore the anchor from original title
			if (title.Contains('#'))
			{
				pageTitle += "#" + EncodePageTitle(title.Split('#')?[1]);
			}

			await Task.CompletedTask;
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
			// Only check part before #
			string[] anchor = str.Split('#', 2);
			if (anchor.Length > 1)
			{
				str = anchor[0];
			}

			// Check if page title length is more than 255 bytes
			if (checkLength && Encoding.UTF8.GetByteCount(str) > 255) return true;

			// Check if it contains illegal sequences
			if (str == "." || str == "..") return true;
			if (str.StartsWith("./") || str.StartsWith("../")) return true;
			if (str.Contains("/./") || str.Contains("/../")) return true;
			if (str.EndsWith("/.") || str.EndsWith("/..")) return true;

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
			if (Regex.IsMatch(str, "^ *:{2,}")) return true;

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

			foreach (string expr in illegalExprs)
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
			if (!escapePar && (title.EndsWith(")") || title.EndsWith(".")) && !title.Contains("#"))
			{
				title += "_";
			}
			return format.Replace("$1", title.Trim());
		}

		/// <summary>
		/// Test if a channel override exists for the channel.
		/// </summary>
		/// <param name="channel">Discord channel information.</param>
		/// <returns>Goal ID compatible with data.</returns>
		private static string GetConfigGoal(DiscordChannel channel)
		{
			var goal = "#" + channel.Id;
			if (channel.IsThread)
			{
				goal = "#" + channel.ParentId;
			}

			string channelWiki = Config.GetWiki(goal.ToString(), false);
			if (channelWiki != null)
			{
				return goal;
			}

			return channel.GuildId.ToString();
		}

		/// <summary>
		/// Capitalise a string.
		/// </summary>
		/// <param name="value">String to be capitalised.</param>
		/// <returns>String with the first letter in uppercase.</returns>
		private static string Capitalise(string value)
		{
			if (value.Length == 0)
			{
				return value;
			}
			
			return value[0].ToString().ToUpper() + value.Substring(1);
		}

		/// <summary>
		/// Encode page title according to the rules of MediaWiki.
		/// </summary>
		/// <param name="str">Page title.</param>
		/// <param name="escapePar">Escape parentheses for Markdown links.</param>
		/// <param name="spaceChar">Character that should be used instead of space-like characters.</param>
		/// <returns>An encoded page title.</returns>
		private static string EncodePageTitle(string str, bool escapePar = false, string spaceChar = "_")
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
				'>',
			};

			char[] deletedChars = {
				// Soft hyphen (useless in links)
				'\u00ad',
				// LTR/RTL marks (useless in links)
				'\u200e',
				'\u200f',
			};

			// Decode HTML-encoded symbols before encoding
			if (str.Contains("&")) str = HttpUtility.HtmlDecode(str);

			// Decode percent-encoded symbols before encoding
			if (str.Contains("%")) str = Uri.UnescapeDataString(str);

			// Replace all spaces to required space symbol
			str = str.Trim().Replace('_', ' ');
			str = Regex.Replace(str, @"\s{1,}", spaceChar);

			// Percent encoding for special characters
			foreach (var ch in specialChars)
			{
				str = str.Replace(ch.ToString(), Uri.EscapeDataString(ch.ToString()));
			}

			// Remove deleted special characters
			foreach (var ch in deletedChars)
			{
				str = str.Replace(ch.ToString(), "");
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
