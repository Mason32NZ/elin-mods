using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using BepInEx;
using BepInEx.Configuration;
using HarmonyLib;

namespace MaterialAlchemy;

internal static class ModInfo
{
    internal const string Guid = "m32.material_alchemy";
    internal const string Name = "Material Alchemy";
    internal const string Version = "2026.10.31.1"; //yyyy.mm.dd.v
}

internal sealed class RecipeDefinition
{
    internal RecipeDefinition(string key, string section, string station, string input, int inputCount, string output, int outputCount, bool direct)
    {
        Key = key;
        Section = section;
        Station = station;
        Input = input;
        InputCount = inputCount;
        Output = output;
        OutputCount = outputCount;
        Direct = direct;
    }

    internal string Key { get; }
    internal string Section { get; }
    internal string Station { get; }
    internal string Input { get; }
    internal int InputCount { get; }
    internal string Output { get; }
    internal int OutputCount { get; }
    internal bool Direct { get; }
    internal ConfigEntry<bool> Enabled { get; set; } = null!;
    internal int DirectId { get; set; }
    internal SourceRecipe.Row? DirectRow { get; set; }

    internal string RecipeId => ModInfo.Guid + "." + Key;
}

[BepInPlugin(ModInfo.Guid, ModInfo.Name, ModInfo.Version)]
internal sealed class MaterialAlchemyPlugin : BaseUnityPlugin
{
    internal static readonly List<RecipeDefinition> Recipes = new List<RecipeDefinition>
    {
        new RecipeDefinition("rock_to_ore", "Alchemy Table", "tool_alchemy", "rock", 1, "ore", 1, false),
        new RecipeDefinition("rock_to_fiber", "Alchemy Table", "tool_alchemy", "rock", 1, "fiber", 1, false),
        new RecipeDefinition("rock_to_fragment", "Alchemy Table", "tool_alchemy", "rock", 1, "fragment", 1, false),
        new RecipeDefinition("rock_to_log", "Alchemy Table", "tool_alchemy", "rock", 1, "log", 1, false),
        new RecipeDefinition("rock_to_shining_ore", "Alchemy Table", "tool_alchemy", "rock", 1, "ore_gem", 1, false),
        new RecipeDefinition("ore_to_rock", "Alchemy Table", "tool_alchemy", "ore", 1, "rock", 1, false),
        new RecipeDefinition("fiber_to_rock", "Alchemy Table", "tool_alchemy", "fiber", 1, "rock", 1, false),
        new RecipeDefinition("fragment_to_rock", "Alchemy Table", "tool_alchemy", "fragment", 1, "rock", 1, false),
        new RecipeDefinition("log_to_rock", "Alchemy Table", "tool_alchemy", "log", 1, "rock", 1, false),
        new RecipeDefinition("shining_ore_to_rock", "Alchemy Table", "tool_alchemy", "ore_gem", 1, "rock", 1, false),
        new RecipeDefinition("ingot_to_scrap", "Blacksmith Table", "factory_metal", "ingot", 1, "scrap", 1, false),
        new RecipeDefinition("ingot_to_material_hammer", "Blacksmith Table", "factory_metal", "ingot", 10, "mathammer", 1, false),
        new RecipeDefinition("scrap_to_ingot", "Smelter", "Smelter", "scrap", 1, "ingot", 1, true),
        new RecipeDefinition("sun_crystal_to_shining_ore", "Gem Cutter", "GemCutter", "crystal_sun", 1, "ore_gem", 1, true),
        new RecipeDefinition("earth_crystal_to_shining_ore", "Gem Cutter", "GemCutter", "crystal_earth", 1, "ore_gem", 1, true),
        new RecipeDefinition("mana_crystal_to_shining_ore", "Gem Cutter", "GemCutter", "crystal_mana", 1, "ore_gem", 1, true),
        new RecipeDefinition("plank_to_stick", "Wood Cutter", "SawMill", "plank", 1, "stick", 2, true),
    };

    private static readonly Dictionary<string, RecipeDefinition> FactoryRecipes =
        Recipes.Where(recipe => !recipe.Direct).ToDictionary(recipe => recipe.RecipeId);

    private readonly Harmony harmony = new Harmony(ModInfo.Guid);
    private static readonly MethodInfo MemberwiseCloneMethod = typeof(object).GetMethod("MemberwiseClone", BindingFlags.Instance | BindingFlags.NonPublic)!;
    private static ConfigEntry<int>? materialHammerIngotCount;
    private bool directRecipesRegistered;

