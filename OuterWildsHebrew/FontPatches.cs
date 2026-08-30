using HarmonyLib;
using OWML.Common;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using UnityEngine.UI;

namespace OuterWildsHebrew
{
	/// <summary>
	/// LocalizationUtility swaps the language font on most UI, but a handful of components
	/// keep their prefab font — the Nomai translator, the ship / suit / cockpit notification
	/// panels (Flashlight ON, Autopilot aborted, …) and the signalscope labels. Those texts
	/// then render Hebrew as missing-glyph tofu from the original Latin font (which shows up
	/// as tiny flickering pixels), so we patch each one to install the right font by hand.
	/// </summary>
	[HarmonyPatch]
	internal static class FontPatches
	{
		// The cockpit's own font controller, which owns the console, the ship signalscope and
		// the cockpit HUD labels. Captured so the font scale can be aimed at those and not at
		// the ship log or the menus, which are sized fine as they are.
		private static FontAndLanguageController _cockpitFontController;

		// What one line of a cockpit Text is supposed to occupy, and the scale it started at.
		// Kept per element because the cockpit mixes prefab fonts and sizes.
		private struct CockpitText
		{
			public Vector3 BaseScale;
			public float TargetLineHeight;
		}

		private static readonly Dictionary<Text, CockpitText> CockpitTexts = new Dictionary<Text, CockpitText>();

		// The console's target, remembered so the notification lines — which are clones with no
		// entry of their own in the font controller — can be sized like the rest of the console.
		private static float _consoleTargetLineHeight;
		private static ShipNotificationDisplay _shipConsole;
		private static bool _loggedScale;

		// Our font ignores Text.fontSize: the same string measured 4943x376 units at size 225
		// and at size 34. Its glyphs are baked at a fixed size and drawn at that size whatever
		// the point size says, which is why the cockpit's lines overflow their 40-unit boxes and
		// the console's mask clips them to slivers, and why changing sizes never did anything.
		// Transform scale is the only lever left that the font honours, so scale each line down
		// to the height the prefab's font would have drawn it at.
		private static float TargetLineHeight(Font original, int originalFontSize)
		{
			if (original == null || original.fontSize <= 0 || originalFontSize <= 0) return 0f;
			return originalFontSize * (original.lineHeight / (float)original.fontSize);
		}

		private static void RegisterCockpitText(Text text, float targetLineHeight)
		{
			if (text == null || targetLineHeight <= 0f) return;

			if (!CockpitTexts.ContainsKey(text))
			{
				// A RectTransform scales about its pivot, so a label pivoted somewhere other
				// than where its text begins swings out of its box as it shrinks — which is
				// what threw the signal name above the signalscope screen while the frequency
				// underneath it, pivoted differently, stayed put. Move the pivot to the corner
				// the text is aligned to and the block shrinks into place instead of away.
				MovePivot(text.rectTransform, PivotFor(text.alignment));

				CockpitTexts[text] = new CockpitText
				{
					BaseScale = text.rectTransform.localScale,
					TargetLineHeight = targetLineHeight
				};
			}

			ApplyCockpitTextScale(text);
		}

		// Where the text sits in its box, as a pivot.
		private static Vector2 PivotFor(TextAnchor alignment)
		{
			float x;
			switch (alignment)
			{
				case TextAnchor.UpperLeft:
				case TextAnchor.MiddleLeft:
				case TextAnchor.LowerLeft: x = 0f; break;
				case TextAnchor.UpperRight:
				case TextAnchor.MiddleRight:
				case TextAnchor.LowerRight: x = 1f; break;
				default: x = 0.5f; break;
			}

			float y;
			switch (alignment)
			{
				case TextAnchor.UpperLeft:
				case TextAnchor.UpperCenter:
				case TextAnchor.UpperRight: y = 1f; break;
				case TextAnchor.LowerLeft:
				case TextAnchor.LowerCenter:
				case TextAnchor.LowerRight: y = 0f; break;
				default: y = 0.5f; break;
			}

			return new Vector2(x, y);
		}

		// Changing a pivot moves the element, so shift it back by the same amount to leave the
		// box exactly where the prefab put it.
		private static void MovePivot(RectTransform rectTransform, Vector2 pivot)
		{
			var delta = pivot - rectTransform.pivot;
			if (delta == Vector2.zero) return;

			var size = rectTransform.rect.size;
			rectTransform.pivot = pivot;
			rectTransform.anchoredPosition += new Vector2(delta.x * size.x, delta.y * size.y);
		}

		internal static void ApplyCockpitTextScale(Text text)
		{
			if (text == null || !CockpitTexts.TryGetValue(text, out var info)) return;

			var font = text.font;
			if (font == null || font.lineHeight <= 0f) return;

			var factor = info.TargetLineHeight / font.lineHeight * OuterWildsHebrew.CockpitFontScale;
			if (factor <= 0f) return;

			text.rectTransform.localScale = info.BaseScale * factor;

			if (_loggedScale) return;
			_loggedScale = true;
			// preferredHeight is what the font actually draws, which is not the same as its
			// declared line height — the gap between the two is why the computed factor came
			// out about half of what looked right, and is the number to correct from next.
			OuterWildsHebrew.Instance.ModHelper.Console.WriteLine(
				$"Cockpit text scaled by {factor:F3} (target line {info.TargetLineHeight:F1} " +
				$"vs {font.name} declared line {font.lineHeight:F1}, drawn {text.preferredHeight:F1})",
				MessageType.Success);
		}

