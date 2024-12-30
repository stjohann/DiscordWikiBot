using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using DSharpPlus;
using DSharpPlus.CommandsNext;
using DSharpPlus.CommandsNext.Attributes;
using DSharpPlus.Entities;

namespace DiscordWikiBot
{
	/// <summary>
	/// Configuring class.
	/// <para>Adds commands for overriding bot settings per server.</para>
	/// </summary>
	[RequireModOrOwner]
	class Configuring : BaseCommandModule
	{
		/// <summary>
		/// Set language of the bot for a Discord server.
		/// </summary>
		/// <param name="ctx">Discord information.</param>
		/// <param name="value">Language code in ISO 639 format.</param>
		[Command("serverAnswerBots")]
		[Aliases("guildAnswerBots")]
		[Description("configuring-help-answerBots")]
		public async Task SetAnswerBots(CommandContext ctx,
			[Description("configuring-help-answerBots-value")] string value)
		{
			var lang = Config.GetLang(ctx.Guild);
			object overrideVal = Config.ParseBool(value, lang);

			// Check for required parameter
			bool isEmpty = await RespondIfEmpty(ctx, lang, value);
			if (isEmpty) return;

			// Do action and respond
			int succeeds = await Config.SetOverride(ctx.Guild, "answerBots", overrideVal);
			if (succeeds == Config.RESULT_CHANGE || succeeds == Config.RESULT_RESET)
			{
				await ctx.RespondAsync(Locale.GetMessage("configuring-changed-answerBots", lang, overrideVal));
				return;
			}
			await RespondOnErrors(succeeds, ctx, lang);
		}

