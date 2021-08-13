using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace DiscordWikiBot.Tests
{
	[TestClass]
	public class LinkingTests
	{
		[TestMethod]
		public void BasicLink()
		{
			DiscordSetup();

			string actual = Linking.PrepareMessage("[[test link]]", "ru", "https://ru.wikipedia.org/wiki/$1");
			string expected = "Ссылка: <https://ru.wikipedia.org/wiki/Test_link>";
			Assert.AreEqual(expected, actual);
		}

		[TestMethod]
		public void BasicLinkWithHack()
		{
			DiscordSetup();

			string actual = Linking.PrepareMessage("[[test (disambiguation)]]", "ru", "https://ru.wikipedia.org/wiki/$1");
			string expected = "Ссылка: <https://ru.wikipedia.org/wiki/Test_(disambiguation)_>";
			Assert.AreEqual(expected, actual);
		}

		[TestMethod]
		public void MultipleLinks()
		{
			DiscordSetup();

			string actual = Linking.PrepareMessage("[[Кот]]о[[пёс]]", "ru", "https://ru.wikipedia.org/wiki/$1");
			string expected = @"Ссылки:
<https://ru.wikipedia.org/wiki/Кот>
<https://ru.wikipedia.org/wiki/Пёс>";
			Assert.AreEqual(expected, actual);
		}

		[TestMethod]
		public void GenderedUserLinks()
		{
			DiscordSetup();

			string actual = Linking.PrepareMessage(@"
				[[user:stjn]]
				[[user:Udacha]]
				[[user:js]]
			", "ru", "https://ru.wikipedia.org/wiki/$1");
			string expected = @"Ссылки:
<https://ru.wikipedia.org/wiki/Участник:Stjn>
<https://ru.wikipedia.org/wiki/Участница:Udacha>
<https://ru.wikipedia.org/wiki/Участник:Js>";
			Assert.AreEqual(expected, actual);
		}

		[TestMethod]
		public void TemplateLinks()
		{
			DiscordSetup();

			string actual = Linking.PrepareMessage(@"
				{{С отвратительным дизайном}}
				{{int:lang}}
				{{subst:тест}}
				{{:тест}}
			", "ru", "https://ru.wikipedia.org/wiki/$1");
			string expected = @"Ссылки:
<https://ru.wikipedia.org/wiki/Шаблон:С_отвратительным_дизайном>
<https://ru.wikipedia.org/wiki/MediaWiki:Lang>
<https://ru.wikipedia.org/wiki/Шаблон:Тест>
<https://ru.wikipedia.org/wiki/Тест>";
			Assert.AreEqual(expected, actual);
		}

		[TestMethod]
		public void NamespaceAliasLink()
		{
			DiscordSetup();

			string actual = Linking.PrepareMessage("[[ВП:Страшное место]]", "ru", "https://ru.wikipedia.org/wiki/$1");
			string expected = "Ссылка: <https://ru.wikipedia.org/wiki/Википедия:Страшное_место>";
			Assert.AreEqual(expected, actual);
		}

		[TestMethod]
		public void NonCapitalisedLinks()
		{
			DiscordSetup();

			string actual = Linking.PrepareMessage("[[wikt:пёс]] [[wikt:mediawiki:common.js]]", "ru", "https://ru.wikipedia.org/wiki/$1");
			string expected = @"Ссылки:
<https://ru.wiktionary.org/wiki/пёс>
<https://ru.wiktionary.org/wiki/MediaWiki:Common.js>";
			Assert.AreEqual(expected, actual);
		}

		[TestMethod]
		public void BasicInterwikiLink()
		{
			DiscordSetup();

			string actual = Linking.PrepareMessage("[[en:wikipedia:sandbox]]", "ru", "https://ru.wikipedia.org/wiki/$1");
			string expected = "Ссылка: <https://en.wikipedia.org/wiki/Wikipedia:Sandbox>";
			Assert.AreEqual(expected, actual);
		}

		[TestMethod]
		public void NonWikiInterwikiLink()
		{
			DiscordSetup();

			string actual = Linking.PrepareMessage("[[google:lmgtfy]]", "ru", "https://ru.wikipedia.org/wiki/$1");
			string expected = "Ссылка: <https://www.google.com/search?q=lmgtfy>";
			Assert.AreEqual(expected, actual);
		}

		[TestMethod]
		public void NestedInterwikiLinks()
		{
			DiscordSetup();

			string actual = Linking.PrepareMessage("[[en:wikt:test]] [[en:wikt:mediawiki:common.js]]", "ru", "https://ru.wikipedia.org/wiki/$1");
			string expected = @"Ссылки:
<https://en.wiktionary.org/wiki/test>
<https://en.wiktionary.org/wiki/MediaWiki:Common.js>";
			Assert.AreEqual(expected, actual);
		}

		[TestMethod]
		public void EmojiInterwikiLink()
		{
			DiscordSetup();

			string actual = Linking.PrepareMessage("[[<:meta:873203055804436513>]]", "ru", "https://ru.wikipedia.org/wiki/$1");
			string expected = "Ссылка: <https://meta.wikimedia.org/wiki/>";
			Assert.AreEqual(expected, actual);
		}

		[TestMethod]
		public void WeirdLinks()
		{
			DiscordSetup();

			string actual1 = Linking.PrepareMessage("[[:Test#One%20Two]]", "ru", "https://ru.wikipedia.org/wiki/$1");
			string expected1 = "Ссылка: <https://ru.wikipedia.org/wiki/Test#One_Two>";
			Assert.AreEqual(expected1, actual1);

			string actual2 = Linking.PrepareMessage("[[File:Rainbow lorikeet.jpg%20]]", "ru", "https://ru.wikipedia.org/wiki/$1");
			string expected2 = "Ссылка: <https://ru.wikipedia.org/wiki/Файл:Rainbow_lorikeet.jpg>";
			Assert.AreEqual(expected2, actual2);

			string actual3 = Linking.PrepareMessage(@"
				[[wikt::Gift]]
				[[:ja : おちんちん]]
			", "ru", "https://ru.wikipedia.org/wiki/$1");
			string expected3 = @"Ссылки:
<https://ru.wiktionary.org/wiki/Gift>
<https://ja.wikipedia.org/wiki/おちんちん>";
			Assert.AreEqual(expected3, actual3);
		}

		[TestMethod]
		public void InvalidLinks()
		{
			DiscordSetup();

			string actual = Linking.PrepareMessage(@"
				[[ ]]
				{{ }}
				[[https://ru.wikipedia.org/wiki/Ithappens]]
				[[<test>]]
				{{#tag:ref}}
				{{subst:#time:d.m.Y H:i}}
				[[ВП:]]
				[[Eh bien, mon prince. Gênes et Lucques ne sont plus que des apanages, des поместья, de la famille Buonaparte. Non, je vous préviens que si vous ne me dites pas que nous avons la guerre, si vous vous permettez encore de pallier toutes les infamies, toutes les atrocités de cet Antichrist (ma parole, j'y crois) — je ne vous connais plus, vous n'êtes plus mon ami, vous n'êtes plus мой верный раб, comme vous dites]]
			", "ru", "https://ru.wikipedia.org/wiki/$1");
			Assert.AreEqual("", actual);
		}

		[TestMethod]
		public void LinksInIgnoredBlocks()
		{
			DiscordSetup();

			string actual = Linking.PrepareMessage(@"
				`[[one-liner]]`
				`[[multiline 1]]
				[[multiline 2]]`

				``[[double one-liner]]``
				``[[double multiline 1]]
				[[double multiline 2]]``

				```
				[[code block]]
				```
				
> quote block example [[test]]

>>> test
[[ignore everything after triple quote block]]
", "ru", "https://ru.wikipedia.org/wiki/$1");
			Assert.AreEqual("", actual);
		}

		private void DiscordSetup()
		{
			if (Program.Client == null)
			{
				new Program().Run(false).Wait();
			}
		}
	}
}
