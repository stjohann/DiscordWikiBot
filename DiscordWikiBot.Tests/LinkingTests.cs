﻿using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace DiscordWikiBot.Tests
{
	[TestClass]
	public class LinkingTests
	{
		[TestMethod]
		public void BasicLink()
		{
			string actual = Linking.PrepareMessage("[[test link]]", "ru", "https://ru.wikipedia.org/wiki/$1");
			string expected = "Ссылка: [[[Test link]]](<https://ru.wikipedia.org/wiki/Test_link>)";
			Assert.AreEqual(expected, actual);
		}

		[TestMethod]
		public void BasicLinkWithHack()
		{
			string actual = Linking.PrepareMessage("[[test (disambiguation)]]", "ru", "https://ru.wikipedia.org/wiki/$1");
			string expected = "Ссылка: [[[Test (disambiguation)]]](<https://ru.wikipedia.org/wiki/Test_(disambiguation)_>)";
			Assert.AreEqual(expected, actual);
		}

		[TestMethod]
		public void MultipleLinks()
		{
			string actual = Linking.PrepareMessage("[[Кот]]о[[пёс]]", "ru", "https://ru.wikipedia.org/wiki/$1");
			string expected = @"Ссылки: [[[Кот]]](<https://ru.wikipedia.org/wiki/Кот>), [[[Пёс]]](<https://ru.wikipedia.org/wiki/Пёс>)";
			Assert.AreEqual(expected, actual);
		}

		[TestMethod]
		public void GenderedUserLinks()
		{
			string actual = Linking.PrepareMessage(@"
				[[user:stjn]]
				[[user:Udacha]]
				[[user:js]]
			", "ru", "https://ru.wikipedia.org/wiki/$1");
			string expected = @"Ссылки: [[[Участник:Stjn]]](<https://ru.wikipedia.org/wiki/Участник:Stjn>), [[[Участница:Udacha]]](<https://ru.wikipedia.org/wiki/Участница:Udacha>), [[[Участник:Js]]](<https://ru.wikipedia.org/wiki/Участник:Js>)";
			Assert.AreEqual(expected, actual);
		}

		[TestMethod]
		public void TemplateLinks()
		{
			string actual = Linking.PrepareMessage(@"
				{{С отвратительным дизайном}}
				{{подст:int:lang}}
				{{subst:внутр:lang}}
				{{#invoke:Math}}
				{{#вызвать:Math}}
				{{subst:тест}}
				{{подст:тест 2}}
				{{:тест}}
			", "ru", "https://ru.wikipedia.org/wiki/$1");
			string expected = @"Ссылки: [{{С отвратительным дизайном}}](<https://ru.wikipedia.org/wiki/Шаблон:С_отвратительным_дизайном>), [{{MediaWiki:Lang}}](<https://ru.wikipedia.org/wiki/MediaWiki:Lang>), [{{Модуль:Math}}](<https://ru.wikipedia.org/wiki/Модуль:Math>), [{{Тест}}](<https://ru.wikipedia.org/wiki/Шаблон:Тест>), [{{Тест 2}}](<https://ru.wikipedia.org/wiki/Шаблон:Тест_2>), [{{:Тест}}](<https://ru.wikipedia.org/wiki/Тест>)";
			Assert.AreEqual(expected, actual);
		}

		[TestMethod]
		public void MagicWordLinks()
		{
			string actual = Linking.PrepareMessage(@"
				// Should return a template name, but currently cannot
				{{pagename}}
				{{SERVERNAME}}
				{{safesubst:SERVERNAME}}
				{{servername}}
				{{SERVERNAME: test}}
				{{PAGENAME|test}}
				{{Special:RecentChanges}}
				{{#special:RecentChanges}}
				{{#tag:ref}}
				{{tag test}}
				{{subst:#time:d.m.Y H:i}}
				{{formatnum:223}}
				{{GENDER:stjn}}
			", "ru", "https://ru.wikipedia.org/wiki/$1");
			string expected = @"Ссылки: [{{Служебная:RecentChanges}}](<https://ru.wikipedia.org/wiki/Служебная:RecentChanges>), [{{Tag test}}](<https://ru.wikipedia.org/wiki/Шаблон:Tag_test>)";
			Assert.AreEqual(expected, actual);
		}

		[TestMethod]
		public void NamespaceAliasLink()
		{
			string actual = Linking.PrepareMessage("[[ВП:Страшное место]]", "ru", "https://ru.wikipedia.org/wiki/$1");
			string expected = "Ссылка: [[[Википедия:Страшное место]]](<https://ru.wikipedia.org/wiki/Википедия:Страшное_место>)";
			Assert.AreEqual(expected, actual);
		}

		[TestMethod]
		public void NonCapitalisedLinks()
		{
			string actual = Linking.PrepareMessage("[[wikt:пёс]] [[wikt:mediawiki:common.js]]", "ru", "https://ru.wikipedia.org/wiki/$1");
			string expected = @"Ссылки: [[[wikt:пёс]]](<https://ru.wiktionary.org/wiki/пёс>), [[[wikt:MediaWiki:Common.js]]](<https://ru.wiktionary.org/wiki/MediaWiki:Common.js>)";
			Assert.AreEqual(expected, actual);
		}

		[TestMethod]
		public void BasicInterwikiLinks()
		{
			string actual = Linking.PrepareMessage(@"
			[[en:wikipedia:sandbox]]
			[[fr:]]
			{{de:test}}
			", "ru", "https://ru.wikipedia.org/wiki/$1");
			string expected = @"Ссылки: [[[en:Wikipedia:Sandbox]]](<https://en.wikipedia.org/wiki/Wikipedia:Sandbox>), [[[fr:Wikipédia:Accueil principal]]](<https://fr.wikipedia.org/wiki/Wikipédia:Accueil_principal>), [{{De:test}}](<https://ru.wikipedia.org/wiki/Шаблон:De:test>)";
			Assert.AreEqual(expected, actual);
		}

		[TestMethod]
		public void NonWikiInterwikiLinks()
		{
			string actual = Linking.PrepareMessage(@"
			[[toollabs:guc]]
			[[google:]]
			[[google:lmgtfy]]
			", "ru", "https://ru.wikipedia.org/wiki/$1");
			string expected = @"Ссылки: [[[toollabs:guc]]](<https://iw.toolforge.org/guc>), [[[google:]]](<https://www.google.com/search?q=>), [[[google:lmgtfy]]](<https://www.google.com/search?q=lmgtfy>)";
			Assert.AreEqual(expected, actual);
		}

		[TestMethod]
		public void NestedInterwikiLinks()
		{
			string actual = Linking.PrepareMessage(@"
			[[en:wikt:test]]
			[[en:wikt:mediawiki:common.js]]
			[[google:de:test]]
			", "ru", "https://ru.wikipedia.org/wiki/$1");
			string expected = @"Ссылки: [[[en:wikt:test]]](<https://en.wiktionary.org/wiki/test>), [[[en:wikt:MediaWiki:Common.js]]](<https://en.wiktionary.org/wiki/MediaWiki:Common.js>), [[[google:de:test]]](<https://www.google.com/search?q=de:test>)";
			Assert.AreEqual(expected, actual);
		}

		[TestMethod]
		public void EmojiInterwikiLink()
		{
			string actual = Linking.PrepareMessage("[[<:meta:873203055804436513>Discord]]", "ru", "https://ru.wikipedia.org/wiki/$1");
			string expected = "Ссылка: [[[meta:Discord]]](<https://meta.wikimedia.org/wiki/Discord>)";
			Assert.AreEqual(expected, actual);
		}

		[TestMethod]
		public void WeirdlyAdjacentLinks()
		{
			string actual = Linking.PrepareMessage(@"
				[[[каждый]]
				[[охотник]]]
				{{желает}}}
				{{{знать}}
				{{{где}}}
				{{{{сидит}}}}
				{{{{{{фазан}}
			", "ru", "https://ru.wikipedia.org/wiki/$1");
			string expected = @"Ссылки: [[[Каждый]]](<https://ru.wikipedia.org/wiki/Каждый>), [[[Охотник]]](<https://ru.wikipedia.org/wiki/Охотник>), [{{Желает}}](<https://ru.wikipedia.org/wiki/Шаблон:Желает>), [{{Знать}}](<https://ru.wikipedia.org/wiki/Шаблон:Знать>), [{{Фазан}}](<https://ru.wikipedia.org/wiki/Шаблон:Фазан>)";
			Assert.AreEqual(expected, actual);
		}

		[TestMethod]
		public void WeirdLinks()
		{
			string actual1 = Linking.PrepareMessage("[[:Test#One%20Two]] [[Tam&#x200F;zin]]", "ru", "https://ru.wikipedia.org/wiki/$1");
			string expected1 = "Ссылки: [[[Test#One Two]]](<https://ru.wikipedia.org/wiki/Test#One_Two>), [[[Tamzin]]](<https://ru.wikipedia.org/wiki/Tamzin>)";
			Assert.AreEqual(expected1, actual1);

			string actual2 = Linking.PrepareMessage("[[File:Rainbow lorikeet.jpg%20]]", "ru", "https://ru.wikipedia.org/wiki/$1");
			string expected2 = "Ссылка: [[[Файл:Rainbow lorikeet.jpg]]](<https://ru.wikipedia.org/wiki/Файл:Rainbow_lorikeet.jpg>)";
			Assert.AreEqual(expected2, actual2);

			string actual3 = Linking.PrepareMessage(@"
				[[wikt::Gift]]
				[[:ja : おちんちん]]
				[[: Википедия:ЗЛВ]]
				[[Википедия: ЗЛВ]]
				{{ int: mainpage }}
			", "ru", "https://ru.wikipedia.org/wiki/$1");
			string expected3 = @"Ссылки: [[[wikt:Gift]]](<https://ru.wiktionary.org/wiki/Gift>), [[[ja:おちんちん]]](<https://ja.wikipedia.org/wiki/おちんちん>), [[[Википедия:ЗЛВ]]](<https://ru.wikipedia.org/wiki/Википедия:ЗЛВ>), [{{MediaWiki:Mainpage}}](<https://ru.wikipedia.org/wiki/MediaWiki:Mainpage>)";
			Assert.AreEqual(expected3, actual3);

			string actual4 = Linking.PrepareMessage(@"
				[[foo|bar|baz]]
				[[nested|[pipes]]]
			", "ru", "https://ru.wikipedia.org/wiki/$1");
			string expected4 = @"Ссылки: [[[Foo]]](<https://ru.wikipedia.org/wiki/Foo>), [[[Nested]]](<https://ru.wikipedia.org/wiki/Nested>)";
			Assert.AreEqual(expected4, actual4);
		}

		[TestMethod]
		public void InvalidLinks()
		{
			string actual = Linking.PrepareMessage(@"
				[[ ]]
				{{ }}
				[[#top]]
				[[https://ru.wikipedia.org/wiki/Ithappens]]
				[[<test>]]
				[[ВП:]]
				[[Eh bien, mon prince. Gênes et Lucques ne sont plus que des apanages, des поместья, de la famille Buonaparte. Non, je vous préviens que si vous ne me dites pas que nous avons la guerre, si vous vous permettez encore de pallier toutes les infamies, toutes les atrocités de cet Antichrist (ma parole, j'y crois) — je ne vous connais plus, vous n'êtes plus mon ami, vous n'êtes plus мой верный раб, comme vous dites]]
			", "ru", "https://ru.wikipedia.org/wiki/$1");
			Assert.AreEqual("", actual);
		}

		[TestMethod]
		public void LinksInIgnoredBlocks()
		{
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

		[TestMethod]
		public void LinksInSpoilerBlocks()
		{
			string actual1 = Linking.PrepareMessage("||[[Test]]||", "ru", "https://ru.wikipedia.org/wiki/$1");
			string expected1 = "Ссылка: ||[[[Test]]](<https://ru.wikipedia.org/wiki/Test>)||";
			Assert.AreEqual(expected1, actual1);

			string actual2 = Linking.PrepareMessage("[[A]] ||[[B]] [[C]]|| [[D]]", "ru", "https://ru.wikipedia.org/wiki/$1");
			string expected2 = @"Ссылки: [[[A]]](<https://ru.wikipedia.org/wiki/A>), ||[[[B]]](<https://ru.wikipedia.org/wiki/B>)||, ||[[[C]]](<https://ru.wikipedia.org/wiki/C>)||, [[[D]]](<https://ru.wikipedia.org/wiki/D>)";
			Assert.AreEqual(expected2, actual2);
		}

		[ClassInitialize]
		public static void DiscordSetup(TestContext ctx)
		{
			if (Program.Client == null)
			{
				new Program().Run(false).Wait();
			}
		}
	}
}
