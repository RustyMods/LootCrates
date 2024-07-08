using System.Collections.Generic;
using BepInEx;
using HarmonyLib;
using LootCrates.Behaviors;
using YamlDotNet.Serialization;

namespace LootCrates.Managers;

public static class TerminalCommands
{
    [HarmonyPatch(typeof(Terminal), nameof(Terminal.Awake))]
    private static class RegisterLootCrateCommands
    {
        private static void Postfix()
        {
            Terminal.ConsoleCommand commands = new("lootcrate", "Commands for LootCrate, use help to find commands",
                (Terminal.ConsoleEventFailable)
                (args =>
                    {
                        if (args.Length < 2) return false;
                        switch (args[1])
                        {
                            case "help":
                                ListCommandOptions();
                                break;
                            case "reset":
                                ClearCodes();
                                break;
                            case "used":
                                PrintUsedCodes();
                                break;
                        }
                        return true;
                    }), 
                onlyAdmin: true , 
                optionsFetcher: () => new(){"help", "reset", "used"}
                );
        }
    }
    
    private static void ListCommandOptions()
    {
        foreach (string data in new List<string>()
                 {
                     "reset - clears player data of all loot crate codes",
                     "used - list used codes"
                 }) LootCratesPlugin.LootCratesLogger.LogInfo(data);
    }

    public static void ClearCodes()
    {
        Player.m_localPlayer.m_customData[LootCrate.m_customDataKey] = "";
        LootCratesPlugin.LootCratesLogger.LogInfo("Cleared loot crate codes from player save");
    }

    private static void PrintUsedCodes()
    {
        if (Player.m_localPlayer.m_customData.TryGetValue(LootCrate.m_customDataKey, out string playerData))
        {
            if (playerData.IsNullOrWhiteSpace())
            {
                LootCratesPlugin.LootCratesLogger.LogInfo("No codes found on player save");
                return;
            }
            IDeserializer deserializer = new DeserializerBuilder().Build();
            LootManager.PlayerCustomData deserialized = deserializer.Deserialize<LootManager.PlayerCustomData>(playerData);
            foreach (var code in deserialized.m_usedCodes)
            {
                LootCratesPlugin.LootCratesLogger.LogInfo(code);
            }
        }
        else
        {
            LootCratesPlugin.LootCratesLogger.LogInfo("No codes found on player save");
        }
    }
}