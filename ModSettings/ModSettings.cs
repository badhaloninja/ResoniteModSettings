using FrooxEngine;
using FrooxEngine.UIX;
using Elements.Core;
using HarmonyLib;
using ResoniteModLoader;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Globalization;
using System.Dynamic;
using System.Linq.Expressions;

namespace ModSettings
{
    // Maybe consider to split into a partial class
    public class ModSettings : ResoniteMod
    {
        public override string Name => "ResoniteModSettings";
        public override string Author => "badhaloninja";
        public override string Version => "2.1.3";
        public override string Link => "https://github.com/badhaloninja/ResoniteModSettings";

        [AutoRegisterConfigKey]
        private static readonly ModConfigurationKey<float> ITEM_HEIGHT = new("itemHeight", "Determines height of config items like this one. You need to click on another page for it to apply.", () => 24);
        [AutoRegisterConfigKey]
        private static readonly ModConfigurationKey<bool> SHOW_INTERNAL = new("showInternal", "Whether to show internal use only config options, their text will be yellow.", () => false);
        [AutoRegisterConfigKey]
        private static readonly ModConfigurationKey<bool> SHOW_NAMES = new("showNames", "Whether to show the internal key names next to descriptions.", () => false);
        [AutoRegisterConfigKey]
        private static readonly ModConfigurationKey<bool> HIGHLIGHT_ITEMS = new("highlightAlternateItems", "Highlight alternate configuration items", () => false);
        [AutoRegisterConfigKey]
        private static readonly ModConfigurationKey<colorX> HIGHLIGHT_TINT = new("highlightColor", "Highlight color", () => RadiantUI_Constants.Sub.PURPLE.SetA(0.3f));
        [AutoRegisterConfigKey]
        private static readonly ModConfigurationKey<bool> RESET_INTERNAL = new("resetInternal", "Also reset internal use only config options, <b>Can cause unintended behavior</b>", () => false);
        [AutoRegisterConfigKey]
        private static readonly ModConfigurationKey<bool> PER_KEY_RESET = new("showPerKeyResetButtons", "Show reset buttons for each config key", () => false);
        [AutoRegisterConfigKey]
        private static readonly ModConfigurationKey<bool> SHOW_ALL_MODS = new("showAllMods", "Show mods without config items", () => true);


        // Test Variables
        [AutoRegisterConfigKey] // Huh dummy can be used as a spacer, neat
            private static readonly ModConfigurationKey<dummy> TEST_DUMMY = new("dummy", "---------------------------------------------------------------------------------------------------------------------------------");
            [AutoRegisterConfigKey]
            private static readonly ModConfigurationKey<bool> TEST_BOOL = new("testBool", "Test Boolean", () => true);
            [AutoRegisterConfigKey]
            private static readonly ModConfigurationKey<string> TEST_STRING = new("testStr", "Test String", () => "Value");
            [AutoRegisterConfigKey]
            private static readonly ModConfigurationKey<Key> TEST_KEYENUM = new("testKeyEnum", "Test Key Enum", () => Key.None);
            [AutoRegisterConfigKey]
            private static readonly ModConfigurationKey<Key> TEST_ENUM_NODEFAULT = new("testKeyEnumNoDefault", "Test Key Enum with no default");
            [AutoRegisterConfigKey]
            private static readonly ModConfigurationKey<int4> TEST_INTVECTOR = new("testIntVector", "Test int4", () => new int4(12), valueValidator: (value) => value.x == 12);
            [AutoRegisterConfigKey]
            private static readonly ModConfigurationKey<float3x3> TEST_float3x3 = new("testFloat3x3", "Test float3x3", () => float3x3.Identity);
            [AutoRegisterConfigKey]
            private static readonly ModConfigurationKey<colorX> TEST_COLOR = new("testColor", "Test Color", () => colorX.Blue);
            [AutoRegisterConfigKey]
            private static readonly ModConfigurationKey<Type> TEST_TYPE = new("testType", "Test Type", () => typeof(Button));
            [AutoRegisterConfigKey]
            private static readonly ModConfigurationKey<Uri> TEST_URI = new("testUri", "Test Uri", () => null);
            [AutoRegisterConfigKey]
            private static readonly ModConfigurationKey<Uri> TEST_INTERNAL = new("testInternal", "Test internal access only key, must be http or https", () => new Uri("https://example.com"), true, (uri) => uri != null && (uri.Scheme == "https" || uri.Scheme == "http"));
            [AutoRegisterConfigKey]
            private static readonly ModConfigurationKey<Uri> TEST_INTERNAL_NO_NULL_CHECK = new("testInternalNoNull", "Test internal access only key, must be http or https, error thrown on null", () => new Uri("https://example.com"), true, (uri) => uri.Scheme == "https" || uri.Scheme == "http");
            [AutoRegisterConfigKey]
            private static readonly ModConfigurationKey<float2x2> TEST_NAN_VECTOR_INTERNAL = new("testNanVectorInternal", "Test internal access only NaN Vector for pr #11", () => new float2x2(float.NaN, float.NaN, float.NaN, float.NaN), true);
            [AutoRegisterConfigKey]
            private static readonly ModConfigurationKey<string> TEST_LOCAL_KEY = new("testLocaleKey", "Settings.Locale.ChangeLanguage", () => "Locale Test", true, (str) => str == "Locale Test");
            [AutoRegisterConfigKey]
            private static readonly ModConfigurationKey<string> TEST_ERROR_ON_STR_EMPTY = new("testErrOnStringEmpty", "Test error on string empty", () => "Value", valueValidator: (str) =>
            {
                if (string.IsNullOrWhiteSpace(str))
                    throw new ArgumentNullException(nameof(str));
                return true;
            });
            [Range(0,1)]
            [AutoRegisterConfigKey]
            private static readonly ModConfigurationKey<float> TEST_SLIDER = new("testSlider", "Test Slider", () => 0f);
        //

