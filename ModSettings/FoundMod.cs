using ResoniteModLoader;
using System.Collections.Generic;
using System.Reflection;

namespace ModSettings
{
    internal record FoundMod(ResoniteModBase Owner)
    {
        public ResoniteModBase Owner { get; private set; } = Owner;
        public ModConfiguration Config { get => Owner.GetConfiguration(); }

        public Dictionary<ModConfigurationKey, FieldInfo> ConfigKeyFields { get; private set; } = new();
    }
}