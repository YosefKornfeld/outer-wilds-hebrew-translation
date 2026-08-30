using HarmonyLib;
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

		// Ship / suit / cockpit notification panels (the "Flashlight ON" / "Autopilot aborted"
		// lines) render through NotificationDisplayTextLayout, which clones Text items from
		// _textDisplayTemplate into _textItemPool and reuses them for every posted line.
		// The template's font is baked in at the prefab and never routed through
		// TextTranslation.GetFont, so LU's language-font swap never reaches those clones and
		// Hebrew renders as missing-glyph tofu. ExpandPool is where the pool grows, so
		// postfixing it lets us re-stamp the font on both the template and every existing
		// pooled item — the initial pool that Awake built, plus anything ExpandPool just
		// added — which covers every clone the panel will ever show.
		[HarmonyPostfix]
		[HarmonyPatch(typeof(NotificationDisplayTextLayout), nameof(NotificationDisplayTextLayout.ExpandPool))]
		public static void NotificationDisplayTextLayout_ExpandPool(NotificationDisplayTextLayout __instance)
		{
			var font = TextTranslation.GetFont(false);
			if (font == null) return;

			if (__instance._textDisplayTemplate != null)
			{
				foreach (var text in __instance._textDisplayTemplate.GetComponentsInChildren<Text>(true))
					text.font = font;
			}

			if (__instance._textItemPool == null) return;
			foreach (var item in __instance._textItemPool)
			{
				if (item == null) continue;
				foreach (var text in item.GetComponentsInChildren<Text>(true))
					text.font = font;
			}
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
	}
}
