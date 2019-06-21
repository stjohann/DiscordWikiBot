using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text.RegularExpressions;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using DSharpPlus;
using DSharpPlus.Entities;
using XmlRcs;

namespace DiscordWikiBot
{
	/// <summary>
	/// EventStreams class.
	/// <para>Adds methods for subscribing and unsubscribing to streams, and formatting recent changes in Discord embeds.</para>
	/// </summary>
	class EventStreams
	{
		// EventSource stream instance
		private static Provider Stream;

		// EventSource stream translations
		private static JObject Data = null;

		/// <summary>
		/// Latest message timestamp.
		/// </summary>
		public static DateTime LatestTimestamp;

		/// <summary>
		/// List of all allowed Wikimedia projects.
		/// </summary>
		public static string[] WMProjects = {
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
			// Get JSON
			Program.Client.DebugLogger.LogMessage(LogLevel.Info, "EventStreams", $"Reading JSON config", DateTime.Now);
			string json = "";
			if (!File.Exists(JSON_PATH))
			{
				Program.Client.DebugLogger.LogMessage(LogLevel.Error, "EventStreams", $"Please create a JSON file called \"{JSON_PATH}\" before trying to use EventStreams.", DateTime.Now);
				return;
			}
			json = File.ReadAllText(JSON_PATH, Encoding.Default);

			Data = JObject.Parse(json);

			// Check if default domain is a Wikimedia project
			if (!CanBeUsed(Config.GetDomain()))
			{
				Program.Client.DebugLogger.LogMessage(LogLevel.Error, "EventStreams", $"Default stream domain should be a Wikimedia project.\nList of available projects: {string.Join(", ", WMProjects)}", DateTime.Now);
				return;
			}

			// Open new EventStreams instance
			Program.Client.DebugLogger.LogMessage(LogLevel.Info, "EventStreams", $"Connecting to huggle-rc.wmflabs.org", DateTime.Now);
			Stream = new Provider(true, true);
			LatestTimestamp = DateTime.Now;
			Stream.Subscribe(Config.GetDomain());

			// Respond when server is ready
			Stream.On_OK += new Provider.OKHandler((o, e) =>
			{
				Program.Client.DebugLogger.LogMessage(LogLevel.Info, "EventStreams", $"Ready!", DateTime.Now);
			});

			// Log any exceptions
			Stream.On_Exception += new Provider.ExceptionHandler((o, e) =>
			{
				Program.Client.DebugLogger.LogMessage(LogLevel.Info, "EventStreams", $"Stream returned the following exception: {e.Exception}", DateTime.Now);
			});

			// Start recording events
			Stream.On_Change += Stream_On_Change;

			Stream.Connect();
		}

		/// <summary>
		/// Subscribe to recent changes for a specified domain.
		/// </summary>
		/// <param name="domain">Wikimedia domain.</param>
		public static void Subscribe(string domain)
		{
			if (domain == null || domain == Config.GetDomain()) return;
			Stream.Subscribe(domain);
		}

		/// <summary>
		/// Unsubscribe from recent changes to a specified domain.
		/// </summary>
		/// <param name="domain">Wikimedia domain.</param>
		public static void Unsubscribe(string domain = null)
		{
			if (domain == null || domain == Config.GetDomain()) return;
			Stream.Unsubscribe(domain);
		}

