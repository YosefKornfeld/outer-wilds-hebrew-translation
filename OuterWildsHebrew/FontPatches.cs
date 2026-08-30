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

			if (display._textDisplayTemplate != null)
			{
				foreach (var text in display._textDisplayTemplate.GetComponentsInChildren<Text>(true))
					text.font = font;
			}

			if (display._textItemPool == null) return;
			foreach (var item in display._textItemPool)
			{
				if (item == null) continue;
				foreach (var text in item.GetComponentsInChildren<Text>(true))
					text.font = font;
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