        internal static ModSettings Current;
        internal static ModConfiguration Config;
        private static RadiantDashScreen CurrentScreen;
        private static readonly Dictionary<string, FoundMod> foundModsDictionary = new();

        private static Slot configKeysRootSlot;
        private static Slot modButtonsRoot;

        private static readonly MethodInfo generateConfigFieldMethod = typeof(ModSettingsScreen).GetMethod(nameof(ModSettingsScreen.GenerateConfigField));
        private static readonly MethodInfo fireConfigurationChangedEventMethod = typeof(ModConfiguration).GetMethod("FireConfigurationChangedEvent", BindingFlags.NonPublic | BindingFlags.Instance);

        internal static readonly string _internalConfigUpdateLabel = "ModSettingsScreen Edit Value";
        internal static readonly string _internalConfigResetLabel = "ModSettingsScreen Config Reset";

        private static readonly Dictionary<ModConfigurationKey, string> ConfigKeyVariableNames = new(){
            { SHOW_NAMES, "Config/_showFullName" },
            { SHOW_INTERNAL, "Config/_showInternal" },
            { RESET_INTERNAL, "Config/_resetInternal" },
            { PER_KEY_RESET, "Config/_showResetButtons" },
            { HIGHLIGHT_ITEMS, "Config/_highlightKeys" },
            { HIGHLIGHT_TINT, "Config/_highlightTint" },
            { SHOW_ALL_MODS, "Config/_showAllMods" }
        };

        public override void OnEngineInit()
        {
            Current = this;

            Config = GetConfiguration();
            ModConfiguration.OnAnyConfigurationChanged += OnConfigurationChanged;
            Config.OnThisConfigurationChanged += OnThisConfigurationChanged;

            Harmony harmony = new("ninja.badhalo.ModSettings");
            harmony.PatchAll();

            SettingsDataFeedInjector.SettingsInjection.PatchOtherMethods(harmony);
            SettingsDataFeedInjector.InjectCategory(SettingCategoryDefinitions.Mods, "Mods");
        }

       /* private static FieldInfo worker_assemblies;
        private void WorkerInjector(Assembly assembly)
        {
            if (worker_assemblies == null)
            {
                worker_assemblies = AccessTools.Field(typeof(WorkerInitializer), "assemblies");
            }

            Assembly[] assemblies = (Assembly[])worker_assemblies.GetValue(null);

            var hashset = assemblies.ToHashSet();

            hashset.Add(assembly);

            worker_assemblies.SetValue(null, hashset.ToArray());
        }*/
       

        [HarmonyPatch(typeof(UserspaceScreensManager))]
        static class Patches
        {
            [HarmonyPostfix]
            [HarmonyPatch("SetupDefaults")]
            // If you don't have an account or sign out this will be generated
            public static void SetupDefaults(UserspaceScreensManager __instance) => ModSettingsScreen.GenerateModSettingsScreen(__instance);
            [HarmonyPostfix]
            [HarmonyPatch("OnLoading")]
            // If you have an account/sign in OnLoading triggers and replaces the contents generated by SetupDefaults
            public static void OnLoading(UserspaceScreensManager __instance) => ModSettingsScreen.GenerateModSettingsScreen(__instance);

            [HarmonyReversePatch]
            [HarmonyPatch(typeof(RadiantDashScreen), "BuildBackground")] // This method is protected for some reason
            public static void BuildScreenBackground(RadiantDashScreen instance, UIBuilder ui) => throw new NotImplementedException("It's a stub");
        }

