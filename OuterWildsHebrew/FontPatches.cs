using HarmonyLib;
using UnityEngine;
using UnityEngine.UI;

namespace OuterWildsHebrew
{
	/// <summary>
	/// The Nomai translator tool draws its text with its own font, chosen separately from the
	/// rest of the UI. LocalizationUtility's AddLanguageFont only sets one font for the whole
	/// language, so to give the translator a different look we override it here: everything
	/// else keeps the UI font, and the translator alone gets the dedicated Nomai font.
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
	}
}
