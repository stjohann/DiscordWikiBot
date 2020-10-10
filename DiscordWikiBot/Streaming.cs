using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using DSharpPlus;
using DSharpPlus.Entities;
using DSharpPlus.CommandsNext;
using DSharpPlus.CommandsNext.Attributes;
using System.Text.RegularExpressions;
using Newtonsoft.Json.Linq;

namespace DiscordWikiBot
{
	/// <summary>
	/// Streaming class.
	/// <para>Adds commands for configuring the recent changes streams.</para>
	/// </summary>
	[RequireUserPermissions(Permissions.ManageGuild)]
	class Streaming : BaseCommandModule
	{
		/// <summary>
		/// Open a new stream in a specified channel.
		/// </summary>
		/// <param name="ctx">Discord information.</param>
		/// <param name="channel">Discord channel.</param>
		/// <param name="args">Stream parameters.</param>
		[Command("openStream")]
		[Description("streaming-help-open")]
		public async Task OpenStream(CommandContext ctx,
			[Description("streaming-help-channel")] DiscordChannel channel,
			[RemainingText, Description("streaming-help-args")] string args = "")
		{
			await CommandChecks(ctx, channel, args, async(arguments, lang) =>
			{
				Dictionary<string, dynamic> temp = EventStreams.SetData(channel.Id.ToString(), arguments);
				string type = (arguments.ContainsKey("title") ? "title" : "namespace");
				string goal = GetGoalMessage(lang, type, arguments[type].ToString());

				string desc = ListArguments(temp, lang);
				desc = (desc.Length > 0 ? $":\n{desc}" : ".");
				await ctx.RespondAsync(Locale.GetMessage("streaming-opened", lang, goal, channel.Mention, desc));
			});
		}

		/// <summary>
		/// Edit stream parameters in a specified channel.
		/// </summary>
		/// <param name="ctx">Discord information.</param>
		/// <param name="channel">Discord channel.</param>
		/// <param name="args">Stream parameters.</param>
		[Command("editStream")]
		[Description("streaming-help-edit")]
		public async Task EditStream(CommandContext ctx,
			[Description("streaming-help-channel")] DiscordChannel channel,
			[RemainingText, Description("streaming-help-args")] string args = "")
		{
			await CommandChecks(ctx, channel, args, async(arguments, lang) =>
			{
				Dictionary<string, dynamic> temp = EventStreams.SetData(channel.Id.ToString(), arguments, false);
				string type = (arguments.ContainsKey("title") ? "title" : "namespace");
				string goal = GetGoalMessage(lang, type, arguments[type].ToString());

				// Return a specific message if nothing was changed
				if (temp.Count == 0)
				{
					await ctx.RespondAsync(Locale.GetMessage("streaming-edited-nothing", lang, goal, channel.Mention));
					return;
				}

				string desc = ListArguments(temp, lang);
				await ctx.RespondAsync(Locale.GetMessage("streaming-edited", lang, goal, channel.Mention, desc));
			});
		}

		/// <summary>
		/// Close a stream in a specified channel.
		/// </summary>
		/// <param name="ctx">Discord information.</param>
		/// <param name="channel">Discord channel.</param>
		/// <param name="args">Stream parameters.</param>
		[Command("closeStream")]
		[Description("streaming-help-close")]
		public async Task CloseStream(CommandContext ctx,
			[Description("streaming-help-channel")] DiscordChannel channel,
			[RemainingText, Description("streaming-help-args")] string args = "")
		{
			await CommandChecks(ctx, channel, args, async(arguments, lang) =>
			{
				EventStreams.RemoveData(channel.Id.ToString(), arguments);
				string type = (arguments.ContainsKey("title") ? "title" : "namespace");
				string goal = GetGoalMessage(lang, type, arguments[type].ToString());

				await ctx.RespondAsync(Locale.GetMessage("streaming-closed", lang, goal, channel.Mention));
			});
		}

