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

	// How much bigger the cockpit displays draw their text than the prefab asks for. Our font
	// renders far smaller than its point size on those screens, and the right correction is
	// something to see rather than calculate, so it is a slider in the mod's settings.
	public static float CockpitFontScale = 2f;

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
	        // Marker mistakes are the translator's to fix, so they go to the console rather
	        // than being swallowed. Wired up before anything can compile a value.
	        MarkupCompiler.LogError = message => ModHelper.Console.WriteLine(message, MessageType.Error);

	        // The fixer has to be registered right after the language, before the XML is read,
	        // otherwise LocalizationUtility loads the entries with no fixer attached.
	        api.RegisterLanguage(this, "Hebrew", "assets/Translation.xml");

	        // Order matters: the compiler turns the Hebrew markers into real tags, which is
	        // what HebrewFixer needs to see in order to carry them through reordering intact.
	        api.AddLanguageFixer("Hebrew", text => HebrewFixer.Fix(MarkupCompiler.Compile(text)));

	        ValidateTranslation();

	        // The stock fonts only cover the game's official languages, so every Hebrew
	        // codepoint draws as a missing glyph. A bundled font that has the Hebrew block
	        // is the only thing that makes the text visible at all. This UI font is used
	        // everywhere except the Nomai translator, which FontPatches overrides below.
			api.AddLanguageFont(this, "Hebrew", "assets/ui_font", "assets/ui_font.ttf");

	        // The Nomai translator gets its own font from a separate bundle. Loaded here so
	        // it is ready before the translator's InitializeFont patch runs in-game.
	        LoadNomaiFont();

	        // Listing what actually got patched is the one cheap way to tell, from the log
	        // alone, whether a build's changes reached the running game — a patch that silently
	        // failed to find its target looks exactly like a patch that ran and did nothing.
	        var harmony = Harmony.CreateAndPatchAll(Assembly.GetExecutingAssembly());
	        foreach (var method in harmony.GetPatchedMethods())
	        {
	            ModHelper.Console.WriteLine(
	                $"Patched {method.DeclaringType?.Name}.{method.Name}", MessageType.Success);
	        }

	        // OnCompleteSceneLoad is ours to subscribe; nothing calls it for us.
	        LoadManager.OnCompleteSceneLoad += OnCompleteSceneLoad;
	    }
	    else
	    {
	        ModHelper.Console.WriteLine("Could not find xen.LocalizationUtility", MessageType.Error);
	    }
	}

	// Called by OWML on load and again whenever the player moves the slider, so the cockpit
	// text resizes while the game is running and the right value can just be dialled in.
	public override void Configure(IModConfig config)
	{
		// A config written before this setting existed has no value for it, and the zero that
		// comes back would shrink the cockpit text to nothing — the very problem this is meant
		// to fix. Anything not sensible falls back to the default.
		var scale = config.GetSettingsValue<float>("CockpitFontScale");
		CockpitFontScale = scale > 0f ? scale : 2f;

		FontPatches.ReapplyCockpitFontScale();
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