        static class ModSettingsScreen
        {
            public static void GenerateModSettingsScreen(UserspaceScreensManager __instance)
            {
                bool screenExists = CurrentScreen != null && !CurrentScreen.IsRemoved;
                if (__instance.World != Userspace.UserspaceWorld || screenExists) return;

                RadiantDash dash = __instance.Slot.GetComponentInParents<RadiantDash>();

                //if (dash.GetScreen<RadiantDashScreen>(screen => screen.Name == "Mods") != null) return;

                CurrentScreen = dash.AttachScreen("Mods", RadiantUI_Constants.Hero.PURPLE, OfficialAssets.Graphics.Icons.Dash.Tools);
                
                Slot screenSlot = CurrentScreen.Slot;
                screenSlot.OrderOffset = 256; // Settings Screen is 60, Exit screen is set to int.MaxValue 
                screenSlot.PersistentSelf = false; // So it doesn't save

                var ui = new UIBuilder(CurrentScreen.ScreenCanvas);
                RadiantUI_Constants.SetupDefaultStyle(ui);
                Patches.BuildScreenBackground(CurrentScreen, ui);

                ui.NestInto(ui.Empty("Split"));
                ui.SplitHorizontally(0.25f, out RectTransform left, out RectTransform right);


                ui.NestInto(left); // Mod List

                ui.HorizontalFooter(50f, out RectTransform modsFooter, out RectTransform modsContent);
                ui.NestInto(modsFooter);

                var saveAllBtn = ui.Button("Save All");
                saveAllBtn.RectTransform.OffsetMax.Value = new(0f, -4f); // padding
                saveAllBtn.LocalPressed += SaveAllConfigs;
                
                // List Mods
                ui.NestInto(modsContent);
                var scrollRect = ui.ScrollArea<Image>(Alignment.TopCenter, out _, out var scrollMask);
                // Setup rounded scroll rect
                var maskSprite = scrollRect.Slot.CopyComponent(RadiantUI_Constants.GetButtonSprite(scrollRect.World));
                maskSprite.Scale.Value = 0.04f;

                scrollMask.Sprite.TrySet(maskSprite);
                scrollMask.NineSliceSizing.Value = NineSliceSizing.TextureSize;
                //

                ui.VerticalLayout(4f);

                ui.FitContent(SizeFit.Disabled, SizeFit.PreferredSize);

                modButtonsRoot = ui.Root;
                GenerateModButtons(ui);


                // Config Panel
                ui.NestInto(right);

                BuildInfoPage(ui, out RectTransform configUiRoot); // Shows when no mod is selected

                ui.NestInto(configUiRoot);
                var splitList = ui.SplitVertically(96f, 884f, 100f);
                var header = splitList[0];
                var content = splitList[1];
                var footer = splitList[2];


                ui.NestInto(header);
                ui.Style.PreferredHeight = 64f;
                ui.Text("")
                    .Content.SyncWithVariable("Config/SelectedMod.Name");



                ui.NestInto(footer);

                var offset = new float2(8f, 8f);
                footer.OffsetMin.Value += offset;
                footer.OffsetMax.Value += -offset;

                var splits = ui.SplitHorizontally(0.25f, 0.55f, 0.25f);

                ui.NestInto(splits[0]); //Author (Left)
                ui.Text(Current.Author)
                    .Content.SyncWithVariable("Config/SelectedMod.Author");

                splits[0].OffsetMin.Value += offset.x_;
                splits[0].OffsetMax.Value += -offset.x_;

                ui.NestInto(splits[1]); //Link (Center)
                splits[1].OffsetMin.Value += offset.x_;
                splits[1].OffsetMax.Value += -offset.x_;

                var linkText = ui.Text("");
                var hyperlink = linkText.Slot.AttachComponent<Hyperlink>();
                hyperlink.URL.SyncWithVariable("Config/SelectedMod.Uri");
                linkText.Content.DriveFrom(hyperlink.URL, "{0}"); // Drive the text 
                var hyperlinkButton = linkText.Slot.AttachComponent<Button>();
                hyperlinkButton.SetupBackgroundColor(linkText.Color); // Drive the text color

                ui.NestInto(splits[2]); // Version (Right)
                splits[2].OffsetMin.Value += offset.x_;
                splits[2].OffsetMax.Value += -offset.x_;

                var versionText = ui.Text(Current.Version);
                versionText.Content.SyncWithVariable("Config/SelectedMod.Version");



                // Setup config root
                RadiantUI_Constants.SetupDefaultStyle(ui);
                ui.NestInto(content);
                var booleanSwitcher = ui.Root.AttachComponent<BooleanSwitcher>();
                var bvd = ui.Root.AttachComponent<BooleanValueDriver<int>>();
                bvd.TrueValue.Value = 1;

                bvd.TargetField.TryLink(booleanSwitcher.ActiveIndex);
                bvd.State.Value = true;

                var hasKeysVar = ui.Root.AttachComponent<DynamicField<bool>>();
                hasKeysVar.VariableName.Value = "Config/SelectedMod.HasKeys";
                hasKeysVar.TargetField.TrySet(bvd.State);


                // No mod keys 
                ui.Panel();
                booleanSwitcher.Targets.Add().Target = ui.Root.ActiveSelf_Field;

                ui.Text("This mod does not have any configuration keys.");


                ui.NestOut();

                ui.Style.PreferredHeight = 45f;
                ui.ScrollArea(Alignment.TopCenter);
                booleanSwitcher.Targets.Add().Target = ui.Root.ActiveSelf_Field;
                ui.VerticalLayout(4f, 24f);
                ui.FitContent(SizeFit.Disabled, SizeFit.PreferredSize);




                // Config Items Vertical layout
                ui.Style.PreferredHeight = -1f;
                ui.VerticalLayout(4f); // New layout to easily clear config items and not delete buttons
                ui.FitContent(SizeFit.Disabled, SizeFit.PreferredSize);
                configKeysRootSlot = ui.Root;
                //

                configKeysRootSlot.CreateVariable(ConfigKeyVariableNames[SHOW_NAMES], Config.GetValue(SHOW_NAMES));
                configKeysRootSlot.CreateVariable(ConfigKeyVariableNames[SHOW_INTERNAL], Config.GetValue(SHOW_INTERNAL));
                configKeysRootSlot.CreateVariable(ConfigKeyVariableNames[PER_KEY_RESET], Config.GetValue(PER_KEY_RESET));
                configKeysRootSlot.CreateVariable(ConfigKeyVariableNames[HIGHLIGHT_TINT], Config.GetValue(HIGHLIGHT_TINT));
                configKeysRootSlot.CreateVariable(ConfigKeyVariableNames[HIGHLIGHT_ITEMS], Config.GetValue(HIGHLIGHT_ITEMS));
                configKeysRootSlot.CreateVariable(ConfigKeyVariableNames[SHOW_ALL_MODS], Config.GetValue(SHOW_ALL_MODS));

                // Controls
                ui.NestOut();
                RadiantUI_Constants.SetupDefaultStyle(ui);
                ui.HorizontalLayout(4f).PaddingTop.Value = 8f;

                ui.Style.PreferredHeight = 24f;
                var saveCurrentBtn = ui.Button("Save Settings");
                saveCurrentBtn.RequireLockInToPress.Value = true; // So you can scroll with laser without worrying about pressing it
                saveCurrentBtn.LocalPressed += SaveCurrentConfig;

                var defaultsBtn = ui.Button("Reset Default Settings");
                var defaultsBtnLabelDrive = defaultsBtn.Label.Slot.AttachComponent<BooleanValueDriver<string>>();
                defaultsBtnLabelDrive.FalseValue.Value = "Reset Default Settings";
                defaultsBtnLabelDrive.TrueValue.Value = $"Reset Default Settings <size=90%><color={RadiantUI_Constants.Hero.RED_HEX}>Including Internal</color></size>";

                defaultsBtnLabelDrive.State.SyncWithVariable(ConfigKeyVariableNames[RESET_INTERNAL]);
                defaultsBtnLabelDrive.TargetField.TryLink(defaultsBtn.LabelTextField);

                defaultsBtn.RequireLockInToPress.Value = true; // So you can scroll with laser without worrying about pressing it
                defaultsBtn.LocalReleased += ResetCurrentConfig;


                var space = screenSlot.AttachComponent<DynamicVariableSpace>();
                space.SpaceName.Value = "Config";

                var selectedModVar = screenSlot.AttachComponent<DynamicValueVariable<string>>();
                selectedModVar.VariableName.Value = "Config/SelectedMod";
                selectedModVar.Value.OnValueChange += (field) => {
                    try
                    {
                        GenerateConfigItems(field.Value); // Regen Config items on change
                    }
                    catch (Exception e) { Error(e); }
                };
            }
            private static void BuildInfoPage(UIBuilder ui, out RectTransform content)
            {
                Slot descRoot = ui.Next("Info"); // New Slot for the ModSettings info
                ui.Nest();

                ui.HorizontalFooter(100f, out RectTransform footer, out RectTransform body);
                ui.NestInto(body);

                ui.VerticalLayout(4f, 24f, childAlignment: Alignment.MiddleCenter);
                ui.FitContent(SizeFit.Disabled, SizeFit.PreferredSize);

                ui.Text($"<size=150%>{Current.Name}</size>"); //Title

                ui.Spacer(45f);
                ui.Style.PreferredHeight = 250f;
                string Desc = "ResoniteModSettings is a modification to the base game that allows the users to directly interact with the mods that they have installed onto their game from inside the application.\n\nCurrently only supports configs that are valid DynamicValueVariable types and those of type Type meaning <b>Arrays are not supported</b> <size=30%>including any other collections</size>";
                ui.Text(Desc, alignment: Alignment.MiddleCenter);


                ui.NestInto(footer);
                var offset = new float2(8f, 8f);
                ui.CurrentRect.OffsetMin.Value += offset;
                ui.CurrentRect.OffsetMax.Value += -offset;

                var splits = ui.SplitHorizontally(0.25f, 0.55f, 0.25f);


                ui.NestInto(splits[0]); //Author (Left)
                splits[0].OffsetMin.Value += offset.x_;
                splits[0].OffsetMax.Value += -offset.x_;

                ui.Text(Current.Author);


                ui.NestInto(splits[1]); //Link (Center)
                splits[1].OffsetMin.Value += offset.x_;
                splits[1].OffsetMax.Value += -offset.x_;

                var linkText = ui.Text(Current.Link);
                var hyperlink = linkText.Slot.AttachComponent<Hyperlink>();
                hyperlink.URL.Value = new(Current.Link);

                var hyperlinkButton = linkText.Slot.AttachComponent<Button>();
                hyperlinkButton.SetupBackgroundColor(linkText.Color); // Drive the text color

                
                ui.NestInto(splits[2]); // Version (Right)
                splits[2].OffsetMin.Value += offset.x_;
                splits[2].OffsetMax.Value += -offset.x_;

                var versionText = ui.Text(Current.Version);


                ui.NestInto(descRoot.Parent); // Go up one from Info

                var contentRoot = ui.Empty("Content");
                content = contentRoot.GetComponent<RectTransform>();
                // Drive the state of info based on if a mod is selected
                var dynVar = ui.Root.AttachComponent<DynamicValueVariable<string>>();
                dynVar.VariableName.Value = "Config/SelectedMod";

                var equalityDriver = ui.Root.AttachComponent<ValueEqualityDriver<string>>(); // Put value equality driver on the parent of Info
                equalityDriver.TargetValue.TrySet(dynVar.Value);
                equalityDriver.Target.TrySet(descRoot.ActiveSelf_Field); // Drive boolean value driver

                var invertEqualityDriver = ui.Root.AttachComponent<ValueEqualityDriver<bool>>(); // Drive contentRoot.Active to !descRoot.Active
                invertEqualityDriver.TargetValue.TrySet(descRoot.ActiveSelf_Field);
                invertEqualityDriver.Target.TrySet(contentRoot.ActiveSelf_Field);

                ui.Style.PreferredWidth = -1f;
                RadiantUI_Constants.SetupDefaultStyle(ui); // Reset style
            }

