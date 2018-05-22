using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using DSharpPlus;
using DSharpPlus.Entities;
using DSharpPlus.CommandsNext;
using DSharpPlus.CommandsNext.Attributes;

namespace DiscordWikiBot
{
	class Streaming
	{
		[Command("openStream"), Description("streaming-help-open")]
		public async Task OpenStream(CommandContext ctx,
			[Description("streaming-help-channel")] DiscordChannel channel,
			[Description("streaming-help-goal")] string goal = "",
			[Description("streaming-help-minlength")] int minLength = -1)
		{
			string lang = Config.GetLang(ctx.Guild.Id.ToString());

			// Ensure that we are in private channel
			if (ctx.Channel.Name != "moderators") {
				await ctx.RespondAsync(Locale.GetMessage("denied", lang));
				return;
			};
			await ctx.TriggerTypingAsync();

			// Check for required parameters
			if (channel.ToString() == "")
			{
				await ctx.RespondAsync(Locale.GetMessage("streaming-required-channel", lang, "!help openStream"));
				return;
			}

			if (goal == "")
			{
				await ctx.RespondAsync(Locale.GetMessage("streaming-required-goal", lang, "!help openStream"));
				return;
			}

			// Format the goal
			goal = goal.Replace("_", " ").Replace("\\", "");

			// Change JSON config
			string len = (minLength != -1 ? minLength.ToString() : "");
			EventStreams.SetData(goal, channel.Id.ToString(), len);

			await ctx.RespondAsync(Locale.GetMessage("streaming-added", lang, goal, channel.Mention));
		}

		[Command("closeStream"), Description("streaming-help-close")]
		public async Task CloseStream(CommandContext ctx,
			[Description("streaming-help-channel")] DiscordChannel channel,
			[Description("streaming-help-goal")] string goal = "",
			[Description("streaming-help-minlength")] int minLength = -1)
		{
			string lang = Config.GetLang(ctx.Guild.Id.ToString());

			// Ensure that we are in private channel
			if (ctx.Channel.Name != "moderators") {
				await ctx.RespondAsync(Locale.GetMessage("denied", lang));
				return;
			};
			await ctx.TriggerTypingAsync();

			// Check for required parameters
			if (channel == null)
			{
				await ctx.RespondAsync(Locale.GetMessage("streaming-required-channel", lang, "!help closeStream"));
				return;
			}

			if (goal == "")
			{
				await ctx.RespondAsync(Locale.GetMessage("streaming-required-goal", lang, "!help closeStream"));
				return;
			}

			// Format the goal
			goal = goal.Replace("_", " ").Replace("\\", "");

			// Change JSON config
			string len = (minLength != -1 ? minLength.ToString() : "");
			EventStreams.RemoveData(goal, channel.Id.ToString(), len);

			await ctx.RespondAsync(Locale.GetMessage("streaming-removed", lang, goal, channel.Mention));
		}
	}
}