		// Re-applies to everything already registered, so moving the slider takes effect without
		// reloading.
		internal static void ReapplyCockpitFontScale()
		{
			foreach (var text in new List<Text>(CockpitTexts.Keys))
			{
				if (text != null) ApplyCockpitTextScale(text);
			}
		}

		// NomaiTranslatorProp.InitializeFont normally installs the language font on the
		// translator's text field. The triple-underscore parameters are Harmony's way of
		// reaching the method's private fields by name.
		[HarmonyPrefix]
		[HarmonyPatch(typeof(NomaiTranslatorProp), nameof(NomaiTranslatorProp.InitializeFont))]
		public static bool NomaiTranslatorProp_InitializeFont(
			ref Font ____fontInUse,
			ref Font ____dynamicFontInUse,
			ref float ____fontSpacingInUse,
			Text ____textField)
		{
			var font = OuterWildsHebrew.Instance.NomaiFont;

			// If the Nomai bundle never loaded, let the game run its own InitializeFont so
			// the translator at least falls back to the UI font instead of nothing.
			if (font == null) return true;

			____fontInUse = font;
			____dynamicFontInUse = font;
			____fontSpacingInUse = TextTranslation.GetDefaultFontSpacing();

			____textField.font = font;
			____textField.lineSpacing = ____fontSpacingInUse;
			return false;
		}

		// The signalscope's on-screen labels are set from a prefab font that LU doesn't
		// touch. Postfix Activate so our font wins over whatever Activate assigned.
		[HarmonyPostfix]
		[HarmonyPatch(typeof(SignalscopeUI), nameof(SignalscopeUI.Activate))]
		public static void SignalscopeUI_Activate(SignalscopeUI __instance)
		{
			var font = TextTranslation.GetFont(false);
			if (font == null) return;

			if (__instance._signalscopeLabel != null) __instance._signalscopeLabel.font = font;
			if (__instance._distanceLabel != null) __instance._distanceLabel.font = font;
		}

		// TextTranslation indexes its font arrays by language, and a mod-registered language
		// sits past the end of those vanilla-sized arrays. GetGameOverFont does the lookup
		// with no bounds check, so for Hebrew it throws IndexOutOfRangeException — and it is
		// called from FontAndLanguageController.InitializeFont, the method that installs the
		// fonts on the ship, suit and cockpit text. The exception aborted InitializeFont part
		// way through, which is why the cockpit console rendered nothing at all rather than
		// tofu: those Text elements were never given a font. Stand in for the missing entry
		// with the UI font, and leave the vanilla path alone when the index is in range.
		[HarmonyPrefix]
		[HarmonyPatch(typeof(TextTranslation), nameof(TextTranslation.GetGameOverFont))]
		public static bool TextTranslation_GetGameOverFont(ref Font __result)
		{
			var table = TextTranslation.Get();
			if (table == null) return true;

			var fonts = table.m_gameOverFonts;
			var language = (int)table.m_language;
			if (fonts != null && language >= 0 && language < fonts.Length) return true;

			__result = TextTranslation.GetFont(false);
			return false;
		}

		// FontAndLanguageController owns the fonts of the ship / suit / cockpit text, and it
		// is also what puts them back: every Text registered with it is stored in a
		// TextContainer alongside its originalFont, and InitializeFont re-applies either the
		// language font or that original, depending on how the prefab flagged the item. The
		// console items are flagged to keep their original (Latin) font, so stamping them
		// anywhere earlier gets undone. This postfix runs after the controller has had its
		// say and forces the Hebrew font onto every Text it manages.
		[HarmonyPostfix]
		[HarmonyPatch(typeof(FontAndLanguageController), nameof(FontAndLanguageController.InitializeFont))]
		public static void FontAndLanguageController_InitializeFont(FontAndLanguageController __instance)
		{
			var font = TextTranslation.GetFont(false);
			if (font == null || __instance._textContainerList == null) return;

			foreach (var container in __instance._textContainerList)
			{
				if (container.textElement == null) continue;
				container.textElement.font = font;

				// The game shrinks the text it manages when the language is not Latin, and
				// LocalizationUtility makes a custom-font language report as non-Latin, so the
				// shrink applies to us. LU undoes it — but only for the elements the prefab
				// flagged as using the language font. The cockpit's are not flagged that way,
				// which is exactly why their font was never swapped either, so nothing restores
				// them and they draw at a fraction of their intended size. Put back what the
				// prefab had, the same way LU does for the elements it covers.
				if (container.isLanguageFont) continue;
				if (container.originalFontSize > 0)
					container.textElement.fontSize = TextTranslation.GetModifiedFontSize(container.originalFontSize);
				container.textElement.rectTransform.localScale = container.originalScale;
			}

			// Only the cockpit's text needs the size correction.
			if (__instance != _cockpitFontController) return;

			// The container remembers the font and size the prefab shipped with, which is what
			// the cockpit's boxes were built around.
			foreach (var container in __instance._textContainerList)
			{
				var target = TargetLineHeight(container.originalFont, container.originalFontSize);
				RegisterCockpitText(container.textElement, target);

				// The console's own measuring field stands in for the notification lines, which
				// are clones the controller never sees.
				if (target > 0f && container.textElement != null && container.textElement.name == "TestText")
					_consoleTargetLineHeight = target;
			}

			// The pool was built during Awake, before the target above could be known, so give
			// those lines their scale now.
			if (_shipConsole != null) StampNotificationFont(_shipConsole);
		}

