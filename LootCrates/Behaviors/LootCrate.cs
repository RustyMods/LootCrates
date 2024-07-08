using System.Text;
using BepInEx;
using HarmonyLib;
using LootCrates.Managers;
using UnityEngine;
using YamlDotNet.Serialization;

namespace LootCrates.Behaviors;

public class LootCrate : MonoBehaviour, Hoverable, Interactable, TextReceiver
{
    private static LootCrate? m_currentLootCrate;
    public static readonly string m_customDataKey = "LootCrateData";

    private readonly int m_key = "lootCode".GetStableHashCode();
    public string m_name = "$piece_lootcrate";
    public Sprite? m_bkg;
    public int m_width = 7;
    public int m_height = 4;
    public GameObject m_open = null!;
    public GameObject m_closed = null!;
    public EffectList m_openEffects = new();
    public EffectList m_closedEffects = new();
    public ZNetView m_nview = null!;
    private Inventory m_inventory = null!;
    public bool m_inUse;
    private static readonly int Visible = Animator.StringToHash("visible");

    public void Awake()
    {
        m_nview = GetComponent<ZNetView>();
        m_inventory = new Inventory(m_name, m_bkg, m_width, m_height);
        if (!m_nview) return;
        m_nview.Register<long>(nameof(RPC_RequestOpen), RPC_RequestOpen);
        m_nview.Register<bool>(nameof(RPC_OpenResponse), RPC_OpenResponse);
        m_nview.Register<long>(nameof(RPC_RequestStack), RPC_RequestStack);
        m_nview.Register<bool>(nameof(RPC_StackResponse), RPC_StackResponse);
        m_nview.Register<long>(nameof(RPC_RequestTakeAll),RPC_RequestTakeAll);
        m_nview.Register<bool>(nameof(RPC_TakeAllResponse), RPC_TakeAllResponse);
        m_nview.Register<string>(nameof(RPC_SetCode),RPC_SetCode);
    }

    public void AddItems()
    {
        if (GetText().IsNullOrWhiteSpace()) return;
        LootManager.CrateData? crate = LootManager.GetCrate(GetText());
        if (crate == null) return;
        m_inventory.RemoveAll();
        foreach (LootManager.LootData? loot in crate.Loot)
        {
            GameObject item = ObjectDB.instance.GetItemPrefab(loot.ItemName);
            if (!item) continue;
            if (!item.TryGetComponent(out ItemDrop component)) continue;
            ItemDrop.ItemData data = component.m_itemData.Clone();
            data.m_stack = loot.Stack;
            data.m_quality = loot.Quality;
            data.m_dropPrefab = item;
            m_inventory.AddItem(data);
        }
    }

    public void RPC_RequestOpen(long uid, long playerID)
    {
        if (IsInUse())
        {
            m_nview.InvokeRPC(uid, nameof(RPC_OpenResponse), false);
        }
        else
        {
            if (!m_nview.IsOwner()) m_nview.ClaimOwnership();
            ZDOMan.instance.ForceSendZDO(uid, m_nview.GetZDO().m_uid);
            m_nview.GetZDO().SetOwner(uid);
            m_nview.InvokeRPC(uid, nameof(RPC_OpenResponse), true);
        }
        
    }

    public void RPC_OpenResponse(long uid, bool granted)
    {
        if (!Player.m_localPlayer) return;
        if (granted)
        {
            Hud.HidePieceSelection();
            InventoryGui.instance.m_animator.SetBool(Visible, true);
            InventoryGui.instance.SetActiveGroup(1, false);
            InventoryGui.instance.SetupCrafting();
            m_currentLootCrate = this;
        }
        else
        {
            Player.m_localPlayer.Message(MessageHud.MessageType.Center, "$msg_inuse");
        }
    }

    public void SaveCodeToPlayer()
    {
        ISerializer serializer = new SerializerBuilder().Build();
        LootManager.PlayerCustomData data = GetCodesFromPlayer();
        if (data.m_usedCodes.Contains(GetText())) return;
        data.m_usedCodes.Add(GetText());
        Player.m_localPlayer.m_customData[m_customDataKey] = serializer.Serialize(data);
    }

    public LootManager.PlayerCustomData GetCodesFromPlayer()
    {
        if (!Player.m_localPlayer) return new LootManager.PlayerCustomData();
        if (!Player.m_localPlayer.m_customData.TryGetValue(m_customDataKey, out string data)) return new LootManager.PlayerCustomData();
        if (data.IsNullOrWhiteSpace()) return new LootManager.PlayerCustomData();
        IDeserializer deserializer = new DeserializerBuilder().Build();
        return deserializer.Deserialize<LootManager.PlayerCustomData>(data);

    }
    public void StackAll() => m_nview.InvokeRPC(nameof(RPC_RequestStack), Game.instance.GetPlayerProfile().GetPlayerID());

