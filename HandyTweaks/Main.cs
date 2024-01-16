using BepInEx;
using ConfigTweaks;
using HarmonyLib;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;
using System.Text;
using UnityEngine;
using Object = UnityEngine.Object;

namespace HandyTweaks
{
    [BepInPlugin("com.aidanamite.HandyTweaks", "Handy Tweaks", "1.0.6")]
    [BepInDependency("com.aidanamite.ConfigTweaks")]
    public class Main : BaseUnityPlugin
    {
        [ConfigField]
        public static KeyCode DoFarmStuff = KeyCode.KeypadMinus;
        [ConfigField]
        public static bool AutoSpendFarmGems = false;
        [ConfigField]
        public static bool BypassFarmGemCosts = false;
        [ConfigField]
        public static bool DoFarmStuffOnTimer = false;
        [ConfigField]
        public static bool CanPlaceAnywhere = false;
        [ConfigField]
        public static bool SkipTrivia = false;
        [ConfigField]
        public static KeyCode DontApplyGeometry = KeyCode.LeftShift;
        [ConfigField]
        public static KeyCode DontApplyTextures = KeyCode.LeftAlt;
        [ConfigField]
        public static bool SortStableQuestDragonsByValue = false;
        [ConfigField]
        public static bool ShowRacingEquipmentStats = false;
        [ConfigField]
        public static KeyCode ChangeDragonsGender = KeyCode.Equals;

        public void Awake()
        {
            new Harmony("com.aidanamite.HandyTweaks").PatchAll();
            Logger.LogInfo("Loaded");
        }