		/// <summary>
		/// Respond to a new recent change if it matches a title or a namespace number.
		/// </summary>
		/// <param name="sender"></param>
		/// <param name="e">Recent change information.</param>
		private static void Stream_On_Change(object sender, EditEventArgs e)
		{
			LatestTimestamp = e.Change.Timestamp.ToUniversalTime();
			bool notEdit = (e.Change.Type != RecentChange.ChangeType.Edit && e.Change.Type != RecentChange.ChangeType.New);
			if (notEdit) return;
			
			string ns = e.Change.Namespace.ToString();
			string title = e.Change.Title;

			// React if there is server data for namespace
			if (Data[$"<{ns}>"] != null)
			{
				React($"<{ns}>", Data.Value<JObject>($"<{ns}>"), e.Change).Wait();
			}

			// React if there is server data for title
			if (Data[title] != null)
			{
				React(title, Data.Value<JObject>(title), e.Change).Wait();
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
			foreach (KeyValuePair<string, JToken> item in data)
			{
				Dictionary<string, dynamic> args = item.Value.ToObject<Dictionary<string, dynamic>>();
				DiscordChannel channel = null;
				try
				{
					ulong channelID = ulong.Parse(item.Key);
					channel = await client.GetChannelAsync(channelID);
				} catch(Exception ex) {
					Program.Client.DebugLogger.LogMessage(LogLevel.Info, "EventStreams", $"Channel can’t be reached: {ex.Message}", DateTime.Now);

					// Remove data if channel was deleted
					if (ex is DSharpPlus.Exceptions.NotFoundException)
					{
						goal = goal.Trim('<', '>');
						if (goal != item.Key)
						{
							args["namespace"] = goal;
						} else
						{
							args["title"] = goal;
						}
						RemoveData(item.Key, args);
					}
				}

				// Stop if channel is not assigned
				if (channel == null)
				{
					return;
				}

				// Check if domain is the same
				string domain = Config.GetDomain();
				if (channel != null)
				{
					domain = Config.GetDomain(channel.Guild.Id.ToString());
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
					bool patrolStatus = (args["patrolled"] == "only" ? true : false);
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
					int revLength = (change.LengthNew - change.LengthOld);
					
					if (revLength < minLength)
					{
						continue;
					}
				}

				// Check string in comment text
				if (args.ContainsKey("in-comment"))
				{
					string comment = change.Summary.ToString().ToLower();
					if (!comment.Contains(args["in-comment"]))
					{
						continue;
					}
				}

				// Send the message
				string lang = Config.GetLang(channel.Guild.Id.ToString());
				await client.SendMessageAsync(channel, embed: GetEmbed(change, domain, lang));
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
				.WithTimestamp(change.Timestamp);

			DiscordColor embedColor = new DiscordColor(0x72777d);
			string embedIcon = "2/25/MobileFrontend_bytes-neutral.svg/512px-MobileFrontend_bytes-neutral.svg.png";

			// Parse statuses from the diff
			string status = "";
			if (change.Type == RecentChange.ChangeType.New)
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
			int length = (change.LengthNew - change.LengthOld);
			if (length > 0)
			{
				embedColor = new DiscordColor(0x00af89);
				embedIcon = "a/ab/MobileFrontend_bytes-added.svg/512px-MobileFrontend_bytes-added.svg.png";
			} else if (length < 0)
			{
				embedIcon = "7/7c/MobileFrontend_bytes-removed.svg/512px-MobileFrontend_bytes-removed.svg.png";
				embedColor = new DiscordColor(0xdd3333);
			}

			embed
				.WithAuthor(
					change.Title,
					Linking.GetLink(change.Title, format),
					string.Format("https://upload.wikimedia.org/wikipedia/commons/thumb/{0}", embedIcon)
				)
				.WithColor(embedColor)
				.WithDescription(GetMessage(change, format, lang));

			return embed;
		}

		/// <summary>
		/// Build a string in the style of a MediaWiki diff.
		/// </summary>
		/// <param name="change">Recent change information.</param>
		/// <param name="format">Wiki link URL.</param>
		/// <param name="lang">Language code in ISO 639 format.</param>
		/// <returns>String with recent change information and links.</returns>
		public static string GetMessage(RecentChange change, string format, string lang)
		{
			// Parse length of the diff
			int length = (change.LengthNew - change.LengthOld);
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
			string link = format.Replace("/wiki/$1", string.Format("/?{0}{1}", (change.OldID != 0 ? "diff=" : "oldid="), change.RevID));
			link = string.Format("([{0}]({1}))", Locale.GetMessage("eventstreams-diff", lang), link);

			// Markdownify user
			string user = "User:" + change.User;
			string talk = "User_talk:" + change.User;
			string contribs = "Special:Contributions/" + change.User;

			user = Linking.GetLink(user, format, true);
			talk = Linking.GetLink(talk, format, true);
			contribs = Linking.GetLink(contribs, format, true);

			talk = string.Format("[{0}]({1})", Locale.GetMessage("eventstreams-talk", lang), talk);

			if (IPAddress.TryParse(change.User, out IPAddress address))
			{
				user = $"[{change.User}]({contribs}) ({talk})";
			} else
			{
				contribs = string.Format("[{0}]({1})", Locale.GetMessage("eventstreams-contribs", lang), contribs);
				user = $"[{change.User}]({user}) ({talk} | {contribs})";
			}

			// Parse comment, adjusting for its length
			string comment = ParseComment(change.Summary, format);
			string msg = $"{link} . . {strLength} . . {user}";
			if (msg.Length + comment.Length > 2000)
			{
				comment = ParseComment(change.Summary, format, false);
			}
			msg += comment;

			return msg;
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
		/// <param name="reset">Should original parameters be discarded.</param>
		/// <returns>A list with changed parameters.</returns>
		public static Dictionary<string, dynamic> SetData(string channel, Dictionary<string, dynamic> args, bool reset = true)
		{
			if (Data == null) return null;
			string goal = (args.ContainsKey("title") ? args["title"] : $"<{ args["namespace"] }>");
			Program.Client.DebugLogger.LogMessage(LogLevel.Info, "EventStreams", $"Changing JSON config after a command was fired", DateTime.Now);
			Dictionary<string, dynamic> changes = new Dictionary<string, dynamic>();

			// List of allowed keys
			string[] allowedKeys =
			{
				"bot",
				"in-comment",
				"diff-length",
				"minor",
				"patrolled",
				"type",
			};

			// Default values for keys
			Dictionary<string, dynamic> defaults = new Dictionary<string, dynamic>
			{
				{ "bot", false },
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
			if (reset == false && (JObject)Data[goal][channel] != null)
			{
				result = (JObject)Data[goal][channel];
			}

			foreach (KeyValuePair<string, dynamic> item in args)
			{
				if (allowedKeys.Contains(item.Key))
				{
					string key = item.Key;
					dynamic value = item.Value;

					// Ignore same values
					if (reset == false && result[key] != null && result[key] == value) {
						continue;
					}

					// Reset to default
					if (defaults.ContainsKey(key) && value == defaults[key])
					{
						if (reset == false)
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
			}

			// Set and save data
			if (reset == false && changes.Count == 0)
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
			string goal = (args.ContainsKey("title") ? args["title"] : $"<{ args["namespace"] }>");
			Program.Client.DebugLogger.LogMessage(LogLevel.Info, "EventStreams", $"Changing JSON config after the command was fired", DateTime.Now);

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
		/// <param name="linkify">Should links be linkified or just removed.</param>
		/// <returns>Parsed comment with styling.</returns>
		private static string ParseComment(string summary, string format, bool linkify = true)
		{
			if (summary.Length == 0)
			{
				return "";
			}

			string linkPattern = "\\[{2}([^\\[\\]\\|\n]+)\\]{2}";
			string linkPatternPipe = "\\[{2}([^\\[\\]\\|\n]+)\\|([^\\[\\]\n]+)\\]{2}";

			// Transform code for section to simpler version
			string comment = summary.ToString().Replace("/* ", "→");
			comment = Regex.Replace(comment, " \\*/$", string.Empty).Replace(" */", ":");
			
			if (linkify)
			{
				// Linkify every wiki link in comment text
				comment = Regex.Replace(comment, linkPattern, m => {
					string title = m.Groups[1].Value;
					string link = string.Format("[{0}]({1})", title, Linking.GetLink(title, format, true));

					return link;
				});

				comment = Regex.Replace(comment, linkPatternPipe, m => {
					string title = m.Groups[1].Value;
					string text = m.Groups[2].Value;
					string link = string.Format("[{0}]({1})", text, Linking.GetLink(title, format, true));

					return link;
				});
			} else {
				// Display wiki links as plain text
				comment = Regex.Replace(comment, linkPattern, "$1");
				comment = Regex.Replace(comment, linkPatternPipe, "$2");
			}

			// Add italic and parentheses
			comment = $" *({comment})*";
			return comment;
		}

		/// <summary>
		/// Check if a domain can use EventStreams.
		/// </summary>
		/// <param name="domain">EventStreams domain.</param>
		public static bool CanBeUsed(string domain)
		{
			if (domain == null)
			{
				return false;
			}

			return WMProjects.Any(domain.EndsWith);
		}
	}
}