    public void RPC_RequestStack(long uid, long playerID)
    {
        if (!IsOwner()) ;
        else if (IsInUse())
        {
            m_nview.InvokeRPC(uid, nameof(RPC_StackResponse));
        }
        else
        {
            ZDOMan.instance.ForceSendZDO(uid, m_nview.GetZDO().m_uid);
            m_nview.GetZDO().SetOwner(uid);
            m_nview.InvokeRPC(uid, nameof(RPC_StackResponse), true);
        }
    }
    public void RPC_StackResponse(long uid, bool granted)
    {
        if (!Player.m_localPlayer) return;
        if (granted)
        {
            if (m_inventory.StackAll(Player.m_localPlayer.GetInventory(), true) <= 0) return;
            InventoryGui.instance.m_moveItemEffects.Create(transform.position, Quaternion.identity);
        }
        else
        {
            Player.m_localPlayer.Message(MessageHud.MessageType.Center, "$msg_inuse");
        }
    }

    public void TakeAll(Humanoid character) => m_nview.InvokeRPC(nameof(RPC_RequestTakeAll));

    public void RPC_RequestTakeAll(long uid, long playerID)
    {
        if (!IsOwner()) return;
        m_nview.InvokeRPC(uid, nameof(RPC_TakeAllResponse), !IsInUse());
    }

    public void RPC_TakeAllResponse(long uid, bool granted)
    {
        if (!Player.m_localPlayer) return;
        if (granted)
        {
            m_nview.ClaimOwnership();
            ZDOMan.instance.ForceSendZDO(uid, m_nview.GetZDO().m_uid);
            Player.m_localPlayer.GetInventory().MoveAll(m_inventory);
        }
        else
        {
            Player.m_localPlayer.Message(MessageHud.MessageType.Center, "$msg_inuse");
        }
    }

    public bool IsInUse() => m_inUse;

    public void SetInUse(bool inUse)
    {
        if (!IsOwner()) m_nview.ClaimOwnership();
        if (m_inUse == inUse) return;
        m_inUse = inUse;
        UpdateUseVisual();
        var transform1 = transform;
        if (inUse)
        {
            m_openEffects.Create(transform1.position, transform1.rotation);
        }
        else
        {
            m_closedEffects.Create(transform1.position, transform1.rotation);
        }
    }

    public void UpdateUseVisual()
    {
        bool flag;
        if (m_nview.IsOwner())
        {
            flag = m_inUse;
            m_nview.GetZDO().Set(ZDOVars.s_inUse, m_inUse ? 1 : 0, false);
        }
        else
        {
            flag = m_nview.GetZDO().GetInt(ZDOVars.s_inUse) == 1;
        }
        if (m_open) m_open.SetActive(flag);
        if (!m_closed) return;
        m_closed.SetActive(!flag);
        
    }

    public bool IsOwner() => m_nview.IsOwner();

    public Inventory GetInventory() => m_inventory;

    public string GetHoverText()
    {
        StringBuilder stringBuilder = new StringBuilder();
        
        stringBuilder.Append(m_name + "\n");
        stringBuilder.AppendFormat("[<color=yellow>{0}</color>] {1}\n", "$KEY_Use", "$piece_container_open");
        stringBuilder.AppendFormat("[<color=yellow>{0}</color>] {1}", "L.Shift + $KEY_Use", "$piece_input_code");

        return Localization.instance.Localize(stringBuilder.ToString());
    }

    public string GetHoverName() => m_name;

    public bool Interact(Humanoid user, bool hold, bool alt)
    {
        if (hold) return false;
        if (alt)
        {
            TextInput.instance.RequestText(this, "$text_code", 40);
            return true;
        }
        if (!CheckCode() || IsInUse())
        {
            user.Message(MessageHud.MessageType.Center, "$msg_cantopen");
            return true;
        }
        
        AddItems();
        SaveCodeToPlayer();
        SetText("");
        
        m_nview.InvokeRPC(nameof(RPC_RequestOpen), Game.instance.GetPlayerProfile().GetPlayerID());
        return true;
    }

    private bool CheckCode()
    {
        string code = GetText();
        if (code.IsNullOrWhiteSpace()) return false;

        if (!CheckPlayerCodes(code)) return false;

        return LootManager.CodeExists(code);
    }

    public bool CheckPlayerCodes(string code)
    {
        if (LootCratesPlugin._checkPlayerCodes.Value is LootCratesPlugin.Toggle.Off) return true;
        if (!Player.m_localPlayer.m_customData.TryGetValue(m_customDataKey, out string data)) return true;
        if (data.IsNullOrWhiteSpace()) return true;
        IDeserializer deserializer = new DeserializerBuilder().Build();
        LootManager.PlayerCustomData deserialized = deserializer.Deserialize<LootManager.PlayerCustomData>(data);
        return !deserialized.m_usedCodes.Contains(code);
    }

    public bool UseItem(Humanoid user, ItemDrop.ItemData item) => false;
    
    public string GetText() => m_nview.GetZDO().GetString(m_key);