		/// <summary>
		/// Set EventStreams domain for a Discord server.
		/// </summary>
		/// <param name="ctx">Discord information.</param>
		/// <param name="value">New EventStreams domain.</param>
		[Command("serverDomain")]
		[Aliases("guildDomain")]
		[Description("configuring-help-domain")]
		public async Task SetDomain(CommandContext ctx,
			[Description("configuring-help-domain-value"), RemainingText] string value)
		{
			string lang = Config.GetLang(ctx.Guild);

			// Check for required parameter
			bool isEmpty = await RespondIfEmpty(ctx, lang, value);
			if (isEmpty) return;

			// Check if value matches Wikimedia project
			string[] projectList = null;
			bool notWmProject = value != "-" && !EventStreams.CanBeUsed(value, out projectList);
			if (notWmProject)
			{
				await ctx.RespondAsync(Locale.GetMessage("configuring-badvalue-domain", lang, "`" + string.Join("`, `", projectList) + "`"));
				return;
			}

			// Do action and respond
			int succeeds = await Config.SetOverride(ctx.Guild, "domain", value);
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
		[Command("serverLang")]
		[Aliases("guildLang")]
		[Description("configuring-help-lang")]
		public async Task SetLanguage(CommandContext ctx,
			[Description("configuring-help-lang-value")] string value)
		{
			string lang = Config.GetLang(ctx.Guild);

			// Check for required parameter
			bool isEmpty = await RespondIfEmpty(ctx, lang, value);
			if (isEmpty) return;

			// Set language to lowercase
			value = value.ToLower();

			// Check if it is a valid language
			if (value != "-" && !Locale.IsValidLanguage(value))
			{
				await ctx.RespondAsync(Locale.GetMessage("configuring-badvalue-lang", lang));
				return;
			}

			// Use new language in this command only
			lang = value == "-" ? Config.GetLang() : value;

			// Do action and respond
			int succeeds = await Config.SetOverride(ctx.Guild, "lang", value);
			if (succeeds == Config.RESULT_CHANGE)
			{
				await ctx.RespondAsync(Locale.GetMessage("configuring-changed-lang", lang, GetLanguageInfo(lang)));
			}
			await RespondOnErrors(succeeds, ctx, lang);
		}

		/// <summary>
		/// Set TranslateWiki notifications channel for a Discord server.
		/// </summary>
		/// <param name="ctx">Discord information.</param>
		/// <param name="channel">Discord channel.</param>
		/// <param name="value">Language code in ISO 639 format.</param>
		[Command("serverTW")]
		[Aliases("guildTW")]
		[Description("configuring-help-translatewiki")]
		public async Task SetTranslateWiki(CommandContext ctx,
			[Description("configuring-help-translatewiki-channel")] DiscordChannel channel,
			[Description("configuring-help-translatewiki-value"), RemainingText] string value)
		{
			string chanId = channel.Id.ToString();
			string chanPrevId = Config.GetTWChannel(ctx.Guild);
			string chanPrevLang = Config.GetTWLang(ctx.Guild);
			string lang = Config.GetLang(ctx.Guild);

			// Check for return to default
			if (value == "-")
			{
				chanId = "-";
				value = "-";
			}

			// Check for required parameter
			bool isEmpty = await RespondIfEmpty(ctx, lang, value);
			if (isEmpty) return;

			// Set language to lowercase
			value = value.ToLower();

			// Check if it is a valid language
			if (value != "-" && !Locale.IsValidLanguage(value))
			{
				await ctx.RespondAsync(Locale.GetMessage("configuring-badvalue-lang", lang));
				return;
			}

			// Do actions and respond
			int succeedsChan = await Config.SetOverride(ctx.Guild, "translatewiki-channel", chanId);
			int succeedsLang = await Config.SetOverride(ctx.Guild, "translatewiki-lang", value);

			// Remove both keys if language was reset
			if (succeedsLang == Config.RESULT_RESET)
			{
				await ctx.RespondAsync(Locale.GetMessage("configuring-changed-reset", lang));
				TranslateWiki.Remove(channel.Id.ToString(), chanPrevLang);
				return;
			}

			// Language was changed
			if (succeedsLang == Config.RESULT_CHANGE)
			{
				// Check if language is also different
				if (succeedsChan == Config.RESULT_CHANGE)
				{
					// Different channel, default language
					await ctx.RespondAsync(Locale.GetMessage("configuring-changed-translatewiki", lang, channel.Mention, GetLanguageInfo(value)));
					TranslateWiki.Remove(chanId, chanPrevLang);
				}
				else
				{
					await ctx.RespondAsync(Locale.GetMessage("configuring-changed-translatewiki", lang, channel.Mention));
					TranslateWiki.Remove(chanId, chanPrevLang);
				}

				TranslateWiki.Init(chanId, value);
				return;
			}

			// Channel was changed but language is same
			if (succeedsChan == Config.RESULT_CHANGE)
			{
				await ctx.RespondAsync(Locale.GetMessage("configuring-changed-translatewiki-channel", lang, channel.Mention));
				TranslateWiki.Remove(chanPrevId, chanPrevLang);
				TranslateWiki.Init(chanId, value);
				return;
			}

			// Other strange errors
			await ctx.RespondAsync(Locale.GetMessage("configuring-error-strange", lang));
		}

		/// <summary>
		/// Set default wiki link URL for a Discord server.
		/// </summary>
		/// <param name="ctx">Discord information.</param>
		/// <param name="value">Wiki link URL.</param>
		[Command("serverWiki")]
		[Aliases("guildWiki")]
		[Description("configuring-help-wiki")]
		public async Task SetWiki(CommandContext ctx,
			[Description("configuring-help-wiki-value"), RemainingText] string value)
		{
			string lang = Config.GetLang(ctx.Guild);

			// Check for required parameter
			bool isEmpty = await RespondIfEmpty(ctx, lang, value);
			if (isEmpty) return;

			if (value != "-" && !value.Contains("/wiki/$1"))
			{
				await ctx.RespondAsync(Locale.GetMessage("configuring-badvalue-wiki", lang, "/wiki/$1"));
				return;
			}

			// Provide some changes
			value = value.Trim('<', '>');

			// Check if a wiki was passed
			if (value != "-")
			{
				var data = Linking.GetWikiSite(value).Result;
				if (data == null)
				{
					await ctx.RespondAsync(Locale.GetMessage("configuring-badvalue-wiki", lang, "/wiki/$1"));
					return;
				}
			}

			// Do action and respond
			int succeeds = await Config.SetOverride(ctx.Guild, "wiki", value);
			if (succeeds == Config.RESULT_CHANGE)
			{
				await Linking.Init(ctx.Guild);
				await ctx.RespondAsync(Locale.GetMessage("configuring-changed-wiki", lang, value));
			}
			if (succeeds == Config.RESULT_RESET)
			{
				Linking.Remove(ctx.Guild.Id.ToString());
			}
			await RespondOnErrors(succeeds, ctx, lang);
		}

		/// <summary>
		/// Set default wiki link URL for a Discord channel.
		/// </summary>
		/// <param name="ctx">Discord information.</param>
		/// <param name="value">Wiki link URL.</param>
		[Command("channelWiki")]
		[Description("configuring-help-wiki-channel")]
		public async Task SetChannelWiki(CommandContext ctx,
			[Description("configuring-help-wiki-value"), RemainingText] string value)
		{
			string lang = Config.GetLang(ctx.Guild);

			// Check for required parameter
			bool isEmpty = await RespondIfEmpty(ctx, lang, value);
			if (isEmpty) return;

			if (value != "-" && !value.Contains("/wiki/$1"))
			{
				await ctx.RespondAsync(Locale.GetMessage("configuring-badvalue-wiki", lang, "/wiki/$1"));
				return;
			}

			// Provide some changes
			value = value.Trim('<', '>');

			// Reset to default server value if necessary
			if (value == Config.GetWiki(ctx.Guild))
			{
				value = "-";
			}

			// Check if a wiki was passed
			if (value != "-")
			{
				var data = Linking.GetWikiSite(value).Result;
				if (data == null)
				{
					await ctx.RespondAsync(Locale.GetMessage("configuring-badvalue-wiki", lang, "/wiki/$1"));
					return;
				}
			}

			// Do action and respond
			int succeeds = await Config.SetOverride(ctx.Channel, "wiki", value);
			if (succeeds == Config.RESULT_CHANGE)
			{
				await Linking.Init(ctx.Channel);
				await ctx.RespondAsync(Locale.GetMessage("configuring-changed-wiki-channel", lang, value));
			}
			if (succeeds == Config.RESULT_RESET)
			{
				Linking.Remove($"#{ctx.Channel.Id}");
			}
			await RespondOnErrors(succeeds, ctx, lang);
		}

		/// <summary>
		/// Respond with a rejection message if a value is empty.
		/// </summary>
		/// <param name="ctx">Command context.</param>
		/// <param name="lang">Language code in ISO 639 format.</param>
		/// <param name="value">String to be checked.</param>
		private async Task<bool> RespondIfEmpty(CommandContext ctx, string lang, string value = "")
		{
			if (value == null || value == "")
			{
				await ctx.RespondAsync(Locale.GetMessage("configuring-required-value", lang, ctx.Command.Name, Program.CommandPrefix));
				return true;
			}

			await ctx.TriggerTypingAsync();
			return false;
		}

		/// <summary>
		/// Common responses to error response codes.
		/// </summary>
		/// <param name="response">Response code from configuration function.</param>
		/// <param name="ctx">Command context.</param>
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

		/// <summary>
		/// Get language code and name in native language, if possible.
		/// </summary>
		/// <param name="code">MediaWiki-compatible language code.</param>
		private static string GetLanguageInfo(string code)
		{
			return Locale.GetLanguageName(code, "{1} ({0})");
		}
	}