        float timer;
        public void Update()
        {
            if ((timer -= Time.deltaTime) <= 0 && (Input.GetKeyDown(DoFarmStuff) || DoFarmStuffOnTimer) && MyRoomsIntMain.pInstance is FarmManager f)
            {
                timer = 0.2f;
                foreach (var i in Resources.FindObjectsOfTypeAll<FarmItem>())
                    if (i && i.gameObject.activeInHierarchy && i.pCurrentStage != null && !i.IsWaitingForWsCall())
                    {
                        if (i is CropFarmItem c)
                        {
                            if (c.pCurrentStage._Name == "NoInteraction")
                            {
                                if (BypassFarmGemCosts)
                                    c.GotoNextStage();
                                else if (AutoSpendFarmGems && c.CheckGemsAvailable(c.GetSpeedupCost()))
                                    c.GotoNextStage(true);
                            }
                            else
                                c.GotoNextStage();
                        }
                        else if (i is FarmSlot s)
                        {
                            if (!s.IsCropPlaced())
                            {
                                var items = CommonInventoryData.pInstance.GetItems(s._SeedsCategory);
                                if (items != null)
                                    foreach (var seed in items)
                                        if (seed != null && seed.Quantity > 0)
                                            s.OnContextAction(seed.Item.ItemName);
                                break;
                            }
                        }
                        else if (i is AnimalFarmItem a)
                        {
                            if (a.pCurrentStage._Name.Contains("Feed"))
                            {
                                a.ConsumeFeed();
                                if (a.IsCurrentStageFeedConsumed())
                                    a.GotoNextStage(false);
                            }
                            else if (a.pCurrentStage._Name.Contains("Harvest"))
                                a.GotoNextStage(false);
                            else
                            {
                                if (BypassFarmGemCosts)
                                    a.GotoNextStage();
                                else if (AutoSpendFarmGems && a.CheckGemsAvailable(a.GetSpeedupCost()))
                                    a.GotoNextStage(true);
                            }
                        }
                        else if (i is ComposterFarmItem d)
                        {
                            if (d.pCurrentStage._Name.Contains("Harvest"))
                                d.GotoNextStage();
                            else if (d.pCurrentStage._Name.Contains("Feed"))
                                foreach (var consumable in d._CompostConsumables)
                                    if (consumable != null)
                                    {
                                        var userItemData = CommonInventoryData.pInstance.FindItem(consumable.ItemID);
                                        if (userItemData != null && consumable.Amount <= userItemData.Quantity)
                                        {
                                            d.SetCurrentUsedConsumableCriteria(consumable);
                                            d.GotoNextStage(false);
                                            break;
                                        }
                                    }
                            
                        }
                        else if (i is FishTrapFarmItem t)
                        {
                            if (t.pCurrentStage._Name.Contains("Harvest"))
                                t.GotoNextStage();
                            else if (t.pCurrentStage._Name.Contains("Feed"))
                                foreach (var consumable in t._FishTrapConsumables)
                                    if (consumable != null)
                                    {
                                        var userItemData = CommonInventoryData.pInstance.FindItem(consumable.ItemID);
                                        if (userItemData != null && consumable.Amount <= userItemData.Quantity)
                                        {
                                            t.SetCurrentUsedConsumableCriteria(consumable);
                                            t.GotoNextStage(false);
                                            break;
                                        }
                                    }
                        }
                    }
            }
            if (Input.GetKeyDown(ChangeDragonsGender) && AvAvatar.pState != AvAvatarState.PAUSED && AvAvatar.pState != AvAvatarState.NONE && AvAvatar.GetUIActive() && SanctuaryManager.pCurPetInstance)
            {
                AvAvatar.SetUIActive(false);
                AvAvatar.pState = AvAvatarState.PAUSED;
                if (SanctuaryManager.pCurPetInstance.pData.Gender != Gender.Male && SanctuaryManager.pCurPetInstance.pData.Gender != Gender.Female)
                    GameUtilities.DisplayOKMessage("PfKAUIGenericDB", $"{SanctuaryManager.pCurPetInstance.pData.Name} does not have a gender. Unable to change it", gameObject, "OnPopupClose");
                else
                {
                    changingPet = SanctuaryManager.pCurPetInstance;
                    GameUtilities.DisplayGenericDB("PfKAUIGenericDB", $"Are you sure you want to change {changingPet.pData.Name} to {(changingPet.pData.Gender == Gender.Male ? "fe" : "")}male?", "Change Dragon Gender", gameObject, "ChangeDragonGender", "OnPopupClose", null, "OnPopupClose",true);
                }
            }
        }
        SanctuaryPet changingPet;
        void ChangeDragonGender()
        {
            if (!changingPet || changingPet.pData == null)
                return;
            changingPet.pData.Gender = changingPet.pData.Gender == Gender.Male ? Gender.Female : Gender.Male;
            changingPet.SaveData();
            OnPopupClose();
        }
        void OnPopupClose()
        {
            AvAvatar.pState = AvAvatarState.IDLE;
            AvAvatar.SetUIActive(true);
        }

        static Dictionary<string, (PetStatType,string)> FieldToType = new Dictionary<string, (PetStatType, string)>
        {
            { "_YawTurnRate",(PetStatType.TURNRATE,"TRN") },
            { "_PitchTurnRate",(PetStatType.PITCHRATE,"PCH") },
            { "_Acceleration",(PetStatType.ACCELERATION,"ACL") },
            { "_Speed",(PetStatType.MAXSPEED,"SPD") }
        };
        static Dictionary<SanctuaryPetMeterType, (string,string)> MeterToName = new Dictionary<SanctuaryPetMeterType, (string, string)>
        {
            { SanctuaryPetMeterType.ENERGY, ("Energy","NRG") },
            { SanctuaryPetMeterType.HAPPINESS, ("Happiness","HAP") },
            { SanctuaryPetMeterType.HEALTH, ("Health","DHP") },
            { SanctuaryPetMeterType.RACING_ENERGY, ("Racing Energy","RNR") },
            { SanctuaryPetMeterType.RACING_FIRE, ("Racing Fire","RFR") }
        };
        static Dictionary<string, CustomStatInfo> statCache = new Dictionary<string, CustomStatInfo>();
        public static CustomStatInfo GetCustomStatInfo(string AttributeName)
        {
            if (AttributeName == null)
                return null;
            if (!statCache.TryGetValue(AttributeName, out var v))
            {
                var name = AttributeName;
                var abv = "???";
                var found = false;
                if (AttributeName.TryGetAttributeField(out var field) && FieldToType.TryGetValue(field, out var type))
                {
                    found = true;
                    name = SanctuaryData.GetDisplayTextFromPetStat(type.Item1);
                    abv = type.Item2;
                }
                else if (Enum.TryParse<SanctuaryPetMeterType>(AttributeName, true, out var type2) && MeterToName.TryGetValue(type2, out var meterName))
                {
                    found = true;
                    (name,abv) = meterName;
                }
                statCache[AttributeName] = v = new CustomStatInfo(AttributeName,name,abv,found);
            }
            return v;
        }
    }

