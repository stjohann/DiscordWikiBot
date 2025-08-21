using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Web;
using DSharpPlus;
using DSharpPlus.Entities;
using DSharpPlus.EventArgs;
using WikiClientLibrary.Sites;
using WikiClientLibrary.Pages;
using DiscordWikiBot.Schemas;

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
		private static readonly int[] capitalisedNamespaces = [
			// Special
			-1,
			// User
			2, 3,
			// MediaWiki
			8, 9,
		];

		/// <summary>
		/// Link patterns for which an embed is always useful
		/// </summary>
		private static readonly string[] alwaysEmbeddableLinks = [
			"://phabricator.wikimedia.org/T",
			// Wikidata links only have descriptions in embeds, so this isn’t useful yet
			// "://www.wikidata.org/wiki/Lexeme:L",
			"://www.wikidata.org/wiki/Property:P",
		];

		private static readonly bool useDiscordLinkEmbeds = Config.GetValue("useDiscordLinkEmbeds") != null;

		/// <summary>
		/// Replacement string for long messages.
		/// </summary>
		private static readonly string TOO_LONG = "TOO_LONG";

		/// <summary>
		/// Message cache length.
		/// </summary>
		private static readonly int CACHE_LENGTH = 1000;

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
		/// <param name="goal">Discord server/channel ID.</param>
		/// <param name="refresh">Refresh the info even if site info already has a key.</param>
		public static async Task Init(string goal = "", bool refresh = false)
		{
			string wiki = Config.GetWiki(goal);

			// Fetch values for the goal’s wiki
			if (refresh || !WikiSiteInfo.ContainsKey(wiki))
			{
				var data = await GetWikiSite(wiki);
				if (data != null)
				{
					if (!WikiSiteInfo.ContainsKey(wiki))
					{
						WikiSiteInfo.Add(wiki, data);
					}
					else
					{
						await WikiSiteInfo[wiki].RefreshSiteInfoAsync();
						if (refresh)
						{
							Program.LogMessage($"Updated the site info for {wiki} (#{goal})");
						}
					}
				}
			}
		}

		/// <param name="server">Discord server instance.</param>
		/// <inheritdoc cref="Init" />
		public static async Task Init(DiscordGuild server, bool refresh = false)
		{
			await Init(server.Id.ToString(), refresh);
		}

		/// <param name="channel">Discord channel instance.</param>
		/// <inheritdoc cref="Init" />
		public static async Task Init(DiscordChannel channel, bool refresh = false)
		{
			var channelWiki = Config.GetWiki(channel, false);
			if (channelWiki != null)
			{
				await Init($"#{channel.Id}", refresh);
			}
		}

		/// <summary>
		/// Remove wiki site information for a specified server.
		/// </summary>
		/// <param name="goal">Discord server ID.</param>
		public static void Remove(string goal = "")
		{
			//if (goal == "" || goal == null) return;
			return;

			// TODO: Reimplement removal mechanism for wiki info
		}

		/// <summary>
		/// React to a Discord message containing wiki links.
		/// </summary>
		/// <param name="e">Discord message information.</param>
		public static async Task Answer(DiscordClient sender, MessageCreateEventArgs e)
		{
			// Ignore empty messages / bots if necessary
			if (e.Message?.Content == null) return;

			if (CannotAnswerBots(e.Guild, e.Message)) return;

			string content = e.Message.Content;

			// Determine our goal (default for DMs)
			bool isServerMessage = e.Guild != null;
			var wikiUrl = Config.GetWiki();
			var lang = Config.GetLang();

			if (isServerMessage)
			{
				wikiUrl = Config.GetWiki(e.Channel);
				lang = Config.GetLang(e.Guild);

				await Init(e.Channel);
			}

			// Send message
			string msg = PrepareMessage(content, lang, wikiUrl);
			if (msg != "")
			{
				bool isTooLong = msg == TOO_LONG;
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
		public static async Task Edit(DiscordClient sender, MessageUpdateEventArgs e)
		{
			// Ignore empty messages / DMs / bots if necessary
			if (e.Message?.Content == null || e.Guild == null) return;

			if (CannotAnswerBots(e.Guild, e.Message)) return;

			var messageId = e.Message.Id;
			bool isRecentMessage = (DateTime.UtcNow - e.Message.CreationTimestamp).TotalMinutes <= 5;

			// Only update known or recent messages
			if (!Cache.ContainsKey(messageId) && !isRecentMessage) return;

			// Only update on real edits, because new messages with embeds trigger two events
			if (e.Message.Content == e.MessageBefore?.Content) return;

			// Determine our goal
			string lang = Config.GetLang(e.Guild);
			await Init(e.Channel);

			// Get a message
			string msg = PrepareMessage(e.Message.Content, lang, Config.GetWiki(e.Channel));
			bool isTooLong = msg == TOO_LONG;

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
		public static async Task Delete(DiscordClient sender, MessageDeleteEventArgs e)
		{
			// Ignore DMs / bots if necessary
			if (e.Channel?.Guild == null) return;

			if (CannotAnswerBots(e.Guild, e.Message)) return;

			// Ignore other bots / DMs
			bool isOurBot = e.Message?.Author == Program.Client.CurrentUser;
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
		public static async Task BulkDelete(DiscordClient sender, MessageBulkDeleteEventArgs e)
		{
			// Ignore DMs / bots if necessary
			if (e.Channel?.Guild == null) return;

			if (CannotAnswerBots(e.Guild, e.Messages?[0])) return;

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
		/// Check whether the bot cannot answer other bots on this server.
		/// </summary>
		/// <param name="server"></param>
		private static bool CannotAnswerBots(DiscordGuild server, DiscordMessage message)
		{
			if (message?.Author?.IsBot == false) return false;

			// Always ignore our bot
			if (message?.Author == Program.Client.CurrentUser) return true;

			return !Config.GetAnswerBots(server);
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

			// Remove unescaped code syntax
			content = Regex.Replace(content, @"(`{2,3}).*?\1", string.Empty, RegexOptions.Singleline);
			content = Regex.Replace(content, @"(?<!\\)(`).*?\1", string.Empty, RegexOptions.Singleline);

			// Remove nowiki tags
			content = Regex.Replace(content, @"<nowiki>.*?<\/nowiki>", string.Empty, RegexOptions.Singleline | RegexOptions.IgnoreCase);

			// Remove quote blocks
			content = Regex.Replace(content, @"^>>> [^$]+$", string.Empty, RegexOptions.Multiline);
			content = Regex.Replace(content, @"^> .+", string.Empty, RegexOptions.Multiline);

			// Replace emojis like <:meta:873203055804436513> with :meta:
			content = Regex.Replace(content, @"<:([^:]+):[\d]+>", ":$1:", RegexOptions.Multiline);

			// Remove Markdown escaping for Markdown-specific symbols
			content = Regex.Replace(content, @"\\([_*~`])", "$1");

			// Convert mobile links to current wiki to wikilinks
			content = ReplaceMobileLinks(content, linkFormat);

			// Ignore messages without wiki syntax
			if (!content.Contains("[[") && !content.Contains("{{")) return "";

			// Extract visible content (no spoilers)
			string visibleContent = Regex.Replace(content, @"\|{2}(.+?)\|{2}", "", RegexOptions.Multiline);

			// Start digging for links
			var matches = linkPattern.Matches(content);
			var linkIds = new List<string>();
			var links = new List<string>();

			if (matches.Count > 0)
			{
				// Add a unique link for each match into the list
				foreach (Match link in matches)
				{
					var linkData = AddLink(link, linkFormat);
					if (linkData == null) continue;

					var strId = linkData.Item1;
					var str = linkData.Item2;
					if (str.Length == 0) continue;

					// Present in content but not visible, therefore it's marked as spoiler
					if (!visibleContent.Contains(link.Value))
					{
						str = $"||{str}||";
					}

					// Remember title to prevent duplicate links with different formatting
					if (!linkIds.Contains(strId))
					{
						links.Add(str);
						linkIds.Add(strId);
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
		/// <returns>Resolved title and link string.</returns>
		public static Tuple<string, string> AddLink(Match link, string linkFormat)
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

			// Check for invalid page titles (without length)
			if (IsInvalid(str, false)) return null;

			// Reject if empty
			if (str.Length == 0) return null;

			// Reject if a regular link starts with an anchor
			if (isLink && str.StartsWith('#')) return null;

			// Reject if it is a transclusion of most likely a subpage
			if (isTransclusion && str.StartsWith('/')) return null;

			// Storage for default and current site data
			var defaultSiteInfo = GetWikiSite(linkFormat).Result;
			var currentSiteInfo = defaultSiteInfo;
			var currentLinkFormat = linkFormat;

			// Storage for page data
			NamespaceInfo ns = null;
			string nsName = null;
			bool capitalised = !defaultSiteInfo.SiteInfo.IsTitleCaseSensitive;
			bool isMediaWiki = true;

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
					else
					{
						isMediaWiki = false;
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
			var isDifferentWiki = isMediaWiki && currentSiteInfo.SiteInfo.BaseUrl != defaultSiteInfo.SiteInfo.BaseUrl;
			if (isDifferentWiki && str.Length == 0)
			{
				str = currentSiteInfo.SiteInfo.MainPage;
			}

			// Decode page title once before any transforms
			str = DecodePageTitle(str);

			// Check for invalid page title length (namespace number + title)
			var nsTitle = (ns == null || ns.Id == 0) ? str : $"{ns.Id}:{str}";
			if (IsInvalid(nsTitle, true, isMediaWiki, !isDifferentWiki)) return null;

			// Capitalise the title if necessary
			str = Capitalise(str, capitalised);

			// Remember the link string (without namespace for template links)
			var linkStr = str;

			// Add namespace before any transformations
			if (ns != null && ns.Id != 0)
			{
				nsName = nsName == null ? ns.CustomName : nsName;
				str = string.Join(":", [nsName, str]);

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

			// Create link text from the result
			var linkStart = brackets.Substring(0, 2);
			var linkEnd = endBrackets.Substring(0, 2);
			var linkInterwikis = string.Join(":", iwList);
			if (linkInterwikis.Length > 0) linkInterwikis += ":";
			linkStr = $"{linkInterwikis}{linkStr}";

			// Account for links to [[`]] and other pages with that symbol
			var linkEsc = "`";
			if (linkStr.StartsWith(linkEsc)) linkStr = $" {linkStr}";
			if (linkStr.EndsWith(linkEsc)) linkStr = $"{linkStr} ";
			if (linkStr.Contains(linkEsc)) linkEsc = "``";

			var linkText = $"{linkStart}{linkEsc}{linkStr}{linkEsc}{linkEnd}";
			return Tuple.Create(
				$"{linkInterwikis}{str}",
				GetMarkdownLink(str, currentLinkFormat, linkText)
			);
		}

		/// <summary>
		/// Try to replace mobile links in a given string to wikilinks.
		/// </summary>
		/// <param name="content">Message content.</param>
		/// <param name="linkFormat">Standard link format for the message.</param>
		private static string ReplaceMobileLinks(string content, string linkFormat)
		{
			if (!content.Contains("//") || !content.Contains("m."))
			{
				return content;
			}

			// Guess the subdomain format: most are abcd.m.domain.org, but www.domain.org are m.domain.org
			string mobileLinkFormat;
			if (linkFormat.Contains("://www."))
			{
				mobileLinkFormat = Regex.Replace(linkFormat, @"://www\.", "://m.");
			}
			else
			{
				mobileLinkFormat = Regex.Replace(linkFormat, @$"://(.*?)\.", $"://$1.m.");
			}

			// Do not respond to unchanged links
			if (mobileLinkFormat == linkFormat)
			{
				return content;
			}

			// Replace Markdown [link syntax]() with URLs
			content = Regex.Replace(content, @"\[[^\[\]]+\]\( *\<?([^\)]+)\>? *\)", "$1", RegexOptions.Multiline);

			// Replace guessed subdomain instances to wikilinks
			var linkRegex = new Regex(mobileLinkFormat.Replace("$1", @"([^\s\>]+)"));
			var matches = linkRegex.Matches(content);

			foreach (Match match in matches)
			{
				// Ignore links with URL parameters (?action=history)
				var value = match.Value;
				var link = new Uri(value);
				if (link.Query.Length > 0 && link.Query != "?")
				{
					continue;
				}

				content = content.Replace(value, linkRegex.Replace(value, "[[$1]]"));
			}

			return content;
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
			var junkRegex = string.Join('|', [
				string.Join('|', substNames),
				string.Join('|', safesubstNames),
				string.Join('|', rawNames),
				string.Join('|', msgNames),
			]);

			title = Regex.Replace(title, $"^ *(?:{junkRegex}) *", "", RegexOptions.IgnoreCase);

			// Guess that it is a MediaWiki: page
			var intNames = GetMagicWordNames("int", magicWords);
			if (intNames.Any(x => title.StartsWith(x, InvariantCultureIgnoreCase)))
			{
				var intRegex = string.Join('|', intNames);
				title = Regex.Replace(title, $"^ *(?:{intRegex}) *", "", RegexOptions.IgnoreCase);
				return Tuple.Create(site.Namespaces["mediawiki"], title);
			}

			// Guess that it is a Special: page
			var specialNames = GetMagicWordNames("special", magicWords);
			if (specialNames.Any(x => title.StartsWith(x, InvariantCultureIgnoreCase)))
			{
				var specialRegex = string.Join('|', specialNames);
				title = Regex.Replace(title, $"^ *(?:{specialRegex}) *", "", RegexOptions.IgnoreCase);
				return Tuple.Create(site.Namespaces["special"], title);
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
				return [name];
			}

			// Format #invoke: and #special: manually
			if (name == "invoke" || name == "special")
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
			return magicWords.FirstOrDefault(x => HasParserFunction(str, x)) != null;
		}

		/// <summary>
		/// Check if a string has a specific parser function from a wiki.
		/// For simplicity {{if: test}} (for {{#if}} etc.) is treated as a parser function, though it is allowed to create templates with these names.
		/// </summary>
		/// <param name="str">String to check for parser functions.</param>
		/// <param name="data">Magic word info.</param>
		/// <returns></returns>
		private static bool HasParserFunction(string str, MagicWordInfo data)
		{
			// Ignore things that cannot be parser functions
			var notParserFunction = (
				// SERVERNAME etc.
				data.Name.StartsWith("server")
				// STYLEPATH etc.
				|| data.Name.EndsWith("path")
				// REVISIONID etc.
				|| data.Name.StartsWith("revision")
				// NUMBEROFADMINS etc.
				|| data.Name.StartsWith("numberof")
				// CURRENTDAY/LOCALDAY etc.
				|| data.Name.StartsWith("current")
				|| data.Name.StartsWith("local")
			);
			if (notParserFunction)
			{
				return false;
			}

			var comparisonType = data.CaseSensitive ? StringComparison.InvariantCulture : StringComparison.InvariantCultureIgnoreCase;
			bool Compare(string value)
			{
				var alias = value.TrimEnd(':') + ":";
				return str.StartsWith(alias, comparisonType);
			}

			// Canonical name is not present in alias list
			return Compare(data.Name) || data.Aliases.FirstOrDefault(Compare) != null;
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
		/// <param name="isMediaWiki">Is the title length checked for a wiki.</param>
		/// <param name="checkProtocol">Whether to check for URI protocols.</param>
		/// <returns>Is page title invalid.</returns>
		public static bool IsInvalid(string str, bool checkLength = false, bool isMediaWiki = true, bool checkProtocol = true)
		{
			// Only check part before #
			string[] anchor = str.Split('#', 2);
			if (anchor.Length > 1)
			{
				str = anchor[0];
			}

			// Check if page title length is more than 255 bytes
			// There is an undocumented higher limit for special pages:
			// https://github.com/wikimedia/mediawiki/blob/8a0ae03da00d9f031b8ea5cd1b7d7b2694ee6bd5/includes/title/MediaWikiTitleCodec.php#L521
			if (checkLength)
			{
				var isSpecial = isMediaWiki && str.StartsWith("-1:");
				if (isSpecial && Encoding.UTF8.GetByteCount(str) > 512) return true;

				if (Encoding.UTF8.GetByteCount(str) > 255) return true;
			}

			// Check if it contains illegal sequences
			if (str == "." || str == "..") return true;
			if (str.StartsWith("./") || str.StartsWith("../")) return true;
			if (str.Contains("/./") || str.Contains("/../")) return true;
			if (str.EndsWith("/.") || str.EndsWith("/..")) return true;

			// Check if it is a MediaWiki-valid URL
			// https://www.mediawiki.org/wiki/Manual:$wgUrlProtocols
			string[] uriProtocols = [
				"bitcoin:", "ftp://", "ftps://", "geo:", "git://", "gopher://", "http://",
				"https://", "irc://", "ircs://", "magnet:", "mailto:", "matrix:", "mms://",
				"news:", "nntp://", "redis://", "sftp://", "sip:", "sips:", "sms:",
				"ssh://", "svn://", "tel:", "telnet://", "urn:", "worldwind://", "xmpp:",
				"//",
			];
			if (checkProtocol && uriProtocols.Any(str.StartsWith)) return true;

			// Check if it has two : or more
			if (Regex.IsMatch(str, "^ *:{2,}")) return true;

			// Following checks are based on MediaWiki page title restrictions:
			// https://www.mediawiki.org/wiki/Manual:Page_title
			string[] illegalExprs =
			[
				@"\<", @"\>",
				@"\[", @"\]",
				@"\{", @"\}",
				@"\|",
				@"~{3,}",
				@"&(?:[a-z]+|#x?\d+);"
			];

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
		/// <returns>A page URL in specified format.</returns>
		public static string GetMarkdownLink(string title, string format = null, string text = "")
		{
			if (text == null || text == "")
			{
				text = DecodePageTitle(title);
			}

			string url = GetUrl(title, format);
			bool isEmbeddable = alwaysEmbeddableLinks.Any(l => url.Contains(l));
			if (useDiscordLinkEmbeds || isEmbeddable)
			{
				return $"[{text}]( {url} )";
			}
			return $"[{text}]( <{url}> )";
		}

		/// <summary>
		/// Get a URL for a specified title and a wiki URL.
		/// </summary>
		/// <param name="title">Page title.</param>
		/// <param name="format">Wiki URL.</param>
		/// <returns>A page URL in specified format.</returns>
		public static string GetUrl(string title, string format = null)
		{
			if (format == null)
			{
				format = Config.GetWiki();
			}

			title = EncodePageTitle(title);
			return format.Replace("$1", title);
		}

		/// <summary>
		/// Capitalise a string.
		/// </summary>
		/// <param name="value">String to be capitalised.</param>
		/// <param name="capitalise">Whether to not perform capitalisation.</param>
		/// <returns>String with the first letter in uppercase.</returns>
		private static string Capitalise(string value, bool capitalise = true)
		{
			if (capitalise == false || value.Length == 0)
			{
				return value;
			}

			// Georgian letters should not be capitalised
			// TODO: Implement more proper language support 
			if (Regex.IsMatch(value[0].ToString(), @"\p{IsGeorgian}"))
			{
				return value;
			}

			return value[0].ToString().ToUpper() + value.Substring(1);
		}

		/// <summary>
		/// Encode page title according to the rules of MediaWiki.
		/// </summary>
		/// <param name="str">Page title.</param>
		/// <param name="spaceChar">Character that should be used instead of space-like characters.</param>
		private static string EncodePageTitle(string str, string spaceChar = "_")
		{
			// Following character conversions are based on {{PAGENAMEE}} specification:
			// https://www.mediawiki.org/wiki/Manual:PAGENAMEE_encoding
			char[] specialChars =
			[
				// Discord already escapes this character in URLs
				// '"',
				// Discord breaks the links with %25 in them
				// '%',
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
				// Added: Causes problems for {{)!}} etc.
				'(',
				')',
			];

			// Decode and replace all spaces to required space symbol
			str = DecodePageTitle(str);
			str = Regex.Replace(str, @"\s{1,}", spaceChar);

			// Percent encoding for special characters
			foreach (var ch in specialChars)
			{
				str = str.Replace(ch.ToString(), Uri.EscapeDataString(ch.ToString()));
			}

			return str;
		}

		/// <summary>
		/// Clean up page title from unnecessary stuff.
		/// </summary>
		/// <param name="str">Page title.</param>
		private static string DecodePageTitle(string str)
		{
			str = str.Trim().TrimStart(':').Replace('_', ' ');
			str = Regex.Replace(str, @" {2,}", " ");

			// Remove escaping symbols for \ in Discord
			str = Regex.Replace(str, @"\\\\", "");

			// Decode HTML-encoded symbols before encoding
			if (str.Contains("&")) str = HttpUtility.HtmlDecode(str);

			// Decode percent-encoded symbols before encoding
			if (str.Contains("%")) str = Uri.UnescapeDataString(str);

			// Remove deleted special characters
			char[] deletedChars = [
				// Soft hyphen (useless in links)
				'\u00ad',
				// LTR/RTL marks (useless in links)
				'\u200e',
				'\u200f',
			];

			foreach (var ch in deletedChars)
			{
				str = str.Replace(ch.ToString(), "");
			}

			return str.Trim();
		}
	}
}