	/// <summary>
	/// Check if command is run by bot owner or user with ManageGuild permissions.
	/// </summary>
	[AttributeUsage(AttributeTargets.Method | AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
	class RequireModOrOwnerAttribute : CheckBaseAttribute
	{
		public override Task<bool> ExecuteCheckAsync(CommandContext ctx, bool help)
		{
			DiscordApplication app = ctx.Client.CurrentApplication;

			var result = false;
			if (app != null)
			{
				// Ignore DMs or weird stuff
				DiscordMember usr = ctx.Member;
				if (ctx.Guild == null || usr == null) return Task.FromResult(result);

				// Always allow bot/server owners
				result = app.Owners.Any(x => x.Id == ctx.User.Id);
				if (result) return Task.FromResult(result);

				result = usr.Id == ctx.Guild.OwnerId;
				if (result) return Task.FromResult(result);

				// Allow people with mod permissions
				Permissions pusr = ctx.Channel.PermissionsFor(usr);
				result = pusr.HasPermission(Permissions.ManageGuild);
			}

			return Task.FromResult(result);
		}
	}

	class Pinging : BaseCommandModule
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

			string lang = Config.GetLang(ctx.Guild);
			IReadOnlyList<DiscordChannel> channelList = await ctx.Guild.GetChannelsAsync();
			string[] list = channelList.Cast<DiscordChannel>().Select(x => x.Id.ToString()).ToArray();
			var streams = EventStreams.GetData(list);

			// Inform about version and streams if they exist on a server
			var appendedMsg = " " + Locale.GetMessage("help-version", lang, Program.Version);
			if (streams.Count > 0)
			{
				TimeSpan timestamp = DateTime.UtcNow - EventStreams.LatestTimestamp;
				appendedMsg = " " + Locale.GetMessage("configuring-status-streaming", lang, (int)timestamp.TotalMinutes, timestamp.Seconds);

				// Restart the stream if it is offline for five minutes
				if (timestamp.TotalMinutes > 5)
				{
					Program.LogMessage("EventStreams restart was requested from !status command.", "EventStreams");
					EventStreams.Init();
				}
			}

			// Refresh site info for the channel (or the server if default)
			await Linking.Init(ctx.Channel, true);

			// Respond to message
			await ctx.RespondAsync(Locale.GetMessage("configuring-status", lang) + appendedMsg);
		}
	}
}