    public class CustomStatInfo
    {
        public readonly string AttributeName;
        public readonly string DisplayName;
        public readonly string Abreviation;
        public readonly bool Valid;
        public CustomStatInfo(string Att, string Dis, string Abv, bool Val)
        {
            AttributeName = Att;
            DisplayName = Dis;
            Abreviation = Abv;
            Valid = Val;
        }
    }

    public enum AimMode
    {
        Default,
        MouseWhenNoTargets,
        FindTargetNearMouse,
        AlwaysMouse
    }

    static class ExtentionMethods
    {
        static MethodInfo _IsCropPlaced = typeof(FarmSlot).GetMethod("IsCropPlaced", ~BindingFlags.Default);
        public static bool IsCropPlaced(this FarmSlot item) => (bool)_IsCropPlaced.Invoke(item, new object[0]);
        static MethodInfo _OnContextAction = typeof(MyRoomItem).GetMethod("OnContextAction", ~BindingFlags.Default);
        public static void OnContextAction(this MyRoomItem item, string actionName) => _OnContextAction.Invoke(item, new[] { actionName });
        static MethodInfo _IsCurrentStageFeedConsumed = typeof(AnimalFarmItem).GetMethod("IsCurrentStageFeedConsumed", ~BindingFlags.Default);
        public static bool IsCurrentStageFeedConsumed(this AnimalFarmItem item) => (bool)_IsCurrentStageFeedConsumed.Invoke(item, new object[0]);
        static MethodInfo _ConsumeFeed = typeof(AnimalFarmItem).GetMethod("ConsumeFeed", ~BindingFlags.Default);
        public static void ConsumeFeed(this AnimalFarmItem item) => _ConsumeFeed.Invoke(item, new object[0]);
        static FieldInfo _mCurrentUsedConsumableCriteria = typeof(ComposterFarmItem).GetField("mCurrentUsedConsumableCriteria", ~BindingFlags.Default);
        public static void SetCurrentUsedConsumableCriteria(this ComposterFarmItem item, ItemStateCriteriaConsumable consumable) => _mCurrentUsedConsumableCriteria.SetValue(item, consumable);
        static FieldInfo _mCurrentUsedConsumableCriteria2 = typeof(FishTrapFarmItem).GetField("mCurrentUsedConsumableCriteria", ~BindingFlags.Default);
        public static void SetCurrentUsedConsumableCriteria(this FishTrapFarmItem item, ItemStateCriteriaConsumable consumable) => _mCurrentUsedConsumableCriteria2.SetValue(item, consumable);
        static MethodInfo _GetSpeedupCost = typeof(FarmItem).GetMethod("GetSpeedupCost", ~BindingFlags.Default);
        public static int GetSpeedupCost(this FarmItem item) => (int)_GetSpeedupCost.Invoke(item, new object[0]);
        static MethodInfo _CheckGemsAvailable = typeof(FarmItem).GetMethod("CheckGemsAvailable", ~BindingFlags.Default);
        public static bool CheckGemsAvailable(this FarmItem item, int count) => (bool)_CheckGemsAvailable.Invoke(item, new object[] { count });
        static FieldInfo _mIsWaitingForWsCall = typeof(FarmItem).GetField("mIsWaitingForWsCall", ~BindingFlags.Default);
        public static bool IsWaitingForWsCall(this FarmItem item) => (bool)_mIsWaitingForWsCall.GetValue(item);
        static MethodInfo _SaveAndExitQuiz = typeof(UiQuizPopupDB).GetMethod("SaveAndExitQuiz", ~BindingFlags.Default);
        public static void SaveAndExitQuiz(this UiQuizPopupDB item) => _SaveAndExitQuiz.Invoke(item, new object[0]);
        static MethodInfo _CreateDragonWiget = typeof(UiStableQuestDragonsMenu).GetMethod("CreateDragonWiget", ~BindingFlags.Default);
        public static void CreateDragonWiget(this UiStableQuestDragonsMenu menu, RaisedPetData rpData) => _CreateDragonWiget.Invoke(menu, new object[] { rpData });
        static MethodInfo _ShowStatInfo = typeof(UiStatsCompareMenu).GetMethod("ShowStatInfo", ~BindingFlags.Default);
        public static void ShowStatInfo(this UiStatsCompareMenu instance, KAWidget widget, string baseStat, string statName, string compareStat, string diffVal, StatCompareResult compareResult = StatCompareResult.Equal, bool showCompare = false) =>
            _ShowStatInfo.Invoke(instance, new object[] { widget, baseStat, statName, compareStat, diffVal, (int)compareResult, showCompare });
        static FieldInfo _mModifierFieldMap = typeof(AvAvatarController).GetField("mModifierFieldMap", ~BindingFlags.Default);
        public static bool TryGetAttributeField(this string att, out string fieldName)
        {
            if (att != null && _mModifierFieldMap.GetValue(null) is Dictionary<string, string> d)
                return d.TryGetValue(att, out fieldName);
            fieldName = null;
            return false;
        }
        public static string GetAttributeField(this string att) => att.TryGetAttributeField(out var f) ? f : null;
        static FieldInfo _mContentMenuCombat = typeof(UiStatPopUp).GetField("mContentMenuCombat", ~BindingFlags.Default);
        public static KAUIMenu GetContentMenuCombat(this UiStatPopUp item) => (KAUIMenu)_mContentMenuCombat.GetValue(item);
    }
    public enum StatCompareResult
    {
        Equal,
        Greater,
        Lesser
    }

