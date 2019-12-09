using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using DSharpPlus;
using DSharpPlus.CommandsNext;
using DSharpPlus.EventArgs;

namespace DiscordWikiBot
{
	/// <summary>
	/// Main class of the bot.
	/// <para>Provides functions for bot’s initialisation.</para>
	/// </summary>
	class Program
	{
		/// <summary>
		/// An instance of Discord client.
		/// </summary>
		public static DiscordClient Client;

		/// <summary>
		/// DiscordWikiBot version.
		/// </summary>
		public static string Version = Assembly.GetEntryAssembly().GetCustomAttribute<AssemblyInformationalVersionAttribute>().InformationalVersion;

		/// <summary>
		/// DiscordWikiBot user agent.
		/// Please specify your own when modifying the bot.
		/// <para>See https://meta.wikimedia.org/wiki/User-Agent_policy </para>
		/// </summary>
		public static string UserAgent = $"DiscordWikiBot/{Version}";

		/// <summary>
		/// Available bot commands.
		/// </summary>
		private CommandsNextModule Commands { get; set; }

		/// <summary>
		/// Discord developer token.
		/// </summary>
		private static string Token;

		static void Main(string[] args) => new Program().Run().GetAwaiter().GetResult();

		/// <summary>
		/// Initialise the bot and keep it running
		/// </summary>
		public async Task Run()
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

			// Get JSON config file
			Config.Init();

			// Initialise Discord client
			Client = new DiscordClient(new DiscordConfiguration()
			{
				AutoReconnect = true,
				LargeThreshold = 250,
				LogLevel = LogLevel.Info,
				Token = Token,
				TokenType = TokenType.Bot,
				UseInternalLogHandler = true,
			});

			// Initialise events
			LogMessage($"DiscordWikiBot, version {Version}");

			// Get default locale
			Locale.Init();

			// Get site information and start linking bot
			LogMessage("Getting wiki site information");
			Linking.Init();

			// Methods for linking bot
			Client.MessageCreated += Linking.Answer;
			Client.MessageUpdated += Linking.Edit;
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
				StringPrefix = Config.GetValue("prefix"),
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
			await CtrlC();
		}

		/// <summary>
		/// Have the ability to stop the bot on Ctrl+C
		/// </summary>
		/// TODO: Fix this after porting to .NET Core
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
		private Task Client_GuildAvailable(GuildCreateEventArgs e)
		{
			// Log the name of the guild that just became available
			LogMessage($"Server is loaded: {e.Guild.Name}");

			// Load custom values if needed
			string guild = e.Guild.Id.ToString();

			Linking.Init(guild);

			Locale.Init(Config.GetLang(guild));

			if (Config.GetTWChannel(guild) != null && Config.GetTWLang(guild) != null)
			{
				TranslateWiki.Init(Config.GetTWChannel(guild), Config.GetTWLang(guild));
			}

			return Task.FromResult(0);
		}
		
		/// <summary>
		/// Log message when the bot is added to new guilds.
		/// </summary>
		/// <param name="e">Discord event information.</param>
		private Task Client_GuildCreated(GuildCreateEventArgs e)
		{
			LogMessage($"Bot was added to a server: {e.Guild.Name}");

			return Task.FromResult(0);
		}

		/// <summary>
		/// Log message when the bot is removed from guilds.
		/// </summary>
		/// <param name="e">Discord event information.</param>
		private Task Client_GuildDeleted(GuildDeleteEventArgs e)
		{
			LogMessage($"Bot was removed from a server: {e.Guild.Name}");

			return Task.FromResult(0);
		}

		/// <summary>
		/// Log the ready state of the bot.
		/// </summary>
		/// <param name="e">Discord event information.</param>
		private Task Client_Ready(ReadyEventArgs e)
		{
			// Log the ready event
			LogMessage("Ready!");

			return Task.FromResult(0);
		}

		/// <summary>
		/// Log the errors from the bot.
		/// </summary>
		/// <param name="e">Discord event information.</param>
		private Task Client_ClientErrored(ClientErrorEventArgs e)
		{
			// Log the exception
			LogMessage($"Exception occurred: {e.Exception.ToString()}", level: LogLevel.Error);

			return Task.FromResult(0);
		}

		/// <summary>
		/// Log a message into console.
		/// </summary>
		/// <param name="message">Message.</param>
		/// <param name="component">Component the message is from.</param>
		/// <param name="level">Threat level.</param>
		public static void LogMessage(string message, string component = "DiscordWikiBot", LogLevel level = LogLevel.Info)
		{
			Client.DebugLogger.LogMessage(level, component, message, DateTime.Now);
		}
	}
}
