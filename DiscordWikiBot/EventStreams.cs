using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text.RegularExpressions;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using DiscordWikiBot.Schemas;
using LaunchDarkly.EventSource;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using DSharpPlus;
using DSharpPlus.Entities;

namespace DiscordWikiBot
{
	/// <summary>
	/// EventStreams class.
	/// <para>Adds methods for subscribing and unsubscribing to streams, and formatting recent changes in Discord embeds.</para>
	/// </summary>
	class EventStreams
	{
		/// <summary>
		/// Flag for whether EventStreams was enabled or not.
		/// </summary>
		public static bool Enabled = false;

		// EventSource stream instance
		private static EventSource Stream;

		// EventSource stream translations
		private static JObject Data = null;

		/// <summary>
		/// Latest message timestamp.
		/// </summary>
		public static DateTime LatestTimestamp;

		/// <summary>
		/// List of all allowed Wikimedia projects.
		/// </summary>
		private static readonly string[] WMProjects = {
			".wikipedia.org",
			".wiktionary.org",
			".wikibooks.org",
			".wikinews.org",
			".wikiquote.org",
			".wikisource.org",
			".wikiversity.org",
			".wikivoyage.org",
			".wikimedia.org",
			"www.mediawiki.org",
			"www.wikidata.org"
		};

		// Path to JSON file
		private static readonly string JSON_PATH = @"eventStreams.json";

		/// <summary>
		/// Initialise the default settings and setup things for overrides.
		/// </summary>
		public static void Init()
		{
			// Restart the stream if everything is initialised
			if (Enabled)
			{
				Stream.Restart(false);
				return;
			}

			// Get JSON
			Program.LogMessage($"Reading JSON config", "EventStreams");
			string json = "";
			if (!File.Exists(JSON_PATH))
			{
				Program.LogMessage($"Please create a JSON file called \"{JSON_PATH}\" before trying to use EventStreams.", "EventStreams", "error");
				return;
			}
			json = File.ReadAllText(JSON_PATH, Encoding.Default);

			Data = JObject.Parse(json);

			// Check if default domain is a Wikimedia project
			if (!CanBeUsed(Config.GetDomain(), out string[] temp))
			{
				Program.LogMessage($"Default stream domain should be a Wikimedia project.\nList of available projects: {string.Join(", ", WMProjects)}", "EventStreams", "error");
				return;
			}

			// Open new EventStreams instance
			Enabled = true;
			Program.LogMessage($"Connecting to stream.wikimedia.org", "EventStreams");
			Stream = new EventSource(new Uri("https://stream.wikimedia.org/v2/stream/recentchange"));
			LatestTimestamp = DateTime.Now;

			// Log any errors
			Stream.Error += (sender, args) =>
			{
				var exception = args.Exception;
				// See https://phabricator.wikimedia.org/T242767 for why IOExceptions are ignored
				if (!(exception is IOException))
				{
					Program.LogMessage($"Stream returned the following exception: {exception}", "EventStreams", "warning");
				}

				// Reconnect to the stream
				Stream.Restart(false);
			};

			// Start recording events
			Stream.MessageReceived += Stream_MessageReceived;

			Task.Run(async () => await Stream.StartAsync().ConfigureAwait(false));
		}

		/// <summary>
		/// Respond to a new recent change if it matches a title or a namespace number.
		/// </summary>
		/// <param name="sender"></param>
		/// <param name="e">Recent change information.</param>
		private static async void Stream_MessageReceived(object sender, MessageReceivedEventArgs e)
		{
			if (e.EventName != "message")
			{
				return;
			}
			RecentChange change = null;
			try
			{
				change = RecentChange.FromJson(e.Message.Data);
			}
			catch (Exception exception)
			{
				Program.LogMessage($"Stream returned the following exception: {exception}", "EventStreams", "warning");
			}
			if (change == null) return;
			var changeTimestamp = change.Metadata.DateTime.ToUniversalTime();
			LatestTimestamp = changeTimestamp;
			bool notEdit = change.Type != "edit" && change.Type != "new";
			if (notEdit) return;

			// Do not post anything if it is an edit older than a day
			// TODO: Investigate the proper fix
			if (DateTime.UtcNow > changeTimestamp.AddHours(24))
			{
				return;
			}

			string ns = change.Namespace.ToString();
			string title = change.Title;

			// React if there is server data for namespace
			if (Data[$"<{ns}>"] != null)
			{
				await React($"<{ns}>", Data.Value<JObject>($"<{ns}>"), change);
			}

			// React if there is server data for title
			if (Data[title] != null)
			{
				await React(title, Data.Value<JObject>(title), change);
			}
		}