            internal static void GenerateModButtons(UIBuilder ui)
            {
                RadiantUI_Constants.SetupDefaultStyle(ui);
                ui.Style.PreferredHeight = 90f;

                var dVar = ui.Root.GetComponentOrAttach<DynamicValueVariable<string>>(out bool varAttached);
                if (varAttached)
                {
                    dVar.VariableName.Value = "Config/SelectedMod";
                }
                bool haveModsBeenListed = foundModsDictionary.Count == 0;

                foreach (ResoniteModBase modBase in ModLoader.Mods())
                {
                    string modKey = $"{modBase.Author}.{modBase.Name}";

                    // To prevent adding mods multiple times if the screen is regenerated
                    if (haveModsBeenListed) foundModsDictionary.Add(modKey, new FoundMod(modBase));

                    var button = ui.Button(modBase.Name);

                    // Adds a little bit of padding to the text, to prevent long mod names from touching the edges
                    var textRect = button.Slot[0].GetComponent<RectTransform>();
                    textRect.OffsetMin.Value = new float2(24, 0);
                    textRect.OffsetMax.Value = new float2(-24, 0);

                    var deselected = new OptionDescription<string>(null, label: modBase.Name, buttonColor: RadiantUI_Constants.BUTTON_COLOR);
                    var selected = new OptionDescription<string>(modKey, label: modBase.Name, buttonColor: RadiantUI_Constants.HIGHLIGHT_COLOR);

                    button.ConvertTintToAdditive();
                    button.SetupValueToggle(dVar.Value, modKey, selected, deselected);



                    int validKeyCount = 0;
                    bool allValidKeysInternal = false;

                    if (modBase.GetConfiguration() != null)
                    {
                        var validKeys = modBase.GetConfiguration()?.ConfigurationItemDefinitions
                            ?.Where(c => c.ValueType() == typeof(Type) || (bool)typeof(DynamicValueVariable<>).MakeGenericType(c.ValueType()).GetProperty("IsValidGenericType").GetValue(null));

                        validKeyCount = validKeys.Count();

                        allValidKeysInternal = validKeys.All(c => c.InternalAccessOnly);
                    }

                    Debug($"{modBase.Name} has {validKeyCount} available config items");

                    if (validKeyCount == 0)
                    {
                        button.Slot.ActiveSelf_Field.DriveFromVariable(ConfigKeyVariableNames[SHOW_ALL_MODS]);
                    }
                    else if (allValidKeysInternal)
                    {
                        var funnyAndGate = button.Slot.AttachComponent<BooleanValueDriver<bool>>();
                        funnyAndGate.TargetField.TryLink(button.Slot.ActiveSelf_Field);
                        funnyAndGate.State.DriveFromVariable(ConfigKeyVariableNames[SHOW_ALL_MODS]);
                        funnyAndGate.TrueValue.Value = true;
                        funnyAndGate.FalseValue.DriveFromVariable(ConfigKeyVariableNames[SHOW_INTERNAL]);
                    }
                }

                Debug($"Found {foundModsDictionary.Count} mods");
            }

