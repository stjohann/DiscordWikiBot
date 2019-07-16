using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using DSharpPlus;
using DSharpPlus.CommandsNext;
using DSharpPlus.CommandsNext.Attributes;
using DSharpPlus.Entities;
using Newtonsoft.Json.Linq;

namespace DiscordWikiBot
{
	/// <summary>
	/// Configuring class.
	/// <para>Adds commands for overriding bot settings per server.</para>
	/// </summary>
	[RequireUserPermissions(Permissions.ManageGuild)]
	class Configuring
	{
		/// <summary>
		/// Set EventStreams domain for a Discord server.
		/// </summary>
		/// <param name="ctx">Discord information.</param>
		/// <param name="value">New EventStreams domain.</param>
		[Command("guildDomain")]
		[Description("configuring-help-domain")]
		public async Task SetDomain(CommandContext ctx,
			[Description("configuring-help-domain-value"), RemainingText] string value)
		{
			string prevDomain = Config.GetDomain(ctx.Guild.Id.ToString());
			string lang = Config.GetLang(ctx.Guild.Id.ToString());
			await ctx.TriggerTypingAsync();

			// Check for required parameters
			if (value.ToString() == "")
			{
				await ctx.RespondAsync(Locale.GetMessage("configuring-required-value", lang, ctx.Command.Name, Config.GetValue("prefix")));
				return;
			}

			// Check if matches Wikimedia project
			string[] projectList = null;
			bool notWmProject = (value != "-" && !EventStreams.CanBeUsed(value, out projectList));
			if (notWmProject)
			{
				await ctx.RespondAsync(Locale.GetMessage("configuring-badvalue-domain", lang, "`" + string.Join("`, `", projectList) + "`"));
				return;
			}

			// Do action and respond
			int succeeds = Config.SetOverride(ctx.Guild.Id.ToString(), "domain", value);
			if (succeeds == Config.RESULT_CHANGE)
			{
				await ctx.RespondAsync(Locale.GetMessage("configuring-changed-domain", lang, value));
			}
			await RespondOnErrors(succeeds, ctx, lang);
		}

		/// <summary>
		/// Set language of the bot for a Discord server.
		/// </summary>
		/// <param name="ctx">Discord information.</param>
		/// <param name="value">Language code in ISO 639 format.</param>
		[Command("guildLang")]
		[Description("configuring-help-lang")]
		public async Task SetLanguage(CommandContext ctx,
			[Description("configuring-help-lang-value")] string value)
		{
			string lang = Config.GetLang(ctx.Guild.Id.ToString());
			await ctx.TriggerTypingAsync();

			// Check for required parameters
			if (value.ToString() == "")
			{
				await ctx.RespondAsync(Locale.GetMessage("configuring-required-value", lang, ctx.Command.Name, Config.GetValue("prefix")));
				return;
			}

			// Set language to lowercase
			value = value.ToLower();

			// Check if it is a valid language
			if (value != "-" && !IsValidLanguage(value))
			{
				await ctx.RespondAsync(Locale.GetMessage("configuring-badvalue-lang", lang));
				return;
			}

			// Use new language in this command only
			lang = (value == "-" ? Config.GetLang() : value);

			// Do action and respond
			int succeeds = Config.SetOverride(ctx.Guild.Id.ToString(), "lang", value);
			if (succeeds == Config.RESULT_CHANGE)
			{
				await ctx.RespondAsync(Locale.GetMessage("configuring-changed-lang", lang, value.ToUpper()));
			}
			await RespondOnErrors(succeeds, ctx, lang);
		}

		/// <summary>
		/// Set TranslateWiki notifications channel for a Discord server.
		/// </summary>
		/// <param name="ctx">Discord information.</param>
		/// <param name="channel">Discord channel.</param>
		/// <param name="value">Language code in ISO 639 format.</param>
		[Command("guildTW")]
		[Description("configuring-help-tw")]
		public async Task SetTranslate(CommandContext ctx,
			[Description("configuring-help-tw-channel")] DiscordChannel channel,
			[Description("configuring-help-tw-value"), RemainingText] string value)
		{
			string chanId = channel.Id.ToString();
			string chanPrevId = Config.GetTWChannel(ctx.Guild.Id.ToString());
			string chanPrevLang = Config.GetTWLang(ctx.Guild.Id.ToString());
			string lang = Config.GetLang(ctx.Guild.Id.ToString());
			await ctx.TriggerTypingAsync();

			// Check for return to default
			if (value == "-")
			{
				chanId = "-";
				value = "-";
			}

			// Check for required parameters
			if (value.ToString() == "")
			{
				await ctx.RespondAsync(Locale.GetMessage("configuring-required-value", lang, ctx.Command.Name, Config.GetValue("prefix")));
				return;
			}

			// Set language to lowercase
			value = value.ToLower();

			// Do action and respond
			int succeedsChan = Config.SetOverride(ctx.Guild.Id.ToString(), "translatewiki-channel", chanId);
			int succeedsLang = Config.SetOverride(ctx.Guild.Id.ToString(), "translatewiki-lang", value);

			if (succeedsChan == Config.RESULT_CHANGE && succeedsLang == Config.RESULT_CHANGE)
			{
				// Different channel and language
				await ctx.RespondAsync(Locale.GetMessage("configuring-changed-translatewiki", lang, channel.Mention, value.ToUpper()));
				TranslateWiki.Init(chanId, value);
			}

			if (succeedsChan == Config.RESULT_CHANGE && succeedsLang == Config.RESULT_RESET)
			{
				// Different channel, default language
				await ctx.RespondAsync(Locale.GetMessage("configuring-changed-translatewiki-channel", lang, channel.Mention));
				TranslateWiki.Remove(chanId, chanPrevLang);
				TranslateWiki.Init(chanId, value);
			}

			if (succeedsChan == Config.RESULT_CHANGE && succeedsLang == Config.RESULT_SAME)
			{
				// Different channel, same language
				await ctx.RespondAsync(Locale.GetMessage("configuring-changed-translatewiki-channel", lang, channel.Mention));
				TranslateWiki.Remove(chanPrevId, chanPrevLang);
				TranslateWiki.Init(chanId, value);
			}

			if ( succeedsChan == Config.RESULT_RESET && succeedsLang == Config.RESULT_CHANGE
				|| (
					succeedsChan == Config.RESULT_SAME
					&& (succeedsLang == Config.RESULT_RESET || succeedsLang == Config.RESULT_CHANGE)
				)
			)
			{
				// Same or default channel, different language
				await ctx.RespondAsync(Locale.GetMessage("configuring-changed-translatewiki-lang", lang, value.ToUpper()));
				TranslateWiki.Remove(chanId, chanPrevLang);
				TranslateWiki.Init(chanId, value);
			}

			if (succeedsChan == Config.RESULT_RESET && succeedsLang == Config.RESULT_RESET)
			{
				// Reset both channel and language with -
				await ctx.RespondAsync(Locale.GetMessage("configuring-changed-reset", lang));
				TranslateWiki.Remove(channel.Id.ToString(), chanPrevLang);
			}

			if (succeedsChan == Config.RESULT_STRANGE || succeedsLang == Config.RESULT_STRANGE)
			{
				// Other strange errors
				await ctx.RespondAsync(Locale.GetMessage("configuring-error-strange", lang));
			}
		}