		/// <summary>
		/// List all streams on a server.
		/// </summary>
		/// <param name="ctx">Discord information.</param>
		[Command("listStreams")]
		[Description("streaming-help-list")]
		public async Task ListStreams(CommandContext ctx)
		{
			string lang = Config.GetLang(ctx.Guild.Id.ToString());
			await ctx.TriggerTypingAsync();

			IReadOnlyList<DiscordChannel> channelList = await ctx.Guild.GetChannelsAsync();
			string[] list = channelList.Cast<DiscordChannel>().Select(x => x.Id.ToString()).ToArray();
			JObject result = EventStreams.GetData(list);

			// Send a ping if there are no results
			if (result.Count == 0)
			{
				await ctx.RespondAsync(Locale.GetMessage("streaming-list-nothing", lang));
				return;
			}

			// Compile a list of streams
			List<string> msg = new List<string>();
			foreach (KeyValuePair<string, JToken> entry in result)
			{
				string output = "";
				string goal = entry.Key.Trim('<', '>');
				string goalMsg = GetGoalMessage(lang, (goal == entry.Key ? "title" : "namespace"), goal);
				output += Locale.GetMessage("streaming-list-stream", lang, goalMsg, entry.Value.Count()) + "\n";

				// List each stream with an editing command
				foreach (KeyValuePair<string, JToken> item in (JObject)entry.Value)
				{
					ulong id = ulong.Parse(item.Key);
					DiscordChannel channel = ctx.Guild.GetChannel(id);
					Dictionary<string, dynamic> args = ((JObject)item.Value).ToObject<Dictionary<string, dynamic>>();

					// Combine everything
					string editGoal = (goal == entry.Key ? $" --title {goal}" : $" --namespace {goal}");
					string argsMsg = ListArguments(args, lang);
					argsMsg = (argsMsg.Length > 0 ? $":\n{argsMsg}" : "");

					output += $"{channel.Mention}{argsMsg}\n`!editStream #{channel.Name}{editGoal}`";
				}

				msg.Add(output);
			}

			string response = Locale.GetMessage("streaming-list", lang, result.Count, string.Join("\n", msg));
			if (response.Length <= 2000)
			{
				await ctx.RespondAsync(response);
			} else
			{
				// Split long lists of streams into multiple messages
				response = Locale.GetMessage("streaming-list", lang, result.Count, "");
				foreach (var stream in msg)
				{
					string output = stream + "\n";
					if (response.Length + output.Length <= 2000)
					{
						response += output;
					} else
					{
						await ctx.RespondAsync(response);
						response = "";
					}
				}

				if (response.Length > 0)
				{
					await ctx.RespondAsync(response);
				}
			}
		}

		/// <summary>
		/// Common checks for streaming commands.
		/// </summary>
		/// <param name="ctx">Discord information.</param>
		/// <param name="channel">Discord channel.</param>
		/// <param name="args">Stream arguments.</param>
		/// <param name="callback">Method to execute after all checks.</param>
		private async Task CommandChecks(CommandContext ctx, DiscordChannel channel, string args, Action<Dictionary<string, dynamic>, string> callback)
		{
			string lang = Config.GetLang(ctx.Guild.Id.ToString());
			Dictionary<string, dynamic> arguments = ParseArguments(args);
			await ctx.TriggerTypingAsync();

			// Check for the goal
			if (
				args == ""
				|| !(arguments.ContainsKey("title") || arguments.ContainsKey("namespace"))
				|| arguments.ContainsKey("title") && Linking.IsInvalid(arguments["title"])
			)
			{
				await ctx.RespondAsync(Locale.GetMessage("streaming-required-goal", lang, ctx.Command.Name, Config.GetValue("prefix")));
				return;
			}

			callback(arguments, lang);
		}
		
