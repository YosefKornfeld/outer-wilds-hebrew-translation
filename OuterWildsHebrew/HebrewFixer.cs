using System;
using System.Collections.Generic;
using System.Text;

namespace OuterWildsHebrew
{
	/// <summary>
	/// Outer Wilds draws text with Unity's legacy font system, which lays glyphs out in the
	/// order they appear in the string and has no bidirectional support at all. Unicode
	/// direction marks (RLE/PDF/RLM) do nothing here, so the only way to get readable Hebrew
	/// is to reorder the text ourselves and hand the game the result already in *visual*
	/// order: drawn left to right, it reads right to left.
	///
	/// LocalizationUtility calls the fixer once per entry while the XML is being loaded, so
	/// everything here runs at startup and never per frame.
	///
	/// This is a cut-down Unicode Bidirectional Algorithm: two embedding levels (a right to
	/// left base with left to right islands for Latin and numbers), neutral resolution,
	/// bracket mirroring, and rich text tags carried through as atomic units.
	/// </summary>
	public static class HebrewFixer
	{
		/// <summary>
		/// Visible characters allowed per line before we break it ourselves. The game
		/// word-wraps *after* the fixer has run, and wrapping text that is already in visual
		/// order makes the lines come out bottom-to-top, so long entries have to be broken
		/// up here instead.
		///
		/// 0 leaves all wrapping to the game, which is only correct for text that never
		/// wraps. If long dialogue reads bottom-to-top in game, set this to roughly the
		/// number of characters that fit across a dialogue box (start around 42) and tune it.
		/// </summary>
		public static int MaxLineLength = 42;

		public static string Fix(string text)
		{
			// Untranslated entries are still English. Reordering those would only break them.
			if (string.IsNullOrEmpty(text) || !ContainsHebrew(text)) return text;

			var result = new StringBuilder(text.Length + 16);
			var paragraphs = text.Replace("\r\n", "\n").Split('\n');

			for (int p = 0; p < paragraphs.Length; p++)
			{
				if (p > 0) result.Append('\n');

				// Every display line is reordered on its own, the same way a real bidi
				// implementation treats each wrapped line separately.
				var lines = MaxLineLength > 0
					? WrapLogical(paragraphs[p], MaxLineLength)
					: new List<string> { paragraphs[p] };

				for (int l = 0; l < lines.Count; l++)
				{
					if (l > 0) result.Append('\n');
					result.Append(FixLine(lines[l]));
				}
			}

			return result.ToString();
		}

		#region reordering

		// Level 1 is the right to left base direction, level 2 a left to right island in it.
		private const int RtlLevel = 1;
		private const int LtrLevel = 2;
		private const int Neutral = 0;

		private class Item
		{
			public string Text;
			public int Level;
			public bool IsTag;
			public bool IsClosingTag;
			public Item Partner;
		}

		private static string FixLine(string line)
		{
			var items = Tokenize(line);
			ResolveNeutrals(items);
			Reorder(items);

			var emit = ResolveTagText(items);

			var sb = new StringBuilder(line.Length + 8);
			for (int i = 0; i < items.Count; i++)
			{
				if (items[i].IsTag) sb.Append(emit[i]);
				else if (items[i].Level == RtlLevel) AppendReversed(sb, items[i].Text);
				else sb.Append(items[i].Text);
			}
			return sb.ToString();
		}

		/// <summary>
		/// Splits a line into rich text tags and runs of a single direction, and matches
		/// opening tags with the closing tags they belong to.
		/// </summary>
		private static List<Item> Tokenize(string line)
		{
			var items = new List<Item>();
			var open = new Stack<Item>();
			var run = new StringBuilder();
			int runLevel = Neutral;

			for (int i = 0; i < line.Length; i++)
			{
				if (line[i] == '<')
				{
					int end = FindTagEnd(line, i);
					if (end > i)
					{
						FlushRun(items, run, ref runLevel);
						var tag = new Item
						{
							Text = line.Substring(i, end - i + 1),
							Level = Neutral,
							IsTag = true
						};
						items.Add(tag);
						PairTag(tag, open);
						i = end;
						continue;
					}
				}

				int level = ClassifyChar(line[i]);
				if (run.Length > 0 && level != runLevel) FlushRun(items, run, ref runLevel);
				runLevel = level;
				run.Append(line[i]);
			}

			FlushRun(items, run, ref runLevel);
			return items;
		}

