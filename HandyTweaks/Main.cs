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
    [BepInPlugin("com.aidanamite.HandyTweaks", "Handy Tweaks", "1.0.0")]
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
                foreach (var i in f.pFarmItems)
                    if (i && i.pCurrentStage != null && !i.IsWaitingForWsCall())
                    {
                        if (i is CropFarmItem c)
                        {
                            if (c.pCurrentStage._Name == "NoInteraction")
                            {
                                if (AutoSpendFarmGems)
                                {
                                    if (c.CheckGemsAvailable(c.GetSpeedupCost()))
                                        c.GotoNextStage(true);
                                }
                                else if (BypassFarmGemCosts)
                                    c.GotoNextStage();
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
                                if (AutoSpendFarmGems)
                                {
                                    if (a.CheckGemsAvailable(a.GetSpeedupCost()))
                                        a.GotoNextStage(true);
                                }
                                else if (BypassFarmGemCosts)
                                    a.GotoNextStage();
                            }
                        }
                        else if (i is ComposterFarmItem d)
                        {
                            if (d.pCurrentStage._Name.Contains("Harvest"))
                                d.GotoNextStage();
                            else if (d.pCurrentStage._Name.Contains("Feed"))
                            {
                                foreach (var itemStateCriteriaConsumable in d._CompostConsumables)
                                {
                                    var userItemData = CommonInventoryData.pInstance.FindItem(itemStateCriteriaConsumable.ItemID);
                                    if (userItemData != null && itemStateCriteriaConsumable.Amount <= userItemData.Quantity)
                                    {
                                        d.SetCurrentUsedConsumableCriteria(itemStateCriteriaConsumable);
                                        d.GotoNextStage(false);
                                        break;
                                    }
                                }
                            }
                        }
                        else if (i is FishTrapFarmItem t)
                        {
                            if (t.pCurrentStage._Name.Contains("Harvest"))
                                t.GotoNextStage();
                            else if (t.pCurrentStage._Name.Contains("Feed"))
                            {
                                foreach (var itemStateCriteriaConsumable in t._FishTrapConsumables)
                                {
                                    var userItemData = CommonInventoryData.pInstance.FindItem(itemStateCriteriaConsumable.ItemID);
                                    if (userItemData != null && itemStateCriteriaConsumable.Amount <= userItemData.Quantity)
                                    {
                                        t.SetCurrentUsedConsumableCriteria(itemStateCriteriaConsumable);
                                        t.GotoNextStage(false);
                                        break;
                                    }
                                }
                            }
                        }
                    }
            }
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
}