		// Text elements registered after InitializeFont has already run — pooled notification
		// lines get added as the pool grows — would otherwise keep the prefab font until the
		// next language change, so stamp them as they arrive.
		[HarmonyPostfix]
		[HarmonyPatch(typeof(FontAndLanguageController), nameof(FontAndLanguageController.AddTextElement))]
		public static void FontAndLanguageController_AddTextElement(Text textElement)
		{
			var font = TextTranslation.GetFont(false);
			if (font == null || textElement == null) return;
			textElement.font = font;
		}

		// ShipNotificationDisplay uses _testText to measure whether a candidate line fits
		// the panel width, and the measurement is done in the panel's own font. If we leave
		// it on the Latin font, the width the game computes for Hebrew glyphs won't match
		// what the pooled items actually render, so lines can wrap or truncate wrong.
		[HarmonyPostfix]
		[HarmonyPatch(typeof(ShipNotificationDisplay), nameof(ShipNotificationDisplay.Awake))]
		public static void ShipNotificationDisplay_Awake(ShipNotificationDisplay __instance)
		{
			// The cockpit's controller owns the console, the signalscope and the HUD labels.
			// Grabbing it here means the correction can target exactly those.
			if (__instance._fontController != null) _cockpitFontController = __instance._fontController;
			_shipConsole = __instance;

			var font = TextTranslation.GetFont(false);
			if (font == null || __instance._testText == null) return;
			__instance._testText.font = font;
		}

		// Stamps the Hebrew font on a notification panel's line template and on every item
		// currently in its pool. The template is what future clones are made from and the
		// pool holds the clones that already exist, so between the two every line the panel
		// can show is covered.
		internal static void StampNotificationFont(NotificationDisplayTextLayout display)
		{
			var font = TextTranslation.GetFont(false);
			if (font == null) return;

			// The pooled lines are cloned from the template, so both need the size correction
			// too — but only on the ship, whose display is the one drawing our font too small.
			var scale = display is ShipNotificationDisplay;

			if (display._textDisplayTemplate != null)
			{
				foreach (var text in display._textDisplayTemplate.GetComponentsInChildren<Text>(true))
				{
					text.font = font;
					if (scale) RegisterCockpitText(text, _consoleTargetLineHeight);
				}
			}

			if (display._textItemPool == null) return;
			foreach (var item in display._textItemPool)
			{
				if (item == null) continue;
				foreach (var text in item.GetComponentsInChildren<Text>(true))
				{
					text.font = font;
					if (scale) RegisterCockpitText(text, _consoleTargetLineHeight);
				}
			}
		}
	}

	/// <summary>
	/// The notification panels clone their lines from a prefab template into a pool, and
	/// ExpandPool is where that pool grows. It is virtual, and both ShipNotificationDisplay
	/// and SuitNotificationDisplay override it — Harmony patches one concrete method body, so
	/// a patch on the base class alone never runs for the ship, which is why the cockpit
	/// display stayed unreadable. Patch every declaration instead: the base, which
	/// PlayerCockpitNotificationDisplay inherits as-is, plus each override.
	/// </summary>
	[HarmonyPatch]
	internal static class NotificationPoolFontPatch
	{
		public static IEnumerable<MethodBase> TargetMethods()
		{
			var types = new[]
			{
				typeof(NotificationDisplayTextLayout),
				typeof(ShipNotificationDisplay),
				typeof(SuitNotificationDisplay)
			};

			foreach (var type in types)
			{
				// Looked up by name because only some of these declare their own override.
				// A null here would take Harmony's whole PatchAll down with it, so a game
				// update that renames the method costs us this one patch, not the mod.
				var method = AccessTools.DeclaredMethod(type, "ExpandPool");
				if (method != null) yield return method;
				else OuterWildsHebrew.Instance.ModHelper.Console.WriteLine(
					$"No ExpandPool on {type.Name}; its notification font will not be patched",
					MessageType.Error);
			}
		}

		public static void Postfix(NotificationDisplayTextLayout __instance)
		{
			FontPatches.StampNotificationFont(__instance);
		}
	}
}
