using System;
using System.Linq;
using System.Reflection;
using System.Collections.Generic;

using HarmonyLib;
using ResoniteModLoader;

namespace ModSettings
{
    internal record FoundMod()
    {
        public FoundMod(ResoniteModBase owner) : this()
        {
            Owner = owner;


            if (Config == null) return;

            // Prepopulate all of the keys with null field info, for manually defined keys
            Config.ConfigurationItemDefinitions.Do(k => ConfigKeyFields[k] = null);

            // Go over the fields to store the config field info
            var fields = AccessTools.GetDeclaredFields(Owner.GetType());
            // Only get the config key fields and store the fields with their keys
            fields.Where(IsConfigurationKeyField).Do(StoreFieldWithKey);
        }

        public ResoniteModBase Owner { get; private set; }
        public ModConfiguration Config { get => Owner.GetConfiguration(); }

        public Dictionary<ModConfigurationKey, FieldInfo> ConfigKeyFields { get; private set; } = [];



        private static bool IsConfigurationKeyField(FieldInfo field) => Attribute.GetCustomAttribute(field, typeof(AutoRegisterConfigKeyAttribute)) != null;
        private void StoreFieldWithKey(FieldInfo field)
        {
            ModConfigurationKey key = (ModConfigurationKey)field.GetValue(field.IsStatic ? null : Owner);
            ConfigKeyFields[key] = field;
        }
    }
}