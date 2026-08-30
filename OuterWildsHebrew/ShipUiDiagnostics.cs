using HarmonyLib;
using OWML.Common;
using System.Text;
using UnityEngine;
using UnityEngine.UI;

namespace OuterWildsHebrew
{
	/// <summary>
	/// Verification aid for the cockpit console. Everything here only writes to the OWML log,
	/// so it can stay in the build: it reports which notifications the ship is asked to show
	/// and, on demand, exactly what state the console's Text objects are in. That distinguishes
	/// the three ways the display can end up blank — no notification ever arrives, the line is
	/// never taken out of the queue, or the line is there but drawn with a font that renders
	/// nothing — which is not something the screen itself can tell you.
	///
	/// Press F9 in the cockpit to dump the console state.
	/// </summary>
	[HarmonyPatch]
	internal static class ShipUiDiagnostics
	{
		internal const KeyCode DumpKey = KeyCode.F9;

		internal static void Log(string message)
		{
			OuterWildsHebrew.Instance.ModHelper.Console.WriteLine("[ship-ui] " + message, MessageType.Info);
		}

		// Every line the ship console is asked to display. If flying around produces no lines
		// here at all, the text never reaches the display and no font patch could have helped.
		[HarmonyPostfix]
		[HarmonyPatch(typeof(ShipNotificationDisplay), nameof(ShipNotificationDisplay.PushNotification))]
		public static void ShipNotificationDisplay_PushNotification(NotificationData data)
		{
			if (data == null) Log("PushNotification: null data");
			else Log($"PushNotification: display=\"{data.displayMessage}\" markup=\"{data.markupMessage}\"");
		}

		// Called from OuterWildsHebrew.Update when the dump key is pressed.
		public static void DumpConsoleState()
		{
			var display = Object.FindObjectOfType<ShipNotificationDisplay>();
			if (display == null)
			{
				Log("No ShipNotificationDisplay found in the scene — are you in the ship?");
				return;
			}

			var report = new StringBuilder();
			report.AppendLine($"ShipNotificationDisplay on '{display.gameObject.name}'");
			report.AppendLine($"  gameObject active: {display.gameObject.activeInHierarchy}, behaviour enabled: {display.enabled}");

			// A line waiting in the untested queue never made it onto the screen: the ship
			// display measures each candidate line and only shows it once it fits, so a stuck
			// count here means the fit test is the problem rather than the font.
			var untested = display._notificationUntestedQueue == null ? -1 : display._notificationUntestedQueue.Count;
			var ready = display._notificationReadyQueue == null ? -1 : display._notificationReadyQueue.Count;
			report.AppendLine($"  queues: untested={untested} ready={ready} fitTesting={(display._fitTestingData == null ? "none" : display._fitTestingData.displayMessage)}");
			report.AppendLine($"  testText: {Describe(display._testText)}");

			report.AppendLine($"  template: {(display._textDisplayTemplate == null ? "null" : display._textDisplayTemplate.name)}");
			if (display._textDisplayTemplate != null)
			{
				foreach (var text in display._textDisplayTemplate.GetComponentsInChildren<Text>(true))
					report.AppendLine($"    {Describe(text)}");
			}

			var poolCount = display._textItemPool == null ? -1 : display._textItemPool.Count;
			report.AppendLine($"  pool items: {poolCount}");
			if (display._textItemPool != null)
			{
				foreach (var item in display._textItemPool)
				{
					if (item == null) { report.AppendLine("    <destroyed item>"); continue; }
					report.AppendLine($"    item '{item.name}' active={item.activeInHierarchy}");
					foreach (var text in item.GetComponentsInChildren<Text>(true))
						report.AppendLine($"      {Describe(text)}");
				}
			}

			var languageFont = TextTranslation.GetFont(false);
			report.AppendLine($"  TextTranslation.GetFont(false): {(languageFont == null ? "null" : languageFont.name)}");

			Log(report.ToString());
		}

		// The four things that decide whether a line is readable: is it drawn at all, does it
		// have a font, is that font ours, and is there actually any text in it.
		private static string Describe(Text text)
		{
			if (text == null) return "null Text";

			var font = text.font == null ? "NO FONT" : text.font.name;
			var content = string.IsNullOrEmpty(text.text) ? "<empty>" : text.text;
			return $"'{text.name}' active={text.gameObject.activeInHierarchy} enabled={text.enabled} " +
			       $"font={font} size={text.fontSize} color={text.color} text=\"{content}\"";
		}
	}
}
