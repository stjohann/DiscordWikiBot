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
		public static string Version;

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
			Version = Assembly.GetEntryAssembly().GetCustomAttribute<AssemblyInformationalVersionAttribute>().InformationalVersion.ToString();
			Client.DebugLogger.LogMessage(LogLevel.Info, "DiscordWikiBot", $"DiscordWikiBot, version {Version}", DateTime.Now);

			// Get locale
			Client.DebugLogger.LogMessage(LogLevel.Info, "DiscordWikiBot", string.Format("Loading {0} locale", Config.GetLang().ToUpper()), DateTime.Now);
			Locale.Init();

			// Get site information and start linking bot
			Client.DebugLogger.LogMessage(LogLevel.Info, "DiscordWikiBot", "Getting wiki site information", DateTime.Now);
			Linking.Init();

			// Methods for linking bot
			Client.MessageCreated += Linking.Answer;
			Client.MessageUpdated += Linking.Edit;
			Client.MessageDeleted += Linking.Delete;

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
			Client.ClientErrored += Client_ClientErrored;

			// Initialise commands
			Client.DebugLogger.LogMessage(LogLevel.Info, "DiscordWikiBot", "Setting up commands", DateTime.Now);
			Commands = Client.UseCommandsNext(new CommandsNextConfiguration
			{
				StringPrefix = Config.GetValue("prefix"),
				EnableDms = false,
				EnableMentionPrefix = true,
			});

			Commands.RegisterCommands<Pinging>();

			Commands.RegisterCommands<Configuring>();

			Commands.RegisterCommands<Streaming>();

			// Set up custom formatter
			Commands.SetHelpFormatter<LocalisedHelpFormatter>();

			// Connect and start
			Client.DebugLogger.LogMessage(LogLevel.Info, "DiscordWikiBot", "Connecting...", DateTime.Now);
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
			e.Client.DebugLogger.LogMessage(LogLevel.Info, "DiscordWikiBot", $"Guild available: {e.Guild.Name}", DateTime.Now);

			// Load custom values if needed
			string guild = e.Guild.Id.ToString();

			Linking.Init(guild);

			Locale.LoadCustomLocale(Config.GetLang(guild));

			if (Config.GetTWChannel(guild) != null && Config.GetTWLang(guild) != null)
			{
				TranslateWiki.Init(Config.GetTWChannel(guild), Config.GetTWLang(guild));
			}

			if (Config.GetDomain(guild) != null)
			{
				EventStreams.Subscribe(Config.GetDomain(guild));
			}

			return Task.FromResult(0);
		}

		/// <summary>
		/// Log the ready state of the bot.
		/// </summary>
		/// <param name="e">Discord event information.</param>
		private Task Client_Ready(ReadyEventArgs e)
		{
			// Log the ready event
			e.Client.DebugLogger.LogMessage(LogLevel.Info, "DiscordWikiBot", "Ready!", DateTime.Now);

			return Task.FromResult(0);
		}

		/// <summary>
		/// Log the errors from the bot.
		/// </summary>
		/// <param name="e">Discord event information.</param>
		private Task Client_ClientErrored(ClientErrorEventArgs e)
		{
			// Log the exception
			e.Client.DebugLogger.LogMessage(LogLevel.Error, "DiscordWikiBot", $"Exception occurred: {e.Exception.ToString()}", DateTime.Now);

			return Task.FromResult(0);
		}
	}
}
