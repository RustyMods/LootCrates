using System;
using System.Collections.Generic;
using System.IO;
using BepInEx;
using BepInEx.Configuration;
using HarmonyLib;
using LootCrates.Behaviors;
using ServerSync;
using UnityEngine;
using YamlDotNet.Serialization;
using Object = UnityEngine.Object;

namespace LootCrates.Managers;

public static class LootManager
{
    private static readonly CustomSyncedValue<string> m_serverSynced = new(LootCratesPlugin.ConfigSync, "LootCrateServerSyncData", "");
    private static readonly string m_filePath = Paths.ConfigPath + Path.DirectorySeparatorChar + "LootCrates.yml";
    private static FileData m_data = new();

    private static bool m_serverSyncInitialized;

    public static bool CodeExists(string code) => m_data.Crates.Exists(data => data.Code == code);

    public static CrateData? GetCrate(string code) => m_data.Crates.Find(data => data.Code == code);

    public static void ReadFile()
    {
        if (!File.Exists(m_filePath))
        {
            m_data.Crates = CreateDefaultCrate();
            ISerializer serializer = new SerializerBuilder().Build();
            string serial = serializer.Serialize(m_data);
            File.WriteAllText(m_filePath, serial);
        }
        else
        {
            try
            {
                string file = File.ReadAllText(m_filePath);
                IDeserializer deserializer = new DeserializerBuilder().Build();
                m_data = deserializer.Deserialize<FileData>(file);
            }
            catch
            {
                LootCratesPlugin.LootCratesLogger.LogWarning("Failed to deserialize loot crate file");
            }
        }
    }

    public static void InitFileWatch()
    {
        FileSystemWatcher watcher = new FileSystemWatcher(Paths.ConfigPath, "LootCrates.yml")
        {
            IncludeSubdirectories = false,
            SynchronizingObject = ThreadingHelper.SynchronizingObject,
            EnableRaisingEvents = true
        };

        watcher.Changed += OnFileChange;
    }

    public static void OnFileChange(object sender, FileSystemEventArgs e)
    {
        LootCratesPlugin.LootCratesLogger.LogDebug("LootCrate file changed");
        string file = File.ReadAllText(m_filePath);
        IDeserializer deserializer = new DeserializerBuilder().Build();
        try
        {
            if (ZNet.instance)
            {
                if (!ZNet.instance.IsServer())
                {
                    LootCratesPlugin.LootCratesLogger.LogDebug("Not server, loot crates will not be updated");
                }
                else
                {
                    m_data = deserializer.Deserialize<FileData>(file);
                    UpdateServerFile();
                }
            }
            else
            {
                m_data = deserializer.Deserialize<FileData>(file);
            }
        }
        catch
        {
            LootCratesPlugin.LootCratesLogger.LogWarning("Failed to deserialize loot crate file");
        }
    }

    public static void InitServerSync()
    {
        if (m_serverSyncInitialized) return;
        m_serverSynced.ValueChanged += () =>
        {
            if (m_serverSynced.Value.IsNullOrWhiteSpace()) return;
            IDeserializer deserializer = new DeserializerBuilder().Build();
            m_data = deserializer.Deserialize<FileData>(m_serverSynced.Value);
            LootCratesPlugin.LootCratesLogger.LogDebug("Received loot crate data from server");
        };
        m_serverSyncInitialized = true;
    }

    public static void UpdateServerFile()
    {
        ISerializer serializer = new SerializerBuilder().Build();
        m_serverSynced.Value = serializer.Serialize(m_data);
    }

    private static List<CrateData> CreateDefaultCrate()
    {
        List<CrateData> defaults = new()
        {
            new CrateData()
            {
                Code = "1234",
                Loot = new List<LootData>()
                {
                    new LootData()
                    {
                        ItemName = "Resin",
                        Stack = 10,
                        Quality = 1
                    },
                    new LootData()
                    {
                        ItemName = "Wood",
                        Stack = 10,
                        Quality = 1
                    },
                    new LootData()
                    {
                        ItemName = "Club",
                        Stack = 1,
                        Quality = 3
                    }
                }
            }
        };
        return defaults;
    }