		/// <summary>
		/// React to a change in channels if it matches their parameters.
		/// </summary>
		/// <param name="goal">A page title or a namespace number.</param>
		/// <param name="data">A list of Discord channels and their parameters.</param>
		/// <param name="change">Recent change information.</param>
		/// <returns></returns>
		public static async Task React(string goal, JObject data, RecentChange change)
		{
			DiscordClient client = Program.Client;
			Dictionary<string, Dictionary<string, dynamic>> badChannels = new Dictionary<string, Dictionary<string, dynamic>>();

			foreach (var item in data.ToObject<Dictionary<string, dynamic>>())
			{
				Dictionary<string, dynamic> args = item.Value.ToObject<Dictionary<string, dynamic>>();
				DiscordChannel channel = null;
				try
				{
					ulong channelID = ulong.Parse(item.Key);
					channel = await client.GetChannelAsync(channelID);
				}
				catch (Exception ex)
				{
					string goalInfo = $"title={goal}";
					string rawGoal = goal.Trim('<', '>');
					if (rawGoal != goal)
					{
						goalInfo = $"namespace={rawGoal}";
					}
					Program.LogMessage($"Channel {item.Key} ({goalInfo}) can’t be reached: {ex}", "EventStreams", "warning");

					// Remove data if channel is deleted or unavailable
					if (Program.IsChannelInvalid(ex))
					{
						if (rawGoal != goal)
						{
							args["namespace"] = rawGoal;
						}
						else
						{
							args["title"] = rawGoal;
						}
						badChannels.Add(item.Key, args);
					}
				}

				// Stop if channel is not assigned
				if (channel == null)
				{
					continue;
				}

				// Check if domain is the same
				string domain = Config.GetDomain();
				if (channel != null)
				{
					domain = Config.GetDomain(channel.GuildId.ToString());
					if (domain != change.ServerName)
					{
						continue;
					}
				}

				// Set up domain for future usage for link formatting
				domain = $"https://{domain}/wiki/$1";

				// Check if bot edits are allowed
				if (!args.ContainsKey("bot") && change.Bot == true)
				{
					continue;
				}

				// Check if minor edits are disallowed
				if (args.ContainsKey("minor"))
				{
					if (args["minor"] == false && change.Minor == true)
					{
						continue;
					}
				}

				// Check if patrolled edits are allowed
				if (args.ContainsKey("patrolled"))
				{
					bool patrolStatus = args["patrolled"] == "only" ? true : false;
					if (change.Patrolled != patrolStatus)
					{
						continue;
					}
				}

				// Check the edit type if it is defined
				if (args.ContainsKey("type"))
				{
					if (args["type"] != change.Type.ToString().ToLower())
					{
						continue;
					}
				}

				// Check for minimum length of the diff
				if (args.ContainsKey("diff-length"))
				{
					int minLength = Convert.ToInt32(args["diff-length"]);
					long revLength = change.Length.New - change.Length.Old;

					if (revLength < minLength)
					{
						continue;
					}
				}

				// Check string in comment text
				if (args.ContainsKey("in-comment"))
				{
					string comment = change.Comment.ToLower();
					if (!comment.Contains(args["in-comment"]))
					{
						continue;
					}
				}

				// Send the message
				string lang = Config.GetLang(channel.GuildId.ToString());
				try
				{
					await client.SendMessageAsync(channel, embed: GetEmbed(change, domain, lang));
				}
				catch (Exception ex)
				{
					string goalInfo = $"title={goal}";
					string rawGoal = goal.Trim('<', '>');
					if (rawGoal != item.Key)
					{
						goalInfo = $"namespace={rawGoal}";
					}
					Program.LogMessage($"Message in channel #{channel.Name} (ID {item.Key}; {goalInfo}) could not be posted: {ex}", "EventStreams", "warning");

					// Remove data if channel is deleted or unavailable
					if (Program.IsChannelInvalid(ex))
					{
						if (rawGoal != item.Key)
						{
							args["namespace"] = rawGoal;
						}
						else
						{
							args["title"] = rawGoal;
						}
						badChannels.Add(item.Key, args);
					}
				}
			}

			// Remove streams from bad channels
			foreach (KeyValuePair<string, Dictionary<string, dynamic>> entry in badChannels)
			{
				RemoveData(entry.Key, entry.Value);
			}
		}

