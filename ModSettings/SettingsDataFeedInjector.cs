using Elements.Core;
using Elements.Quantity;
using FrooxEngine;
using HarmonyLib;
using ResoniteModLoader;
using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Reflection;

namespace ModSettings
{
    // This is such a huge mess because specifically the SettingsDataFeed wants exposed data model stuff or specific attributes everywhere
    public static class SettingsDataFeedInjector
    {
        private static FieldInfo _categoryInfos;
        private static FieldInfo _typesByCategory;
        internal static void InjectCategory(SettingCategoryInfo settingCategoryInfo, string key)
        {
            if (_categoryInfos == null || _typesByCategory == null)
            {
                _categoryInfos = AccessTools.Field(typeof(Settings), "_categoryInfos");
                _typesByCategory = AccessTools.Field(typeof(Settings), "_typesByCategory");
            }

            var categories = (Dictionary<string, SettingCategoryInfo>)_categoryInfos.GetValue(null);

            settingCategoryInfo.InitKey(key);
            categories.Add(key, settingCategoryInfo);

            var typesByCategories = (Dictionary<string, HashSet<Type>>)_typesByCategory.GetValue(null);

            typesByCategories.Add(key, new HashSet<Type>()
            {
                typeof(ModSettings) // Testing
            });
        }

        [HarmonyPatch(typeof(SettingsDataFeed))]
        public static class SettingsInjection
        {

            //InitBasePrefix
            static readonly Type FieldIdentityClass = typeof(SettingsDataFeed).GetNestedType("FieldIdentity", BindingFlags.NonPublic | BindingFlags.Instance);
            static readonly Type SettingMemberIdentityClass = typeof(SettingsDataFeed).GetNestedType("SettingMemberIdentity", BindingFlags.NonPublic | BindingFlags.Instance);

            static readonly MethodInfo InitBaseMethod = typeof(SettingsDataFeed).GetMethod("InitBase", BindingFlags.NonPublic | BindingFlags.Static);
            static readonly MethodInfo GenerateItemField = typeof(SettingsDataFeed).GetMethod("GenerateItem", BindingFlags.NonPublic | BindingFlags.Static, null, new Type[] { FieldIdentityClass, typeof(SettingPropertyAttribute), typeof(IReadOnlyList<string>), typeof(IReadOnlyList<string>) }, null);

            static readonly FieldInfo settingTypeField = SettingMemberIdentityClass.GetField("settingType");
            static readonly FieldInfo subcategoryField = SettingMemberIdentityClass.GetField("subcategory");
            static readonly FieldInfo settingGetterField = SettingMemberIdentityClass.GetField("settingGetter");
            static readonly FieldInfo settingKeyField = SettingMemberIdentityClass.GetField("settingKey");
            static readonly FieldInfo fieldField = FieldIdentityClass.GetField("field");

            //static readonly FieldInfo fieldField = FieldIdentityClass.GetField("field");


            static readonly MethodInfo _generateValueField = typeof(GenerateFieldMethods).GetMethod("GenerateValueField", BindingFlags.Static | BindingFlags.NonPublic);
            static readonly MethodInfo _generateEnumField = typeof(GenerateFieldMethods).GetMethod("GenerateEnumField", BindingFlags.Static | BindingFlags.NonPublic);
            static readonly MethodInfo _generateSlider = typeof(GenerateFieldMethods).GetMethod("GenerateSlider", BindingFlags.Static | BindingFlags.NonPublic);
            static readonly MethodInfo _generateQuantityField = typeof(GenerateFieldMethods).GetMethod("GenerateQuantityField", BindingFlags.Static | BindingFlags.NonPublic);
            //static readonly MethodInfo _generateIndicator = typeof(SettingsDataFeed).GetMethod("GenerateIndicator", BindingFlags.Static | BindingFlags.NonPublic);


