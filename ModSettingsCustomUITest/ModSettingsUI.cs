using Elements.Core;
using FrooxEngine;
using FrooxEngine.UIX;
using ResoniteModLoader;
using System;

namespace ModSettingsTests
{
    public partial class ModSettingsTests_Custom_UI
    {
        /// <summary>
        /// Gives you a UIBuilder which allows you to create custom ui within your page.<br/>
        /// It's setup with default RadiantUI styles (<see cref="RadiantUI_Constants.SetupDefaultStyle"/>)<br/>
        /// The PreferredHeight is set to <see cref="ModSettings_GetHeight"/>, please respect this value when building your UI.<br/>
        /// The UI builder is located within a VerticalLayout with a spacing of 4 with ForceExpandWidth set to true.<br/>
        /// The vertical layout also has a ContentSizeFitter with VerticalFit set to PreferredSize
        /// </summary>
        /// <param name="ui"></param>
        public void ModSettings_BuildModUi(UIBuilder ui)
        {
            ui.Button("hi").LocalPressed += (a,b) => Msg("First button pressed");
            ModSettings_BuildDefaultFields(this, ui);
            ui.Button("hello").LocalPressed += (a, b) => Msg("Second button pressed");
        }

        /// <summary>
        /// Gets the height that a single config item should be in your settings page.<br/>
        /// This value can be changed on a per user basis in the ModSettings config.
        /// </summary>
        /// <returns></returns>
        public static float ModSettings_GetHeight()
        {
            throw new NotImplementedException("It's a stub.");
        }
        /// <summary>
        /// Gets the color that every other item should have, null if items shouldn't be highlighted.
        /// </summary>
        public static colorX? ModSettings_GetHighlightColor()
        {
            throw new NotImplementedException("It's a stub.");
        }
        /// <summary>
        /// Builds all of the fields present in the config menu by default.<br/>
        /// Use this method when you want to add something to the page before/after the default UI.
        /// </summary>
        public static void ModSettings_BuildDefaultFields(ResoniteModBase mod, UIBuilder ui)
        {
            throw new NotImplementedException("It's a stub.");
        }

        /// <summary>
        /// Builds a singular field using the provided <see cref="ModConfigurationKey"/><br/>
        /// When building multiple fields you are expected to add a highlight to every other field with the color obtained using <see cref="ModSettings_GetHighlightColor"/><br/>
        /// If the highlight color is null then you don't have to add highlighting
        /// </summary>
        public static Slot ModSettings_BuildDefaultField(ResoniteModBase mod, UIBuilder ui, ModConfigurationKey key)
        {
            throw new NotImplementedException("It's a stub.");
        }
    }
}