		private static void FlushRun(List<Item> items, StringBuilder run, ref int level)
		{
			if (run.Length == 0) return;
			items.Add(new Item { Text = run.ToString(), Level = level });
			run.Length = 0;
			level = Neutral;
		}

		/// <summary>
		/// Index of the '&gt;' closing a tag that starts at <paramref name="start"/>, or -1
		/// when this '&lt;' is a stray character rather than the start of markup.
		/// </summary>
		private static int FindTagEnd(string line, int start)
		{
			for (int i = start + 1; i < line.Length; i++)
			{
				if (line[i] == '<') return -1;
				if (line[i] == '>') return i;
			}
			return -1;
		}

		private static void PairTag(Item tag, Stack<Item> open)
		{
			string inner = Inner(tag.Text);
			// <Pause/> and the substitution tokens like <TimeMinutes> never pair up.
			if (inner.Length == 0 || inner.EndsWith("/")) return;

			if (inner[0] == '/')
			{
				tag.IsClosingTag = true;
				var popped = new List<Item>();
				while (open.Count > 0)
				{
					var candidate = open.Pop();
					if (string.Equals(TagName(candidate.Text), TagName(tag.Text), StringComparison.OrdinalIgnoreCase))
					{
						candidate.Partner = tag;
						tag.Partner = candidate;
						return; // whatever we popped past was never closed, e.g. <TimeMinutes>
					}
					popped.Add(candidate);
				}
				for (int i = popped.Count - 1; i >= 0; i--) open.Push(popped[i]);
			}
			else
			{
				open.Push(tag);
			}
		}

		private static string Inner(string tag)
		{
			return tag.Substring(1, tag.Length - 2).Trim();
		}

		private static string TagName(string tag)
		{
			string inner = Inner(tag).TrimStart('/');
			for (int i = 0; i < inner.Length; i++)
			{
				char c = inner[i];
				if (c == '=' || c == ' ' || c == '/') return inner.Substring(0, i);
			}
			return inner;
		}

		/// <summary>
		/// Gives every neutral run and every tag a direction: the surrounding direction when
		/// both sides agree, and the right to left base direction otherwise.
		/// </summary>
		private static void ResolveNeutrals(List<Item> items)
		{
			for (int i = 0; i < items.Count; i++)
			{
				if (!items[i].IsTag && items[i].Level != Neutral) continue;

				int before = Neutral, after = Neutral;
				for (int j = i - 1; j >= 0; j--)
				{
					if (!items[j].IsTag && items[j].Level != Neutral) { before = items[j].Level; break; }
				}
				for (int j = i + 1; j < items.Count; j++)
				{
					if (!items[j].IsTag && items[j].Level != Neutral) { after = items[j].Level; break; }
				}

				items[i].Level = (before != Neutral && before == after) ? before : RtlLevel;
			}

			// Brackets face the other way once the text around them has been reversed.
			foreach (var item in items)
			{
				if (!item.IsTag && item.Level == RtlLevel) item.Text = Mirror(item.Text);
			}
		}

		/// <summary>
		/// Unicode rule L2: from the highest level down to the lowest odd level, reverse each
		/// contiguous run of items at that level or above. Two passes, since we only ever
		/// produce levels 1 and 2.
		/// </summary>
		private static void Reorder(List<Item> items)
		{
			for (int level = LtrLevel; level >= RtlLevel; level--)
			{
				int start = -1;
				for (int i = 0; i <= items.Count; i++)
				{
					bool inRun = i < items.Count && items[i].Level >= level;
					if (inRun)
					{
						if (start < 0) start = i;
					}
					else if (start >= 0)
					{
						items.Reverse(start, i - start);
						start = -1;
					}
				}
			}
		}