            public static void PatchOtherMethods(Harmony harmony)
            {
                harmony.Patch(GenerateItemField, prefix: new(typeof(SettingsInjection).GetMethod("GenerateFieldItemPrefix")));

                //harmony.Patch(InitBaseMethod, prefix: new(typeof(SettingsInjection).GetMethod("InitBasePrefix")));
            }

            public static Func<object> GetActivator(Type type)

            { // TODO: REPLACE THIS
                return Expression.Lambda<Func<object>>(Expression.New(type)).Compile();
            }
            static object InitIdentity(Type settingType, Type containerType, object subcategory, object settingGetter, object settingKey, FieldInfo element)
            {
                var a = GetActivator(FieldIdentityClass);

                object identity = a();

                settingType ??= containerType;

                settingTypeField.SetValue(identity, settingType);
                subcategoryField.SetValue(identity, subcategory);
                settingGetterField.SetValue(identity, settingGetter);
                settingKeyField.SetValue(identity, settingKey);

                fieldField.SetValue(identity, element);
                return identity;
            }

            [HarmonyPostfix]
            [HarmonyPatch("EnumerateSettingProperties")]
            public static IEnumerable<DataFeedItem> EnumerateSettingProperties(IEnumerable<DataFeedItem> values, Type containerType, IReadOnlyList<string> path, Type settingType = null, string subcategory = null, string settingGetter = null, string settingKey = null)
            {
                foreach (var item in values) {
                    yield return item;
                }

                if (FieldIdentityClass == null || GenerateItemField == null) yield break;

                string[] grouping = new string[1] { containerType?.Name };
                ModSettings.Msg(containerType);
                FieldInfo[] fieldInfoArray = containerType?.GetFields(bindingAttr: BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);

                int index;
                for (index = 0; index < fieldInfoArray.Length; ++index)
                {
                    FieldInfo element = fieldInfoArray[index];

                    SettingPropertyAttribute customAttribute = element.GetCustomAttribute<SettingPropertyAttribute>();
                    AutoRegisterConfigKeyAttribute configKeyAttribute = element.GetCustomAttribute<AutoRegisterConfigKeyAttribute>();

                    if (customAttribute == null && configKeyAttribute != null)
                    {
                        ModSettings.Msg($"  [{index}] {element.Name}");
                        var identity = InitIdentity(settingType, containerType, subcategory, settingGetter, settingKey, element);

                        DataFeedItem dataFeedItem = null;
                        try
                        {
                            GenerateFieldItemPrefix(identity, ref dataFeedItem, customAttribute, path, (IReadOnlyList<string>)grouping);
                            ModSettings.Msg(dataFeedItem.ToString());
                            //dataFeedItem = (DataFeedItem)GenerateItemField.Invoke(__instance, new object[] { identity, customAttribute, path, (IReadOnlyList<string>)grouping });
                        }
                        catch (Exception ex)
                        {
                            UniLog.Error(ex.ToString());
                            UniLog.Error(string.Format("Exception generating item from field {0} on {1}", identity?.GetType(), containerType));
                            throw;
                        }
                        yield return dataFeedItem;
                    }
                }
            }


            private static Action<IField<T>> GenerateFieldSyncModConfig<T>(object identity)
            {
                return delegate (IField<T> f)
                {
                    ModSettings.Config.Set((ModConfigurationKey<T>)((FieldInfo)fieldField.GetValue(identity)).GetValue(null), f.Value);
                    //f.SyncWithSetting(identity.settingType, identity.field.Name, identity.settingGetter, identity.settingKey);
                };
            }


            public static bool InitBasePrefix(DataFeedItem item, object identity, SettingPropertyAttribute setting, IReadOnlyList<string> path, IReadOnlyList<string> grouping)
            {
                ModSettings.Msg("ININTBASEPREFIX: ", setting != null);
                //if (setting != null) return true;
                var field = (FieldInfo)fieldField.GetValue(identity);
                if (field.GetValue(null) is not ModConfigurationKey) return false;

                var modKey = (ModConfigurationKey)field.GetValue(null);

                string text = modKey.Name;
                LocaleString name = text;
                LocaleString description = modKey.Description;
                item.InitBase(text, path, grouping, name, setting?.Icon, null, null, null);

                if (item is DataFeedElement dataFeedElement)
                {
                    dataFeedElement.InitDescription(description);
                }

                return false;
            }

