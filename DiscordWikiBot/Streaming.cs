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
		[Command("openStream")]
		[Description("Начать трансляцию части потока в канал. Работает только в #moderators.")]
		public async Task OpenStream(CommandContext ctx,
			[Description("Доступный вам канал, в который необходимо начать трансляцию.")] DiscordChannel channel,
			[Description("Заголовок страницы (через \\_) или номер пространства.")] string goal = "",
			[Description("Минимальный размер диффа (необязательно).")] int minLength = -1)
		{
			// Ensure that we are in private channel
			if (ctx.Channel.Name != "moderators") return;
			await ctx.TriggerTypingAsync();

			// Check for required parameters
			if (channel.ToString() == "")
			{
				await ctx.RespondAsync("Название канала — обязательный параметр. Чтобы получить полную справку, введите `!help openStream`.");
				return;
			}

			if (goal == "")
			{
				await ctx.RespondAsync("Цель потока — обязательный параметр. Чтобы получить полную справку, введите `!help openStream`.");
				return;
			}

			// Format the goal
			goal = goal.Replace("_", " ").Replace("\\", "");

			// Change JSON config
			string len = (minLength != -1 ? minLength.ToString() : "");
			EventStreams.SetData(goal, channel.Id.ToString(), len);

			await ctx.RespondAsync($"Поток с целью `{goal}` добавлен для обработки в канал {channel.Mention}.");
		}

		[Command("closeStream")]
		[Description("Прекратить трансляцию части потока в канал. Работает только в #moderators.")]
		public async Task CloseStream(CommandContext ctx,
			[Description("Доступный вам канал, в который необходимо начать трансляцию.")] DiscordChannel channel,
			[Description("Заголовок страницы (через \\_) или номер пространства (в <>).")] string goal = "",
			[Description("Минимальный размер диффа (необязательно).")] int minLength = -1)
		{
			// Ensure that we are in private channel
			if (ctx.Channel.Name != "moderators") return;
			await ctx.TriggerTypingAsync();

			// Check for required parameters
			if (channel.ToString() == "")
			{
				await ctx.RespondAsync("Название канала — обязательный параметр. Чтобы получить полную справку, введите `!help openStream`.");
				return;
			}

			if (goal == "")
			{
				await ctx.RespondAsync("Цель потока — обязательный параметр. Чтобы получить полную справку, введите `!help openStream`.");
				return;
			}

			// Format the goal
			goal = goal.Replace("_", " ").Replace("\\", "");

			// Change JSON config
			string len = (minLength != -1 ? minLength.ToString() : "");
			EventStreams.RemoveData(goal, channel.Id.ToString(), len);

			await ctx.RespondAsync($"Поток с целью `{goal}` удалён из обработки в канале {channel.Mention}.");
		}
	}
}
