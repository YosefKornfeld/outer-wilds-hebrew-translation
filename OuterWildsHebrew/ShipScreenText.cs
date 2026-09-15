using System;
using HarmonyLib;
using UnityEngine;
using UnityEngine.UI;

namespace OuterWildsHebrew
{
	/// <summary>
	/// The two cockpit monitors — the signalscope screen and the autopilot / notification
	/// console — are world-space canvases whose CanvasScaler has a very high
	/// dynamicPixelsPerUnit (75 and 370). On a world-space canvas that value becomes the
	/// canvas scale factor, and Unity rasterises *dynamic* font text at fontSize × scale
	/// factor pixels, then shrinks the mesh back down by the same factor.
	///
	/// The stock screens use VCR_OSD_MONO, a bitmap (non-dynamic) font, which ignores the
	/// scale factor entirely, so the huge values never mattered. Our Hebrew font is dynamic,
	/// so the same 45–48 size text asks for glyphs 3,600 to 16,650 pixels tall. Unity clamps
	/// the glyph size far below that but still divides by the full scale factor, and the
	/// text comes out a fraction of its intended size — a few specks on the console.
	///
	/// Lowering dynamicPixelsPerUnit on those canvases fixes the size without touching the
	/// font size anywhere, so the rest of the game is unaffected. It only changes how many
	/// pixels a dynamic glyph is rasterised with, and the stock bitmap-font text on the same
	/// canvases doesn't use it at all.
	/// </summary>
	[HarmonyPatch]
	internal static class ShipScreenText
	{
		// Pixel height the largest text on a cockpit canvas gets rasterised at. Well below
		// where Unity starts clamping, and still sharp when the player leans into a screen.
		private const float TargetGlyphPixels = 128f;

		public static Action<string> Log = _ => { };

		// ShipCockpitUI sits above both SignalScreen and CockpitCanvases, and its Start runs
		// once the ship has built its screens, including the console's pooled text items.
		[HarmonyPostfix]
		[HarmonyPatch(typeof(ShipCockpitUI), nameof(ShipCockpitUI.Start))]
		public static void ShipCockpitUI_Start(ShipCockpitUI __instance)
		{
			foreach (var scaler in __instance.GetComponentsInChildren<CanvasScaler>(true))
				CapDynamicPixelsPerUnit(scaler);
		}

		private static void CapDynamicPixelsPerUnit(CanvasScaler scaler)
		{
			// dynamicPixelsPerUnit is only used as the scale factor on world-space canvases;
			// the screen-space HUD canvases under the cockpit size themselves differently.
			var canvas = scaler.GetComponent<Canvas>();
			if (canvas == null || canvas.renderMode != RenderMode.WorldSpace) return;

			var largestFontSize = 0;
			foreach (var text in scaler.GetComponentsInChildren<Text>(true))
				largestFontSize = Mathf.Max(largestFontSize, text.fontSize);
			if (largestFontSize == 0) return;

			var cap = TargetGlyphPixels / largestFontSize;
			if (scaler.dynamicPixelsPerUnit <= cap) return;

			Log($"Ship screen {scaler.name}: dynamicPixelsPerUnit {scaler.dynamicPixelsPerUnit:0.##} -> {cap:0.##}");
			scaler.dynamicPixelsPerUnit = cap;
		}
	}
}
