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

	// A manual nudge on top of the cockpit font correction, as a fraction of it. The correction
	// itself is computed from the two fonts' line heights and should need no help; this is here
	// so the result can be adjusted in the mod's settings without another build.
	public static float CockpitFontScale = 1f;

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
		// Whole percent rather than a fraction, because a config written before this setting
		// existed returns zero, and a zero scale would shrink the cockpit text away entirely —
		// the very problem this exists to fix. Anything not sensible falls back to no nudge.
		var percent = config.GetSettingsValue<float>("CockpitFontPercent");
		CockpitFontScale = percent > 0f ? percent / 100f : 1f;

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
