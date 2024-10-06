using System;
using System.Linq;
using System.Reflection;
using System.Collections.Generic;

using HarmonyLib;
using ResoniteModLoader;
using FrooxEngine.UIX;

namespace ModSettings
{
    internal record FoundMod()
    {
        private static Harmony harmony = new ("ninja.badhalo.ModSettingsCustomUi");

        public FoundMod(ResoniteModBase owner) : this()
        {
            Owner = owner;


            if (Config == null) return;

            // Prepopulate all of the keys with null field info, for manually defined keys
            Config.ConfigurationItemDefinitions.Do(k => ConfigKeyFields[k] = null);

            var modType = Owner.GetType();

            // Go over the fields to store the config field info
            var fields = AccessTools.GetDeclaredFields(modType);
            // Only get the config key fields and store the fields with their keys
            fields.Where(IsConfigurationKeyField).Do(StoreFieldWithKey);

            // Inject Custom Ui Stuff
            foreach (var apiMethod in ModSettings.ModSettingsScreen.PublicApiMethods)
            {
                var method = AccessTools.DeclaredMethod(modType, apiMethod.Key);
                if (method == null) continue;
                
                // The public api methods has 5 methods, 4 are implemented in this mod, and one is just the entry point
                if (apiMethod.Value.Item1 == null)
                {
                    BuildCustomUI = (ui) => method.Invoke(owner, [ui]);
                }
                else
                {
                    // According to harmony docs __args has a small performance overhead, so unrolling this loop and removing that might be useful
                    harmony.Patch(method, new HarmonyMethod(apiMethod.Value.Item2));
                }
            }
        }

        public ResoniteModBase Owner { get; private set; }
        public ModConfiguration Config { get => Owner.GetConfiguration(); }

        public Dictionary<ModConfigurationKey, FieldInfo> ConfigKeyFields { get; private set; } = [];

        public Action<UIBuilder>? BuildCustomUI;


        private static bool IsConfigurationKeyField(FieldInfo field) => Attribute.GetCustomAttribute(field, typeof(AutoRegisterConfigKeyAttribute)) != null;
        private void StoreFieldWithKey(FieldInfo field)
        {
            ModConfigurationKey key = (ModConfigurationKey)field.GetValue(field.IsStatic ? null : Owner);
            ConfigKeyFields[key] = field;
        }
    }
}