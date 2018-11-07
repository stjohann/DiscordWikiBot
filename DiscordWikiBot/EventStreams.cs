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
	class EventStreams
	{
		// EventSource stream instance
		public static Provider Stream;

		// EventSource stream translations
		public static dynamic Data;

		public static void Init()
		{
			// Get JSON
			Program.Client.DebugLogger.LogMessage(LogLevel.Info, "EventStreams", $"Reading JSON config", DateTime.Now);
			string json = "";
			string jsonPath = @"eventStreams.json";
			if (!File.Exists(jsonPath))
			{
				Program.Client.DebugLogger.LogMessage(LogLevel.Error, "EventStreams", "Please create a JSON file called \"eventStreams.json\" before trying to use EventStreams.", DateTime.Now);
				return;
			}
			json = File.ReadAllText(jsonPath);

			Data = JsonConvert.DeserializeObject(json);

			// Open new EventStreams instance
			Program.Client.DebugLogger.LogMessage(LogLevel.Info, "EventStreams", $"Connecting to huggle-rc.wmflabs.org", DateTime.Now);
			Stream = new Provider(true, true);
			Stream.Subscribe(Config.GetDomain());

			// Respond when server is ready
			Stream.On_OK += new Provider.OKHandler((o, e) =>
			{
				Program.Client.DebugLogger.LogMessage(LogLevel.Info, "EventStreams", $"Ready!", DateTime.Now);
			});

			// Log any exceptions
			Stream.On_Exception += new Provider.ExceptionHandler(async (o, e) =>
			{
				Program.Client.DebugLogger.LogMessage(LogLevel.Info, "EventStreams", $"Stream returned the following exception: {e.Exception}", DateTime.Now);
			});

			// Start recording events
			Stream.On_Change += new Provider.EditHandler((o, e) =>
			{
				bool isEdit = (e.Change.Type == RecentChange.ChangeType.Edit || e.Change.Type == RecentChange.ChangeType.New);

				if (e.Change.Bot == false && isEdit)
				{
					string ns = e.Change.Namespace.ToString();
					string title = e.Change.Title;

					// React if there is server data for namespace
					if (Data[$"<{ns}>"] != null)
					{
						React(Data[$"<{ns}>"], e.Change).Wait();
					}

					// React if there is server data for title
					if (Data[title] != null)
					{
						React(Data[title], e.Change).Wait();
					}
				}
			});

			Stream.Connect();
		}

		public static void Subscribe(string domain)
		{
			if (domain == null || domain == Config.GetDomain()) return;
			Stream.Subscribe(domain);
		}

		public static void Unsubscribe(string domain = null)
		{
			if (domain == null || domain == Config.GetDomain()) return;
			Stream.Unsubscribe(domain);
		}

		public static async Task React(JArray data, RecentChange change)
		{
			DiscordClient client = Program.Client;
			for (int i = 0; i < data.Count; i++)
			{
				// Setup basic info
				string[] info = data[i].ToString().Split('|');
				ulong channelId = ulong.Parse(info[0]);
				DiscordChannel channel = await client.GetChannelAsync(channelId);
				int minLength = (info.Length > 1 ? Convert.ToInt32(info[1]) : -1);

				// Check if domain is the same
				string domain = Config.GetDomain();
				if (channel != null)
				{
					domain = domain = Config.GetDomain(channel.Guild.Id.ToString());
					if (domain != change.ServerName)
					{
						continue;
					}
				}

				// Set up domain for future usage for link formatting
				domain = $"https://{domain}/wiki/$1";
				
				// Check if the diff is above required length if it is set
				if (minLength > -1)
				{
					int revLength = (change.LengthNew - change.LengthOld);
					bool isMinLength = (revLength > minLength);
					if (isMinLength == false)
					{
						continue;
					}
				}

				// Send the message
				string lang = Config.GetLang(channel.Guild.Id.ToString());
				await client.SendMessageAsync(channel, embed: GetEmbed(change, domain, lang));
			}
		}
		
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
					Linking.GetLink(change.Title, format, true),
					string.Format("https://upload.wikimedia.org/wikipedia/commons/thumb/{0}", embedIcon)
				)
				.WithColor(embedColor)
				.WithDescription(GetMessage(change, format, lang));

			return embed;
		}

		public static string GetMessage(RecentChange change, string format, string lang)
		{
			string linkPattern = "\\[{2}([^\\[\\]\\|\n]+)\\]{2}";
			string linkPatternPipe = "\\[{2}([^\\[\\]\\|\n]+)\\|";

			// Parse length of the diff
			string strLength = "";
			int length = (change.LengthNew - change.LengthOld);
			strLength = length.ToString();
			if (length > 0)
			{
				strLength = "+" + strLength;
			}
			strLength = $"({strLength})";

			if (length > 500 || length < -500)
			{
				strLength = $"**{strLength}**";
			}

			// Parse edit comment
			string comment = "";
			if (change.Summary != "")
			{
				// Transform code for section to simpler version
				comment = change.Summary.ToString().Replace("/* ", "→");
				comment = Regex.Replace(comment, " \\*/$", string.Empty).Replace(" */", ":");

				// Remove links
				comment = Regex.Replace(comment, linkPattern, "$1");
				comment = Regex.Replace(comment, linkPatternPipe, string.Empty).Replace("]]", string.Empty);

				// Add italic and parentheses
				comment = $" *({comment})*";
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

			IPAddress address;
			if (IPAddress.TryParse(change.User, out address))
			{
				user = $"[{change.User}]({contribs}) ({talk})";
			} else
			{
				contribs = string.Format("[{0}]({1})", Locale.GetMessage("eventstreams-contribs", lang), contribs);
				user = $"[{change.User}]({user}) ({talk} | {contribs})";
			}

			string msg = $"{link} . . {strLength} . . {user}{comment}";
			return msg;
		}

		public static void SetData(string goal, string channel, string minLength)
		{
			if (Data == null) return;
			Program.Client.DebugLogger.LogMessage(LogLevel.Info, "EventStreams", $"Changing JSON config after a command was fired", DateTime.Now);
			
			// Change current data
			string str = string.Format("{0}{1}", channel, (minLength != "" ? $"|{minLength}" : ""));
			if (Data[goal] == null)
			{
				Data[goal] = new JArray();
			} else
			{
				var el = ((IEnumerable<dynamic>)Data[goal]).FirstOrDefault(j => j.ToString() == str);
				if (el != null) return;
			}
			Data[goal].Add(str);

			// Write it to JSON file
			string jsonPath = @"eventStreams.json";
			File.WriteAllText(jsonPath, Data.ToString());
		}

		public static void RemoveData(string goal, string channel, string minLength) {
			if (Data == null) return;
			Program.Client.DebugLogger.LogMessage(LogLevel.Info, "EventStreams", $"Changing JSON config after the command was fired", DateTime.Now);

			// Change current data and remove an item if necessary
			if (Data[goal] == null) return;
			string str = string.Format("{0}{1}", channel, (minLength != "" ? $"|{minLength}" : ""));
			var el = ((IEnumerable<dynamic>)Data[goal]).FirstOrDefault(j => j.ToString() == str);
			if (el != null)
			{
				Data[goal].Remove(el);
			}

			if(Data[goal].Count == 0)
			{
				Data.Property(goal).Remove();
			}

			// Write it to JSON file
			string jsonPath = @"eventStreams.json";
			File.WriteAllText(jsonPath, Data.ToString());
		}
	}
}