    private void Awake()
    {
        foreach (RecipeDefinition recipe in Recipes)
        {
            bool defaultEnabled = recipe.Key != "ingot_to_material_hammer";
            recipe.Enabled = Config.Bind(recipe.Section, recipe.Key, defaultEnabled,
                $"Enable {recipe.Input} x{recipe.InputCount} -> {recipe.Output} x{recipe.OutputCount}.");
            recipe.Enabled.SettingChanged += OnRecipeSettingChanged;

            if (recipe.Key == "ingot_to_material_hammer")
            {
                materialHammerIngotCount = Config.Bind(recipe.Section, "material_hammer_ingots", 10,
                    "Number of ingots required to make a Material Hammer.");
                materialHammerIngotCount.SettingChanged += OnRecipeSettingChanged;
            }
        }

        harmony.PatchAll(typeof(MaterialAlchemyPlugin).Assembly);
    }

    private void Start()
    {
        RegisterDirectRecipes();
        RebuildRecipes();
    }

    private void OnDestroy()
    {
        foreach (RecipeDefinition recipe in Recipes)
        {
            recipe.Enabled.SettingChanged -= OnRecipeSettingChanged;
        }

        if (materialHammerIngotCount != null)
        {
            materialHammerIngotCount.SettingChanged -= OnRecipeSettingChanged;
        }

        harmony.UnpatchSelf();
    }

    private void OnRecipeSettingChanged(object sender, EventArgs args)
    {
        if (!directRecipesRegistered)
        {
            return;
        }

        UpdateDirectRecipes();
        RebuildRecipes();
    }

    private void RegisterDirectRecipes()
    {
        int nextId = 320000000;
        foreach (RecipeDefinition recipe in Recipes.Where(recipe => recipe.Direct))
        {
            recipe.DirectId = nextId++;
            recipe.DirectRow = new SourceRecipe.Row
            {
                id = recipe.DirectId,
                factory = recipe.Station,
                type = "Resource",
                thing = recipe.Output,
                num = recipe.OutputCount.ToString(),
                sp = 1,
                time = 1,
                ing1 = new[] { recipe.Input },
                ing2 = Array.Empty<string>(),
                ing3 = Array.Empty<string>(),
                tag = Array.Empty<string>(),
            };
        }

        directRecipesRegistered = true;
        UpdateDirectRecipes();
    }

    private static void UpdateDirectRecipes()
    {
        foreach (RecipeDefinition recipe in Recipes.Where(recipe => recipe.Direct))
        {
            if (recipe.DirectRow == null)
            {
                continue;
            }

            EClass.sources.recipes.rows.RemoveAll(row => row.id == recipe.DirectId);
            EClass.sources.recipes.map.Remove(recipe.DirectId);
            if (recipe.Enabled.Value)
            {
                EClass.sources.recipes.rows.Add(recipe.DirectRow);
                EClass.sources.recipes.map[recipe.DirectId] = recipe.DirectRow;
            }
        }
    }

    private static void RebuildRecipes()
    {
        Recipe.recipeCache.Clear();
        RecipeManager.rebuild = true;
        RecipeManager.BuildList();
    }

    internal static RecipeDefinition? GetFactoryRecipe(string id)
    {
        return FactoryRecipes.TryGetValue(id, out RecipeDefinition? recipe) ? recipe : null;
    }

    private static int GetInputCount(RecipeDefinition definition)
    {
        if (definition.Key == "ingot_to_material_hammer" && materialHammerIngotCount != null)
        {
            return Math.Max(1, materialHammerIngotCount.Value);
        }

        return definition.InputCount;
    }

    internal static void AddEnabledFactoryRecipes()
    {
        foreach (RecipeDefinition definition in Recipes.Where(recipe => !recipe.Direct && recipe.Enabled.Value))
        {
            if (RecipeManager.dict.TryGetValue(definition.RecipeId, out RecipeSource? existing))
            {
                int inputCount = GetInputCount(definition);
                existing.row.components = new[] { definition.Input + (inputCount == 1 ? string.Empty : "/" + inputCount) };
                continue;
            }

            RecipeSource recipe = new RecipeSource
            {
                id = definition.RecipeId,
                type = string.Empty,
                row = CreateFactoryRow(definition),
                noRandomRecipe = true,
            };

            RecipeManager.list.Add(recipe);
            RecipeManager.dict.Add(recipe.id, recipe);
        }
    }

    private static CardRow CreateFactoryRow(RecipeDefinition definition)
    {
        // A shallow clone retains the complete card metadata but never evaluates the lazy model
        // property that made JSON-based deep copies fail during save loading.
        CardRow row = (CardRow)MemberwiseCloneMethod.Invoke(EClass.sources.cards.map[definition.Output], null)!;
        row.factory = new[] { definition.Station };
        int inputCount = GetInputCount(definition);
        row.components = new[] { definition.Input + (inputCount == 1 ? string.Empty : "/" + inputCount) };
        row.recipeKey = new[] { "*" };
        return row;
    }

