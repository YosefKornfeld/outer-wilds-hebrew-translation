using System.Collections;
using HarmonyLib;
using UnityEngine;
using UnityEngine.UI;

namespace OuterWildsHebrew
{
	/// <summary>
	/// LocalizationUtility swaps the language font on most UI, but a handful of components
	/// keep their prefab font — the Nomai translator, the ship cockpit console that shows
	/// "Flashlight ON" / "Autopilot aborted", and the signalscope labels. Those texts then
	/// render Hebrew as missing-glyph tofu from the original Latin font (which shows up as
	/// tiny flickering pixels), so we patch each one to install the right font by hand.
	///
	/// This class also lets each component use a different font from the general UI when the
	/// user configures a component-specific bundle (see FontManager). When no override is
	/// configured, every component falls back to the UI font and behaves as before.
	/// </summary>
	[HarmonyPatch]
	internal static class FontPatches
	{
		// Path to the LayoutGroup that ConsoleDisplay parents its notification lines under.
		// The template GameObject is a Text prefab that gets cloned every time a new line
		// appears, so patching the template fixes future lines and iterating existing
		// clones fixes the ones the cockpit spawned before we arrived.
		private const string ConsoleLayoutPath =
			"Ship_Body/Module_Cockpit/Systems_Cockpit/ShipCockpitUI/CockpitCanvases/ShipWorldSpaceUI/ConsoleDisplay/Mask/LayoutGroup";

		private static FontManager Fonts => OuterWildsHebrew.Instance.Fonts;

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
			var font = Fonts.NomaiFont;

			// If neither the Nomai bundle nor the UI font is available, let the game run its
			// own InitializeFont so the translator at least falls back to whatever it can.
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
			var font = Fonts.ShipUiFont;
			if (font == null) return;

			if (__instance._signalscopeLabel != null) __instance._signalscopeLabel.font = font;
			if (__instance._distanceLabel != null) __instance._distanceLabel.font = font;
		}

		// Character dialog boxes are prefab clones that persist through the whole scene, so
		// we run one sweep per SolarSystem load rather than hooking each conversation. Any
		// Text component under a DialogueBoxVer2 gets the dialog font; inactive boxes are
		// included because most conversation UIs are activated on demand.
		public static void ApplyDialogFont()
		{
			var font = Fonts.DialogFont;
			if (font == null) return;

			var boxes = Resources.FindObjectsOfTypeAll<DialogueBoxVer2>();
			foreach (var box in boxes)
			{
				if (box == null) continue;
				var texts = box.GetComponentsInChildren<Text>(true);
				foreach (var text in texts)
				{
					if (text != null) text.font = font;
				}
			}
		}

		// Called from OuterWildsHebrew.OnCompleteSceneLoad once the solar system scene
		// starts loading in. The cockpit isn't guaranteed to exist the instant the scene
		// callback fires, so we poll for the LayoutGroup and patch it as soon as it shows
		// up. One-shot per scene load — the cockpit persists for the whole SolarSystem.
		public static IEnumerator ApplyShipConsoleFont()
		{
			GameObject layout = null;
			while (layout == null)
			{
				layout = GameObject.Find(ConsoleLayoutPath);
				if (layout == null) yield return null;
			}

			var font = Fonts.ShipUiFont;
			if (font == null) yield break;

			var template = layout.transform.Find("TextTemplate");
			if (template != null)
			{
				var text = template.GetComponent<Text>();
				if (text != null) text.font = font;
			}

			foreach (Transform child in layout.transform)
			{
				if (child.name != "TextTemplate(Clone)") continue;
				var text = child.GetComponent<Text>();
				if (text != null) text.font = font;
			}
		}
	}
}