		/// <summary>
		/// Set wiki link URL for a Discord server.
		/// </summary>
		/// <param name="ctx">Discord information.</param>
		/// <param name="value">Wiki link URL.</param>
		[Command("guildWiki")]
		[Description("configuring-help-wiki")]
		public async Task SetWiki(CommandContext ctx,
			[Description("configuring-help-wiki-value"), RemainingText] string value)
		{
			string lang = Config.GetLang(ctx.Guild.Id.ToString());
			await ctx.TriggerTypingAsync();

			// Check for required parameters
			if (value.ToString() == "")
			{
				await ctx.RespondAsync(Locale.GetMessage("configuring-required-value", lang, ctx.Command.Name, Config.GetValue("prefix")));
				return;
			}

			if (value != "-" && !value.Contains("/wiki/$1"))
			{
				await ctx.RespondAsync(Locale.GetMessage("configuring-badvalue-wiki", lang));
				return;
			}

			// Provide some changes
			value = value.Replace("<", String.Empty).Replace(">", String.Empty);

			// Do action and respond
			int succeeds = Config.SetOverride(ctx.Guild.Id.ToString(), "wiki", value);
			if (succeeds == Config.RESULT_CHANGE)
			{
				Linking.Init(ctx.Guild.Id.ToString());
				await ctx.RespondAsync(Locale.GetMessage("configuring-changed-wiki", lang, value));
			}
			if (succeeds == Config.RESULT_RESET)
			{
				Linking.Remove(ctx.Guild.Id.ToString());
			}
			await RespondOnErrors(succeeds, ctx, lang);
		}
		
		/// <summary>
		/// Common responses to error response codes.
		/// </summary>
		/// <param name="response">Response code from configuration function.</param>
		/// <param name="ctx">Discord information.</param>
		/// <param name="lang">Language code in ISO 639 format.</param>
		private async Task RespondOnErrors(int response, CommandContext ctx, string lang)
		{
			if (response == Config.RESULT_RESET)
			{
				await ctx.RespondAsync(Locale.GetMessage("configuring-changed-reset", lang));
			}

			if (response == Config.RESULT_SAME)
			{
				await ctx.RespondAsync(Locale.GetMessage("configuring-error-same", lang));
			}

			if (response == Config.RESULT_STRANGE)
			{
				await ctx.RespondAsync(Locale.GetMessage("configuring-error-strange", lang));
			}
		}

		private bool IsValidLanguage(string name)
		{
			return CultureInfo
				.GetCultures(CultureTypes.NeutralCultures)
				.Any(c => c.Name == name);
		}
	}
	
	class Pinging
	{
		/// <summary>
		/// Check if the bot’s functions are operational.
		/// </summary>
		/// <param name="ctx">Discord information.</param>
		[Command("status")]
		[Aliases("ping", "pong")]
		[Description("configuring-help-status")]
		public async Task Status(CommandContext ctx)
		{
			await ctx.TriggerTypingAsync();

			string lang = Config.GetLang(ctx.Guild.Id.ToString());
			IReadOnlyList<DiscordChannel> channelList = await ctx.Guild.GetChannelsAsync();
			string[] list = channelList.Cast<DiscordChannel>().Select(x => x.Id.ToString()).ToArray();
			JObject streams = EventStreams.GetData(list);

			// Inform about streams if they exist on a server
			string streamingMsg = "";
			if (streams.Count > 0)
			{
				TimeSpan timestamp = DateTime.UtcNow - EventStreams.LatestTimestamp;
				streamingMsg = " " + Locale.GetMessage("configuring-status-streaming", lang, (int)timestamp.TotalMinutes, timestamp.Seconds);
			}

			// Respond to message
			await ctx.RespondAsync(Locale.GetMessage("configuring-status", lang) + streamingMsg);
		}
	}
}