    [HarmonyPatch(typeof(RecipeManager), nameof(RecipeManager.BuildList))]
    private static class RecipeManagerBuildListPatch
    {
        private static void Postfix()
        {
            AddEnabledFactoryRecipes();
        }
    }

    [HarmonyPatch(typeof(RecipeManager), nameof(RecipeManager.ListSources))]
    private static class RecipeManagerListSourcesPatch
    {
        private static void Postfix(Thing factory, ref List<RecipeSource> __result)
        {
            __result.RemoveAll(recipe => GetFactoryRecipe(recipe.id) != null);
            if (factory == null)
            {
                return;
            }

            foreach (RecipeDefinition definition in Recipes)
            {
                if (!definition.Direct && definition.Enabled.Value && factory.id == definition.Station &&
                    RecipeManager.dict.TryGetValue(definition.RecipeId, out RecipeSource recipe))
                {
                    __result.Add(recipe);
                }
            }
        }
    }

    [HarmonyPatch(typeof(RecipeCard), nameof(RecipeCard.BuildIngredientList))]
    private static class RecipeCardBuildIngredientListPatch
    {
        private static bool Prefix(RecipeCard __instance)
        {
            RecipeDefinition? definition = GetFactoryRecipe(__instance.source.id);
            if (definition == null)
            {
                return true;
            }

            if (__instance.ingredients.Count == 0)
            {
                __instance.ingredients.Add(new Recipe.Ingredient
                {
                    id = definition.Input,
                    req = GetInputCount(definition),
                });
            }

            return false;
        }
    }

    [HarmonyPatch(typeof(RecipeCard), nameof(RecipeCard.Craft))]
    private static class RecipeCardCraftPatch
    {
        private static bool Prefix(RecipeCard __instance, bool model, ref Thing __result)
        {
            RecipeDefinition? definition = GetFactoryRecipe(__instance.source.id);
            if (definition != null && !definition.Enabled.Value && !model)
            {
                __result = null!;
                return false;
            }

            return true;
        }

        private static void Postfix(RecipeCard __instance, List<Thing> ings, ref Thing __result)
        {
            RecipeDefinition? definition = GetFactoryRecipe(__instance.source.id);
            if (definition != null && __result != null)
            {
                Thing? input = ings?.FirstOrDefault(thing => thing != null) ??
                    __instance.ingredients.FirstOrDefault()?.thing;
                if (input != null)
                {
                    __result.ChangeMaterial(input.material);
                }

                __result.SetNum(definition.OutputCount);
            }
        }
    }

    [HarmonyPatch(typeof(ButtonGrid), nameof(ButtonGrid.RefreshCraftable))]
    private static class ButtonGridRefreshCraftablePatch
    {
        private static bool Prefix(ButtonGrid __instance)
        {
            Recipe? recipe = __instance.recipe;
            if (recipe == null)
            {
                return true;
            }

            RecipeDefinition? definition = GetFactoryRecipe(recipe.source.id);
            if (definition == null)
            {
                return true;
            }

            // Elin normally uses Recipe.id as the output card ID. Our IDs are intentionally
            // unique, so use the configured output only for this inventory-count lookup.
            ThingStack stack = EClass.pc.things.GetThingStack(definition.Output);
            bool craftable = recipe.IsCraftable();
            string name = recipe.Name;
            RecipeCard? cardRecipe = recipe as RecipeCard;
            if (cardRecipe?.Mold != null && cardRecipe.Mold.trait.CraftNum > 1)
            {
                name += " x " + cardRecipe.Mold.trait.CraftNum;
            }

            __instance.mainText.SetText(name, craftable ? FontColor.Good : FontColor.Bad);
            __instance.subText.text = stack.count.ToString();
            recipe.SetTextDifficulty(__instance.subText2);
            return false;
        }
    }

    [HarmonyPatch(typeof(ThingContainer), nameof(ThingContainer.GetThingStack), new[] { typeof(string), typeof(int) })]
    private static class ThingContainerGetThingStackPatch
    {
        private static void Prefix(ref string id)
        {
            RecipeDefinition? definition = GetFactoryRecipe(id);
            if (definition != null)
            {
                // Factory UI treats Recipe.id as a card ID in more than one view. Only our
                // private recipe IDs need translation to their real output card IDs.
                id = definition.Output;
            }
        }
    }

}
