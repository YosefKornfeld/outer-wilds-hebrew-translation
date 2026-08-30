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

			// The mask clips anything drawn outside it, so a line laid out past its edge is
			// invisible while looking perfectly healthy on the object itself.
			var mask = display.GetComponentInChildren<Mask>(true);
			if (mask != null) report.AppendLine($"  mask: '{mask.name}' enabled={mask.enabled} showGraphic={mask.showMaskGraphic} rect={((RectTransform)mask.transform).rect}");
			var mask2D = display.GetComponentInChildren<RectMask2D>(true);
			if (mask2D != null) report.AppendLine($"  rectMask2D: '{mask2D.name}' enabled={mask2D.enabled} rect={((RectTransform)mask2D.transform).rect}");

			// Everything under the display, pooled or not, so nothing is missed if the lines
			// live somewhere other than the pool list.
			report.AppendLine("  all Text under the display:");
			foreach (var text in display.GetComponentsInChildren<Text>(true))
			{
				report.AppendLine($"    {Describe(text)}");
				// Only the lines actually on screen are worth the extra detail, and only they
				// can tell us why nothing appears.
				if (text == null || !text.gameObject.activeInHierarchy) continue;
				report.AppendLine($"      {DescribeRendering(text)}");
				AppendScaleChain(report, text.transform);
			}

			var languageFont = TextTranslation.GetFont(false);
			report.AppendLine($"  TextTranslation.GetFont(false): {(languageFont == null ? "null" : languageFont.name)}");

			AppendFontControllerState(report, display);
			AppendWorkingTextComparison(report, display);

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

		// The ship's FontAndLanguageController holds, for every Text it manages, the size and
		// scale the prefab shipped with. Printing those beside the live values says outright
		// whether something shrank the text after the fact and by how much — the one question
		// the objects themselves cannot answer, since a shrunk element looks entirely normal.
		private static void AppendFontControllerState(StringBuilder report, ShipNotificationDisplay display)
		{
			var controller = display._fontController;
			if (controller == null)
			{
				report.AppendLine("  fontController: none on the ship display");
				return;
			}

			report.AppendLine($"  fontController '{controller.name}': containers={(controller._textContainerList == null ? -1 : controller._textContainerList.Count)}");
			if (controller._textContainerList == null) return;

			foreach (var container in controller._textContainerList)
			{
				var text = container.textElement;
				if (text == null) { report.AppendLine("    <null textElement>"); continue; }

				report.AppendLine($"    '{text.name}' isLanguageFont={container.isLanguageFont} shouldScale={container.shouldScale}");
				report.AppendLine($"      size: now={text.fontSize} original={container.originalFontSize}");
				report.AppendLine($"      scale: now={text.rectTransform.localScale.ToString("F4")} original={container.originalScale.ToString("F4")}");
				report.AppendLine($"      sizeDelta: now={text.rectTransform.sizeDelta} original={container.originalSizeDelta}");
			}
		}

		// The scale that hides text is often on an ancestor rather than the Text itself, so
		// walk up to the canvas and print each step.
		private static void AppendScaleChain(StringBuilder report, Transform transform)
		{
			report.AppendLine("      scale chain:");
			for (var t = transform; t != null; t = t.parent)
			{
				report.AppendLine($"        '{t.name}' localScale={t.localScale.ToString("F4")}");
				if (t.GetComponent<Canvas>() != null) break;
			}
		}

		// A control group. Text elsewhere on screen is readable in the same font, so whatever
		// differs between one of those and a console line is where the console is losing its
		// glyphs. Without the comparison the console's numbers are hard to judge — there is
		// nothing to say which of them is abnormal.
		private static void AppendWorkingTextComparison(StringBuilder report, ShipNotificationDisplay display)
		{
			report.AppendLine("  other on-screen Text using the same font, for comparison:");

			var shown = 0;
			foreach (var text in Object.FindObjectsOfType<Text>())
			{
				if (shown >= 4) break;
				if (text == null || text.font == null) continue;
				// Only lines that are genuinely being drawn, and not the console's own.
				if (!text.gameObject.activeInHierarchy || !text.enabled) continue;
				if (string.IsNullOrEmpty(text.text)) continue;
				if (text.GetComponentInParent<ShipNotificationDisplay>() == display) continue;

				report.AppendLine($"    {Path(text.transform)}");
				report.AppendLine($"      {Describe(text)}");
				report.AppendLine($"      {DescribeRendering(text)}");
				AppendScaleChain(report, text.transform);
				shown++;
			}

			if (shown == 0) report.AppendLine("    none found");
		}

		// Splits the two remaining ways a healthy-looking line can be invisible. Either the
		// font produced no glyphs — the generator reports no visible characters and no verts,
		// which means the font asset itself cannot draw this text — or it produced them and
		// something about where or how they are drawn hides them: a zero scale, an empty rect,
		// a culled or transparent CanvasRenderer, or a material with no font atlas behind it.
		private static string DescribeRendering(Text text)
		{
			var generator = text.cachedTextGenerator;
			var rect = text.rectTransform;
			var renderer = text.canvasRenderer;

			// The size the glyphs actually come out at, measured from the vertices Unity is
			// about to draw. Compared against fontSize it says outright whether the font
			// renders at the point size it was asked for — the thing that makes cockpit text
			// unreadable while every value on the object looks correct.
			var glyphs = generator == null
				? "generator=null"
				: $"visibleChars={generator.characterCountVisible} verts={generator.vertexCount} drawn={MeasureGlyphs(generator)}";

			var material = text.materialForRendering;
			var texture = text.mainTexture;
			var atlas = texture == null ? "no texture" : $"{texture.name} {texture.width}x{texture.height}";

			// Vector3.ToString rounds to one decimal, which prints every world-space scale as
			// zero and hides exactly the differences we are looking for.
			return $"{glyphs} rect={rect.rect.size} localScale={rect.localScale.ToString("F4")} lossyScale={rect.lossyScale.ToString("F6")} " +
			       $"cull={renderer.cull} rendererAlpha={renderer.GetAlpha()} " +
			       $"shader={(material == null || material.shader == null ? "none" : material.shader.name)} atlas={atlas} " +
			       $"fontDynamic={(text.font == null ? "no font" : text.font.dynamic.ToString())}";
		}

		// Width and height of the drawn text in the Text's own units, taken from the generated
		// vertices. A height far below the font size means the font draws small for its point
		// size, which no amount of correct scaling or layout can compensate for.
		private static string MeasureGlyphs(TextGenerator generator)
		{
			var verts = generator.verts;
			if (verts == null || verts.Count == 0) return "nothing";

			float minX = float.MaxValue, maxX = float.MinValue, minY = float.MaxValue, maxY = float.MinValue;
			foreach (var vert in verts)
			{
				var p = vert.position;
				if (p.x < minX) minX = p.x;
				if (p.x > maxX) maxX = p.x;
				if (p.y < minY) minY = p.y;
				if (p.y > maxY) maxY = p.y;
			}

			return $"{(maxX - minX):F1}x{(maxY - minY):F1}";
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
