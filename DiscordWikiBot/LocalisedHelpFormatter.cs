using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using DSharpPlus.CommandsNext;
using DSharpPlus.CommandsNext.Converters;
using DSharpPlus.CommandsNext.Entities;
using DSharpPlus.Entities;

namespace DiscordWikiBot
{
	/// <summary>
	/// Provide customised help with proper localisation abilities.
	/// </summary>
	class LocalisedHelpFormatter : BaseHelpFormatter
	{
		/// <summary>
		/// Default command prefix.
		/// </summary>
		private static readonly string _prefix = Program.CommandPrefix;

		/// <summary>
		/// Command for which the help message is being produced.
		/// </summary>
		private Command Command;

		/// <summary>
		/// Language of the help command response.
		/// </summary>
		private string Lang = "en";

		/// <summary>
		/// Resulting Discord embed.
		/// </summary>
		private DiscordEmbedBuilder EmbedBuilder { get; }

		/// <summary>
		/// Creates a new help formatter.
		/// </summary>
		public LocalisedHelpFormatter(CommandContext ctx) : base(ctx)
		{
			// Initialise the Discord embed.
			EmbedBuilder = new DiscordEmbedBuilder().WithColor(new DiscordColor(0x72777d));

			// Get the language of the server
			Lang = Config.GetLang(ctx.Guild);
		}

		/// <summary>
		/// Sets the command this help message will be for.
		/// </summary>
		/// <param name="command">Command for which the help message is being produced.</param>
		/// <returns>This help formatter.</returns>
		public override BaseHelpFormatter WithCommand(Command command)
		{
			Command = command;
			EmbedBuilder.WithTitle(Locale.GetMessage("help-title-command", Lang, Command.Name, Program.CommandPrefix));

			// Set command description
			string description = command.Description;
			if (this.Command.Name == "help")
			{
				description = "help-command";
			}
			EmbedBuilder.WithDescription(Locale.GetMessage(description, Lang));

			// Say if the command is a standalone group
			if (command is CommandGroup cgroup && cgroup.IsExecutableWithoutSubcommands)
			{
				EmbedBuilder.WithFooter(Locale.GetMessage("help-group-standalone", Lang));
			}

			// List the aliases
			if (command.Aliases?.Any() == true)
			{
				EmbedBuilder.AddField(
					Locale.GetMessage("help-aliases", Lang),
					string.Join(", ", command.Aliases.Select(xa => $"`{_prefix}{xa}`"))
				);
			}

			// List the command arguments
			if (command.Overloads?.Any() == true)
			{
				string arguments = ListArguments(command);
				if (arguments.Length > 0)
				{
					EmbedBuilder.AddField(Locale.GetMessage("help-arguments", Lang), arguments);
				}
			}

			return this;
		}

		/// <summary>
		/// Sets the subcommands for this command, if applicable. This method will be called with filtered data.
		/// </summary>
		/// <param name="subcommands">Subcommands for this command group.</param>
		/// <returns>This help formatter.</returns>
		public override BaseHelpFormatter WithSubcommands(IEnumerable<Command> subcommands)
		{
			string header = Locale.GetMessage("help-subcommands", Lang);
			if (Command == null)
			{
				header = Locale.GetMessage("help-commands", Lang);
			}
			var sb = new StringBuilder();

			foreach (var xc in subcommands)
			{
				if (xc.IsHidden) continue;
				sb.Append(Locale.GetMessage("bullet", Lang, $"`{_prefix}{xc.QualifiedName}`"));

				// Provide the description
				if (xc.Description.Length > 0)
				{
					sb.Append(Locale.GetMessage("separator", Lang, Locale.GetMessage(xc.Name == "help" ? "help-command" : xc.Description, Lang)));
				}

				sb.Append('\n');
			}

			EmbedBuilder.AddField(header, sb.ToString().Trim());
			return this;
		}

		/// <summary>
		/// Respond with a Discord embed.
		/// </summary>
		public override CommandHelpMessage Build()
		{
			if (Command == null)
			{
				string content = Locale.GetMessage("help-version", Lang, Program.Version);

				// Add a link to the source code repository
				if (Config.GetValue("repo") != null)
				{
					content = string.Format("{0} {1}",
						content,
						Locale.GetMessage("help-repo", Lang, Config.GetValue("repo"))
					);
				}

				EmbedBuilder
					.WithTitle(Locale.GetMessage("help-title", Lang))
					.WithDescription(Locale.GetMessage("help-all", Lang));

				return new CommandHelpMessage(content, EmbedBuilder.Build());
			}

			return new CommandHelpMessage(embed: EmbedBuilder.Build());
		}

		/// <summary>
		/// Format the list of arguments in the command.
		/// </summary>
		/// <param name="command">Command for which the help message is being produced.</param>
		/// <returns>Formatted list of arguments.</returns>
		private string ListArguments(Command command) {
			var sb = new StringBuilder();

			foreach (var ovl in command.Overloads.OrderByDescending(x => x.Priority)) {
				// Format the example of command
				if (command.Overloads.Count > 1)
				{
					sb.Append('`').Append(_prefix).Append(command.QualifiedName);
					foreach (var arg in ovl.Arguments)
					{
						sb.Append(arg.IsOptional || arg.IsCatchAll ? " [" : " <").Append(arg.Name).Append(arg.IsCatchAll ? "…" : "").Append(arg.IsOptional || arg.IsCatchAll ? ']' : '>');
					}
					sb.Append("`\n");
				}

				// Format the list of arguments
				foreach (var arg in ovl.Arguments)
				{
					string desc = arg.Description;
					bool hasDefaultValue = arg.DefaultValue != null && arg.DefaultValue.ToString() != "";
					sb.Append('`').Append(arg.Name).Append('`');

					// List type and optionality
					sb.Append(" (*").Append(this.CommandsNext.GetUserFriendlyTypeName(arg.Type));
					if (arg.IsOptional && hasDefaultValue)
					{
						sb.Append(", " + Locale.GetMessage("help-optional", Lang));
					}
					sb.Append("*)");

					// Override the description key for help command
					if (command.Name == "help")
					{
						desc = "help-command-" + arg.Name;
					}

					// Provide the description
					if (desc.Length > 0)
					{
						sb.Append(Locale.GetMessage("separator", Lang, Locale.GetMessage(desc, Lang)));
					}

					// List the default value
					if (hasDefaultValue)
					{
						sb.Append(" " + Locale.GetMessage("help-default", Lang, arg.DefaultValue.ToString()));
					}

					sb.Append('\n');
				}
			}

			return sb.ToString().Trim();
		}
	}
}
