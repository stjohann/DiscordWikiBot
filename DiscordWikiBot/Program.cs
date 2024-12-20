using System;
using System.IO;
using System.Net;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using DSharpPlus;
using DSharpPlus.CommandsNext;
using DSharpPlus.Exceptions;
using DSharpPlus.EventArgs;
using Microsoft.Extensions.Logging;
using WikiClientLibrary.Client;
using System.Text.RegularExpressions;

namespace DiscordWikiBot
{
	/// <summary>
	/// Main class of the bot.
	/// <para>Provides functions for bot’s initialisation.</para>
	/// </summary>
	public class Program
	{
		/// <summary>
		/// An instance of Discord client.
		/// </summary>
		public static DiscordClient Client;

		/// <summary>
		/// An instance of MediaWiki client with necessary useragent.
		/// </summary>
		public static WikiClient WikiClient = new WikiClient
		{
			ClientUserAgent = UserAgent,
		};

		/// <summary>
		/// DiscordWikiBot version.
		/// </summary>
		public static string Version;

		/// <summary>
		/// DiscordWikiBot user agent.
		/// Please specify your own when modifying DiscordWikiBot internals (not including configs).
		/// <para>See https://foundation.wikimedia.org/wiki/Policy:Wikimedia_Foundation_User-Agent_Policy </para>
		/// </summary>
		public static string UserAgent;

		/// <summary>
		/// Default command prefix.
		/// </summary>
		public static string CommandPrefix;

		/// <summary>
		/// Available bot commands.
		/// </summary>
		private CommandsNextExtension Commands { get; set; }

		/// <summary>
		/// Discord developer token.
		/// </summary>
		private static string Token;

		static void Main(string[] args) => new Program().Run().GetAwaiter().GetResult();

		/// <summary>
		/// Initialise the bot and keep it running
		/// </summary>
		/// <param name="runAlways">Enable or disable Ctrl+C handler.</param>
		public async Task Run(bool runAlways = true)
		{
			// Set proper TLS settings
			ServicePointManager.SecurityProtocol |= SecurityProtocolType.Tls11 | SecurityProtocolType.Tls12;

			// Check for a token
			string tokenPath = @"token.txt";
			if (!File.Exists(tokenPath))
			{
				Console.WriteLine("Please create a file called \"token.txt\" before running the bot!");
				Console.WriteLine("[Press any key to exit...]");
				Console.ReadKey();
				Environment.Exit(0);
			}
			Token = File.ReadAllText(tokenPath, Encoding.Default);

			// Get JSON config file and its values
			Config.Init();
			Version = GetBotVersion();
			UserAgent = GetBotUserAgent();
			CommandPrefix = Config.GetValue("prefix").ToString();

			// Initialise Discord client
			Client = new DiscordClient(new DiscordConfiguration()
			{
				AutoReconnect = true,
				LargeThreshold = 250,
				MinimumLogLevel = LogLevel.Information,
				Token = Token,
				TokenType = TokenType.Bot,
				Intents = DiscordIntents.AllUnprivileged | DiscordIntents.MessageContents,
				LogUnknownEvents = false,
			});

			// Initialise events
			LogMessage($"Starting DiscordWikiBot, version {Version}");
			LogMessage($"UserAgent: {UserAgent}");

			// Get default locale
			await Locale.Load();
			Locale.Init();

			// Get site information and start linking bot
			LogMessage("Getting wiki site information");
			await Linking.Init();

			// Methods for linking bot
			Client.MessageCreated += (s, e) =>
			{
				Task.Run(async () =>
				{
					await Linking.Answer(s, e);
				});

				return Task.CompletedTask;
			};
			Client.MessageUpdated += (s, e) =>
			{
				Task.Run(async () =>
				{
					await Linking.Edit(s, e);
				});

				return Task.CompletedTask;
			};
			Client.MessageDeleted += Linking.Delete;
			Client.MessagesBulkDeleted += Linking.BulkDelete;

			// Start EventStreams
			if (Config.GetDomain() != null)
			{
				EventStreams.Init();
			}

			// Start Translatewiki fetches
			if (Config.GetTWChannel() != null && Config.GetTWLang() != null)
			{
				TranslateWiki.Init();
			}

			// Set some events for logging the information
			Client.Ready += Client_Ready;
			Client.GuildAvailable += Client_GuildAvailable;
			Client.GuildCreated += Client_GuildCreated;
			Client.GuildDeleted += Client_GuildDeleted;
			Client.ClientErrored += Client_ClientErrored;

			// Initialise commands
			LogMessage("Setting up commands");
			Commands = Client.UseCommandsNext(new CommandsNextConfiguration
			{
				StringPrefixes = [CommandPrefix],
				EnableDms = false,
				EnableMentionPrefix = true,
			});

			Commands.RegisterCommands<Pinging>();

			Commands.RegisterCommands<Configuring>();

			if (EventStreams.Enabled)
			{
				Commands.RegisterCommands<Streaming>();
			}

			// Set up custom formatter
			Commands.SetHelpFormatter<LocalisedHelpFormatter>();

			// Connect and start
			LogMessage("Connecting...");
			await Client.ConnectAsync();

			// Make sure not to close down automatically
			if (runAlways) await CtrlC();
		}

