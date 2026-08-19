using HarmonyLib;
using OWML.Common;
using OWML.ModHelper;
using System.IO;
using System.Reflection;
using UnityEngine;

namespace OuterWildsHebrew;

public class OuterWildsHebrew : ModBehaviour
{
	public static OuterWildsHebrew Instance;

	// The dedicated font for the Nomai translator tool, loaded from its own bundle and
	// applied by FontPatches. Null if the bundle is missing, in which case the translator
	// falls back to the normal UI font.
	public Font NomaiFont;

	public void Awake()
	{
		Instance = this;
		// You won't be able to access OWML's mod helper in Awake.
		// So you probably don't want to do anything here.
		// Use Start() instead.
	}

	public void Start()
	{
	    var api = ModHelper.Interaction.TryGetModApi<ILocalizationAPI>("xen.LocalizationUtility");
	    if (api != null)
	    {
	        // The fixer has to be registered right after the language, before the XML is read,
	        // otherwise LocalizationUtility loads the entries with no fixer attached.
	        api.RegisterLanguage(this, "Hebrew", "assets/Translation.xml");
	        api.AddLanguageFixer("Hebrew", HebrewFixer.Fix);

	        // The stock fonts only cover the game's official languages, so every Hebrew
	        // codepoint draws as a missing glyph. A bundled font that has the Hebrew block
	        // is the only thing that makes the text visible at all. This UI font is used
	        // everywhere except the Nomai translator, which FontPatches overrides below.
			api.AddLanguageFont(this, "Hebrew", "assets/ui_font", "assets/ui_font.ttf");

	        // The Nomai translator gets its own font from a separate bundle. Loaded here so
	        // it is ready before the translator's InitializeFont patch runs in-game.
	        LoadNomaiFont();
	        Harmony.CreateAndPatchAll(Assembly.GetExecutingAssembly());
	    }
	    else
	    {
	        ModHelper.Console.WriteLine("Could not find xen.LocalizationUtility", MessageType.Error);
	    }
	}

	// The UI font is loaded for us by LocalizationUtility, but the Nomai font lives in its
	// own bundle that we load by hand. Each bundle holds a single font, so we just take the
	// first one and don't have to know its exact in-bundle path.
	private void LoadNomaiFont()
	{
	    var bundlePath = Path.Combine(ModHelper.Manifest.ModFolderPath, "assets", "nomai_font");
	    if (!File.Exists(bundlePath))
	    {
	        ModHelper.Console.WriteLine($"Nomai font bundle missing at {bundlePath}", MessageType.Error);
	        return;
	    }

	    var bundle = AssetBundle.LoadFromFile(bundlePath);
	    if (bundle == null)
	    {
	        ModHelper.Console.WriteLine("Could not load the Nomai font bundle", MessageType.Error);
	        return;
	    }

	    var fonts = bundle.LoadAllAssets<Font>();
	    if (fonts.Length > 0) NomaiFont = fonts[0];
	    else ModHelper.Console.WriteLine("Nomai font bundle contained no font", MessageType.Error);

	    // Unload the bundle's raw data but keep the font asset we just pulled out of it.
	    bundle.Unload(false);
	}

	public void OnCompleteSceneLoad(OWScene previousScene, OWScene newScene)
	{
		if (newScene != OWScene.SolarSystem) return;
		ModHelper.Console.WriteLine("Loaded into solar system!", MessageType.Success);
	}
}
