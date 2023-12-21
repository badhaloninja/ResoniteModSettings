using ResoniteModLoader;

namespace ModSettingsTests
{
    public class ModSettingsTests_Internal_Only : ResoniteMod
    {
        public override string Name => "ModSettingsTests-Internal Only";
        public override string Author => "badhaloninja";
        public override string Version => "1.0.0";
        public override string Link => "https://github.com/badhaloninja/ResoniteModSettings";

        [AutoRegisterConfigKey]
#pragma warning disable IDE0052 // Remove unread private members
        private static readonly ModConfigurationKey<bool> INTERNAL_ONLY_1 = new("internalOnlyKey1", "This is an internal key", () => true, true);
        [AutoRegisterConfigKey]
        private static readonly ModConfigurationKey<bool> INTERNAL_ONLY_2 = new("internalOnlyKey2", "This is an internal key 2", () => true, true);
#pragma warning restore IDE0052 // Remove unread private members
    }
}