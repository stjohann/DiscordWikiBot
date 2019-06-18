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
	class LocalisedHelpFormatter : IHelpFormatter
	{
		/// <summary>
		/// Name of a command that’s being executed.
		/// </summary>
		private string Command;

		/// <summary>
		/// Discord embed.
		/// </summary>
		private DiscordEmbedBuilder EmbedBuilder { get; }

		/// <summary>
		/// Initialise the Discord embed.
		/// </summary>
		public LocalisedHelpFormatter()
		{
			EmbedBuilder = new DiscordEmbedBuilder().WithColor(new DiscordColor(0x72777d));
		}

		/// <summary>
		/// Remember the command name and add a title to the embed.
		/// </summary>
		/// <param name="name">Command name.</param>
		public IHelpFormatter WithCommandName(string name)
		{
			Command = name;
			EmbedBuilder.WithTitle(Locale.GetMessage("help-title-command", "en", name, Config.GetValue("prefix")));

			return this;
		}

		/// <summary>
		/// Provide a localised description to the embed.
		/// </summary>
		/// <param name="description">Description key.</param>
		public IHelpFormatter WithDescription(string description)
		{
			// Override help command
			if (this.Command == "help")
			{
				description = "help-command";
			}

			EmbedBuilder.WithDescription(Locale.GetMessage(description, "en"));
			return this;
		}

		/// <summary>
		/// Add information to the embed if the command is a standalone group.
		/// </summary>
		public IHelpFormatter WithGroupExecutable()
		{
			EmbedBuilder.WithFooter(Locale.GetMessage("help-group-standalone", "en"));

			return this;
		}

		/// <summary>
		/// Add the aliases information to the embed.
		/// </summary>
		/// <param name="aliases">List of aliases.</param>
		public IHelpFormatter WithAliases(IEnumerable<string> aliases)
		{
			EmbedBuilder.AddField(Locale.GetMessage("help-aliases", "en"), string.Join(", ", aliases.Select(xa => $"`{Config.GetValue("prefix")}{xa}`")));

			return this;
		}

		/// <summary>
		/// List arguments with localised information in the embed.
		/// </summary>
		/// <param name="arguments">List of arguments</param>
		public IHelpFormatter WithArguments(IEnumerable<CommandArgument> arguments)
		{
			string args = string.Join("\n",
				arguments.Select(xarg =>
				{
					string desc = xarg.Description;
					string def = "";
					string optional = "";

					// Override help command
					if (Command == "help")
					{
						desc = "help-command-" + xarg.Name;
					}

					// Provide optional information
					if (xarg.Description.Length > 0)
					{
						desc = Locale.GetMessage("separator", "en", Locale.GetMessage(desc, "en"));
					}

					if (xarg.DefaultValue != null && xarg.DefaultValue.ToString() != "")
					{
						def = " " + Locale.GetMessage("help-default", "en", xarg.DefaultValue.ToString());

						if (xarg.IsOptional)
						{
							optional = ", " + Locale.GetMessage("help-optional", "en");
						}
					}

					return string.Format("`{0}` (_{1}{2}_){3}{4}",
						xarg.Name,
						xarg.Type.ToUserFriendlyName(),
						optional,
						desc,
						def
					);
				})
			);

			EmbedBuilder.AddField(Locale.GetMessage("help-arguments", "en"), args);
			return this;
		}

		/// <summary>
		/// List commands with localised information in the embed.
		/// </summary>
		/// <param name="subcommands">List of commands</param>
		public IHelpFormatter WithSubcommands(IEnumerable<Command> subcommands)
		{
			string header = Locale.GetMessage("help-subcommands", "en");
			if (Command == null)
			{
				header = Locale.GetMessage("help-commands", "en");
			}
			string list = string.Join("\n", subcommands.Select(xc => {
				if (!xc.IsHidden)
				{
					string comm = $"{xc.Name}";
					if (Command != null)
					{
						comm = $"{Command} {comm}";
					}
					string descId = (xc.Name == "help" ? "help-command" : xc.Description);

					string desc = Locale.GetMessage("separator", "en", Locale.GetMessage(descId, "en"));
					return Locale.GetMessage("bullet", "en", $"`{Config.GetValue("prefix")}{comm}`{desc}");
				}

				return null;
			}).Where(s => !string.IsNullOrEmpty(s)));

			EmbedBuilder.AddField(header, list);
			return this;
		}

		/// <summary>
		/// Respond with a Discord embed.
		/// </summary>
		public CommandHelpMessage Build()
		{
			string content = Locale.GetMessage("help-version", "en", Program.Version);
			if (Command == null)
			{
				// Add a link to the source code repository
				if (Config.GetValue("repo") != null)
				{
					content = string.Format("{0} {1}",
						content,
						Locale.GetMessage("help-repo", "en", Config.GetValue("repo"))
					);
				}

				EmbedBuilder
					.WithTitle(Locale.GetMessage("help-title", "en"))
					.WithDescription(Locale.GetMessage("help-all", "en"));
			}
			return new CommandHelpMessage(content, EmbedBuilder.Build());
		}
	}
}
