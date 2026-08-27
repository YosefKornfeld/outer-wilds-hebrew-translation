using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace OuterWildsHebrew
{
	/// <summary>
	/// The game's rich text tags are Latin script, so writing them inside a Hebrew line means
	/// dropping a left to right island into a right to left paragraph. In an editor that makes
	/// the caret jump around and the tag land somewhere other than where it looked like it
	/// would, which is miserable to author. This compiler lets the whole translation be typed
	/// in Hebrew: markers delimited by three final tsadi characters are rewritten into the real
	/// tags before anything else sees the string.
	///
	///   ץץץצבע כחול בהירץץץאטלרוקץץץצבע סוףץץץ  ->  &lt;color=lightblue&gt;אטלרוק&lt;/color&gt;
	///
	/// LocalizationUtility hands every value to the registered fixer as the XML loads, so this
	/// runs once per entry at startup, in front of <see cref="HebrewFixer"/>. Compiling first
	/// matters: the fixer already knows how to carry &lt;...&gt; tags through reordering as
	/// atomic units, and it cannot do that for markers it does not recognise.
	/// </summary>
	public static class MarkupCompiler
	{
		/// <summary>
		/// Reports a marker that could not be compiled. Wired to the OWML console by
		/// <see cref="OuterWildsHebrew.Start"/>; a no-op by default so the compiler stays
		/// usable outside the game.
		/// </summary>
		public static Action<string> LogError = _ => { };

		// Final tsadi. Chosen because a run of three can never occur in real Hebrew, so the
		// marker cannot collide with prose. See SplitRun for the one subtlety this creates.
		private const char Marker = 'ץ';
		private const int MarkerLength = 3;

		// The argument that turns any element into its closing tag: ץץץנטוי סוףץץץ -> </i>
		private const string CloseArgument = "סוף";

		// A textual stand-in for a real line break, so a translator can put paragraph breaks
		// inside a single XML line without having to insert a literal newline that would then
		// upset the RTL flow of the file itself. HebrewFixer already treats each \n line
		// separately, so an escape here is exactly what wraps into two properly reordered
		// paragraphs in game.
		private const string NewlineEscape = "//נ";

		public static string Compile(string text)
		{
			if (string.IsNullOrEmpty(text)) return text;

			// Newline escapes are substituted first, so a marker cannot accidentally straddle
			// what becomes a line break, and so the marker scan sees the same layout the
			// player will see.
			if (text.IndexOf(NewlineEscape, StringComparison.Ordinal) >= 0)
				text = text.Replace(NewlineEscape, "\n");

			if (text.IndexOf(Marker) < 0) return text;

			var output = new StringBuilder(text.Length);
			var token = new StringBuilder(32);
			bool inToken = false;

			for (int i = 0; i < text.Length; i++)
			{
				if (text[i] != Marker)
				{
					(inToken ? token : output).Append(text[i]);
					continue;
				}

				int run = RunLength(text, i);
				int literals, markers;
				SplitRun(run, out literals, out markers);

				// Any literal tsadi sits in front of the markers, so it belongs to whichever
				// buffer we are filling before the markers switch us over.
				var target = inToken ? token : output;
				for (int n = 0; n < literals; n++) target.Append(Marker);

				for (int n = 0; n < markers; n++)
				{
					if (inToken) CloseToken(output, token);
					else token.Length = 0;
					inToken = !inToken;
				}

				i += run - 1;
			}

			if (inToken)
			{
				// An odd number of markers: the last one never closed. Put it back exactly as
				// written so the mistake is visible in game rather than swallowing the rest of
				// the line.
				LogError("Unterminated marker in: " + Excerpt(text));
				output.Append(Marker, MarkerLength).Append(token);
			}

			return output.ToString();
		}

		#region scanning

		private static int RunLength(string text, int start)
		{
			int i = start;
			while (i < text.Length && text[i] == Marker) i++;
			return i - start;
		}

		/// <summary>
		/// Splits a run of tsadi into the literal characters that belong to the text and the
		/// markers that delimit a token.
		///
		/// Final tsadi is a real letter and ends common words (ארץ, עץ, חוץ), so a word butted
		/// straight against a marker produces a run of four and a naive replace would consume
		/// the wrong three. The split is still unambiguous: Hebrew never doubles a final letter
		/// and never starts a word with one, so a literal tsadi can only ever *precede* a
		/// marker, never follow one. That leaves at most run % 3 literals, and they come first.
		/// </summary>
		private static void SplitRun(int run, out int literals, out int markers)
		{
			literals = run % MarkerLength;
			markers = run / MarkerLength;
		}

		private static void CloseToken(StringBuilder output, StringBuilder token)
		{
			string raw = token.ToString();
			string tag = Resolve(Normalize(raw));

			if (tag != null)
			{
				output.Append(tag);
				return;
			}

			// Unknown markers are handed back verbatim rather than dropped: a typo that
			// silently vanished would be far harder to notice than one sitting in the dialogue.
			LogError("Unknown marker: " + Excerpt(raw));
			output.Append(Marker, MarkerLength).Append(raw).Append(Marker, MarkerLength);
		}

		/// <summary>Trims the token and collapses inner whitespace, so ץץץ  צבע   כתום ץץץ still resolves.</summary>
		private static string Normalize(string raw)
		{
			var sb = new StringBuilder(raw.Length);
			bool pendingSpace = false;

			foreach (char c in raw)
			{
				if (char.IsWhiteSpace(c))
				{
					pendingSpace = sb.Length > 0;
					continue;
				}
				if (pendingSpace) { sb.Append(' '); pendingSpace = false; }
				sb.Append(c);
			}

			return sb.ToString();
		}

		private static string Excerpt(string text)
		{
			string flat = Normalize(text);
			return flat.Length <= 60 ? flat : flat.Substring(0, 60) + "…";
		}

		#endregion

		#region vocabulary

		private enum ArgumentKind
		{
			None,   // <i>, <b>
			Colour, // a name from Colours, or a #rrggbb literal
			Number  // any number the translator writes
		}

		private class Element
		{
			public string Name;         // what it is called in the tag
			public ArgumentKind Kind;
			public bool AllowsBare;     // may be written with no argument at all
			public string BareForm;     // null means "<Name>"
			public bool AllowsClose;    // may be written with the סוף argument
		}

		/// <summary>
		/// Tags that take an argument. Adding one is a single entry here plus, for colours, one
		/// in <see cref="Colours"/>.
		/// </summary>
		private static readonly Dictionary<string, Element> Elements = new Dictionary<string, Element>
		{
			["צבע"] = new Element { Name = "color", Kind = ArgumentKind.Colour, AllowsClose = true },
			["גודל"] = new Element { Name = "size", Kind = ArgumentKind.Number, AllowsClose = true },
			["נטוי"] = new Element { Name = "i", Kind = ArgumentKind.None, AllowsBare = true, AllowsClose = true },
			["מודגש"] = new Element { Name = "b", Kind = ArgumentKind.None, AllowsBare = true, AllowsClose = true },
			// The stock file writes this as <Pause>, <Pause/>, <Pause /> and <pause>
			// interchangeably; everything we emit uses the one canonical form.
			["השהיה"] = new Element { Name = "Pause", Kind = ArgumentKind.Number, AllowsBare = true, BareForm = "<Pause/>" },
		};

		private static readonly Dictionary<string, string> Colours = new Dictionary<string, string>
		{
			["כתום"] = "orange",
			["כחול בהיר"] = "lightblue",
			["אפור"] = "grey",
			["אדום"] = "red",
			["שחור"] = "black",
		};

		/// <summary>
		/// Placeholders the game substitutes at runtime. These carry no styling and take no
		/// argument, so they are matched whole.
		/// </summary>
		private static readonly Dictionary<string, string> Atomic = new Dictionary<string, string>
		{
			["דקות"] = "<TimeMinutes>",
			["שניות"] = "<TimeSeconds>",
			["דקות נותרו"] = "<RemainingMinutes>",
			["שניות נותרו"] = "<RemainingSeconds>",
			["זמן דקות נותרו"] = "<TimeMinutesRemaining>",
			["דקות מאז ענק אדום"] = "<MinutesSinceRedGiant>",
			["שניות מאז ענק אדום"] = "<SecondsSinceRedGiant>",
			["דקות עד ענק אדום"] = "<MinutesToRedGiant>",
			["שניות עד ענק אדום"] = "<SecondsToRedGiant>",
			["מספר לולאות"] = "<NbTimeloops>",
			["לולאה ראשונה"] = "<FirstLoop>",
			["שם פרופיל"] = "<Profile Name>",
			["סימן קריאה"] = "<!>",
		};

		/// <summary>The tag a normalized token compiles to, or null when nothing matches it.</summary>
		private static string Resolve(string token)
		{
			if (token.Length == 0) return null;

			// Whole-token first, so multi word placeholders are not mistaken for an element
			// followed by an argument.
			string atomic;
			if (Atomic.TryGetValue(token, out atomic)) return atomic;

			int split = token.IndexOf(' ');
			string head = split < 0 ? token : token.Substring(0, split);
			string argument = split < 0 ? string.Empty : token.Substring(split + 1);

			Element element;
			if (!Elements.TryGetValue(head, out element)) return null;

			if (argument.Length == 0)
				return element.AllowsBare ? (element.BareForm ?? "<" + element.Name + ">") : null;

			if (argument == CloseArgument)
				return element.AllowsClose ? "</" + element.Name + ">" : null;

			return FormatArgument(element, argument);
		}

		private static string FormatArgument(Element element, string argument)
		{
			switch (element.Kind)
			{
				case ArgumentKind.Colour:
					string colour;
					if (Colours.TryGetValue(argument, out colour)) return "<color=" + colour + ">";
					// Hex literals are passed through, the same way numbers are.
					return argument[0] == '#' ? "<color=" + argument + ">" : null;

				case ArgumentKind.Number:
					// Deliberately not checked against the sizes and pauses the stock file
					// happens to use: a translation may want a size or a beat that appears
					// nowhere in the English, or one in an entry that had no such tag at all.
					if (!double.TryParse(argument, NumberStyles.Float, CultureInfo.InvariantCulture, out _)) return null;
					return "<" + element.Name + "=" + argument + ">";

				default:
					return null;
			}
		}

		#endregion
	}
}