		/// <summary>
		/// Build a Discord embed in the style of a MediaWiki diff.
		/// </summary>
		/// <param name="change">Recent change information.</param>
		/// <param name="format">Wiki link URL.</param>
		/// <param name="lang">Language code in ISO 639 format.</param>
		/// <returns>Discord embed.</returns>
		public static DiscordEmbedBuilder GetEmbed(RecentChange change, string format, string lang)
		{
			DiscordEmbedBuilder embed = new DiscordEmbedBuilder()
				.WithTimestamp(change.Metadata.DateTime);

			DiscordColor embedColor = new DiscordColor(0x72777d);
			string embedIcon = "2/25/MobileFrontend_bytes-neutral.svg/512px-MobileFrontend_bytes-neutral.svg.png";

			// Parse statuses from the diff
			string status = "";
			if (change.Type == "new")
			{
				status += Locale.GetMessage("eventstreams-new", lang);
			}
			if (change.Bot == true)
			{
				status += Locale.GetMessage("eventstreams-bot", lang);
			}
			if (change.Minor == true)
			{
				status += Locale.GetMessage("eventstreams-minor", lang);
			}

			if (status != "")
			{
				embed.WithFooter(status);
			}

			// Parse length of the diff
			long length = change.Length.New - change.Length.Old;
			if (length > 0)
			{
				embedColor = new DiscordColor(0x00af89);
				embedIcon = "a/ab/MobileFrontend_bytes-added.svg/128px-MobileFrontend_bytes-added.svg.png";
			}
			else if (length < 0)
			{
				embedIcon = "7/7c/MobileFrontend_bytes-removed.svg/128px-MobileFrontend_bytes-removed.svg.png";
				embedColor = new DiscordColor(0xdd3333);
			}

			embed
				.WithAuthor(
					change.Title,
					Linking.GetUrl(change.Title, format),
					$"https://upload.wikimedia.org/wikipedia/commons/thumb/{embedIcon}"
				)
				.WithColor(embedColor);

			// Rely on default error catching to guess if the message is too long
			try
			{
				embed.WithDescription(GetMessage(change, format, lang));
			}
			catch
			{
				embed.WithDescription(GetMessage(change, format, lang, false));
			}

			return embed;
		}

		/// <summary>
		/// Build a string in the style of a MediaWiki diff.
		/// </summary>
		/// <param name="change">Recent change information.</param>
		/// <param name="format">Wiki link URL.</param>
		/// <param name="lang">Language code in ISO 639 format.</param>
		/// <param name="linkify">Whether to make wikilinks actual links.</param>
		/// <returns>String with recent change information and links.</returns>
		public static string GetMessage(RecentChange change, string format, string lang, bool linkify = true)
		{
			// Parse length of the diff
			long length = change.Length.New - change.Length.Old;
			string strLength = length.ToString();
			if (length > 0)
			{
				strLength = "+" + strLength;
			}
			strLength = $"({strLength})";

			if (length > 500 || length < -500)
			{
				strLength = $"**{strLength}**";
			}

			// Markdownify link
			string link = format.Replace("/wiki/$1", string.Format("/?{0}{1}", change.Revision.Old != 0 ? "diff=" : "oldid=", change.Revision.New));
			link = string.Format("([{0}]( <{1}> ))", Locale.GetMessage("eventstreams-diff", lang), link);

			// Markdownify user links
			string user = "User:" + change.User;
			string talk = "User talk:" + change.User;
			string contribs = "Special:Contributions/" + change.User;
			talk = Linking.GetMarkdownLink(talk, format, Locale.GetMessage("eventstreams-talk", lang));

			if (IPAddress.TryParse(change.User, out _))
			{
				user = Linking.GetMarkdownLink(contribs, format, change.User);
				user = $"{user} ({talk})";
			}
			else
			{
				contribs = Linking.GetMarkdownLink(contribs, format, Locale.GetMessage("eventstreams-contribs", lang));
				user = Linking.GetMarkdownLink(user, format, change.User);

				user = $"${user} ({talk} | {contribs})";
			}

			string comment = ParseComment(change.Comment, format, change.Title, linkify);
			return $"{link} . . {strLength} . . {user}{comment}";
		}

