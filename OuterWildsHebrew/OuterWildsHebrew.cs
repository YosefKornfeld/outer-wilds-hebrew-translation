using HarmonyLib;
using OWML.Common;
using OWML.ModHelper;
using System.IO;
using System.Reflection;

namespace OuterWildsHebrew;

public class OuterWildsHebrew : ModBehaviour
{
	public static OuterWildsHebrew Instance;

	// Loads and hands out the per-component fonts (UI, ship UI, dialog, Nomai). See
	// FontManager for how the bundles are picked up and how missing ones fall back.
	public FontManager Fonts { get; private set; }

	private ILocalizationAPI _api;
	private string _registeredUiBundle;

	public void Awake()
	{
		Instance = this;
		// You won't be able to access OWML's mod helper in Awake.
		// So you probably don't want to do anything here.
		// Use Start() instead.
	}

	public void Start()
	{
	    _api = ModHelper.Interaction.TryGetModApi<ILocalizationAPI>("xen.LocalizationUtility");
	    if (_api != null)
	    {
	        // Marker mistakes are the translator's to fix, so they go to the console rather
	        // than being swallowed. Wired up before anything can compile a value.
	        MarkupCompiler.LogError = message => ModHelper.Console.WriteLine(message, MessageType.Error);

	        // The fixer has to be registered right after the language, before the XML is read,
	        // otherwise LocalizationUtility loads the entries with no fixer attached.
	        _api.RegisterLanguage(this, "Hebrew", "assets/Translation.xml");

	        // Order matters: the compiler turns the Hebrew markers into real tags, which is
	        // what HebrewFixer needs to see in order to carry them through reordering intact.
	        _api.AddLanguageFixer("Hebrew", text => HebrewFixer.Fix(MarkupCompiler.Compile(text)));

	        ValidateTranslation();

	        // The stock fonts only cover the game's official languages, so every Hebrew
	        // codepoint draws as a missing glyph. A bundled font that has the Hebrew block
	        // is the only thing that makes the text visible at all. The UI font is used
	        // everywhere except the components that FontPatches overrides below.
	        RegisterUiFont();

	        // Component-specific fonts (ship UI, dialog, Nomai). Loaded here so they're
	        // ready before any of the FontPatches hooks fire in-game.
	        Fonts = new FontManager(ModHelper.Console, ModHelper.Manifest.ModFolderPath);
	        Fonts.Configure(ModHelper.Config);

	        Harmony.CreateAndPatchAll(Assembly.GetExecutingAssembly());
	    }
	    else
	    {
	        ModHelper.Console.WriteLine("Could not find xen.LocalizationUtility", MessageType.Error);
	    }
	}

	// OWML calls Configure on load and whenever the user edits the config in-game. Rebuilds
	// the font cache so a changed bundle name takes effect without a game restart. The UI
	// font itself is owned by LocalizationUtility and only re-registers if its bundle name
	// actually changed, since AddLanguageFont doesn't like being called with the same values.
	public override void Configure(IModConfig config)
	{
	    base.Configure(config);
	    if (_api == null || Fonts == null) return;

	    var newUiBundle = SafeGet(config, "uiFont", "ui_font");
	    if (newUiBundle != _registeredUiBundle)
	    {
	        RegisterUiFont(newUiBundle);
	    }

	    Fonts.Configure(config);
	}

	// Cross-checks the translated values against the English keys they replace. Purely
	// diagnostic, so a failure here must never be allowed to stop the language loading.
	private void ValidateTranslation()
	{
	    try
	    {
	        var path = Path.Combine(ModHelper.Manifest.ModFolderPath, "assets", "Translation.xml");
	        TranslationValidator.Validate(path, message => ModHelper.Console.WriteLine(message, MessageType.Warning));
	    }
	    catch (System.Exception exception)
	    {
	        ModHelper.Console.WriteLine("Could not validate the translation: " + exception.Message, MessageType.Warning);
	    }
	}

	private void RegisterUiFont(string bundleName = null)
	{
	    bundleName ??= SafeGet(ModHelper.Config, "uiFont", "ui_font");
	    if (string.IsNullOrEmpty(bundleName))
	    {
	        ModHelper.Console.WriteLine("uiFont is empty — the UI will render Hebrew as missing-glyph tofu", MessageType.Warning);
	        return;
	    }

	    // Convention: bundle at assets/<name>, font asset inside named <name>.ttf. Keeping
	    // the two in lockstep lets the config carry a single value per component.
	    _api.AddLanguageFont(this, "Hebrew", $"assets/{bundleName}", $"assets/{bundleName}.ttf");
	    _registeredUiBundle = bundleName;
	}

	private static string SafeGet(IModConfig config, string key, string fallback)
	{
	    try
	    {
	        var value = config.GetSettingsValue<string>(key);
	        return string.IsNullOrEmpty(value) ? fallback : value;
	    }
	    catch { return fallback; }
	}

	public void OnCompleteSceneLoad(OWScene previousScene, OWScene newScene)
	{
		if (newScene != OWScene.SolarSystem) return;
		ModHelper.Console.WriteLine("Loaded into solar system!", MessageType.Success);

		// The cockpit console (Flashlight ON / Autopilot aborted) isn't reached by
		// LocalizationUtility's font swap, so patch its Text templates once the cockpit
		// appears in the scene. See FontPatches.ApplyShipConsoleFont for the details.
		StartCoroutine(FontPatches.ApplyShipConsoleFont());

		// Character dialog boxes: no-op unless the user configured a dialog-specific font,
		// otherwise the LU-swapped UI font is already what the boxes render with.
		FontPatches.ApplyDialogFont();
	}
}