            public static void GenerateConfigItems(string SelectedMod = null)
            {
                configKeysRootSlot.DestroyChildren(); // Clear configs

                if(SelectedMod == null)
                    configKeysRootSlot.TryReadDynamicValue("Config/SelectedMod", out SelectedMod);
                

                // Reset footer
                configKeysRootSlot.TryWriteDynamicValue("Config/SelectedMod.Name", "");
                configKeysRootSlot.TryWriteDynamicValue("Config/SelectedMod.Author", "");
                configKeysRootSlot.TryWriteDynamicValue("Config/SelectedMod.Version", "");
                configKeysRootSlot.TryWriteDynamicValue<Uri>("Config/SelectedMod.Uri", null);

                if (string.IsNullOrWhiteSpace(SelectedMod) || !foundModsDictionary.TryGetValue(SelectedMod, out FoundMod mod) || mod == null)
                    return; // Skip if no mod is selected

                // Set footer values
                configKeysRootSlot.TryWriteDynamicValue("Config/SelectedMod.Name", mod.Owner.Name);
                configKeysRootSlot.TryWriteDynamicValue("Config/SelectedMod.Author", mod.Owner.Author);
                configKeysRootSlot.TryWriteDynamicValue("Config/SelectedMod.Version", mod.Owner.Version);

                Uri.TryCreate(mod.Owner.Link, UriKind.RelativeOrAbsolute, out Uri modUri); // Catch invalid uris just incase
                configKeysRootSlot.TryWriteDynamicValue("Config/SelectedMod.Uri", modUri);


                UIBuilder ui = new(configKeysRootSlot);
                RadiantUI_Constants.SetupDefaultStyle(ui);

                ui.Style.PreferredHeight = Config.GetValue(ITEM_HEIGHT);

                ModConfiguration config = mod.Config;


                var foundKeys = config?.ConfigurationItemDefinitions.Where(key => config == Config || !key.InternalAccessOnly || Config.GetValue(SHOW_INTERNAL));

                if (config == null || !foundKeys.Any())
                {
                    configKeysRootSlot.TryWriteDynamicValue("Config/SelectedMod.HasKeys", false);
                    return;
                }


                var createdItemCount = 0;
                foreach (ModConfigurationKey key in foundKeys)
                { // Generate field for every supported config
                    var item = GenerateConfigFieldOfType(key.ValueType(), ui, SelectedMod, config, key);
                    if(item == null) continue;


                    item.ForeachComponentInChildren<Button>(button => button.RequireLockInToPress.Value = true);


                    bool shouldShowItemBg = Config.GetValue(HIGHLIGHT_ITEMS) || config == Config;
                    if (shouldShowItemBg && createdItemCount % 2 == 1)
                    {
                        var bg = item.AddSlot("Background");
                        bg.ActiveSelf_Field.DriveFromVariable(ConfigKeyVariableNames[HIGHLIGHT_ITEMS]);

                        bg.OrderOffset = -1;
                        var rect = bg.AttachComponent<RectTransform>();
                        bg.AttachComponent<IgnoreLayout>();
                        rect.AnchorMin.Value = new float2(-0.005f, 0f);
                        rect.AnchorMax.Value = new float2(1.005f, 1f);
                        bg.AttachComponent<Image>().Tint.DriveFromVariable(ConfigKeyVariableNames[HIGHLIGHT_TINT]);
                    }
                    createdItemCount++;
                }
                if(createdItemCount != 0) configKeysRootSlot.TryWriteDynamicValue("Config/SelectedMod.HasKeys", true);
            }

