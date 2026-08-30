using HarmonyLib;
using OWML.Common;
using System.Collections;
using System.Text;
using UnityEngine;
using UnityEngine.UI;

namespace OuterWildsHebrew
{
	/// <summary>
	/// Verification aid for the cockpit console. Everything here only writes to the OWML log:
	/// it reports which notifications the ship is asked to show and, a moment later, what state
	/// the console's Text objects are actually in. That distinguishes the ways the display can
	/// end up blank — no notification arrives, the line never leaves the fit-test queue, the
	/// line has no font, or it is drawn somewhere invisible — which the screen itself cannot
	/// tell you apart.
	/// </summary>
	[HarmonyPatch]
	internal static class ShipUiDiagnostics
	{
		// A dump is several dozen lines, and damage notifications arrive in bursts, so stop
		// after a few. Enough to see the console in its failing state, not enough to bury the
		// rest of the log.
		private const int MaxDumps = 3;
		private static int _dumpsTaken;

		internal static void Log(string message)
		{
			OuterWildsHebrew.Instance.ModHelper.Console.WriteLine("[ship-ui] " + message, MessageType.Info);
		}

		// Every line the ship console is asked to display. The dump is deferred by a moment
		// because the display fit-tests a line over following frames before showing it, so
		// reading the Text objects inside this postfix would catch them mid-flight.
		[HarmonyPostfix]
		[HarmonyPatch(typeof(ShipNotificationDisplay), nameof(ShipNotificationDisplay.PushNotification))]
		public static void ShipNotificationDisplay_PushNotification(ShipNotificationDisplay __instance, NotificationData data)
		{
			if (data == null) Log("PushNotification: null data");
			else Log($"PushNotification: display=\"{data.displayMessage}\" markup=\"{data.markupMessage}\"");

			if (_dumpsTaken >= MaxDumps) return;
			_dumpsTaken++;
			OuterWildsHebrew.Instance.StartCoroutine(DumpAfterDelay(__instance));
		}

		private static IEnumerator DumpAfterDelay(ShipNotificationDisplay display)
		{
			yield return new WaitForSeconds(1.5f);
			DumpConsoleState(display);
		}

		public static void DumpConsoleState(ShipNotificationDisplay display)
		{
			if (display == null)
			{
				Log("ShipNotificationDisplay is gone");
				return;
			}

			var report = new StringBuilder();
			report.AppendLine($"console state for '{Path(display.transform)}'");
			report.AppendLine($"  gameObject active: {display.gameObject.activeInHierarchy}, behaviour enabled: {display.enabled}");

			// A line sitting in the untested queue never made it onto the screen: the ship
			// display measures each candidate and only shows it once it fits, so a count stuck
			// here means the fit test is the problem rather than the font.
			var untested = display._notificationUntestedQueue == null ? -1 : display._notificationUntestedQueue.Count;
			var ready = display._notificationReadyQueue == null ? -1 : display._notificationReadyQueue.Count;
			var testing = display._fitTestingData == null ? "none" : display._fitTestingData.displayMessage;
			report.AppendLine($"  queues: untested={untested} ready={ready} fitTesting={testing}");
			report.AppendLine($"  testText: {Describe(display._testText)}");

			// The canvas the whole thing is drawn on. A disabled canvas or a zero alpha group
			// hides perfectly healthy text.
			var canvas = display.GetComponentInParent<Canvas>();
			report.AppendLine(canvas == null
				? "  canvas: none found in parents"
				: $"  canvas: '{canvas.name}' enabled={canvas.enabled} active={canvas.gameObject.activeInHierarchy}");
			var group = display.GetComponentInParent<CanvasGroup>();
			if (group != null) report.AppendLine($"  canvasGroup: '{group.name}' alpha={group.alpha}");

			report.AppendLine($"  displayRoot: {(display._textDisplayRoot == null ? "null" : $"'{display._textDisplayRoot.name}' children={display._textDisplayRoot.childCount}")}");
			report.AppendLine($"  template: {(display._textDisplayTemplate == null ? "null" : display._textDisplayTemplate.name)}");
			report.AppendLine($"  pool items: {(display._textItemPool == null ? -1 : display._textItemPool.Count)}");

			// Everything under the display, pooled or not, so nothing is missed if the lines
			// live somewhere other than the pool list.
			report.AppendLine("  all Text under the display:");
			foreach (var text in display.GetComponentsInChildren<Text>(true))
				report.AppendLine($"    {Describe(text)}");

			var languageFont = TextTranslation.GetFont(false);
			report.AppendLine($"  TextTranslation.GetFont(false): {(languageFont == null ? "null" : languageFont.name)}");

			Log(report.ToString());
		}

		// The things that decide whether a line is readable: is it drawn at all, does it have
		// a font, is that font ours, and is there any text in it.
		private static string Describe(Text text)
		{
			if (text == null) return "null Text";

			var font = text.font == null ? "NO FONT" : text.font.name;
			var content = string.IsNullOrEmpty(text.text) ? "<empty>" : text.text;
			return $"'{text.name}' active={text.gameObject.activeInHierarchy} enabled={text.enabled} " +
			       $"font={font} size={text.fontSize} color={text.color} text=\"{content}\"";
		}

		private static string Path(Transform transform)
		{
			var path = transform.name;
			for (var parent = transform.parent; parent != null; parent = parent.parent)
				path = parent.name + "/" + path;
			return path;
		}
	}
}
