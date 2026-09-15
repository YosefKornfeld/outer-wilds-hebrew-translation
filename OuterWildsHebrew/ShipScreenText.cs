using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace OuterWildsHebrew
{
	/// <summary>
	/// The cockpit screens (autopilot console, signalscope readout, frequency name) render
	/// through Unity Text components with Best Fit turned on: Unity picks the largest size
	/// between resizeTextMinSize and resizeTextMaxSize that still fits the panel's rect.
	/// The bundled Hebrew font has far taller vertical metrics than the stock Latin fonts —
	/// Hebrew faces reserve room above and below the letters for nikud — so the same string
	/// generates a much taller block and Best Fit collapses down toward the minimum size.
	/// The result is the unreadable text on the ship monitors even though the very same
	/// font looks right everywhere the game uses a fixed size.
	///
	/// Raising the global font size would fix these panels by making every other piece of
	/// text in the game too large, so instead we scale the three size fields on the Text
	/// components under the cockpit UI only. Scaling the minimum matters most: Unity falls
	/// back to it when nothing fits, so it works as a legibility floor while still letting
	/// unusually long lines shrink from the maximum downwards.
	/// </summary>
	internal static class ShipScreenText
	{
		// The world-space canvas holding the cockpit monitors. CockpitCanvases is the
		// parent fallback in case a game update renames or reparents the inner object.
		private const string WorldSpaceUIPath =
			"Ship_Body/Module_Cockpit/Systems_Cockpit/ShipCockpitUI/CockpitCanvases/ShipWorldSpaceUI";

		private const string CanvasesPath =
			"Ship_Body/Module_Cockpit/Systems_Cockpit/ShipCockpitUI/CockpitCanvases";

		/// <summary>How much larger the cockpit screen text should be. 1 leaves it stock.</summary>
		public static float Scale = 1f;

		/// <summary>Dumps each screen's stock sizing to the console the first time we see it.</summary>
		public static bool LogDetails;

		public static Action<string> Log = _ => { };

		// The sizes a Text had before we touched it. Scaling always starts from these, so
		// re-applying after a settings change replaces the previous scale instead of
		// compounding on top of it.
		private struct Metrics
		{
			public int FontSize;
			public int MinSize;
			public int MaxSize;
		}

		private static readonly Dictionary<Text, Metrics> Originals = new Dictionary<Text, Metrics>();

		/// <summary>
		/// Waits for the cockpit to exist — the scene-load callback can fire before the ship
		/// is built — then scales every Text on its screens. One shot per SolarSystem load.
		/// </summary>
		public static IEnumerator ApplyToCockpit()
		{
			// Everything we recorded last time belongs to the previous scene's ship.
			Originals.Clear();

			GameObject root = null;
			while (root == null)
			{
				root = GameObject.Find(WorldSpaceUIPath) ?? GameObject.Find(CanvasesPath);
				if (root == null) yield return null;
			}

			// Give the displays one frame to lay their text out, so the diagnostic dump can
			// report the size Best Fit actually settled on rather than an ungenerated zero.
			yield return null;

			foreach (var text in root.GetComponentsInChildren<Text>(true))
				Apply(text);

			Log($"Ship screens: scaled {Originals.Count} text fields by {Scale:0.00}x");
		}

		/// <summary>
		/// Scales one Text from its stock sizes, recording those sizes the first time round.
		/// Safe to call repeatedly on the same component.
		/// </summary>
		public static void Apply(Text text)
		{
			if (text == null) return;

			if (!Originals.TryGetValue(text, out var original))
			{
				original = new Metrics
				{
					FontSize = text.fontSize,
					MinSize = text.resizeTextMinSize,
					MaxSize = text.resizeTextMaxSize
				};
				Originals[text] = original;

				if (LogDetails) LogStockSizing(text, original);
			}

			var fontSize = Mathf.Max(1, Mathf.RoundToInt(original.FontSize * Scale));
			var minSize = Mathf.Max(1, Mathf.RoundToInt(original.MinSize * Scale));
			var maxSize = Mathf.Max(minSize, Mathf.RoundToInt(original.MaxSize * Scale));

			// Max first: a minimum above the current maximum would leave Best Fit with an
			// empty range to search for the moment in between the two assignments.
			text.resizeTextMaxSize = maxSize;
			text.resizeTextMinSize = minSize;
			text.fontSize = fontSize;
			text.SetAllDirty();
		}

		/// <summary>
		/// Re-runs the scaling over everything we have already seen. Called when the scale
		/// changes in the mod settings so it takes effect without reloading the save.
		/// </summary>
		public static void Reapply()
		{
			if (Originals.Count == 0) return;

			foreach (var text in new List<Text>(Originals.Keys))
			{
				// Destroyed components compare equal to null but still work as dictionary
				// keys, so they can be removed by the same reference we looked them up with.
				if (text == null) Originals.Remove(text);
				else Apply(text);
			}
		}

		private static void LogStockSizing(Text text, Metrics original)
		{
			var rect = text.rectTransform.rect;
			// Only meaningful when Best Fit is on — it reports the size Best Fit settled on
			// for the last string it laid out, which is the number we actually want to see.
			var rendered = text.cachedTextGenerator != null
				? text.cachedTextGenerator.fontSizeUsedForBestFit
				: 0;

			Log($"[ship screen] {HierarchyPath(text.transform)} " +
			    $"font={(text.font != null ? text.font.name : "none")} " +
			    $"size={original.FontSize} bestFit={text.resizeTextForBestFit} " +
			    $"min={original.MinSize} max={original.MaxSize} rendered={rendered} " +
			    $"rect={rect.width:0}x{rect.height:0}");
		}

		private static string HierarchyPath(Transform transform)
		{
			var path = transform.name;
			for (var parent = transform.parent; parent != null; parent = parent.parent)
				path = parent.name + "/" + path;
			return path;
		}
	}
}