            public static Slot GenerateConfigFieldOfType(Type type, UIBuilder ui, string ModName, ModConfiguration config, ModConfigurationKey key)
            { // Generics go brr
                var genMethod = generateConfigFieldMethod.MakeGenericMethod(type); // Convert to whatever type requested
                object[] args = new object[] { ui, ModName, config, key }; // Pass the arguments
                
                return (Slot)genMethod.Invoke(null, args); // Run the method
            }
            public static Slot GenerateConfigField<T>(UIBuilder ui, string ModName, ModConfiguration config, ModConfigurationKey key)
            {
                bool isType = typeof(T) == typeof(Type);
                if (!(isType || DynamicValueVariable<T>.IsValidGenericType)) return null; // Check if supported type
                
                if (isType) Debug($"GenerateConfigField for type Type");

                string configName = $"{ModName}.{key.Name}";
                RadiantUI_Constants.SetupEditorStyle(ui);

                ui.Style.MinHeight = Config.GetValue(ITEM_HEIGHT);

                Slot root = ui.Empty("ConfigElement");
                if (key.InternalAccessOnly) root.ActiveSelf_Field.DriveFromVariable(ConfigKeyVariableNames[SHOW_INTERNAL]);

                ui.NestInto(root);

                SyncField<T> syncField;

                foundModsDictionary[ModName].ConfigKeyFields.TryGetValue(key, out FieldInfo fieldInfo);


                if (!isType)
                {
                    var dynvar = root.AttachComponent<DynamicValueVariable<T>>();
                    dynvar.VariableName.Value = $"Config/{configName}";

                    syncField = dynvar.Value;
                    fieldInfo ??= dynvar.GetSyncMemberFieldInfo(4);
                } else
                {
                    var dynvar = root.AttachComponent<DynamicReferenceVariable<SyncType>>();
                    dynvar.VariableName.Value = $"Config/{configName}";

                    var typeField = root.AttachComponent<TypeField>();
                    dynvar.Reference.TrySet(typeField.Type);

                    syncField = typeField.Type as SyncField<T>;
                    fieldInfo ??= typeField.GetSyncMemberFieldInfo(3);
                }


                var typedKey = key as ModConfigurationKey<T>;

                
                T defaultValue = default;
                if (Coder<T>.IsSupported) defaultValue = Coder<T>.Default;

                var initialValue = config.TryGetValue(key, out object currentValue) ? (T)currentValue : defaultValue; // Set initial value

                syncField.Value = initialValue;
                syncField.OnValueChange += (syncF) => HandleConfigFieldChange(syncF, config, typedKey);

                // Validate the value changes
                // LocalFilter changes the value passed to InternalSetValue
                syncField.LocalFilter = (value, field) => ValidateConfigField(value, config, typedKey, defaultValue);


                RadiantUI_Constants.SetupDefaultStyle(ui);
                ui.Style.TextAutoSizeMax = Config.GetValue(ITEM_HEIGHT);

                bool nameAsKey = string.IsNullOrWhiteSpace(key.Description);
                string localeText = nameAsKey ? key.Name : key.Description;
                string format = "{0}";
                if (Config.GetValue(SHOW_NAMES) && !nameAsKey)
                {
                    format = $"<i><b>{key.Name}</i></b> - " + "{0}";
                }

                var internalFormat = "{0}";
                if (key.InternalAccessOnly) internalFormat = $"<color={RadiantUI_Constants.Hero.YELLOW_HEX}>{{0}}</color>";




                // Build ui
                
                var localized = new LocaleString(localeText, string.Format(internalFormat,format), true, true, null);
                ui.HorizontalElementWithLabel<Component>(localized, 0.7f, () =>
                {// Using HorizontalElementWithLabel because it formats nicer than SyncMemberEditorBuilder with text
                    if(config == Config && !nameAsKey)
                    {
                        var localeDriver = root.GetComponentInChildren<LocaleStringDriver>();
                        if(localeDriver != null)
                        {
                            var nameDrive = localeDriver.Slot.AttachComponent<BooleanValueDriver<string>>();

                            nameDrive.State.DriveFromVariable(ConfigKeyVariableNames[SHOW_NAMES]);

                            nameDrive.FalseValue.Value = string.Format(internalFormat, "{0}");
                            nameDrive.TrueValue.Value = string.Format(internalFormat, $"<i><b>{key.Name}</i></b> - {{0}}");


                            nameDrive.TargetField.TrySet(localeDriver.Format);
                        }
                    }

                    ui.HorizontalLayout(4f, childAlignment: Alignment.MiddleLeft).ForceExpandHeight.Value = false;

                    ui.Style.FlexibleWidth = 10f;
                    
                    SyncMemberEditorBuilder.Build(syncField, null, fieldInfo, ui); // Using null for name makes it skip generating text
                    ui.Style.FlexibleWidth = -1f;

                    // Update the root layout element so I don't need to do checks for every field size
                    var fieldElement = ui.Root[0]?.GetComponent<LayoutElement>();
                    if(fieldElement != null)
                    {
                        // account for user's config value
                        float diff = Config.GetValue(ITEM_HEIGHT) / 24f;
                        fieldElement.MinHeight.Value*=diff;

                        root.GetComponent<LayoutElement>().MinHeight.Value = fieldElement.MinHeight.Value;


                        // go over nested elements and apply new size
                        var layouts = fieldElement.Slot.GetComponentsInChildren<LayoutElement>(element=>element.MinHeight.Value == 24f);
                        foreach(LayoutElement layout in layouts)
                        {
                            layout.MinHeight.Value = Config.GetValue(ITEM_HEIGHT);
                        }
                    }


                    ui.Root.ForeachComponentInChildren<Text>((text) =>
                    { // Make value path text readable
                        // XYZW, RGBA, etc.
                        if (text.Slot.Parent.GetComponent<Button>() != null) return; // Ignore text under buttons
                        text.Color.Value = RadiantUI_Constants.TEXT_COLOR;
                    });

                    AddResetKeyButton(ui, config, typedKey);
                    ui.NestOut();

                    return null; // HorizontalElementWithLabel requires a return type that implements a component
                });

                ui.Style.MinHeight = -1f;
                ui.NestOut();

                return root;
            }


