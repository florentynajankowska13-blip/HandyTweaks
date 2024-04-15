using BepInEx;
using ConfigTweaks;
using HarmonyLib;
using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;
using System.Text;
using System.Net;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace HandyTweaks
{
    [BepInPlugin("com.aidanamite.HandyTweaks", "Handy Tweaks", "1.1.3")]
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
        [ConfigField]
        public static bool InfiniteZoom = false;
        [ConfigField]
        public static float ZoomSpeed = 1;
        [ConfigField]
        public static bool DisableDragonAutomaticSkinUnequip = true;
        [ConfigField]
        public static bool ApplyDragonPrimaryToEmission = false;
        [ConfigField]
        public static bool AllowCustomizingSpecialDragons = false;
        [ConfigField]
        public static int StableQuestChanceBoost = 0;
        [ConfigField]
        public static float StableQuestDragonValueMultiplier = 1;
        [ConfigField]
        public static float StableQuestTimeMultiplier = 1;
        [ConfigField]
        public static bool BiggerInputBoxes = true;
        [ConfigField]
        public static bool MoreNameFreedom = true;
        [ConfigField]
        public static bool AutomaticFireballs = true;
        [ConfigField]
        public static bool AlwaysMaxHappiness = false;
        [ConfigField]
        public static bool CheckForModUpdates = true;
        [ConfigField]
        public static int UpdateCheckTimeout = 60;
        [ConfigField]
        public static int MaxConcurrentUpdateChecks = 4;

        public static Main instance;
        static List<(BaseUnityPlugin, string)> updatesFound = new List<(BaseUnityPlugin, string)>();
        static ConcurrentDictionary<WebRequest,bool> running = new ConcurrentDictionary<WebRequest, bool>();
        static int currentActive;
        static bool seenLogin = false;
        static GameObject waitingUI;
        static RectTransform textContainer;
        static Text waitingText;
        float waitingTime;
        public void Awake()
        {
            instance = this;
            if (CheckForModUpdates)
            {
                waitingUI = new GameObject("Waiting UI", typeof(RectTransform));
                var c = waitingUI.AddComponent<Canvas>();
                DontDestroyOnLoad(waitingUI);
                c.renderMode = RenderMode.ScreenSpaceOverlay;
                var s = c.gameObject.AddComponent<CanvasScaler>();
                s.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                s.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
                s.matchWidthOrHeight = 1;
                s.referenceResolution = new Vector2(Screen.width, Screen.height);
                var backing = new GameObject("back", typeof(RectTransform)).AddComponent<Image>();
                backing.transform.SetParent(c.transform, false);
                backing.color = Color.black;
                backing.gameObject.layer = LayerMask.NameToLayer("UI");
                waitingText = new GameObject("text", typeof(RectTransform)).AddComponent<Text>();
                waitingText.transform.SetParent(backing.transform, false);
                waitingText.text = "Checking for mod updates (??? remaining)";
                waitingText.font = Font.CreateDynamicFontFromOSFont("Consolas", 100);
                waitingText.fontSize = 25;
                waitingText.color = Color.white;
                waitingText.alignment = TextAnchor.MiddleCenter;
                waitingText.material = new Material(Shader.Find("Unlit/Text"));
                waitingText.gameObject.layer = LayerMask.NameToLayer("UI");
                waitingText.supportRichText = true;
                textContainer = backing.GetComponent<RectTransform>();
                textContainer.anchorMin = new Vector2(0, 1);
                textContainer.anchorMax = new Vector2(0, 1);
                textContainer.offsetMin = new Vector2(0, -waitingText.preferredHeight - 40);
                textContainer.offsetMax = new Vector2(waitingText.preferredWidth + 40, 0);
                var tT = waitingText.GetComponent<RectTransform>();
                tT.anchorMin = new Vector2(0, 0);
                tT.anchorMax = new Vector2(1, 1);
                tT.offsetMin = new Vector2(20, 20);
                tT.offsetMax = new Vector2(-20, -20);
                foreach (var plugin in Resources.FindObjectsOfTypeAll<BaseUnityPlugin>())
                    CheckModVersion(plugin);
            }
            new Harmony("com.aidanamite.HandyTweaks").PatchAll();
            Logger.LogInfo("Loaded");
        }

        [MethodImpl(MethodImplOptions.Synchronized)]
        bool CanStartCheck()
        {
            if (currentActive < MaxConcurrentUpdateChecks)
            {
                currentActive++;
                return true;
            }
            return false;
        }
        void CheckStopped() => currentActive--;
        public async void CheckModVersion(BaseUnityPlugin plugin)
        {
            string url = null;
            bool isGit = true;
            var f = plugin.GetType().GetField("UpdateUrl", ~BindingFlags.Default);
            if (f != null)
            {
                var v = f.GetValue(plugin);
                if (v is string s)
                {
                    url = s;
                    isGit = false;
                }
            }
            f = plugin.GetType().GetField("GitKey", ~BindingFlags.Default);
            if (f != null)
            {
                var v = f.GetValue(plugin);
                if (v is string s)
                    url = "https://api.github.com/repos/" + s + "/releases/latest";
            }
            if (url == null)
            {
                var split = plugin.Info.Metadata.GUID.Split('.');
                if (split.Length >= 2)
                {
                    if (split[0] == "com" && split.Length >= 3)
                        url = $"https://api.github.com/repos/{split[1]}/{split[split.Length - 1]}/releases/latest";
                    else
                        url = $"https://api.github.com/repos/{split[0]}/{split[split.Length - 1]}/releases/latest";
                }
            }
            if (url == null)
            {
                Logger.LogInfo($"No update url found for {plugin.Info.Metadata.Name} ({plugin.Info.Metadata.GUID})");
                return;
            }
            var request = WebRequest.CreateHttp(url);
            request.Timeout = UpdateCheckTimeout * 1000;
            request.UserAgent = "SoDMod-HandyTweaks-UpdateChecker";
            request.Accept = isGit ? "application/vnd.github+json" : "raw";
            request.Method = "GET";
            running[request] = true;
            try
            {
                while (!CanStartCheck())
                    await System.Threading.Tasks.Task.Delay(100);
                using (var req = request.GetResponseAsync())
                {
                    await req;
                    if (req.Status == System.Threading.Tasks.TaskStatus.RanToCompletion)
                    {
                        var res = req.Result;
                        var v = isGit ? res.GetJsonEntry("tag_name") : res.ReadContent();
                        if (string.IsNullOrEmpty(v))
                            Logger.LogInfo($"Update check failed for {plugin.Info.Metadata.Name} ({plugin.Info.Metadata.GUID})\nURL: {url}\nReason: Responce was null");
                        if (Version.TryParse(v, out var newVersion))
                        {
                            if (plugin.Info.Metadata.Version == newVersion)
                                Logger.LogInfo($"{plugin.Info.Metadata.Name} ({plugin.Info.Metadata.GUID}) is up-to-date");
                            else if (plugin.Info.Metadata.Version > newVersion)
                                Logger.LogInfo($"{plugin.Info.Metadata.Name} ({plugin.Info.Metadata.GUID}) is newer than the latest release. Release is {newVersion}, current is {plugin.Info.Metadata.Version}");
                            else
                            {
                                Logger.LogInfo($"{plugin.Info.Metadata.Name} ({plugin.Info.Metadata.GUID}) has an update available. Latest is {newVersion}, current is {plugin.Info.Metadata.Version}");
                                updatesFound.Add((plugin, newVersion.ToString()));
                            }
                        }
                        else
                            Logger.LogInfo($"Update check failed for {plugin.Info.Metadata.Name} ({plugin.Info.Metadata.GUID})\nURL: {url}\nReason: Responce could not be parsed {(v.Length > 100 ? $"\"{v.Remove(100)}...\" (FullLength={v.Length})" : $"\"{v}\"")}");
                    }
                    else
                        Logger.LogInfo($"Update check failed for {plugin.Info.Metadata.Name} ({plugin.Info.Metadata.GUID})\nURL: {url}\nReason: No responce");
                }
            }
            catch (Exception e)
            {
                Logger.LogError($"Update check failed for {plugin.Info.Metadata.Name} ({plugin.Info.Metadata.GUID})\nURL: {url}\nReason: {e.GetType().FullName}: {e.Message}");
                if (!(e is WebException))
                    Logger.LogError(e);
            } finally
            {
                CheckStopped();
                running.TryRemove(request, out _);
            }
        }

        float timer;
        public void Update()
        {
            if (!seenLogin && UiLogin.pInstance)
                seenLogin = true;
            if (running != null && running.Count == 0 && seenLogin)
            {
                running = null;
                Destroy(waitingUI);
                if (updatesFound.Count == 1)
                    GameUtilities.DisplayOKMessage("PfKAUIGenericDB", $"Mod {updatesFound[0].Item1.Info.Metadata.Name} has an update available\nCurrent: {updatesFound[0].Item1.Info.Metadata.Version}\nLatest: {updatesFound[0].Item2}", null, "");
                else if (updatesFound.Count > 1)
                {
                    var s = new StringBuilder();
                    s.Append(updatesFound.Count);
                    s.Append(" mod updates available:");
                    for (int i = 0; i < updatesFound.Count; i++)
                    {
                        s.Append("\n");
                        if (i == 4)
                        {
                            s.Append("(");
                            s.Append(updatesFound.Count - 4);
                            s.Append(" more) ...");
                            break;
                        }
                        s.Append(updatesFound[i].Item1.Info.Metadata.Name);
                        s.Append(" ");
                        s.Append(updatesFound[i].Item1.Info.Metadata.Version);
                        s.Append(" > ");
                        s.Append(updatesFound[i].Item2);
                    }
                    GameUtilities.DisplayOKMessage("PfKAUIGenericDB", s.ToString(), null, "");
                }
            }
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
            waitingTime += Time.deltaTime;
            if (waitingText)
            {
                if (waitingTime >= 1)
                {
                    textContainer.offsetMin = new Vector2(0, -waitingText.preferredHeight - 40);
                    textContainer.offsetMax = new Vector2(waitingText.preferredWidth + 40, 0);
                    waitingTime -= 1;
                }
                var t = $"Checking for mod updates ({running.Count} remaining)";
                var s = new StringBuilder();
                for (int i = 0; i < t.Length; i++)
                {
                    s.Append("<color=#");
                    s.Append(ColorUtility.ToHtmlStringRGB(Color.HSVToRGB(0, 0, (float)(Math.Sin((i / (double)t.Length - waitingTime) * Math.PI * 2) / 4 + 0.75))));
                    s.Append(">");
                    s.Append(t[i]);
                    s.Append("</color>");
                }
                waitingText.text = s.ToString();
            }
            if (AlwaysMaxHappiness && SanctuaryManager.pCurPetInstance)
            {
                var cur = SanctuaryManager.pCurPetInstance.GetPetMeter(SanctuaryPetMeterType.HAPPINESS).mMeterValData.Value;
                var max = SanctuaryData.GetMaxMeter(SanctuaryPetMeterType.HAPPINESS, SanctuaryManager.pCurPetInstance.pData);
                if (cur < max)
                    SanctuaryManager.pCurPetInstance.UpdateMeter(SanctuaryPetMeterType.HAPPINESS, max - cur);
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

        static Dictionary<string, (PetStatType, string, string)> FlightFieldToType = new Dictionary<string, (PetStatType, string, string)>
        {
            { "_YawTurnRate",(PetStatType.TURNRATE,"TRN","") },
            { "_PitchTurnRate",(PetStatType.PITCHRATE,"PCH", "") },
            { "_Acceleration",(PetStatType.ACCELERATION,"ACL", "") },
            { "_Speed",(PetStatType.MAXSPEED,"FSP", "Pet ") }
        };
        static Dictionary<string, (string, string)> PlayerFieldToType = new Dictionary<string, (string, string)>
        {
            { "_MaxForwardSpeed",("Walk Speed","WSP") },
            { "_Gravity",("Gravity","GRV") },
            { "_Height",("Height","HGT") },
            { "_PushPower",("Push Power","PSH") }
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
                if (AttributeName.TryGetAttributeField(out var field))
                {
                    if (FlightFieldToType.TryGetValue(field, out var type))
                    {
                        found = true;
                        name = type.Item3 + SanctuaryData.GetDisplayTextFromPetStat(type.Item1);
                        abv = type.Item2;
                    }
                    else if (PlayerFieldToType.TryGetValue(field,out var type3))
                    {
                        found = true;
                        (name, abv) = type3;
                    }
                }
                if (!found && Enum.TryParse<SanctuaryPetMeterType>(AttributeName, true, out var type2) && MeterToName.TryGetValue(type2, out var meterName))
                {
                    found = true;
                    (name, abv) = meterName;
                }
                statCache[AttributeName] = v = new CustomStatInfo(AttributeName,name,abv,found);
            }
            return v;
        }

        public static int GemCost;
        public static int CoinCost;
        public static List<ItemData> Buying;
        public void ConfirmBuyAll()
        {
            if ( GemCost > Money.pGameCurrency || CoinCost > Money.pCashCurrency)
            {
                GameUtilities.DisplayOKMessage("PfKAUIGenericDB", GemCost > Money.pGameCurrency ? CoinCost > Money.pCashCurrency ? "Not enough gems and coins" : "Not enough gems" : "Not enough coins", null, "");
                return;
            }
            foreach (var i in Buying)
                CommonInventoryData.pInstance.AddPurchaseItem(i.ItemID, 1, "HandyTweaks.BuyAll");
            KAUICursorManager.SetExclusiveLoadingGear(true);
            CommonInventoryData.pInstance.DoPurchase(0,0,x =>
            {
                KAUICursorManager.SetExclusiveLoadingGear(false);
                GameUtilities.DisplayOKMessage("PfKAUIGenericDB", x.Success ? "Purchase complete" : "Purchase failed", null, "");
                if (x.Success)
                    KAUIStore.pInstance.pChooseMenu.ChangeCategory(KAUIStore.pInstance.pFilter, true);

            });
        }

        public void DoNothing() { }
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
        static FieldInfo _mInventory = typeof(CommonInventoryData).GetField("mInventory", ~BindingFlags.Default);
        public static Dictionary<int, List<UserItemData>> FullInventory(this CommonInventoryData inv) => (Dictionary<int, List<UserItemData>>)_mInventory.GetValue(inv);
        static FieldInfo _mCachedItemData = typeof(KAUIStoreChooseMenu).GetField("mCachedItemData", ~BindingFlags.Default);
        public static Dictionary<ItemData, int> GetCached(this KAUIStoreChooseMenu menu) => (Dictionary<ItemData, int>)_mCachedItemData.GetValue(menu);
        static MethodInfo _RemoveDragonSkin = typeof(UiDragonCustomization).GetMethod("RemoveDragonSkin", ~BindingFlags.Default);
        public static void RemoveDragonSkin(this UiDragonCustomization menu) => _RemoveDragonSkin.Invoke(menu, new object[0]);

        public static string ReadContent(this WebResponse response, Encoding encoding = null)
        {
            using (var stream = response.GetResponseStream())
            {
                var b = new byte[stream.Length];
                stream.Read(b, 0, b.Length);
                return (encoding ?? Encoding.UTF8).GetString(b);
            }
        }
        public static string GetJsonEntry(this WebResponse response, string key, Encoding encoding = null)
        {
            using (var stream = response.GetResponseStream())
            {
                var reader = System.Runtime.Serialization.Json.JsonReaderWriterFactory.CreateJsonReader(stream, new System.Xml.XmlDictionaryReaderQuotas() {  });
                while (reader.Name != key && reader.Read())
                { }
                if (reader.Name == key && reader.Read())
                    return reader.Value;
                return null;
            }
        }

        public static bool IsRankLocked(this ItemData data, out int rid, int rankType)
        {
            rid = 0;
            if (data.RewardTypeID > 0)
                rankType = data.RewardTypeID;
            if (data.Points != null && data.Points.Value > 0)
            {
                rid = data.Points.Value;
                UserAchievementInfo userAchievementInfoByType = UserRankData.GetUserAchievementInfoByType(rankType);
                return userAchievementInfoByType == null || userAchievementInfoByType.AchievementPointTotal == null || rid > userAchievementInfoByType.AchievementPointTotal.Value;
            }
            if (data.RankId != null && data.RankId.Value > 0)
            {
                rid = data.RankId.Value;
                UserRank userRank = (rankType == 8) ? PetRankData.GetUserRank(SanctuaryManager.pCurPetData) : UserRankData.GetUserRankByType(rankType);
                return userRank == null || rid > userRank.RankID;
            }
            return false;
        }

        public static bool HasPrereqItem(this ItemData data)
        {
            if (data.Relationship == null)
                return true;
            ItemDataRelationship[] relationship = data.Relationship;
            foreach (var itemDataRelationship in data.Relationship)
                if (itemDataRelationship.Type == "Prereq")
                    return (ParentData.pIsReady && ParentData.pInstance.HasItem(itemDataRelationship.ItemId)) || CommonInventoryData.pInstance.FindItem(itemDataRelationship.ItemId) != null;
            return true;
        }
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
                Debug.Log($"\n{p.Key}\n - [{p.Value.Item1?.Attribute?.Join(x => x.Key + "=" + x.Value)}]\n - [{item2?.Attribute?.Join(x => x.Key + "=" + x.Value)}]");
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

    [HarmonyPatch(typeof(BaseUnityPlugin), MethodType.Constructor, new Type[0])]
    static class Patch_CreatePluginObj
    {
        static void Postfix(BaseUnityPlugin __instance)
        {
            if (Main.CheckForModUpdates)
                Main.instance.CheckModVersion(__instance);
        }
    }

    [HarmonyPatch(typeof(KAUIStore))]
    static class Patch_Store
    {
        [HarmonyPatch("Start")]
        [HarmonyPostfix]
        static void Start(KAUIStore __instance, KAWidget ___mBtnPreviewBuy)
        {
            var n = __instance.DuplicateWidget(___mBtnPreviewBuy, UIAnchor.Side.BottomLeft);
            n.name = "btnBuyAll";
            n.SetText("Buy All");
            n.SetVisibility(true);
            n.SetInteractive(true);
            var p = ___mBtnPreviewBuy.transform.position;
            p.x = -p.x * 0.7f;
            n.transform.position = p;
        }

        [HarmonyPatch("OnClick")]
        [HarmonyPostfix]
        static void OnClick(KAUIStore __instance, KAWidget item)
        {
            if (item.name == "btnBuyAll")
            {
                var byCatergory = CommonInventoryData.pInstance.FullInventory();
                
                var all = new List<ItemData>();
                var check = new HashSet<int>();
                var gems = 0;
                var coins = 0;
                var cache = KAUIStore.pInstance.pChooseMenu.GetCached();
                foreach (var ite in cache.Keys)
                    if (ite != null
                        && !ite.IsBundleItem()
                        && !ite.HasCategory(Category.MysteryBox)
                        && !ite.HasCategory(Category.DragonTickets)
                        && !ite.HasCategory(Category.DragonAgeUp)
                        && (!ite.Locked || SubscriptionInfo.pIsMember)
                        && (__instance.pCategoryMenu.pDisableRankCheck || !ite.IsRankLocked(out _, __instance.pStoreInfo._RankTypeID))
                        && ite.HasPrereqItem()
                        && CommonInventoryData.pInstance.GetQuantity(ite.ItemID) <= 0
                        && check.Add(ite.ItemID))
                    {
                        all.Add(ite);
                        if (ite.GetPurchaseType() == 1)
                            coins += ite.GetFinalCost();
                        else
                            gems += ite.GetFinalCost();
                    }
                if (all.Count == 0)
                    GameUtilities.DisplayOKMessage("PfKAUIGenericDB", "No items left to buy", null, "");
                else
                {
                    Main.CoinCost = coins;
                    Main.GemCost = gems;
                    Main.Buying = all;
                    GameUtilities.DisplayGenericDB("PfKAUIGenericDB", $"Buying these {Main.Buying.Count} items will cost {(gems > 0 ? coins > 0 ? $"{coins} coins and {gems} gems" : $"{gems} gems" : coins > 0 ? $"{coins} coins" : "nothing")}. Are you sure you want to buy these?", "Buy All", Main.instance.gameObject, nameof(Main.ConfirmBuyAll), nameof(Main.DoNothing), null, null, true);
                }
            }
        }
    }

    [HarmonyPatch(typeof(CaAvatarCam), "LateUpdate")]
    static class Patch_AvatarCam
    {
        static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            var code = instructions.ToList();
            for (int i = code.Count - 1; i >= 0; i--)
                if (code[i].opcode == OpCodes.Ldfld && code[i].operand is FieldInfo f && f.Name == "mMaxCameraDistance")
                    code.Insert(i + 1, new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(Patch_AvatarCam), nameof(EditMaxZoom))));
                else if (code[i].opcode == OpCodes.Ldc_R4 && (float)code[i].operand == 0.25f)
                    code.Insert(i + 1, new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(Patch_AvatarCam), nameof(EditZoomSpeed))));
            return code;
        }
        static float EditMaxZoom(float original) => Main.InfiniteZoom ? float.PositiveInfinity : original;
        static float EditZoomSpeed(float original) => original * Main.ZoomSpeed;
    }

    [HarmonyPatch(typeof(UiDragonCustomization), "RemoveDragonSkin")]
    static class Patch_ChangeDragonColor
    {
        static bool Prefix() => !Main.DisableDragonAutomaticSkinUnequip;
    }

    [HarmonyPatch(typeof(SanctuaryPet), "UpdateShaders")]
    static class Patch_UpdatePetShaders
    {
        static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            var code = instructions.ToList();
            code.InsertRange(code.FindIndex(x => x.opcode == OpCodes.Ldloc_S && ((x.operand is LocalBuilder l && l.LocalIndex == 6) || (x.operand is IConvertible i && i.ToInt32(CultureInfo.InvariantCulture) == 6))) + 1,
                new[] 
                {
                    new CodeInstruction(OpCodes.Ldloc_0),
                    new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(Patch_UpdatePetShaders), nameof(EditMat)))
                });
            return code;
        }
        static Material EditMat(Material material, Color primary)
        {
            if (material.HasProperty("_EmissiveColor"))
            {
                if (Main.ApplyDragonPrimaryToEmission)
                {
                    var e = MaterialEdit.Get(material).OriginalEmissive;
                    material.SetColor("_EmissiveColor", new Color(primary.r * e.strength, primary.g * e.strength, primary.b * e.strength, primary.a * e.alpha));
                }
                else
                    material.SetColor("_EmissiveColor", MaterialEdit.Get(material).OriginalEmissive.original);
            }
            return material;
        }
    }

    public class MaterialEdit
    {
        static ConditionalWeakTable<Material, MaterialEdit> data = new ConditionalWeakTable<Material, MaterialEdit>();
        public static MaterialEdit Get(Material material)
        {
            if (data.TryGetValue(material, out var edit)) return edit;
            edit = data.GetOrCreateValue(material);
            if (material.HasProperty("_EmissiveColor"))
            {
                var c = material.GetColor("_EmissiveColor");
                edit.OriginalEmissive = (Math.Max(Math.Max(c.r,c.g),c.b),c.a, c);
            }
            return edit;
        }
        public (float strength, float alpha, Color original) OriginalEmissive;
    }

    [HarmonyPatch(typeof(SanctuaryData), "GetPetCustomizationType", typeof(int))]
    static class Patch_PetCustomization
    {
        static bool Prefix(ref PetCustomizationType __result)
        {
            if (Main.AllowCustomizingSpecialDragons)
            {
                __result = PetCustomizationType.Default;
                return false;
            }
            return true;
        }
    }

    [HarmonyPatch(typeof(UserNotifyDragonTicket))]
    static class Patch_OpenCloseCustomization
    {
        public static (string, string)? closed;
        [HarmonyPatch("ActivateDragonCreationUIObj")]
        [HarmonyPrefix]
        static void ActivateDragonCreationUIObj()
        {
            if (KAUIStore.pInstance)
            {
                closed = (KAUIStore.pInstance.pCategory, KAUIStore.pInstance.pStoreInfo._Name);
                KAUIStore.pInstance.ExitStore();
            }
        }
        [HarmonyPatch("OnStableUIClosed")]
        [HarmonyPostfix]
        static void OnStableUIClosed()
        {
            if (closed != null)
            {
                var t = closed.Value;
                closed = null;
                StoreLoader.Load(true, t.Item1, t.Item2, null, UILoadOptions.AUTO, "", null);
            }
        }
    }

    [HarmonyPatch(typeof(SanctuaryData), "GetLocalizedPetName")]
    static class Patch_GetPetName
    {
        static void Postfix(RaisedPetData raisedPetData, ref string __result)
        {
            if (__result.Length == 15 && __result.StartsWith("Dragon-") && uint.TryParse(__result.Remove(0, 7), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out _))
                __result = SanctuaryData.GetPetDefaultName(raisedPetData.PetTypeID);
        }
    }

    [HarmonyPatch]
    static class Patch_GetStableQuestDuration
    {
        static IEnumerable<MethodBase> TargetMethods()
        {
            yield return AccessTools.Method(typeof(StableQuestSlotWidget), "HandleAdButtons");
            yield return AccessTools.Method(typeof(StableQuestSlotWidget), "StateChangeInit");
            yield return AccessTools.Method(typeof(StableQuestSlotWidget), "Update");
            yield return AccessTools.Method(typeof(TimedMissionManager), "CheckMissionCompleted", new[] { typeof(TimedMissionSlotData) });
            yield return AccessTools.Method(typeof(TimedMissionManager), "CheckMissionSuccess");
            yield return AccessTools.Method(typeof(TimedMissionManager), "GetCompletionTime");
            yield return AccessTools.Method(typeof(TimedMissionManager), "GetPetEngageTime");
            yield return AccessTools.Method(typeof(TimedMissionManager), "StartMission");
            yield return AccessTools.Method(typeof(UiStableQuestDetail), "HandleAdButton");
            yield return AccessTools.Method(typeof(UiStableQuestDetail), "MissionLogIndex");
            yield return AccessTools.Method(typeof(UiStableQuestDetail), "SetSlotData");
            yield return AccessTools.Method(typeof(UiStableQuestMissionStart), "RefreshUi");
            yield return AccessTools.Method(typeof(UiStableQuestSlotsMenu), "OnAdWatched");
            yield break;
        }

        static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions, ILGenerator iL)
        {
            var code = instructions.ToList();
            for (int i = code.Count - 1; i >= 0; i--)
                if (code[i].operand is FieldInfo f && f.Name == "Duration" && f.DeclaringType == typeof(TimedMission))
                    code.Insert(i + 1, new CodeInstruction(OpCodes.Call, typeof(Patch_GetStableQuestDuration).GetMethod(nameof(EditDuration), ~BindingFlags.Default)));
            return code;
        }

        static int EditDuration(int original) => (int)Math.Round(original * Main.StableQuestTimeMultiplier);
    }

    [HarmonyPatch]
    static class Patch_GetStableQuestBaseChance
    {
        static IEnumerable<MethodBase> TargetMethods() => from m in typeof(TimedMissionManager).GetMethods(~BindingFlags.Default) where m.Name == "GetWinProbability" select m;

        static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions, ILGenerator iL)
        {
            var code = instructions.ToList();
            for (int i = code.Count - 1; i >= 0; i--)
                if (code[i].operand is FieldInfo f && f.Name == "WinFactor" && f.DeclaringType == typeof(TimedMission))
                    code.Insert(i + 1, new CodeInstruction(OpCodes.Call, typeof(Patch_GetStableQuestBaseChance).GetMethod(nameof(EditChance), ~BindingFlags.Default)));
            return code;
        }

        static int EditChance(int original) => original + Main.StableQuestChanceBoost;
    }

    [HarmonyPatch(typeof(TimedMissionManager), "GetWinProbabilityForPet")]
    static class Patch_GetStableQuestPetChance
    {
        static void Postfix(ref float __result) => __result *= Main.StableQuestDragonValueMultiplier;
    }

    [HarmonyPatch]
    static class Patch_GetInputLength
    {
        static IEnumerable<MethodBase> TargetMethods()
        {
            yield return AccessTools.Method(typeof(KAUIStoreBuyPopUp), "RefreshValues");
            yield return AccessTools.Method(typeof(UIInput), "Insert");
            yield return AccessTools.Method(typeof(UIInput), "Validate", new[] { typeof(string) });
            yield return AccessTools.Method(typeof(UiItemTradeGenericDB), "RefreshQuantity");
            yield return AccessTools.Method(typeof(UiPrizeCodeEnterDB), "Start");
            yield break;
        }

        static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions, ILGenerator iL)
        {
            var code = instructions.ToList();
            for (int i = code.Count - 1; i >= 0; i--)
                if (code[i].operand is FieldInfo f && f.Name == "characterLimit" && f.DeclaringType == typeof(UIInput))
                    code.Insert(i + 1, new CodeInstruction(OpCodes.Call, typeof(Patch_GetInputLength).GetMethod(nameof(EditLength), ~BindingFlags.Default)));
            return code;
        }

        static int EditLength(int original) => Main.BiggerInputBoxes ? (int)Math.Min((long)original * original, int.MaxValue) : original;
    }

    [HarmonyPatch(typeof(UIInput),"Validate",typeof(string),typeof(int),typeof(char))]
    static class Patch_CanInput
    {
        static bool Prefix(UIInput __instance, string text, int pos, char ch, ref char __result)
        {
            if (Main.MoreNameFreedom && (__instance.validation == UIInput.Validation.Alphanumeric || __instance.validation == UIInput.Validation.Username || __instance.validation == UIInput.Validation.Name))
            {
                var cat = char.GetUnicodeCategory(ch);
                if (cat == UnicodeCategory.Control || cat == UnicodeCategory.Format || cat == UnicodeCategory.OtherNotAssigned)
                    __result = '\0';
                else
                    __result = ch;
                return false;
            }
            return true;
        }
    }

    [HarmonyPatch(typeof(KAEditBox), "ValidateText", typeof(string), typeof(int), typeof(char))]
    static class Patch_CanInput2
    {
        static bool Prefix(KAEditBox __instance, string text, int charIndex, char addedChar, ref char __result)
        {
            if (Main.MoreNameFreedom && (__instance._CheckValidityOnInput && __instance._RegularExpression != null && __instance._RegularExpression.Contains("a-z")))
            {
                var cat = char.GetUnicodeCategory(addedChar);
                if (cat == UnicodeCategory.Control || cat == UnicodeCategory.Format || cat == UnicodeCategory.OtherNotAssigned)
                    __result = '\0';
                else
                    __result = addedChar;
                return false;
            }
            return true;
        }
    }

    [HarmonyPatch(typeof(UiAvatarControls), "Update")]
    static class Patch_ControlsUpdate
    {
        static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions, ILGenerator iL)
        {
            var code = instructions.ToList();
            var flag = false;
            for (int i = 0; i < code.Count; i++)
                if (code[i].operand is MethodInfo m && flag)
                {
                    if (m.Name == "GetButtonDown")
                        code[i] = new CodeInstruction(OpCodes.Call, typeof(Patch_ControlsUpdate).GetMethod(nameof(ButtonDown), ~BindingFlags.Default));
                    else if (m.Name == "GetButtonUp")
                        code[i] = new CodeInstruction(OpCodes.Call, typeof(Patch_ControlsUpdate).GetMethod(nameof(ButtonUp), ~BindingFlags.Default));
                    flag = false;
                }
                else if (code[i].operand is string str)
                    flag = str == "DragonFire";
            return code;
        }
        static bool ButtonDown(string button) => Main.AutomaticFireballs ? KAInput.GetButton(button) : KAInput.GetButtonDown(button);
        static bool ButtonUp(string button) => Main.AutomaticFireballs ? KAInput.GetButton(button) : KAInput.GetButtonUp(button);
    }

    [HarmonyPatch(typeof(RacingManager),"AddPenalty")]
    static class Patch_AddRacingCooldown
    {
        public static bool Prefix() => RacingManager.Instance.State >= RacingManagerState.RaceCountdown;
    }
}