    [HarmonyPatch(typeof(ZNet), nameof(ZNet.Awake))]
    private static class ZNet_Awake_Patch
    {
        private static void Postfix(ZNet __instance)
        {
            if (!__instance) return;
            if (__instance.IsServer())
            {
                UpdateServerFile();
            }
        }
    }

    [HarmonyPatch(typeof(ZNetScene), nameof(ZNetScene.Awake))]
    private static class RegisterCrate
    {
        private static void Postfix(ZNetScene __instance)
        {
            if (!__instance) return;
            CreateCrates(__instance);
        }
    }

    [HarmonyPatch(typeof(ObjectDB), nameof(ObjectDB.Awake))]
    private static class RegisterForgettingOrb
    {
        private static void Postfix(ObjectDB __instance)
        {
            if (!ZNetScene.instance) return;
            CreateForgettingItem(__instance);
        }
    }

    public static void CreateCrates(ZNetScene __instance)
    {
        Clone(__instance, "piece_chest", "piece_iron_lootcrate", "Loot Crate");
        Clone(__instance, "piece_chest_blackmetal", "piece_blackmetal_lootcrate", "Loot Crate");
        Clone(__instance, "piece_chest_wood", "piece_wood_lootcrate", "Loot Crate");
        Clone(__instance, "piece_gift1", "piece_gift1_lootcrate", "Loot Crate");
        Clone(__instance, "piece_gift2", "piece_gift2_lootcrate", "Loot Crate");
        Clone(__instance, "piece_gift3", "piece_gift3_lootcrate", "Loot Crate");
        Clone(__instance, "piece_pot1", "piece_pot1_lootcrate", "Loot Crate");
        Clone(__instance, "piece_pot1_cracked", "piece_pot1_cracked_lootcrate", "Loot Crate");
        Clone(__instance, "piece_pot1_red", "piece_pot1_red_lootcrate", "Loot Crate");
        Clone(__instance, "piece_pot2", "piece_pot2_lootcrate", "Loot Crate");
        Clone(__instance, "piece_pot2_cracked", "piece_pot2_cracked_lootcrate", "Loot Crate");
        Clone(__instance, "piece_pot2_red", "piece_pot2_red_lootcrate", "Loot Crate");
        Clone(__instance, "piece_pot3", "piece_pot3_lootcrate", "Loot Crate");
        Clone(__instance, "piece_pot3_cracked", "piece_pot3_cracked_lootcrate", "Loot Crate");
        Clone(__instance, "piece_pot3_red", "piece_pot3_red_lootcrate", "Loot Crate");
        Clone(__instance, "piece_chest_private", "piece_private_lootcrate", "Loot Crate");
    }

    private static void Clone(ZNetScene __instance, string prefabName, string name, string displayName)
    {
        GameObject piece_chest = __instance.GetPrefab(prefabName);
        if (!piece_chest) return;
        GameObject clone = Object.Instantiate(piece_chest, LootCratesPlugin._Root.transform, false);
        clone.name = name;

        LootCrate crate = clone.AddComponent<LootCrate>();
        if (clone.TryGetComponent(out Container container))
        {
            crate.m_bkg = container.m_bkg;
            crate.m_open = container.m_open;
            crate.m_closed = container.m_closed;
            crate.m_openEffects = container.m_openEffects;
            crate.m_closedEffects = container.m_closeEffects;
            
            Object.Destroy(container);
        }

        if (clone.TryGetComponent(out Piece piece))
        {
            ConfigEntry<string> nameConfig = LootCratesPlugin._Plugin.config(name, "Display Name", displayName, "Set display name");
            piece.m_name = nameConfig.Value;
            nameConfig.SettingChanged += (sender, args) => piece.m_name = nameConfig.Value;
            ConfigEntry<Piece.PieceCategory> category = LootCratesPlugin._Plugin.config(name, "Category", Piece.PieceCategory.Misc, "Set category");
            piece.m_category = GetCategory(category);
            category.SettingChanged += (sender, args) => piece.m_category = GetCategory(category);
            ConfigEntry<string> recipe = LootCratesPlugin._Plugin.config(name, "Recipe", "SwordCheat:1", "[prefab]:[amount],[prefab]:[amount]");
            piece.m_resources = GetRequirements(recipe).ToArray();
            recipe.SettingChanged += (sender, args) => piece.m_resources = GetRequirements(recipe).ToArray();
        }

        if (clone.TryGetComponent(out WearNTear wearNTear))
        {
            ConfigEntry<float> health = LootCratesPlugin._Plugin.config(name, "Health", wearNTear.m_health, "Set health");
            wearNTear.m_health = health.Value;
            health.SettingChanged += (sender, args) => wearNTear.m_health = health.Value;
            
        }
        
        RegisterToScene(clone);
        RegisterToHammer(clone);
    }