            private static void AddResetKeyButton<T>(UIBuilder ui, ModConfiguration modConfiguration, ModConfigurationKey<T> configKey)
            {
                if (modConfiguration != Config && !Config.GetValue(PER_KEY_RESET)) return;

                ui.PushStyle();
                ui.Style.MinHeight = Config.GetValue(ITEM_HEIGHT);
                ui.Style.MinWidth = Config.GetValue(ITEM_HEIGHT);
                ui.Panel();
                ui.PopStyle();

                ui.Root.ActiveSelf_Field.DriveFromVariable(ConfigKeyVariableNames[PER_KEY_RESET]);

                var btn = ui.Button("🗘");
                btn.RectTransform.Pivot.Value = new float2(0f, 0.5f);
                btn.Slot.AttachComponent<AspectRatioFitter>();
                btn.LocalPressed += (btn, evt) => ResetConfigKey(modConfiguration, configKey);

                if (configKey.InternalAccessOnly) btn.Label.Color.Value = RadiantUI_Constants.Hero.RED;

                ui.NestOut();
            }


            private static T ValidateConfigField<T>(T value, ModConfiguration modConfiguration, ModConfigurationKey<T> configKey, T defaultValue)
            {
                bool isValid = false;

                try {
                    isValid = configKey.Validate(value);
                } catch (Exception e) {
                    //optionsRoot.LocalUser.IsDirectlyInteracting()
                    
                    string valueString = $"the value \"{value}\"";

                    if (value == null)
                        valueString = "a null value";
                    else if (string.IsNullOrWhiteSpace(value.ToString())) 
                        valueString += " (This value is not null)";

                    if (configKeysRootSlot.LocalUser.IsDirectlyInteracting())
                    {
                        Debug($"Validation method for configuration item {configKey.Name} from {modConfiguration.Owner.Name} has thrown an error for {valueString}\n\tThis was hidden as the user is currently editing a field", e);
                    } else
                    {
                        Error($"Validation method for configuration item {configKey.Name} from {modConfiguration.Owner.Name} has thrown an error for {valueString}", e);
                    }
                    
                }

                if (!isValid)
                { // Fallback if validation fails
                    Debug($"Failed Validation for {modConfiguration.Owner.Name}.{configKey.Name}");

                    bool isSet = modConfiguration.TryGetValue(configKey, out T configValue);
                    return isSet ? configValue : defaultValue; // Set to old value if is set Else set to default for that value
                }
                return value;
            }
            private static void HandleConfigFieldChange<T>(SyncField<T> syncField, ModConfiguration modConfiguration, ModConfigurationKey<T> configKey)
            {
                bool isSet = modConfiguration.TryGetValue(configKey, out T configValue);
                if (isSet && (Equals(configValue, syncField.Value) || !Equals(syncField.Value, syncField.Value))) return; // Skip if new value is unmodified or is logically inconsistent (self != self)

                try {
                    if (!configKey.Validate(syncField.Value)) return;
                } catch { return; }

                modConfiguration.Set(configKey, syncField.Value, _internalConfigUpdateLabel);
            }


