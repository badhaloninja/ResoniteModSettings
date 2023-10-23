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
        public FoundMod(ResoniteModBase owner) : this ()
        {
            Owner = owner;


            if (Config == null) return;

            // Go over the fields to store the config field info
            var fields = AccessTools.GetDeclaredFields(Owner.GetType());
            fields.Where(field => Attribute.GetCustomAttribute(field, typeof(AutoRegisterConfigKeyAttribute)) != null) // Only get the config key fields
                .Do(field => ConfigKeyFields.Add((ModConfigurationKey)field.GetValue(field.IsStatic ? null : Owner), field)); // Store the fields with their keys
        }

        public ResoniteModBase Owner { get; private set; }
        public ModConfiguration Config { get => Owner.GetConfiguration(); }

        public Dictionary<ModConfigurationKey, FieldInfo> ConfigKeyFields { get; private set; } = new();
    }
}