    private static void CreateForgettingItem(ObjectDB __instance)
    {
        GameObject orb = __instance.GetItemPrefab("StaminaUpgrade_Greydwarf");
        if (!orb) return;
        GameObject clone = Object.Instantiate(orb, LootCratesPlugin._Root.transform, false);
        clone.name = "LootCrateCodeBreaker";

        if (!clone.TryGetComponent(out ItemDrop component)) return;

        component.m_itemData.m_shared.m_name = "$item_lootcrate_codebreaker";
        component.m_itemData.m_shared.m_description = "$item_lootcrate_codebreaker_desc";

        component.m_itemData.m_shared.m_questItem = false;
        
        if (!__instance.m_items.Contains(clone)) __instance.m_items.Add(clone);
        __instance.m_itemByHash[clone.name.GetStableHashCode()] = clone;
        
        RegisterToScene(clone);
    }

    [HarmonyPatch(typeof(Player), nameof(Player.ConsumeItem))]
    private static class Player_ConsumeItem_Patch
    {
        private static void Prefix(ItemDrop.ItemData item)
        {
            if (item.m_shared.m_name == "$item_lootcrate_codebreaker")
            {
                TerminalCommands.ClearCodes();
            }
        }
    }

    private static List<Piece.Requirement> GetRequirements(ConfigEntry<string> config)
    {
        List<Piece.Requirement> output = new();
        foreach (string input in config.Value.Split(','))
        {
            string[] info = input.Split(':');
            if (info.Length != 2) continue;
            GameObject prefab = ZNetScene.instance.GetPrefab(info[0]);
            if (!prefab) continue;
            if (!prefab.TryGetComponent(out ItemDrop component)) continue;
            output.Add(new Piece.Requirement()
            {
                m_resItem = component,
                m_amount = int.TryParse(info[1], out int amount) ? amount : 1,
                m_extraAmountOnlyOneIngredient = 1,
                m_amountPerLevel = 1,
                m_recover = true
            });
        }

        return output;
    }

    private static Piece.PieceCategory GetCategory(ConfigEntry<Piece.PieceCategory> config)
    {
        return Enum.IsDefined(typeof(Piece.PieceCategory), config.Value) ? config.Value : Piece.PieceCategory.Misc;
    }

    private static void RegisterToScene(GameObject prefab)
    {
        if (!ZNetScene.instance.m_prefabs.Contains(prefab)) ZNetScene.instance.m_prefabs.Add(prefab);
        ZNetScene.instance.m_namedPrefabs[prefab.name.GetStableHashCode()] = prefab;
    }

    private static void RegisterToHammer(GameObject prefab)
    {
        GameObject hammer = ZNetScene.instance.GetPrefab("Hammer");
        if (!hammer) return;
        if (!hammer.TryGetComponent(out ItemDrop component)) return;
        if (component.m_itemData.m_shared.m_buildPieces.m_pieces.Contains(prefab)) return;
        component.m_itemData.m_shared.m_buildPieces.m_pieces.Add(prefab);
    }

    public class FileData
    {
        public List<CrateData> Crates = new();
    }

    public class CrateData
    {
        public string Code = null!;
        public List<LootData> Loot = new();
    }

    public class LootData
    {
        public string ItemName = null!;
        public int Stack;
        public int Quality;
    }

    public class PlayerCustomData
    {
        public List<string> m_usedCodes = new();
    }
}