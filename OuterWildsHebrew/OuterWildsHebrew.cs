using HarmonyLib;
using OWML.Common;
using OWML.ModHelper;
using System.Reflection;

namespace OuterWildsHebrew;

public class OuterWildsHebrew : ModBehaviour
{
	public static OuterWildsHebrew Instance;

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
	    }
	    else
	    {
	        ModHelper.Console.WriteLine("Could not find xen.LocalizationUtility", MessageType.Error);
	    }
	}

	public void OnCompleteSceneLoad(OWScene previousScene, OWScene newScene)
	{
		if (newScene != OWScene.SolarSystem) return;
		ModHelper.Console.WriteLine("Loaded into solar system!", MessageType.Success);
	}
}