		/// <summary>
		/// Parse a string with stream parameters.
		/// </summary>
		/// <param name="args">String with stream parameters.</param>
		/// <returns>A dictionary with stream parameters.</returns>
		private static Dictionary<string, dynamic> ParseArguments(string args = "")
		{
			if (args == "")
			{
				return new Dictionary<string, dynamic>();
			}

			string pattern = @"-{2}(\S+)(?:[=:]?|\s+)([^-\s].*?)?(?=\s+[-\/]{2}\S+|$)";
			MatchCollection matches = Regex.Matches(args, pattern);
			Dictionary<string, dynamic> result = matches.Cast<Match>().ToDictionary(m => m.Groups[1].Value, (m) => {
				dynamic value = m.Groups[2].Value;

				// Normalise to boolean
				if (Boolean.TryParse(value, out bool outBool))
				{
					return outBool;
				}

				// Normalise to integer
				if (Int32.TryParse(value, out int outInt))
				{
					return outInt;
				}

				return value;
			});

			// Remove anchor from page title
			if (result.ContainsKey("title"))
			{
				string[] anchor = ((string)result["title"]).Split('#');
				if (anchor.Length > 1)
				{
					result["title"] = anchor[0];
				}
			}

			// Assume default values if they are undefined
			Dictionary<string, dynamic> defaults = new Dictionary<string, dynamic>
			{
				{ "bot", false },
				{ "minor", true },
				{ "type", "any" },
				{ "patrolled", "any" },
			};

			foreach (KeyValuePair<string, dynamic> item in defaults)
			{
				if (result.ContainsKey(item.Key))
				{
					if (item.Value is bool && result[item.Key] is bool)
					{
						continue;
					}

					if ((result[item.Key] is string) && result[item.Key] == "")
					{
						result[item.Key] = item.Value;
					}
				}
			}

			// Filter out invalid values
			Dictionary<string, string[]> validValues = new Dictionary<string, string[]>
			{
				{
					"patrolled",
					new string[] { "any", "none", "only" }
				},
				{
					"type",
					new string[] { "any", "edit", "new" }
				}
			};

			foreach (KeyValuePair<string, string[]> item in validValues)
			{
				if (result.ContainsKey(item.Key))
				{
					if (!(result[item.Key] is string))
					{
						result.Remove(item.Key);
						continue;
					}

					if (!item.Value.Contains((string)result[item.Key]))
					{
						result.Remove(item.Key);
					}
				}
			}

			// Check for integer values
			if (result.ContainsKey("namespace"))
			{
				if (!(result["namespace"] is int))
				{
					result.Remove("namespace");
				}
			}

			if (result.ContainsKey("diff-length"))
			{
				if (!(result["diff-length"] is int))
				{
					result.Remove("diff-length");
				}
			}

			// Transform comment text to lowercase
			if (result.ContainsKey("in-comment"))
			{
				result["in-comment"] = result["in-comment"].ToLower();
			}
			
			return result;
		}

		/// <summary>
		/// Format a localised message with goal.
		/// </summary>
		/// <param name="lang">Language code in ISO 639 format.</param>
		/// <param name="type">Type of the goal.</param>
		/// <param name="goal">Streaming goal.</param>
		/// <returns></returns>
		private static string GetGoalMessage(string lang, string type, string goal)
		{
			// * streaming-stream-namespace
			// * streaming-stream-title
			return Locale.GetMessage("streaming-stream-" + type, lang, goal.ToString());
		}

		/// <summary>
		/// Format specified stream parameters in human-readable form.
		/// </summary>
		/// <param name="args">Stream parameters.</param>
		/// <param name="lang">Language code in ISO 639 format.</param>
		/// <returns>Information about stream parameters.</returns>
		private static string ListArguments(Dictionary<string, dynamic> args, string lang)
		{
			if (args == null) return "";

			return string.Join("\n", args.Select(x =>
			{
				string value = x.Value.ToString();
				if (x.Value is bool)
				{
					value = Locale.GetMessage((x.Value ? "yes" : "no"), lang);
				}

				return Locale.GetMessage("bullet", lang, Locale.GetMessage($"streaming-key-{x.Key}", lang) + Locale.GetMessage("separator", lang, $"`{value}`"));
			}));
		}
	}
}