    [HarmonyPatch(typeof(UiMyRoomBuilder), "Update")]
    static class Patch_RoomBuilder
    {
        static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            var code = instructions.ToList();
            for (int i = code.Count - 1; i >= 0; i--)
                if (code[i].opcode == OpCodes.Stfld && code[i].operand is FieldInfo f && f.Name == "mCanPlace")
                    code.Insert(i, new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(Patch_RoomBuilder), nameof(EditCanPlace))));
            return code;
        }
        static bool EditCanPlace(bool original) => original || Main.CanPlaceAnywhere;
    }

    [HarmonyPatch(typeof(UiQuizPopupDB))]
    static class Patch_InstantAnswer
    {
        [HarmonyPatch("Start")]
        [HarmonyPostfix]
        static void Start(UiQuizPopupDB __instance, ref bool ___mIsQuestionAttemped, ref bool ___mCheckForTaskCompletion)
        {
            if (Main.SkipTrivia)
            {
                ___mCheckForTaskCompletion = true;
                ___mIsQuestionAttemped = true;
                __instance._MessageObject.SendMessage(__instance._QuizAnsweredMessage, true, SendMessageOptions.DontRequireReceiver);
                __instance.SaveAndExitQuiz();
            }
        }
        [HarmonyPatch("IsQuizAnsweredCorrect")]
        [HarmonyPostfix]
        static void IsQuizAnsweredCorrect(ref bool __result)
        {
            if (Main.SkipTrivia)
            {
                __result = true;
            }
        }
    }

    [HarmonyPatch]
    static class Patch_ApplyTexture
    {
        static IEnumerable<MethodBase> TargetMethods()
        {
            yield return AccessTools.Method(typeof(AvatarData), "SetStyleTexture", new[] { typeof(AvatarData.InstanceInfo), typeof(string), typeof(string), typeof(int) });
            yield return AccessTools.Method(typeof(CustomAvatarState), "SetPartTexture");
            yield return AccessTools.Method(typeof(CustomAvatarState), "SetTextureData");
            yield return AccessTools.Method(typeof(UiAvatarCustomizationMenu), "SetPartTextureByIndex");
            yield return AccessTools.Method(typeof(UiAvatarCustomizationMenu), "UpdatePartTexture");
        }
        static bool Prefix() => !Input.GetKey(Main.DontApplyTextures);
    }

    [HarmonyPatch(typeof(AvatarData), "SetGeometry", typeof(AvatarData.InstanceInfo), typeof(string), typeof(string), typeof(int))]
    static class Patch_ApplyGeometry
    {
        static bool Prefix() => !Input.GetKey(Main.DontApplyGeometry);
    }

    [HarmonyPatch(typeof(UiStableQuestDragonsMenu), "LoadDragonsList")]
    static class Patch_LoadStableQuestDragonsList
    {
        static bool Prefix(UiStableQuestDragonsMenu __instance)
        {
            if (!Main.SortStableQuestDragonsByValue)
                return true;
            __instance.ClearItems();
            
            var l = new SortedSet<(float, RaisedPetData)>(new ComparePetValue());
            if (RaisedPetData.pActivePets != null)
                foreach (RaisedPetData[] array in RaisedPetData.pActivePets.Values)
                    if (array != null)
                        foreach (RaisedPetData pet in array)
                            if (StableData.GetByPetID(pet.RaisedPetID) != null && pet.pStage >= RaisedPetStage.BABY && pet.IsPetCustomized())
                                l.Add((TimedMissionManager.pInstance.GetWinProbabilityForPet(UiStableQuestMain.pInstance._StableQuestDetailsUI.pCurrentMissionData, pet.RaisedPetID),pet));
            foreach (var p in l)
                __instance.CreateDragonWiget(p.Item2);
            __instance.pMenuGrid.repositionNow = true;
            return false;
        }
    }

    class ComparePetValue : IComparer<(float, RaisedPetData)>
    {
        public int Compare((float, RaisedPetData) a, (float, RaisedPetData) b)
        {
            var c = b.Item1.CompareTo(a.Item1);
            return c == 0 ? 1 : c;
        }
    }

    [HarmonyPatch]
    static class Patch_ShowFlightCompare
    {
        static UiStatCompareDB.ItemCompareDetails equipped;
        static UiStatCompareDB.ItemCompareDetails unequipped;
        [HarmonyPatch(typeof(UiStatCompareDB), "Initialize")]
        [HarmonyPrefix]
        static void UiStatCompareDB_Initialize(UiStatCompareDB.ItemCompareDetails inLeftItem, UiStatCompareDB.ItemCompareDetails inRightItem)
        {
            equipped = inLeftItem;
            unequipped = inRightItem;
        }
        [HarmonyPatch(typeof(UiStatsCompareMenu), "Populate")]
        [HarmonyPostfix]
        static void UiStatsCompareMenu_Populate(UiStatsCompareMenu __instance, bool showCompare, ItemStat[] equippedStats, ItemStat[] unequippedStats)
        {
            if (!Main.ShowRacingEquipmentStats)
                return;
            bool shouldClear = !(equippedStats?.Length > 0 || unequippedStats?.Length > 0);
            void Show(string name, string stat1, string stat2)
            {
                if (stat1 == null && stat2 == null)
                    return;
                if (shouldClear)
                {
                    __instance.ClearItems();
                    shouldClear = false;
                }
                KAWidget kawidget2 = __instance.DuplicateWidget(__instance._Template, UIAnchor.Side.Center);
                __instance.AddWidget(kawidget2);
                kawidget2.SetVisibility(true);
                string text = null;
                string text2 = null;
                string diffVal = null;
                var num = 0f;
                var num2 = 0f;
                if (stat1 != null)
                {
                    float.TryParse(stat1, out num);
                    text = Math.Round(num * 100) + "%";
                }
                if (stat2 != null)
                {
                    float.TryParse(stat2, out num2);
                    text2 = Math.Round(num2 * 100) + "%";
                }
                var statCompareResult = (num == num2) ? StatCompareResult.Equal : (num2 > num) ? StatCompareResult.Greater : StatCompareResult.Lesser;
                if (statCompareResult != StatCompareResult.Equal)
                    diffVal = Math.Round(Math.Abs(num - num2) * 100) + "%";
                __instance.ShowStatInfo(kawidget2, text, name, text2, diffVal, statCompareResult, showCompare);
            }
            var s = new SortedSet<string>();
            foreach (var att in new[] { equipped?._ItemData?.Attribute, unequipped?._ItemData?.Attribute })
                if (att != null)
                    foreach (var a in att)
                    {
                        if (a == null || s.Contains(a.Key))
                            continue;
                        var n = Main.GetCustomStatInfo( a.Key);
                        if (n != null && n.Valid)
                            s.Add(a.Key);
                    }
            foreach (var f in s)
                Show(
                    Main.GetCustomStatInfo(f).DisplayName,
                    equipped?._ItemData?.GetAttribute<string>(f, null),
                    unequipped?._ItemData?.GetAttribute<string>(f, null));
        }
        [HarmonyPatch(typeof(UiStoreStatCompare), "UpdateStatsCompareData")]
        [HarmonyPostfix]
        static void UiStoreStatCompare_UpdateStatsCompareData(UiStoreStatCompare __instance, List<UiStoreStatCompare.StatDataContainer> ___mStatDataList, KAUIMenu ___mContentMenu, int previewIndex, List<PreviewItemData> previewList)
        {
            if (!Main.ShowRacingEquipmentStats)
                return;
            ___mStatDataList.RemoveAll(x => x._EquippedStat == x._ModifiedStat);
            void Show(string name, string abv, float equipped, float unequipped)
            {
                var statDataContainer = new UiStoreStatCompare.StatDataContainer();
                statDataContainer._StatName = name;
                statDataContainer._AbvStatName = abv;
                statDataContainer._EquippedStat = equipped;
                statDataContainer._ModifiedStat = unequipped;
                statDataContainer._DiffStat = statDataContainer._ModifiedStat - statDataContainer._EquippedStat;
                ___mStatDataList.Add(statDataContainer);
                if (equipped != unequipped)
                {
                    var kawidget = ___mContentMenu.AddWidget(___mContentMenu._Template.name);
                    kawidget.FindChildItem("AbvStatWidget", true).SetText(statDataContainer._AbvStatName);
                    kawidget.FindChildItem("StatDiffWidget", true).SetText(Math.Round(Math.Abs(equipped - unequipped)) + "%");
                    var arrowWidget = kawidget.FindChildItem("ArrowWidget", true);
                    arrowWidget.SetVisibility(true);
                    arrowWidget.SetRotation(Quaternion.Euler(0f, 0f, 0f));
                    if (statDataContainer._DiffStat == 0f)
                    {
                        arrowWidget.SetVisibility(false);
                    }
                    else if (statDataContainer._DiffStat < 0f)
                    {
                        arrowWidget.pBackground.color = Color.red;
                        arrowWidget.SetRotation(Quaternion.Euler(0f, 0f, 180f));
                    }
                    else
                    {
                        arrowWidget.pBackground.color = Color.green;
                    }
                    kawidget.SetVisibility(true);
                }
            }
            var s = new SortedSet<string>();
            var d = new Dictionary<string, (float, float)>();
            var e = new Dictionary<string, (ItemData, ItemData)>();
            foreach (var part in AvatarData.pInstance.Part)
                if (part != null)
                {
                    var equipped = part.UserInventoryId > 0 ? CommonInventoryData.pInstance.FindItemByUserInventoryID(part.UserInventoryId.Value)?.Item : null;
                    if (equipped != null)
                    {
                        var key = part.PartType;
                        if (key.StartsWith("DEFAULT_"))
                            key = key.Remove(0, 8);
                        var t = e.GetOrCreate(key);
                        e[key] = (equipped, t.Item2);
                    }
                }
            foreach (var preview in previewIndex == -1 ? previewList as IEnumerable<PreviewItemData> : new[] { previewList[previewIndex] })
                if (preview.pItemData != null)
                {
                    var key = AvatarData.GetPartName(preview.pItemData);
                    if (key.StartsWith("DEFAULT_"))
                        key = key.Remove(0, 8);
                    var t = e.GetOrCreate(key);
                    if (t.Item2 == null)
                        e[key] = (t.Item1, preview.pItemData);
                }
            foreach (var p in e)
            {
                var item2 = p.Value.Item2 ?? p.Value.Item1;
                Debug.Log($"\n{p.Key}\n - [{p.Value.Item1?.Attribute.Join(x => x.Key + "=" + x.Value)}]\n - [{item2?.Attribute.Join(x => x.Key + "=" + x.Value)}]");
                if (p.Value.Item1?.Attribute != null)
                    foreach (var a in p.Value.Item1.Attribute)
                    {
                        if (a == null)
                            continue;
                        var cs = Main.GetCustomStatInfo(a.Key);
                        if (cs == null || !cs.Valid)
                            continue;
                        if (!float.TryParse(a.Value, out var value))
                            continue;
                        s.Add(a.Key);
                        var t = d.GetOrCreate(a.Key);
                        d[a.Key] = (t.Item1 + value, t.Item2);
                    }
                if (item2?.Attribute != null)
                    foreach (var a in item2.Attribute)
                    {
                        if (a == null)
                            continue;
                        var cs = Main.GetCustomStatInfo(a.Key);
                        if (cs == null || !cs.Valid)
                            continue;
                        if (!float.TryParse(a.Value, out var value))
                            continue;
                        s.Add(a.Key);
                        var t = d.GetOrCreate(a.Key);
                        d[a.Key] = (t.Item1, t.Item2 + value);
                    }
            }
            foreach (var i in s)
            {
                var t = d[i];
                var c = Main.GetCustomStatInfo(i);
                if (t.Item1 != t.Item2)
                    Show(c.DisplayName, c.Abreviation, t.Item1 * 100, t.Item2 * 100);
            }
        }

        [HarmonyPatch(typeof(UiAvatarCustomization), "ShowAvatarStats")]
        [HarmonyPostfix]
        static void UiAvatarCustomization_ShowAvatarStats(UiAvatarCustomization __instance, UiStatPopUp ___mUiStats)
        {
            if (!Main.ShowRacingEquipmentStats)
                return;
            void Show(string name, string value)
            {
                KAWidget kawidget = ___mUiStats.GetContentMenuCombat().AddWidget(___mUiStats.GetContentMenuCombat()._Template.name);
                kawidget.FindChildItem("CombatStatWidget", true).SetText(name);
                kawidget.FindChildItem("CombatStatValueWidget", true).SetText(value);
            }
            var custom = __instance.pCustomAvatar;
            var e = new HashSet<string>();
            var s = new SortedSet<string>();
            var d = new Dictionary<string, float>();
            foreach (var part in AvatarData.pInstance.Part)
                if (part != null)
                {
                    var equipped = custom == null
                        ? part.UserInventoryId > 0
                            ? CommonInventoryData.pInstance.FindItemByUserInventoryID(part.UserInventoryId.Value)?.Item
                            : null
                        : CommonInventoryData.pInstance.FindItemByUserInventoryID(custom.GetInventoryId(part.PartType))?.Item;
                    if (equipped != null)
                    {
                        var key = part.PartType;
                        if (key.StartsWith("DEFAULT_"))
                            key = key.Remove(0, 8);
                        if (!e.Add(key))
                            continue;
                        if (equipped.Attribute != null)
                            foreach (var a in equipped.Attribute)
                            {
                                if (a == null)
                                    continue;
                                var cs = Main.GetCustomStatInfo(a.Key);
                                if (cs == null || !cs.Valid)
                                    continue;
                                if (!float.TryParse(a.Value, out var value))
                                    continue;
                                s.Add(a.Key);
                                d[a.Key] = d.GetOrCreate(a.Key) + value;
                            }
                    }
                }
            foreach (var k in s)
                Show(Main.GetCustomStatInfo(k).DisplayName, Math.Round(d[k] * 100) + "%");
        }
    }
}