            public static class GenerateFieldMethods {
                private static DataFeedEnum<T> GenerateEnumField<T>(object identity, SettingPropertyAttribute setting, IReadOnlyList<string> path, IReadOnlyList<string> grouping) where T : Enum
                {
                    DataFeedEnum<T> dataFeedEnum = new();
                    InitBasePrefix(dataFeedEnum, identity, setting, path, grouping);
                    dataFeedEnum.InitSetupValue(GenerateFieldSyncModConfig<T>(identity));
                    return dataFeedEnum;
                }
                private static DataFeedQuantityField<Q, T> GenerateQuantityField<Q, T>(object identity, SettingPropertyAttribute setting, QuantityAttribute quantity, RangeAttribute range, IReadOnlyList<string> path, IReadOnlyList<string> grouping) where Q : unmanaged, IQuantity<Q>
                {
                    DataFeedQuantityField<Q, T> dataFeedQuantityField = new();
                    InitBasePrefix(dataFeedQuantityField, identity, setting, path, grouping);
                    dataFeedQuantityField.InitSetup(GenerateFieldSyncModConfig<T>(identity), (T)Convert.ChangeType(range.Min, typeof(T)), (T)Convert.ChangeType(range.Max, typeof(T)));
                    dataFeedQuantityField.InitUnitConfiguration(quantity.DefaultConfiguration, quantity.ImperialConfiguration);
                    return dataFeedQuantityField;
                }
                private static DataFeedSlider<T> GenerateSlider<T>(object identity, SettingPropertyAttribute setting, RangeAttribute range, IReadOnlyList<string> path, IReadOnlyList<string> grouping)
                {
                    DataFeedSlider<T> dataFeedSlider = new();
                    InitBasePrefix(dataFeedSlider, identity, setting, path, grouping);
                    dataFeedSlider.InitSetup(GenerateFieldSyncModConfig<T>(identity), (T)Convert.ChangeType(range.Min, typeof(T)), (T)Convert.ChangeType(range.Max, typeof(T)));
                    return dataFeedSlider;
                }
                private static DataFeedValueField<T> GenerateValueField<T>(object identity, SettingPropertyAttribute setting, IReadOnlyList<string> path, IReadOnlyList<string> grouping)
                {
                    ModSettings.Msg("GenerateValueField");
                    ModSettings.Msg(path);
                    DataFeedValueField<T> dataFeedValueField = new();
                    InitBasePrefix(dataFeedValueField, identity, setting, path, grouping);
                    dataFeedValueField.InitSetupValue(GenerateFieldSyncModConfig<T>(identity));
                    return dataFeedValueField;
                }
            }

