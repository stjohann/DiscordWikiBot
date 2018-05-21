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
	class LocalisedHelpFormatter : IHelpFormatter
	{
		private string Command;
		private DiscordEmbedBuilder EmbedBuilder { get; }

		public LocalisedHelpFormatter()
		{
			this.EmbedBuilder = new DiscordEmbedBuilder().WithColor(new DiscordColor(0x72777d));
		}

		public IHelpFormatter WithCommandName(string name)
		{
			this.Command = name;
			this.EmbedBuilder.WithTitle(Locale.GetMessage("help-title-command", name, Program.Config.Prefix));

			return this;
		}

		public IHelpFormatter WithDescription(string description)
		{
			// Override help command
			if (this.Command == "help")
			{
				description = "help-command";
			}

			this.EmbedBuilder.WithDescription(Locale.GetMessage(description));
			return this;
		}

		public IHelpFormatter WithGroupExecutable()
		{
			this.EmbedBuilder.WithFooter(Locale.GetMessage("help-group-standalone"));

			return this;
		}

		public IHelpFormatter WithAliases(IEnumerable<string> aliases)
		{
			this.EmbedBuilder.AddField(Locale.GetMessage("help-aliases"), string.Join(", ", aliases));

			return this;
		}

		public IHelpFormatter WithArguments(IEnumerable<CommandArgument> arguments)
		{
			string args = string.Join("\n",
				arguments.Select(xarg =>
				{
					string desc = xarg.Description;
					string def = "";
					string optional = "";

					// Override help command
					if (this.Command == "help")
					{
						desc = "help-command-" + xarg.Name;
					}

					// Provide optional information
					if (xarg.Description.Length > 0)
					{
						desc = Locale.GetMessage("help-separator", Locale.GetMessage(desc));
					}

					if (xarg.DefaultValue != null && xarg.DefaultValue.ToString() != "")
					{
						def = " " + Locale.GetMessage("help-default", xarg.DefaultValue.ToString());

						if (xarg.IsOptional)
						{
							optional = ", " + Locale.GetMessage("help-optional");
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

			this.EmbedBuilder.AddField(Locale.GetMessage("help-arguments"), args);
			return this;
		}

		public IHelpFormatter WithSubcommands(IEnumerable<Command> subcommands)
		{
			string header = Locale.GetMessage("help-subcommands");
			if (this.Command == null )
			{
				header = Locale.GetMessage("help-commands");
			}
			this.EmbedBuilder.AddField(header, string.Join(", ", subcommands.Select(xc => $"`{Program.Config.Prefix}{xc.Name}`")));

			return this;
		}

		public CommandHelpMessage Build()
		{
			if (this.Command == null)
			{
				this.EmbedBuilder
					.WithTitle(Locale.GetMessage("help-title"))
					.WithDescription(Locale.GetMessage("help-all"));
			}
			return new CommandHelpMessage(embed: this.EmbedBuilder.Build());
		}
	}
}