            private static int SaveAllConfigs()
            {
                Debug("Save All Configs");
                int errCount = 0;
                foreach (FoundMod mod in foundModsDictionary.Values)
                { // Iterate over every mod with configs
                    if (mod.Config == null) continue;
                    Debug($"Saving Config for {mod.Owner.Name}");
                    try
                    {
                        mod.Config.Save(); // Save config
                    }
                    catch (Exception e)
                    {
                        errCount++;
                        Error($"Failed to save Config for {mod.Owner.Name}");
                        Error(e);
                    }
                }
                return errCount;
            }
            private static void SaveAllConfigs(IButton button, ButtonEventData data)
            {
                button.LabelText = "Saving"; // Saves so fast this might be unnecessary 

                var errCount = SaveAllConfigs();

                if (errCount == 0)
                { // Show Saved! for 1 second
                    button.LabelText = "Saved!";
                    button.RunInSeconds(1f, () => button.LabelText = "Save All");
                    return;
                };
                // Errors

                button.Enabled = false;
                button.LabelText = $"<color={RadiantUI_Constants.Hero.RED_HEX}>Failed to save {errCount} configs\n(Check Logs)</color>";
                button.RunInSeconds(5f, () => // Show error count for 5 seconds
                {
                    button.Enabled = true;
                    button.LabelText = "Save All";
                });
            }
            private static void SaveCurrentConfig(IButton button, ButtonEventData data)
            {
                button.Slot.TryReadDynamicValue("Config/SelectedMod", out string selectedMod);
                if (string.IsNullOrWhiteSpace(selectedMod) || !foundModsDictionary.TryGetValue(selectedMod, out FoundMod mod) || mod == null || mod.Config == null)
                    return;
                button.LabelText = "Saving"; // Saves so fast this might be unnecessary 

                Debug($"Saving Config for {mod.Owner.Name}");
                try
                {
                    mod.Config.Save(); // Save config
                    button.LabelText = "Saved!"; // Show Saved! for 1 second

                    button.RunInSeconds(1f, () => button.LabelText = "Save Settings");
                }
                catch (Exception e)
                {
                    button.Enabled = false;
                    button.LabelText = $"<color={RadiantUI_Constants.Hero.RED_HEX}>An Error Occurred\n(Check Logs)</color>";
                    button.RunInSeconds(5f, () => // Show error for 5 seconds
                    {
                        button.Enabled = true;
                        button.LabelText = "Save Settings";
                    });
                    Error($"Failed to save Config for {mod.Owner.Name}");
                    Error(e);
                }
            }


            private static void ResetCurrentConfig(IButton button, ButtonEventData data)
            {
                button.Slot.TryReadDynamicValue("Config/SelectedMod", out string selectedMod);
                if (string.IsNullOrWhiteSpace(selectedMod) || !foundModsDictionary.TryGetValue(selectedMod, out FoundMod mod) || mod == null || mod.Config == null)
                    return;
                var config = mod.Config;

                bool resetInternal = Config.GetValue(RESET_INTERNAL);
                foreach (ModConfigurationKey key in config.ConfigurationItemDefinitions)
                { // Generate field for every supported config
                    if (!resetInternal && key.InternalAccessOnly) continue;

                    ResetConfigKey(config, key);
                }
            }
            private static void ResetConfigKey(ModConfiguration config, ModConfigurationKey key)
            {
                config.Unset(key);

                // Unset does not trigger the config changed event
                fireConfigurationChangedEventMethod.Invoke(config, new object[] { key, _internalConfigResetLabel });

                // Get default type
                object value = key.TryComputeDefault(out object defaultValue) ? defaultValue : key.ValueType().GetDefaultValue(); // How did I miss this extension??

                configKeysRootSlot.TryWriteDynamicValueOfType(key.ValueType(), $"Config/{config.Owner.Name}.{key.Name}", value);
            }
        }

        private static void OnConfigurationChanged(ConfigurationChangedEvent @event)
        {
            if (@event.Label == _internalConfigUpdateLabel) return;
            Debug($"ConfigurationChangedEvent fired for mod \"{@event.Config.Owner.Name}\" Config \"{@event.Key.Name}\"");
            if (configKeysRootSlot == null) return; // Skip if options root hasn't been generated yet


            if (!@event.Config.TryGetValue(@event.Key, out object value)) return; // Skip if failed to get the value
            string modName = $"{@event.Config.Owner.Author}.{@event.Config.Owner.Name}";
            configKeysRootSlot.SyncWriteDynamicValueType(@event.Key.ValueType(), $"Config/{modName}.{@event.Key.Name}", value);
        }
        private static void OnThisConfigurationChanged(ConfigurationChangedEvent @event)
        {
            if (configKeysRootSlot == null) return; // Skip if options root hasn't been generated yet

            if (ConfigKeyVariableNames.ContainsKey(@event.Key))
            {
                configKeysRootSlot.SyncWriteDynamicValueType(@event.Key.ValueType(), ConfigKeyVariableNames[@event.Key], Config.GetValue(@event.Key));
            }
        }

    }
}