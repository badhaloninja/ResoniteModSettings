using ResoniteModLoader;

namespace ModSettingsTests
{
    public partial class ModSettingsTests_Custom_UI : ResoniteMod
    {
        public override string Name => "ModSettingsTests-Custom UI";
        public override string Author => "badhaloninja";
        public override string Version => "1.0.0";
        public override string Link => "https://github.com/badhaloninja/ResoniteModSettings";

        [AutoRegisterConfigKey]
#pragma warning disable IDE0052 // Remove unread private members
        private static readonly ModConfigurationKey<bool> CUSTOM_UI_1 = new("customUIKey1", "This is a custom ui key", () => true, true);
        [AutoRegisterConfigKey]
        private static readonly ModConfigurationKey<bool> CUSTOM_UI_2 = new("customUIKey2", "This is a custom ui key 2", () => true, true);
#pragma warning restore IDE0052 // Remove unread private members
    }
}