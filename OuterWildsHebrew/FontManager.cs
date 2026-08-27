using System.Collections.Generic;
using System.IO;
using OWML.Common;
using UnityEngine;

namespace OuterWildsHebrew
{
	/// <summary>
	/// Loads and hands out the per-component fonts.
	///
	/// The UI font is installed by LocalizationUtility (via AddLanguageFont) and is what the
	/// game's own TextTranslation.GetFont(false) returns once the Hebrew language is active.
	/// Everything else — ship UI, character dialog, the Nomai translator — is either not
	/// touched by LU's font swap at all, or should be able to use a different font from the
	/// general UI. This class loads each of those extra fonts from its own asset bundle when
	/// the config points at one, and falls back to the UI font when it doesn't.
	///
	/// Each bundle is expected to contain a single Font asset, so we don't need to know the
	/// exact in-bundle path — LoadAllAssets and take the first one.
	/// </summary>
	public sealed class FontManager
	{
		private readonly IModConsole _console;
		private readonly string _assetsFolder;

		// Cache one Font per bundle name so repeated lookups don't reopen the bundle and so
		// we can share the same asset between components that were configured with the same
		// bundle (e.g. dialog and ship UI both pointing at the same override).
		private readonly Dictionary<string, Font> _cache = new Dictionary<string, Font>();

		private string _shipUiBundle;
		private string _dialogBundle;
		private string _nomaiBundle;

		public FontManager(IModConsole console, string modFolderPath)
		{
			_console = console;
			_assetsFolder = Path.Combine(modFolderPath, "assets");
		}

		/// <summary>
		/// Reads the bundle names from the mod config and preloads the ones that are set.
		/// Called on startup and again from Configure so the fonts hot-reload when the user
		/// edits the config in-game.
		/// </summary>
		public void Configure(IModConfig config)
		{
			_shipUiBundle = SafeGet(config, "shipUiFont");
			_dialogBundle = SafeGet(config, "dialogFont");
			_nomaiBundle = SafeGet(config, "nomaiFont");

			// Warm the cache so a missing bundle logs once at startup rather than the first
			// time a dialog opens or the ship cockpit spawns.
			Preload(_shipUiBundle);
			Preload(_dialogBundle);
			Preload(_nomaiBundle);
		}

		/// <summary>The UI font LocalizationUtility installed for the active language.</summary>
		public Font UiFont => TextTranslation.GetFont(false);

		public Font ShipUiFont => Resolve(_shipUiBundle);
		public Font DialogFont => Resolve(_dialogBundle);
		public Font NomaiFont => Resolve(_nomaiBundle);

		// Empty / unset bundle name means "inherit the UI font". A configured bundle that
		// failed to load also falls back to the UI font — the log line from Preload tells the
		// user why, and having the fallback here means the game still renders readable text.
		private Font Resolve(string bundleName)
		{
			if (string.IsNullOrEmpty(bundleName)) return UiFont;
			if (_cache.TryGetValue(bundleName, out var cached) && cached != null) return cached;
			return UiFont;
		}

		private void Preload(string bundleName)
		{
			if (string.IsNullOrEmpty(bundleName)) return;
			if (_cache.ContainsKey(bundleName)) return;

			var font = Load(bundleName);
			// Even a null goes into the cache so we don't try to reopen a missing bundle on
			// every Resolve call.
			_cache[bundleName] = font;
		}

		private Font Load(string bundleName)
		{
			var bundlePath = Path.Combine(_assetsFolder, bundleName);
			if (!File.Exists(bundlePath))
			{
				_console.WriteLine($"Font bundle '{bundleName}' missing at {bundlePath}", MessageType.Error);
				return null;
			}

			var bundle = AssetBundle.LoadFromFile(bundlePath);
			if (bundle == null)
			{
				_console.WriteLine($"Could not load font bundle '{bundleName}'", MessageType.Error);
				return null;
			}

			Font font = null;
			var fonts = bundle.LoadAllAssets<Font>();
			if (fonts.Length > 0) font = fonts[0];
			else _console.WriteLine($"Font bundle '{bundleName}' contained no font", MessageType.Error);

			// Unload the bundle's raw data but keep the font asset we just pulled out of it.
			bundle.Unload(false);
			return font;
		}

		private static string SafeGet(IModConfig config, string key)
		{
			try { return config.GetSettingsValue<string>(key) ?? string.Empty; }
			catch { return string.Empty; }
		}
	}
}