		/// <summary>
		/// Get parameters for streams from specified list of channels.
		/// </summary>
		/// <param name="channels">List of Discord channel IDs.</param>
		/// <returns>An object with a list of channels and their parameters.</returns>
		public static JObject GetData(string[] channels)
		{
			if (Data == null) return null;
			JObject result = new JObject();

			// Remove streams belonging to other servers
			foreach (JProperty entry in Data.Properties())
			{
				JObject value = (JObject)entry.Value;

				foreach (KeyValuePair<string, JToken> item in value)
				{
					if (channels.Contains(item.Key))
					{
						if (result[entry.Name] == null)
						{
							result.Add(entry.Name, new JObject());
						}

						((JObject)result[entry.Name]).Add(new JProperty(item.Key, item.Value));
					}
				}
			}

			return result;
		}

		/// <summary>
		/// Set stream parameters for a specified Discord channel.
		/// </summary>
		/// <param name="channel">Discord channel ID.</param>
		/// <param name="args">List of parameters.</param>
		/// <param name="discardPrevData">Should original parameters be discarded.</param>
		/// <returns>A list with changed parameters.</returns>
		public static Dictionary<string, dynamic> SetData(string channel, Dictionary<string, dynamic> args, bool discardPrevData = true)
		{
			if (Data == null) return null;
			string goal = args.ContainsKey("title") ? args["title"] : $"<{args["namespace"]}>";
			Program.LogMessage($"Channel #{channel} triggered a change ({goal}) in eventStreams.json.", "EventStreams");
			Dictionary<string, dynamic> changes = new Dictionary<string, dynamic>();

			// List of allowed keys and their defaults
			Dictionary<string, dynamic> allowedKeys = new Dictionary<string, dynamic>
			{
				{ "bot", false },
				{ "in-comment", "" },
				{ "diff-length", 0 },
				{ "minor", true },
				{ "patrolled", "any" },
				{ "type", "any" },
			};

			// Set data object if undefined
			if (Data.Value<JObject>(goal) == null)
			{
				Data[goal] = new JObject();
			}

			// Add or append necessary data
			JObject result = new JObject();
			if (discardPrevData == false && (JObject)Data[goal][channel] != null)
			{
				result = (JObject)Data[goal][channel];
			}

			foreach (KeyValuePair<string, dynamic> item in args)
			{
				if (!allowedKeys.ContainsKey(item.Key))
				{
					continue;
				}

				var key = item.Key;
				var value = item.Value;
				var requiredValueType = allowedKeys.GetValueOrDefault(key)?.GetType();
				if (item.Value == null || requiredValueType == null)
				{
					continue;
				}

				// Ignore incorrectly typed values
				if (item.Value.GetType() != requiredValueType)
				{
					continue;
				}

				// Ignore default values if not resetting
				if (discardPrevData == false && result?.Property(key)?.Equals(value) == true)
				{
					continue;
				}

				// Reset to default
				if (allowedKeys.GetValueOrDefault(item.Key)?.Equals(item.Value))
				{
					if (discardPrevData == true)
					{
						JProperty prop = result.Property(key);
						if (prop != null)
						{
							prop.Remove();
							changes.Add(key, value);
						}
					}
					continue;
				}

				// Set and remember data
				result[key] = value;
				changes.Add(key, value);
			}

			// Set and save data
			if (discardPrevData == false && changes.Count == 0)
			{
				return changes;
			}

			Data[goal][channel] = result;
			File.WriteAllText(JSON_PATH, Data.ToString(), Encoding.Default);
			return changes;
		}

