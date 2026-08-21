using System;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml.Linq;

namespace OuterWildsHebrew
{
	/// <summary>
	/// Compares each translated value against the English key it replaces and reports tags that
	/// went missing or changed along the way.
	///
	/// Some tags are placeholders the game fills in at runtime, so dropping one costs nothing at
	/// load time and shows up only when that exact line happens to display, possibly deep in a
	/// loop and possibly never during testing. The fixer cannot catch this because it is handed
	/// values one at a time and never sees the key, so this is a separate read only pass.
	/// </summary>
	public static class TranslationValidator
	{
		private static readonly Regex TagPattern = new Regex(@"<[^<>]*>", RegexOptions.Compiled);

		/// <summary>
		/// Tags the translation is free to place wherever it likes. Sizes and pauses are
		/// typographic judgement calls, and a Hebrew line may well want a beat or a size the
		/// English never had, so comparing them against the key would only produce noise.
		/// </summary>
		private static readonly HashSet<string> Unchecked =
			new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "size", "pause" };

		public static void Validate(string xmlPath, Action<string> log)
		{
			var document = XDocument.Load(xmlPath);

			foreach (var entry in document.Descendants())
			{
				var key = entry.Element("key");
				var value = entry.Element("value");
				if (key == null || value == null) continue;

				string translated = value.Value;

				// Most entries are still verbatim English copies waiting to be translated.
				// Checking those compares a string against itself and would bury the real
				// findings in thousands of trivially passing lines.
				if (!ContainsHebrew(translated)) continue;

				Report(key.Value, MarkupCompiler.Compile(translated), log);
			}
		}

		private static void Report(string key, string compiled, Action<string> log)
		{
			// Tags are compared in a normalized form but reported as they were actually
			// written, which is what the translator has to go and find in the file.
			var written = new Dictionary<string, string>();
			var expected = CountTags(key, written);
			var actual = CountTags(compiled, written);

			var missing = Difference(expected, actual, written);
			var extra = Difference(actual, expected, written);
			if (missing.Count == 0 && extra.Count == 0) return;

			var message = new StringBuilder();
			message.Append("[Hebrew] ").Append(Excerpt(key));
			if (missing.Count > 0) message.Append("\n    missing from translation: ").Append(string.Join(" ", missing.ToArray()));
			if (extra.Count > 0) message.Append("\n    not in the English: ").Append(string.Join(" ", extra.ToArray()));
			log(message.ToString());
		}

		/// <summary>
		/// Tags of <paramref name="from"/> that <paramref name="to"/> does not account for,
		/// listed once per surplus occurrence so a tag used twice where the English used it
		/// once still shows up.
		/// </summary>
		private static List<string> Difference(Dictionary<string, int> from, Dictionary<string, int> to, Dictionary<string, string> written)
		{
			var result = new List<string>();
			foreach (var pair in from)
			{
				int other;
				to.TryGetValue(pair.Key, out other);

				string display;
				if (!written.TryGetValue(pair.Key, out display)) display = pair.Key;

				for (int i = other; i < pair.Value; i++) result.Add(display);
			}
			return result;
		}

		/// <summary>
		/// How many times each checked tag appears, in a normalized form so that the same tag
		/// written differently on the two sides still matches.
		/// </summary>
		private static Dictionary<string, int> CountTags(string text, Dictionary<string, string> written)
		{
			var counts = new Dictionary<string, int>();

			foreach (Match match in TagPattern.Matches(text))
			{
				string tag = Normalize(match.Value);
				if (tag == null) continue;

				if (!written.ContainsKey(tag)) written[tag] = match.Value;

				int seen;
				counts.TryGetValue(tag, out seen);
				counts[tag] = seen + 1;
			}

			return counts;
		}

		/// <summary>
		/// A tag reduced to its comparable form, or null when it is one we do not check.
		/// Case and stray whitespace are ignored: the stock file contains a &lt;Color=...&gt;
		/// alongside its usual &lt;color=...&gt;, and writes &lt;Pause /&gt; with a space.
		/// </summary>
		private static string Normalize(string tag)
		{
			string inner = tag.Substring(1, tag.Length - 2).Trim();
			if (inner.Length == 0) return null;

			bool closing = inner[0] == '/';
			string body = inner.TrimStart('/').Trim();
			if (body.Length == 0) return null;

			int end = body.IndexOfAny(new[] { '=', ' ', '/' });
			string name = end < 0 ? body : body.Substring(0, end);
			if (Unchecked.Contains(name)) return null;

			return "<" + (closing ? "/" : "") + body.ToLowerInvariant().TrimEnd('/').Trim() + ">";
		}

		private static bool ContainsHebrew(string text)
		{
			foreach (char c in text)
			{
				// Hebrew block, plus the presentation forms HebrewFixer also recognises.
				if ((c >= '\u0590' && c <= '\u05FF') || (c >= '\uFB1D' && c <= '\uFB4F')) return true;
			}
			return false;
		}

		private static string Excerpt(string text)
		{
			string flat = string.Join(" ", text.Split((char[])null, StringSplitOptions.RemoveEmptyEntries));
			return "\"" + (flat.Length <= 60 ? flat : flat.Substring(0, 60) + "…") + "\"";
		}
	}
}