    public void SetText(string text)
    {
        if (text.IsNullOrWhiteSpace())
        {
            m_nview.InvokeRPC(nameof(RPC_SetCode), text);
            return;
        }
        if (!CheckPlayerCodes(text))
        {
            Player.m_localPlayer.Message(MessageHud.MessageType.Center, "$msg_codeused");
            return;
        }

        if (!LootManager.CodeExists(text))
        {
            Player.m_localPlayer.Message(MessageHud.MessageType.Center, "$msg_invalidcode");
            return;
        }
        Player.m_localPlayer.Message(MessageHud.MessageType.Center, "$msg_unlockedlootcrate");
        m_nview.InvokeRPC(nameof(RPC_SetCode), text);
    }

    public void RPC_SetCode(long sender, string value) => m_nview.GetZDO().Set(m_key, value);

    public static bool IsLootCrateOpen() => m_currentLootCrate != null;

    [HarmonyPatch(typeof(InventoryGui), nameof(InventoryGui.UpdateContainer))]
    private static class LootCrate_ContainerOverride
    {
        private static bool Prefix(InventoryGui __instance, Player player)
        {
            if (!__instance.m_animator.GetBool(Visible)) return false;
            if (__instance.m_currentContainer)
            {
                m_currentLootCrate = null;
                return true;
            }
            if (m_currentLootCrate != null && m_currentLootCrate.IsOwner())
            {
                m_currentLootCrate.SetInUse(true);
                __instance.m_container.gameObject.SetActive(true);
                __instance.m_containerGrid.UpdateInventory(m_currentLootCrate.GetInventory(), null, __instance.m_dragItem);
                __instance.m_containerName.text = Localization.instance.Localize(m_currentLootCrate.GetInventory().GetName());
                if (__instance.m_firstContainerUpdate)
                {
                    __instance.m_containerGrid.ResetView();
                    __instance.m_firstContainerUpdate = false;
                    __instance.m_containerHoldTime = 0.0f;
                    __instance.m_containerHoldState = 0;
                }

                if (Vector3.Distance(m_currentLootCrate.transform.position, player.transform.position) >
                    __instance.m_autoCloseDistance)
                {
                    CloseLootCrate(__instance);
                }

                if (ZInput.GetButton("Use") || ZInput.GetButton("JoyUse"))
                {
                    __instance.m_containerHoldTime += Time.deltaTime;
                    if (__instance.m_containerHoldTime > __instance.m_containerHoldPlaceStackDelay &&
                        __instance.m_containerHoldState == 0)
                    {
                        m_currentLootCrate.StackAll();
                        __instance.m_containerHoldState = 1;
                    }
                    else
                    {
                        if (__instance.m_containerHoldTime <= __instance.m_containerHoldPlaceStackDelay +
                            __instance.m_containerHoldExitDelay || __instance.m_containerHoldState != 1)
                        {
                            return false;
                        }
                        __instance.Hide();
                    }
                }
                else
                {
                    if (__instance.m_containerHoldState < 0) return false;
                    __instance.m_containerHoldState = -1;
                }
            }
            else
            {
                __instance.m_container.gameObject.SetActive(false);
                if (__instance.m_dragInventory == null ||
                    __instance.m_dragInventory == Player.m_localPlayer.GetInventory()) return false;
                __instance.SetupDragItem(null, null, 1);
            }
            return false;
        }
    }
    
    private static void CloseLootCrate(InventoryGui __instance)
    {
        if (__instance.m_dragInventory != null && __instance.m_dragInventory != Player.m_localPlayer.GetInventory())
        {
            __instance.SetupDragItem(null, null, 1);
        }
        HideLootCrate();
        __instance.m_splitPanel.gameObject.SetActive(false);
        __instance.m_firstContainerUpdate = true;
        __instance.m_container.gameObject.SetActive(false);
    }

    private static void HideLootCrate()
    {
        if (m_currentLootCrate == null) return;
        m_currentLootCrate.SetInUse(false);
        m_currentLootCrate = null;
    }

    [HarmonyPatch(typeof(InventoryGui), nameof(InventoryGui.Hide))]
    private static class InventoryGUI_Hide_Patch
    {
        private static void Postfix() => HideLootCrate();
    }

    [HarmonyPatch(typeof(InventoryGui), nameof(InventoryGui.IsContainerOpen))]
    private static class InventoryGUI_IsContainerOpen_Patch
    {
        private static void Postfix(ref bool __result)
        {
            if (IsLootCrateOpen()) __result = true;
        }
    }

    [HarmonyPatch(typeof(InventoryGui), nameof(InventoryGui.OnTakeAll))]
    private static class InventoryGUI_OnTakeAll_Patch
    {
        private static void Postfix(InventoryGui __instance)
        {
            if (m_currentLootCrate == null) return;
            __instance.SetupDragItem(null, null, 1);
            Player.m_localPlayer.GetInventory().MoveAll(m_currentLootCrate.GetInventory());
        }
    }
}