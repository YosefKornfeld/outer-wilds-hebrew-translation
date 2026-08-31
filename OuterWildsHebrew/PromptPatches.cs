using System;
using HarmonyLib;

namespace OuterWildsHebrew
{
	/// <summary>
	/// Some on-screen prompts are not a single translated entry — the game builds them at
	/// runtime by gluing a translated label onto a value that is not in the XML. "Talk to"
	/// plus a character name is the one the player runs into constantly.
	///
	/// HebrewFixer only ever sees the label, and hands back the *visual* order of that label
	/// on its own. The game then appends the name to the right of it, which in a right to
	/// left reading puts the name first: "‏{שם} דבר אל". Nothing in the XML can fix this,
	/// because the two halves are only joined after the fixer has finished.
	///
	/// So we rebuild the composed prompt ourselves with the halves the other way round.
	/// Each half is already in visual order, so swapping them is all that is needed.
	/// </summary>
	[HarmonyPatch]
	internal static class PromptPatches
	{
		// SingleInteractionVolume.SetPromptText(UITextType, string) is the composed-prompt
		// overload — the game's own parameter is literally named _characterName. Vanilla
		// writes "<label> <name> <CMD>" into _screenPrompt and "<label> <name>" into
		// _noCommandIconPrompt; we write the same two strings with the halves swapped.
		//
		// The command icon marker stays at the end so the button glyph keeps the screen
		// position it has in every other prompt, rather than jumping to the other side.
		[HarmonyPrefix]
		[HarmonyPatch(typeof(SingleInteractionVolume), nameof(SingleInteractionVolume.SetPromptText),
			new Type[] { typeof(UITextType), typeof(string) })]
		public static bool SingleInteractionVolume_SetPromptText(
			SingleInteractionVolume __instance,
			UITextType promptID,
			string _characterName)
		{
			string label = UITextLibrary.GetString(promptID);

			// Untranslated labels are still English and read left to right, so vanilla's
			// order is already the right one. This doubles as the language check: no Hebrew
			// on screen means nothing here should run.
			if (!HebrewFixer.ContainsHebrew(label)) return true;

			// The name comes from the prefab rather than the XML, so it has never been
			// through the fixer. Fix returns it untouched when it is Latin, which is the
			// usual case — character names are not translated by the game.
			string name = HebrewFixer.Fix(_characterName);

			string swapped = name + " " + label;
			if (__instance._screenPrompt != null)
				__instance._screenPrompt.SetText(swapped + " " + ScreenPromptElement.IMAGE_MARKER_STRING);
			if (__instance._noCommandIconPrompt != null)
				__instance._noCommandIconPrompt.SetText(swapped);

			return false;
		}
	}
}
