﻿using System;
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
	class Configuring : BaseCommandModule
	{
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
			bool notWmProject = value != "-" && !EventStreams.CanBeUsed(value, out projectList);
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
		[Command("serverLang")]
		[Aliases("guildLang")]
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
			if (value != "-" && !Locale.IsValidLanguage(value))
			{
				await ctx.RespondAsync(Locale.GetMessage("configuring-badvalue-lang", lang));
				return;
			}

			// Use new language in this command only
			lang = value == "-" ? Config.GetLang() : value;

			// Do action and respond
			int succeeds = Config.SetOverride(ctx.Guild.Id.ToString(), "lang", value);
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

			// Check if it is a valid language
			if (value != "-" && !Locale.IsValidLanguage(value))
			{
				await ctx.RespondAsync(Locale.GetMessage("configuring-badvalue-lang", lang));
				return;
			}

			// Do action and respond
			int succeedsChan = Config.SetOverride(ctx.Guild.Id.ToString(), "translatewiki-channel", chanId);
			int succeedsLang = Config.SetOverride(ctx.Guild.Id.ToString(), "translatewiki-lang", value);

			if (succeedsChan == Config.RESULT_CHANGE && succeedsLang == Config.RESULT_CHANGE)
			{
				// Different channel and language
				await ctx.RespondAsync(Locale.GetMessage("configuring-changed-translatewiki", lang, channel.Mention, GetLanguageInfo(value)));
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

			if (succeedsChan == Config.RESULT_RESET && succeedsLang == Config.RESULT_CHANGE
				|| (
					succeedsChan == Config.RESULT_SAME
					&& (succeedsLang == Config.RESULT_RESET || succeedsLang == Config.RESULT_CHANGE)
				)
			)
			{
				// Same or default channel, different language
				await ctx.RespondAsync(Locale.GetMessage("configuring-changed-translatewiki-lang", lang, GetLanguageInfo(value)));
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
			int succeeds = Config.SetOverride(ctx.Guild.Id.ToString(), "wiki", value);
			if (succeeds == Config.RESULT_CHANGE)
			{
				await Linking.Init(ctx.Guild.Id.ToString());
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
				await ctx.RespondAsync(Locale.GetMessage("configuring-badvalue-wiki", lang, "/wiki/$1"));
				return;
			}

			// Provide some changes
			value = value.Trim('<', '>');

			// Reset to default server value if necessary
			if (value == Config.GetWiki(ctx.Guild.Id.ToString()))
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
			int succeeds = Config.SetOverride($"#{ctx.Channel.Id}", "wiki", value);
			if (succeeds == Config.RESULT_CHANGE)
			{
				await Linking.Init($"#{ctx.Channel.Id}");
				await ctx.RespondAsync(Locale.GetMessage("configuring-changed-wiki-channel", lang, value));
			}
			if (succeeds == Config.RESULT_RESET)
			{
				Linking.Remove($"#{ctx.Channel.Id}");
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

		/// <summary>
		/// Get language code and name in native language, if possible.
		/// </summary>
		/// <param name="code">MediaWiki-compatible language code.</param>
		private static string GetLanguageInfo(string code)
		{
			return Locale.GetLanguageName(code, "{1} ({0})");
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

			string lang = Config.GetLang(ctx.Guild.Id.ToString());
			IReadOnlyList<DiscordChannel> channelList = await ctx.Guild.GetChannelsAsync();
			string[] list = channelList.Cast<DiscordChannel>().Select(x => x.Id.ToString()).ToArray();
			JObject streams = EventStreams.GetData(list);

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
			await Linking.InitChannel(ctx.Channel, true);

			// Respond to message
			await ctx.RespondAsync(Locale.GetMessage("configuring-status", lang) + appendedMsg);
		}
	}
}