		/// <summary>
		/// Remove stream data for a specified channel.
		/// </summary>
		/// <param name="channel">Discord channel ID.</param>
		/// <param name="args">List of parameters.</param>
		public static void RemoveData(string channel, Dictionary<string, dynamic> args)
		{
			if (Data == null) return;
			string goal = args.ContainsKey("title") ? args["title"] : $"<{args["namespace"]}>";
			Program.LogMessage($"Removing data ({goal}) from JSON config", "EventStreams");

			// Change current data and remove an item if necessary
			if (Data[goal] == null) return;
			Data.Value<JObject>(goal).Property(channel)?.Remove();

			if (Data[goal].ToString() == "{}")
			{
				Data.Property(goal).Remove();
			}

			// Write it to JSON file
			File.WriteAllText(JSON_PATH, Data.ToString(), Encoding.Default);
		}

		/// <summary>
		/// Format the comment in the style of a MediaWiki diff.
		/// </summary>
		/// <param name="summary">Comment text.</param>
		/// <param name="format">Wiki link URL.</param>
		/// <param name="page">Page title.</param>
		/// <param name="linkify">Whether to make wikilinks actual links.</param>
		/// <returns>Parsed comment with styling.</returns>
		private static string ParseComment(string summary, string format, string page, bool linkify = true)
		{
			if (summary.Length == 0)
			{
				return "";
			}

			string linkPattern = @"\[{2}([^\[\]\|\n]+)\]{2}";
			string linkPatternPipe = @"\[{2}([^\[\]\|\n]+)\|([^\[\]\n]+)\]{2}";

			if (linkify)
			{
				// Linkify the code for sections
				summary = Regex.Replace(summary, @"/\* (.*?) \*/(.?)", m =>
				{
					string section = m.Groups[1].Value.Trim();
					var link = Linking.GetMarkdownLink(page + "#" + section, format, $"→{section}");

					if (m.Groups?[2].Value != "")
					{
						return $"{link}:{m.Groups[2].Value}";
					}

					return link;
				});

				bool IsLikelyInterwikiLink(string title)
				{
					// See https://gerrit.wikimedia.org/r/plugins/gitiles/mediawiki/core/+/32ded76880f86359a0a9dc6999ed2113c8bbb9c1/includes/specials/SpecialGoToInterwiki.php#73
					return title.Contains(":")
						&& char.IsLower(title[0])
						&& !title.StartsWith("special:");
				}

				// Linkify every wiki link in comment text
				summary = Regex.Replace(summary, linkPattern, m =>
				{
					string title = m.Groups[1].Value;
					string text = title;
					// TODO: Implement proper link parsing, see #16
					if (IsLikelyInterwikiLink(title))
					{
						title = $"Special:GoToInterwiki/{title.TrimStart(':')}";
					}

					return Linking.GetMarkdownLink(title, format, text);
				});

				summary = Regex.Replace(summary, linkPatternPipe, m =>
				{
					string title = m.Groups[1].Value;
					// TODO: Implement proper link parsing, see #16
					if (IsLikelyInterwikiLink(title))
					{
						title = $"Special:GoToInterwiki/{title}";
					}
					string text = m.Groups[2].Value;

					return Linking.GetMarkdownLink(title, format, text);
				});
			}
			else
			{
				// Transform code for sections to simpler version
				string comment = summary.ToString().Replace("/* ", "→");
				comment = Regex.Replace(comment, @" \*/$", string.Empty).Replace(" */", ":");

				// Display wiki links as plain text
				summary = Regex.Replace(summary, linkPattern, "$1");
				summary = Regex.Replace(summary, linkPatternPipe, "$2");
			}

			// Escape * inside summaries
			summary = summary.Replace("*", @"\*");

			// Add italics and parentheses
			return $" *({summary})*";
		}

		/// <summary>
		/// Check if a domain can use EventStreams.
		/// </summary>
		/// <param name="domain">EventStreams domain.</param>
		/// <param name="list">List of allowed projects.</param>
		public static bool CanBeUsed(string domain, out string[] list)
		{
			if (domain == null)
			{
				list = null;
				return false;
			}

			list = WMProjects;
			return list.Any(domain.EndsWith);
		}
	}
}
