using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace DiscordWikiBot.Tests
{
	[TestClass]
	public class LinkingTests
	{
		[TestMethod]
		public void BasicLink()
		{
			string actual = TestMessage("[[test link]]");
			string expected = @"Ссылка: [[[`Test link`]]]( <https://ru.wikipedia.org/wiki/Test_link> )";
			Assert.AreEqual(expected, actual);
		}

		[TestMethod]
		public void BasicEscapedLink()
		{
			string actual = TestMessage("[[test_(disambiguation)]]");
			string expected = @"Ссылка: [[[`Test (disambiguation)`]]]( <https://ru.wikipedia.org/wiki/Test_%28disambiguation%29> )";
			Assert.AreEqual(expected, actual);
		}

		[TestMethod]
		public void MultipleLinks()
		{
			string actual = TestMessage("[[Кот]]о[[пёс]]");
			string expected = @"Ссылки: [[[`Кот`]]]( <https://ru.wikipedia.org/wiki/Кот> ), [[[`Пёс`]]]( <https://ru.wikipedia.org/wiki/Пёс> )";
			Assert.AreEqual(expected, actual);
		}

		[TestMethod]
		public void GenderedUserLinks()
		{
			string actual = TestMessage(@"
				[[user:stjn]]
				[[user:Udacha]]
				[[user:js]]
			");
			string expected = @"Ссылки: [[[`Участник:Stjn`]]]( <https://ru.wikipedia.org/wiki/Участник:Stjn> ), [[[`Участница:Udacha`]]]( <https://ru.wikipedia.org/wiki/Участница:Udacha> ), [[[`Участник:Js`]]]( <https://ru.wikipedia.org/wiki/Участник:Js> )";
			Assert.AreEqual(expected, actual);
		}

		[TestMethod]
		public void TemplateLinks()
		{
			// Basic tests with translations
			Assert.AreEqual(TestMessage(@"
				{{С отвратительным дизайном}}
				{{subst:тест}}
				{{подст:тест 2}}
				{{int:lang}}
				{{подст:int:lang}}
			"), @"Ссылки: [{{`С отвратительным дизайном`}}]( <https://ru.wikipedia.org/wiki/Шаблон:С_отвратительным_дизайном> ), [{{`Тест`}}]( <https://ru.wikipedia.org/wiki/Шаблон:Тест> ), [{{`Тест 2`}}]( <https://ru.wikipedia.org/wiki/Шаблон:Тест_2> ), [{{`MediaWiki:Lang`}}]( <https://ru.wikipedia.org/wiki/MediaWiki:Lang> )");

			// Namespace tests and complications
			Assert.AreEqual(TestMessage(@"
				{{subst:внутр:lang}}
				{{#invoke:Math}}
				{{#вызвать:Math}}
				[[Module:Math]]
				{{:тест}}
			"), @"Ссылки: [{{`MediaWiki:Lang`}}]( <https://ru.wikipedia.org/wiki/MediaWiki:Lang> ), [{{`Модуль:Math`}}]( <https://ru.wikipedia.org/wiki/Модуль:Math> ), [{{`:Тест`}}]( <https://ru.wikipedia.org/wiki/Тест> )");
		}

		[TestMethod]
		public void MagicWordLinks()
		{
			string actual = TestMessage(@"
				// Should return a template name, but currently cannot
				{{pagename}}
				{{SERVERNAME}}
				{{safesubst:SERVERNAME}}
				[[Template:PAGENAME]]
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
			");
			string expected = @"Ссылки: [[[`Шаблон:PAGENAME`]]]( <https://ru.wikipedia.org/wiki/Шаблон:PAGENAME> ), [{{`SERVERNAME: test`}}]( <https://ru.wikipedia.org/wiki/Шаблон:SERVERNAME:_test> ), [{{`Служебная:RecentChanges`}}]( <https://ru.wikipedia.org/wiki/Служебная:RecentChanges> ), [{{`Tag test`}}]( <https://ru.wikipedia.org/wiki/Шаблон:Tag_test> )";
			Assert.AreEqual(expected, actual);
		}

		[TestMethod]
		public void MobileLinks()
		{
			Assert.AreEqual(TestMessage(@"
				[[Тест]]
				[[Заглавная страница]]
				https://ru.m.wikipedia.org/wiki/Заглавная_страница
				https://en.m.wikipedia.org/wiki/Main_Page
			"), @"Ссылки: [[[`Тест`]]]( <https://ru.wikipedia.org/wiki/Тест> ), [[[`Заглавная страница`]]]( <https://ru.wikipedia.org/wiki/Заглавная_страница> )");
		}

		[TestMethod]
		public void NamespaceAliasLink()
		{
			string actual = TestMessage("[[ВП:Страшное место]]");
			string expected = @"Ссылка: [[[`Википедия:Страшное место`]]]( <https://ru.wikipedia.org/wiki/Википедия:Страшное_место> )";
			Assert.AreEqual(expected, actual);
		}

		[TestMethod]
		public void NonCapitalisedLinks()
		{
			string actual = TestMessage("[[wikt:пёс]] [[wikt:mediawiki:common.js]]");
			string expected = @"Ссылки: [[[`wikt:пёс`]]]( <https://ru.wiktionary.org/wiki/пёс> ), [[[`wikt:MediaWiki:Common.js`]]]( <https://ru.wiktionary.org/wiki/MediaWiki:Common.js> )";
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
			string expected = @"Ссылки: [[[`en:Wikipedia:Sandbox`]]]( <https://en.wikipedia.org/wiki/Wikipedia:Sandbox> ), [[[`fr:Wikipédia:Accueil principal`]]]( <https://fr.wikipedia.org/wiki/Wikipédia:Accueil_principal> ), [{{`De:test`}}]( <https://ru.wikipedia.org/wiki/Шаблон:De:test> )";
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
			string expected = @"Ссылки: [[[`toollabs:guc`]]]( <https://iw.toolforge.org/guc> ), [[[`google:`]]]( <https://www.google.com/search?q=> ), [[[`google:lmgtfy`]]]( <https://www.google.com/search?q=lmgtfy> )";
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
			string expected = @"Ссылки: [[[`en:wikt:test`]]]( <https://en.wiktionary.org/wiki/test> ), [[[`en:wikt:MediaWiki:Common.js`]]]( <https://en.wiktionary.org/wiki/MediaWiki:Common.js> ), [[[`google:de:test`]]]( <https://www.google.com/search?q=de:test> )";
			Assert.AreEqual(expected, actual);
		}

		[TestMethod]
		public void EmojiInterwikiLink()
		{
			string actual = TestMessage("[[<:meta:873203055804436513>Discord]]");
			string expected = @"Ссылка: [[[`meta:Discord`]]]( <https://meta.wikimedia.org/wiki/Discord> )";
			Assert.AreEqual(expected, actual);
		}

		[TestMethod]
		public void WeirdlyAdjacentLinks()
		{
			string actual = TestMessage(@"
				[[[каждый]]
				[[охотник]]]
				{{желает}}}
				{{{знать}}
				{{{где}}}
				{{{{сидит}}}}
				{{{{{{фазан}}
			");
			string expected = @"Ссылки: [[[`Каждый`]]]( <https://ru.wikipedia.org/wiki/Каждый> ), [[[`Охотник`]]]( <https://ru.wikipedia.org/wiki/Охотник> ), [{{`Желает`}}]( <https://ru.wikipedia.org/wiki/Шаблон:Желает> ), [{{`Знать`}}]( <https://ru.wikipedia.org/wiki/Шаблон:Знать> ), [{{`Фазан`}}]( <https://ru.wikipedia.org/wiki/Шаблон:Фазан> )";
			Assert.AreEqual(expected, actual);
		}

		[TestMethod]
		public void WeirdLinks()
		{
			string actual1 = TestMessage("[[:Test#One%20Two]] [[Tam&#x200F;zin]]");
			string expected1 = @"Ссылки: [[[`Test#One Two`]]]( <https://ru.wikipedia.org/wiki/Test#One_Two> ), [[[`Tamzin`]]]( <https://ru.wikipedia.org/wiki/Tamzin> )";
			Assert.AreEqual(expected1, actual1);

			string actual2 = TestMessage("[[File:Rainbow lorikeet.jpg%20]] {{)!}}");
			string expected2 = @"Ссылки: [[[`Файл:Rainbow lorikeet.jpg`]]]( <https://ru.wikipedia.org/wiki/Файл:Rainbow_lorikeet.jpg> ), [{{`)!`}}]( <https://ru.wikipedia.org/wiki/Шаблон:%29!> )";
			Assert.AreEqual(expected2, actual2);

			string actual3 = TestMessage(@"
				[[wikt::Gift]]
				[[:ja : おちんちん]]
				[[: Википедия:ЗЛВ]]
				[[Википедия: ЗЛВ]]
				{{ int: mainpage }}
			");
			string expected3 = @"Ссылки: [[[`wikt:Gift`]]]( <https://ru.wiktionary.org/wiki/Gift> ), [[[`ja:おちんちん`]]]( <https://ja.wikipedia.org/wiki/おちんちん> ), [[[`Википедия:ЗЛВ`]]]( <https://ru.wikipedia.org/wiki/Википедия:ЗЛВ> ), [{{`MediaWiki:Mainpage`}}]( <https://ru.wikipedia.org/wiki/MediaWiki:Mainpage> )";
			Assert.AreEqual(expected3, actual3);

			string actual4 = TestMessage(@"
				[[foo|bar|baz]]
				[[nested|[pipes]]]
				[[Special:Search/insource:/Спецыялізуецца ў \./]]
			");
			string expected4 = @"Ссылки: [[[`Foo`]]]( <https://ru.wikipedia.org/wiki/Foo> ), [[[`Nested`]]]( <https://ru.wikipedia.org/wiki/Nested> ), [[[`Служебная:Search/insource:/Спецыялізуецца ў \./`]]]( <https://ru.wikipedia.org/wiki/Служебная:Search/insource:/Спецыялізуецца_ў_%5C./> )";
			Assert.AreEqual(expected4, actual4);
		}

		[TestMethod]
		public void InvalidLinks()
		{
			string actual = TestMessage(@"
				[[ ]]
				{{ }}
				[[#top]]
				[[https://ru.wikipedia.org/wiki/Ithappens]]
				[[<test>]]
				[[ВП:]]
				[[Eh bien, mon prince. Gênes et Lucques ne sont plus que des apanages, des поместья, de la famille Buonaparte. Non, je vous préviens que si vous ne me dites pas que nous avons la guerre, si vous vous permettez encore de pallier toutes les infamies, toutes les atrocités de cet Antichrist (ma parole, j'y crois) — je ne vous connais plus, vous n'êtes plus mon ami, vous n'êtes plus мой верный раб, comme vous dites]]
			");
			Assert.AreEqual("", actual);
		}

		[TestMethod]
		public void LinksInIgnoredBlocks()
		{
			// Code blocks
			Assert.AreEqual("", TestMessage(@"
				`[[one-liner]]` ``[[double]]``
				`[[multiline 1]]
				[[multiline 2]]`

				``[[double multiline 1]]
				[[double multiline 2]]``

				<nowiki>[[test]]</nowiki>
				<nowiki>
				[[nowiki]]
				[[nowiki 2]]
				</nowiki>

				```
				[[code block]]
				```
			"));

			// Quotes
			Assert.AreEqual("", TestMessage(@"
> quote block example [[test]]

>>> test
[[ignore everything after triple quote block]]
			"));

			// Other
			Assert.AreEqual("", TestMessage(@"
				[example [[link text]] example](https://google.com/)
			"));
		}

		[TestMethod]
		public void LinksInSpoilerBlocks()
		{
			string actual1 = TestMessage("||[[Test]]||");
			string expected1 = @"Ссылка: ||[[[`Test`]]]( <https://ru.wikipedia.org/wiki/Test> )||";
			Assert.AreEqual(expected1, actual1);

			string actual2 = TestMessage("[[A]] ||[[B]] [[C]]|| [[D]]");
			string expected2 = @"Ссылки: [[[`A`]]]( <https://ru.wikipedia.org/wiki/A> ), ||[[[`B`]]]( <https://ru.wikipedia.org/wiki/B> )||, ||[[[`C`]]]( <https://ru.wikipedia.org/wiki/C> )||, [[[`D`]]]( <https://ru.wikipedia.org/wiki/D> )";
			Assert.AreEqual(expected2, actual2);
		}

		private static string TestMessage(string str)
		{
			return Linking.PrepareMessage(str, "ru", "https://ru.wikipedia.org/wiki/$1");
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