            public static bool GenerateFieldItemPrefix(object identity, ref DataFeedItem __result, SettingPropertyAttribute setting, IReadOnlyList<string> path, IReadOnlyList<string> grouping)
            {
                var field = (FieldInfo)fieldField.GetValue(identity);
                Type fieldType = field.FieldType;
                if (fieldType.BaseType != typeof(ModConfigurationKey)) return true;

                ModSettings.Msg($"field: {fieldType}");
                ModSettings.Msg($"field base: {fieldType.BaseType}");
                ModSettings.Msg($"field is key: {fieldType.BaseType == typeof(ModConfigurationKey)}");

                var type = fieldType.GetGenericArguments()[0];
                ModSettings.Msg($"type: {type}");
                /*if (setting is SettingIndicatorProperty)
                    {
                        __result = (DataFeedItem)_generateIndicator.MakeGenericMethod(type).Invoke(null, new object[4] { identity, setting, path, grouping });
                        return false;
                    }*/

                if (!Coder.IsEnginePrimitive(type))
                {
                    ModSettings.Msg("NON ENGINE PRIMITIVE");
                    __result = new DataFeedLabel(field.Name, path, grouping, "<color=red>" + field.Name + " (" + field.FieldType.GetNiceName() + ")</color>");
                    return false;
                }

                if (type == typeof(bool))
                {
                    DataFeedToggle dataFeedToggle = new();
                    InitBasePrefix(dataFeedToggle, identity, setting, path, grouping);
                    dataFeedToggle.InitSetupValue(GenerateFieldSyncModConfig<bool>(identity));
                    __result = dataFeedToggle;
                    return false;
                }

                if (type.IsEnum)
                {
                    ModSettings.Msg("ENUJM");
                    __result = (DataFeedItem)_generateEnumField.MakeGenericMethod(type).Invoke(null, new object[4] { identity, setting, path, grouping });
                    ModSettings.Msg(__result.ToString());
                    return false;
                }

                RangeAttribute rangeAttribute = field.GetCustomAttribute<RangeAttribute>();
                QuantityAttribute quantityAttribute = field.GetCustomAttribute<QuantityAttribute>();
                if (quantityAttribute != null)
                {
                    __result = (DataFeedItem)_generateQuantityField.MakeGenericMethod(quantityAttribute.QuantityType, type).Invoke(null, new object[6] { identity, setting, quantityAttribute, rangeAttribute, path, grouping });
                    return false;
                }

                if (rangeAttribute != null)
                {
                    __result = (DataFeedItem)_generateSlider.MakeGenericMethod(type).Invoke(null, new object[5] { identity, setting, rangeAttribute, path, grouping });
                    return false;
                }

                __result = (DataFeedItem)_generateValueField.MakeGenericMethod(type).Invoke(null, new object[4] { identity, setting, path, grouping });
                return false;



            }
            /*[HarmonyPrefix]
            [HarmonyPatch("GenerateCategory")]
            public static bool GenerateCategory(SettingCategoryInfo info, IReadOnlyList<string> path, ref DataFeedCategory __result)
            {
                Msg(info.Key);
                if (info.Key != "Mods") return true;

                Msg("Mods found");
                DataFeedCategory dataFeedCategory = new();
                dataFeedCategory.InitBase(info.Key, path, null, info.Key, info.Icon, null, null, null);
                if (info.OrderOffset != 0L)
                {
                    dataFeedCategory.InitSorting(info.OrderOffset);
                }
                __result = dataFeedCategory;

                return false;
            }
            // Fix breadcrumb names later
            [HarmonyPrefix]
            [HarmonyPatch("GenerateCategory")]*/



            [HarmonyPostfix]
            [HarmonyPatch(typeof(RootCategoryView), "FeedItemAdded")]
            public static void FeedItemAdded(DataFeedItem item) => ModSettings.Msg(item.ToString());
            [HarmonyPostfix]
            [HarmonyPatch(typeof(DataFeedItemMappingManager), methodName: "AddItem", argumentTypes: new Type[] { typeof(DataFeedItem)})]
            public static void AddItemMappingManager(DataFeedItem item) => ModSettings.Msg(item.ToString());
            [HarmonyPostfix]
            [HarmonyPatch(typeof(DataFeedItemMappingManager), methodName: "AddItem", argumentTypes: new Type[] { typeof(DataFeedItem), typeof(Slot)})]
            public static void AddItemMappingManager(DataFeedItem item, Slot root) => ModSettings.Msg(item.ToString(), root.ToString());

            [HarmonyPostfix]
            [HarmonyPatch(typeof(DataFeedItemMapper), methodName: "FindMapping")]
            public static void FindMapping(DataFeedItem item, DataFeedItemMap? __result) => ModSettings.Msg(item.ToString(), __result?.template.ToString());
        }
        /*        [HarmonyPatch(typeof(Settings))]
       static class SettingsInjection
       {
           [HarmonyPostfix]
           [HarmonyPatch("GetCategories")]
           public static void GetCategories(List<SettingCategoryInfo> list) => list.Add(Mods);
       }*/
    }
}