		/// <summary>
		/// Have the ability to stop the bot on Ctrl+C
		/// </summary>
		private static Task CtrlC()
		{
			var tcs = new TaskCompletionSource<object>();

			ConsoleCancelEventHandler handler = null;
			handler = (s, e) =>
			{
				tcs.TrySetResult(null);
				Console.CancelKeyPress -= handler;
				e.Cancel = true;
			};

			Console.CancelKeyPress += handler;
			return tcs.Task;
		}

		/// <summary>
		/// Initialise the common functions for every server.
		/// </summary>
		/// <param name="e">Discord event information.</param>
		private Task Client_GuildAvailable(DiscordClient sender, GuildCreateEventArgs e)
		{
			// Log the name of the guild that just became available
			LogMessage($"Server is loaded: {e.Guild.Name}");

			// Load custom values if needed
			Task.Run(async () =>
			{
				await Linking.Init(e.Guild);

				Locale.Init(Config.GetLang(e.Guild));

				if (Config.GetTWChannel(e.Guild) != null && Config.GetTWLang(e.Guild) != null)
				{
					TranslateWiki.Init(Config.GetTWChannel(e.Guild), Config.GetTWLang(e.Guild));
				}
			});

			return Task.CompletedTask;
		}

		/// <summary>
		/// Log message when the bot is added to new guilds.
		/// </summary>
		/// <param name="e">Discord event information.</param>
		private Task Client_GuildCreated(DiscordClient sender, GuildCreateEventArgs e)
		{
			LogMessage($"Bot was added to a server: {e.Guild.Name}");

			return Task.CompletedTask;
		}

		/// <summary>
		/// Log message when the bot is removed from guilds.
		/// </summary>
		/// <param name="e">Discord event information.</param>
		private Task Client_GuildDeleted(DiscordClient sender, GuildDeleteEventArgs e)
		{
			LogMessage($"Bot was removed from a server: {e.Guild.Name}");

			return Task.CompletedTask;
		}

		/// <summary>
		/// Log the ready state of the bot.
		/// </summary>
		/// <param name="e">Discord event information.</param>
		private Task Client_Ready(DiscordClient sender, ReadyEventArgs e)
		{
			// Log the ready event
			LogMessage("Ready!");

			return Task.CompletedTask;
		}

		/// <summary>
		/// Log the errors from the bot.
		/// </summary>
		/// <param name="e">Discord event information.</param>
		private Task Client_ClientErrored(DiscordClient sender, ClientErrorEventArgs e)
		{
			// Log the exception
			LogMessage($"Exception occurred: {e.Exception.ToString()}", level: "error");

			return Task.CompletedTask;
		}

		/// <summary>
		/// Check the exception on whether the channel is invalid or not.
		/// </summary>
		/// <param name="ex">Exception provided to to the bot.</param>
		public static bool IsChannelInvalid(Exception ex)
		{
			// Channel is deleted
			if (ex is NotFoundException) return true;

			// Token is valid and channel is just private
			if (ex is UnauthorizedException && ex.Message.Contains("403")) return true;

			return false;
		}

		/// <summary>
		/// Log a message into console.
		/// </summary>
		/// <param name="message">Message.</param>
		/// <param name="component">Component the message is from.</param>
		/// <param name="level">Threat level.</param>
		public static void LogMessage(string message, string component = "DiscordWikiBot", string level = "info")
		{
			EventId eventId = new EventId(-1, component);
			switch (level)
			{
				case "debug":
					Client.Logger.LogDebug(eventId, message, DateTime.Now);
					break;
				case "info":
					Client.Logger.LogInformation(eventId, message, DateTime.Now);
					break;
				case "warning":
					Client.Logger.LogWarning(eventId, message, DateTime.Now);
					break;
				case "error":
					Client.Logger.LogError(eventId, message, DateTime.Now);
					break;
				default:
					Client.Logger.LogInformation(eventId, message, DateTime.Now);
					break;
			}
		}

		/// <summary>
		/// Return current bot version from assembly.
		/// </summary>
		private static string GetBotVersion()
		{
			var version = Assembly.GetEntryAssembly().GetCustomAttribute<AssemblyInformationalVersionAttribute>().InformationalVersion;

			return Regex.Replace(version, @"\+.*$", "");
		}

		/// <summary>
		/// Get user agent from config or provide default.
		/// You need to provide your own when modifying the bot's source code:
		/// <para>https://foundation.wikimedia.org/wiki/Policy:Wikimedia_Foundation_User-Agent_Policy </para>
		/// </summary>
		private static string GetBotUserAgent()
		{
			var userAgent = Config.GetValue("userAgent")?.ToString();
			if (userAgent == null || userAgent == "")
			{
				LogMessage("Please add a custom user agent string in config.json if you changed  DiscordWikiBot internals (not including configs). See https://foundation.wikimedia.org/wiki/Policy:Wikimedia_Foundation_User-Agent_Policy for details.", level: "error");
				return $"DiscordWikiBot/{Version} (https://w.wiki/4nm)";
			}

			return userAgent.Replace("{version}", Version);
		}
	}
}