		/// <summary>
		/// Decides the literal text each tag should be written as. Reordering can leave a
		/// pair with the closing tag on the left, which would produce malformed markup, so
		/// those pairs have their text swapped. Pairs that came back around to their original
		/// order are left alone.
		/// </summary>
		private static string[] ResolveTagText(List<Item> items)
		{
			var emit = new string[items.Count];
			var index = new Dictionary<Item, int>(items.Count);
			for (int i = 0; i < items.Count; i++) index[items[i]] = i;

			for (int i = 0; i < items.Count; i++)
			{
				var item = items[i];
				if (!item.IsTag) continue;

				int partnerIndex;
				if (item.Partner == null || !index.TryGetValue(item.Partner, out partnerIndex))
				{
					emit[i] = item.Text;
					continue;
				}

				bool flipped = item.IsClosingTag ? partnerIndex > i : partnerIndex < i;
				emit[i] = flipped ? item.Partner.Text : item.Text;
			}

			return emit;
		}

		#endregion

		#region wrapping

		/// <summary>
		/// Greedy word wrap over the logical (not yet reordered) text, counting only the
		/// characters a player actually sees so markup does not eat into the line budget.
		/// </summary>
		private static List<string> WrapLogical(string line, int max)
		{
			var lines = new List<string>();
			int start = 0, visible = 0, lastSpace = -1, visibleAtSpace = 0;

			for (int i = 0; i < line.Length; i++)
			{
				if (line[i] == '<')
				{
					int end = FindTagEnd(line, i);
					if (end > i) { i = end; continue; }
				}

				if (line[i] == ' ')
				{
					lastSpace = i;
					visibleAtSpace = visible;
				}
				visible++;

				if (visible > max && lastSpace > start)
				{
					lines.Add(line.Substring(start, lastSpace - start));
					start = lastSpace + 1;
					visible -= visibleAtSpace + 1;
					lastSpace = -1;
				}
			}

			lines.Add(line.Substring(start));
			return lines;
		}

		#endregion

		#region characters

		private static int ClassifyChar(char c)
		{
			if (IsHebrew(c)) return RtlLevel;
			if (c >= '؀' && c <= 'ࣿ') return RtlLevel; // Arabic, should it turn up
			if ((c >= 'a' && c <= 'z') || (c >= 'A' && c <= 'Z')) return LtrLevel;
			if (c >= 'À' && c <= 'ɏ') return LtrLevel; // accented Latin
			if (c >= '0' && c <= '9') return LtrLevel; // numbers still read left to right
			return Neutral;
		}

		private static bool IsHebrew(char c)
		{
			return (c >= '֐' && c <= '׿') || (c >= 'יִ' && c <= 'ﭏ');
		}

		private static bool ContainsHebrew(string text)
		{
			foreach (char c in text)
			{
				if (IsHebrew(c)) return true;
			}
			return false;
		}

		private static string Mirror(string text)
		{
			char[] chars = null;
			for (int i = 0; i < text.Length; i++)
			{
				char mirrored = MirrorChar(text[i]);
				if (mirrored == text[i]) continue;
				if (chars == null) chars = text.ToCharArray();
				chars[i] = mirrored;
			}
			return chars == null ? text : new string(chars);
		}

		private static char MirrorChar(char c)
		{
			switch (c)
			{
				case '(': return ')';
				case ')': return '(';
				case '[': return ']';
				case ']': return '[';
				case '{': return '}';
				case '}': return '{';
				case '«': return '»'; // guillemets
				case '»': return '«';
				default: return c;
			}
		}

		private static void AppendReversed(StringBuilder sb, string text)
		{
			for (int i = text.Length - 1; i >= 0; i--) sb.Append(text[i]);
		}

		#endregion
	}
}
