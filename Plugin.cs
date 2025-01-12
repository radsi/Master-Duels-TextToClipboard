using System;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Diagnostics;

using UnityEngine;
using UnityEngine.UI;

using TMPro;

using YgomSystem.UI;
using YgomSystem.YGomTMPro;
using YgomGame.Duel;
using YgomGame.CardBrowser;

using BepInEx;
using BepInEx.Logging;
using BepInEx.Unity.IL2CPP;
using HarmonyLib;
using Il2CppInterop.Runtime.Injection;

using CrossSpeak;

using static BlindMode.BaseClass;

namespace BlindMode
{
    [BepInPlugin("radsi.blindmode", "Blind Mode", "2.1.0")]
    public class Plugin : BasePlugin
    {
        internal new static ManualLogSource Log;

        public override void Load()
        {
            Log = base.Log;
            TryLoad();
        }

        private void TryLoad()
        {
            try
            {
                ClassInjector.RegisterTypeInIl2Cpp<BaseClass>();
                var plugin = new GameObject(typeof(BaseClass).FullName);
                UnityEngine.Object.DontDestroyOnLoad(plugin);
                plugin.AddComponent<BaseClass>();
                plugin.hideFlags = HideFlags.HideAndDontSave;
                new Harmony("radsi.blindmode").PatchAll();

                Log.LogInfo("Plugin has been loaded!");
                TryPatch();
            }
            catch (Exception e)
            {
                Log.LogError("Error loading the plugin!");
                Log.LogError(e);
            }
        }

        private void TryPatch()
        {
            try
            {
                Log.LogInfo("Attempting to patch...");
                Harmony.CreateAndPatchAll(Assembly.GetExecutingAssembly());
                Log.LogInfo("Successfully patched!");
            }
            catch (Exception e)
            {
                Log.LogError("Error registering patch!");
                Log.LogError(e);
            }
        }
    }

    #region card browser patch
    [HarmonyPatch(typeof(CardBrowserViewController), nameof(CardBrowserViewController.Start))]
    class PatchBrowserViewControllerStart
    {
        [HarmonyPostfix]
        static void Postfix(CardBrowserViewController __instance)
        {
            BaseClass.SnapContentManager = __instance.GetComponentInChildren<SnapContentManager>();
        }
    }
    #endregion

    #region duels patch

    [HarmonyPatch(typeof(DuelLP), nameof(DuelLP.ChangeLP), MethodType.Normal)]
    class PatchChangeLP
    {
        [HarmonyPostfix]
        private static void Postfix(DuelLP __instance)
        {
            SpeakText(string.Format("{0} current life points: {1}", __instance.name.Contains("Far") ? "Opponent's" : "Your", __instance.currentLP));
            if (__instance.currentLP < 1)
            {
                IsInDuel = false;
                cardsInDuel.Clear();
            }
        }
    }

    [HarmonyPatch(typeof(DuelClient), nameof(DuelClient.Awake))]
    class PatchDuelClientSetupPvp
    {
        [HarmonyPostfix]
        static void Postfix(DuelClient __instance)
        {
            currentMenu = Menus.DUEL;
            IsInDuel = true;
        }
    }

    [HarmonyPatch(typeof(CardRoot), nameof(CardRoot.Initialize), MethodType.Normal)]
    class PatchCardRoot
    {
        [HarmonyPostfix]
        private static void Postfix(CardRoot __instance)
        {
            cardsInDuel.Add(__instance);
        }
    }

    [HarmonyPatch(typeof(CardInfo), nameof(CardInfo.SetDescriptionArea))]
    class PatchCardInfoSetCard
    {
        [HarmonyPostfix]
        static void Postfix(CardInfo __instance)
        {
            Instance.Invoke("CopyUI", __instance.gameObject.activeInHierarchy ? 0f : 0.2f);
        }
    }

    #endregion

    #region buttons patches

    [HarmonyPatch(typeof(ColorContainerImage), nameof(ColorContainerImage.SetColor), MethodType.Normal)]
    class PatchColorContainerImage
    {
        [HarmonyPostfix]
        private static void Postfix(ColorContainerImage __instance)
        {
            try
            {
                if (__instance.currentStatusMode != ColorContainer.StatusMode.Enter) return;

                textToCopy = "";

                switch (__instance.transform.parent.parent.parent.name)
                {
                    case "DuelMenuButton":
                        textToCopy = $"Menu button";
                        break;
                }

                if (textToCopy != "") SpeakText();
            }
            catch { }
        }
    }

    [HarmonyPatch(typeof(ColorContainerGraphic), nameof(ColorContainerGraphic.SetColor))]
    class PatchColorContainerGraphic
    {
        [HarmonyPostfix]
        static void Postfix(ColorContainerGraphic __instance)
        {
            try
            {
                if (__instance.currentStatusMode != ColorContainer.StatusMode.Enter) return;

                if (IsInDuel && __instance.transform.parent.parent.name.Contains("DuelListCard"))
                {
                    __instance.transform.parent.parent.GetComponent<SelectionButton>().Click();
                    Instance.CopyUI();
                    return;
                }

                textToCopy = "";

                switch (__instance.transform.parent.parent.name)
                {
                    case "ButtonMaintenance":
                        textToCopy = "Maintenance";
                        break;
                    case "ButtonBug":
                        textToCopy = "Issues";
                        break;
                    case "ButtonNotification":
                        textToCopy = "Notification";
                        break;
                    case "InputButton":
                        if (__instance.transform.parent.parent.name.Equals("NameAreaGroup") || currentMenu == Menus.NONE)
                        {
                            textToCopy = "Rename button/input";
                        }
                        else
                        {
                            textToCopy = "Search card input";
                        }
                        break;
                    case "AutoBuildButton":
                        textToCopy = "Auto-build button";
                        break;
                    case "ButtonBookmark": // add bookmark
                        textToCopy = "Add card to bookmark button";
                        break;
                    case "BookmarkButton": // bookmark menu
                        textToCopy = "Bookmarked cards button";
                        break;
                    case "HowToGetButton":
                        textToCopy = "How to get button";
                        break;
                    case "RelatedCard":
                        textToCopy = "Related cards button";
                        break;
                    case "DismantleButton":
                        string dismantle = FindExtendedTextElement(__instance.transform.parent.parent.GetChild(6).gameObject);
                        textToCopy = $"{(string.IsNullOrEmpty(dismantle) ? "Cant be dismantled" : $"Dismantle card for: {FindExtendedTextElement(__instance.transform.parent.parent.GetChild(6).gameObject)} {GetRarity(__instance.transform.parent.parent.GetChild(6).GetComponentInChildren<Image>().sprite.name)} cp")}";
                        break;
                    case "CreateButton":
                        textToCopy = $"Create card for: {FindExtendedTextElement(__instance.transform.parent.parent.GetChild(6).gameObject)} {GetRarity(__instance.transform.parent.parent.GetChild(6).GetComponentInChildren<Image>().sprite.name)} cp";
                        break;
                    case "AddButton":
                        textToCopy = "Add +1";
                        break;
                    case "RemoveButton":
                        textToCopy = "Remove -1";
                        break;
                    case "CardListButton":
                        textToCopy = "Card list button";
                        break;
                    case "HistotyButton": // they dont know how to write "history"
                        textToCopy = "Card history button";
                        break;
                    case "ButtonRegulation":
                        textToCopy = "Regulation button";
                        break;
                    case "ButtonSecretPack":
                        textToCopy = "Secret pack button";
                        break;
                    case "ButtonInfoSwitching":
                        textToCopy = "Switch display mode button";
                        break;
                    case "ButtonSave":
                        textToCopy = "Save button";
                        break;
                    case "ButtonMenu":
                        textToCopy = "Menu button";
                        break;
                    case "ButtonPickupCard":
                        textToCopy = "Show cards on decks preview";
                        break;
                    case "BulkDecksDeletionButton":
                        textToCopy = "Bulk deck deletion button";
                        break;
                    case "ButtonOpenNeuronDecks":
                        textToCopy = "Link with Yu Gi Oh Database";
                        break;
                    case "FilterButton":
                        textToCopy = "Filters button";
                        break;
                    case "SortButton":
                        textToCopy = "Sort button";
                        break;
                    case "ClearButton":
                        textToCopy = "Clear filters button";
                        break;
                    case "Button0":
                        textToCopy = $"{FindExtendedTextElement(__instance.transform.parent.parent.parent.gameObject)}, lower to higher";
                        break;
                    case "Button1":
                        textToCopy = $"{FindExtendedTextElement(__instance.transform.parent.parent.parent.gameObject)}, higher to lower";
                        break;
                    case "ButtonDismantleIncrement":
                        textToCopy = "Increment dismantle amount";
                        break;
                    case "ButtonDismantleDecrement":
                        textToCopy = "Decrement dismantle amount";
                        break;
                    case "ButtonEnter":
                        textToCopy = "Play";
                        break;
                    case "CopyButton":
                        textToCopy = "Copy deck button";
                        break;
                    case "OKButton":
                        textToCopy = "Ok";
                        break;
                    case "ShowOwnedNumToggle":
                        textToCopy = "Show owned button";
                        break;
                }

                switch (__instance.transform.parent.parent.parent.name)
                {
                    case "TabMyDeck":
                        textToCopy = "My Deck";
                        break;
                    case "TabRental":
                        textToCopy = "Loaner";
                        break;
                    case "ChapterDuel(Clone)":
                        textToCopy = $"Duel, {FindExtendedTextElement(__instance.transform.parent.parent.GetChild(4).gameObject)} stars";
                        break;
                    case "DuelMenuButton":
                        textToCopy = $"Menu button";
                        break;
                }

                if (textToCopy != "") SpeakText();
            }
            catch { }
        }
    }


    [HarmonyPatch(typeof(SelectionButton), nameof(SelectionButton.OnClick), MethodType.Normal)]
    class PatchOnClick
    {
        static List<string> previewElements = new() { "CardPict", "CardClone", "CreateButton", "ImageCard", "NextButton", "PrevButton", "Related Cards", "ThumbButton", "SlotTemplate(Clone)", "Locator", "GoldpassRewardButton", "NormalpassRewardButton", "ButtonDuelPass" };

        [HarmonyPostfix]
        static void Postfix(SelectionButton __instance)
        {
            try
            {
                if (menuNames.TryGetValue(FindExtendedTextElement(__instance.gameObject), out Menus menu))
                {
                    currentMenu = menu;
                    //menusRecord.Add(menu);
                    textRecord.Clear();
                }
            }
            catch { }

            if (__instance.name.Equals("ButtonDecidePositive(Clone)") && IsInDuel)
            {
                IsInDuel = false;
                cardsInDuel.Clear();
                currenElement.Clear();
            }

            if (previewElements.Contains(__instance.name))
            {
                Instance.Invoke("CopyUI", currentMenu == Menus.DuelPass ? 1.5f : 0.5f);
            }
        }
    }

    [HarmonyPatch(typeof(SelectionButton), nameof(SelectionButton.OnSelected), MethodType.Normal)]
    class PatchOnSelected
    {
        [HarmonyPostfix]
        static void Postfix(SelectionButton __instance)
        {
            textToCopy = FindExtendedTextElement(__instance.gameObject);

            switch (currentMenu)
            {
                case Menus.NONE:
                    ProcessNotificationsPopup(__instance);
                    ProcessFriendsMenu(__instance);
                    ProcessProfile(__instance);
                    //ProcessDailyReward(__instance);
                    ProcessEventBanner(__instance);
                    ProcessTopicsBanner(__instance);
                break;
                case Menus.Settings:
                    ProcessSettingsMenu(__instance);
                break;
                case Menus.Notifications:
                    ProcessNotifications(__instance);
                break;
                case Menus.Missions:
                    ProcessMissionsMenu(__instance);
                break;
                case Menus.SHOP:
                    ProcessPacks(__instance);
                    ProcessCardPack(__instance);
                break;
                case Menus.DuelPass:
                    ProcessDuelPass(__instance);
                break;
                case Menus.DECK:
                    ProcessDecksMenu(__instance);
                    ProcessNewDeck(__instance);
                break;
                case Menus.SOLO:
                    ProcessDuelGame(__instance);
                    ProcessDuelMenu(__instance);
                break;
                case Menus.DUEL:
                    ProcessDuelGame(__instance);
                    ProcessDuelMenu(__instance);
                break;
            }

            SpeakText();
        }
    }

    [HarmonyPatch(typeof(SelectionButton), nameof(SelectionButton.OnDeselected), MethodType.Normal)]
    class PatchOnDeselected
    {
        [HarmonyPostfix]
        static void Postfix(SelectionButton __instance)
        {
            DeselectButton();
        }
    }

    #endregion

    [HarmonyPatch(typeof(ViewController), nameof(ViewController.OnBack))]
    class PatchViewController
    {
        [HarmonyPostfix]
        public static void Postfix(ViewController __instance)
        {
            if (__instance.manager.GetFocusViewController().name == "Home")
            {
                currentMenu = Menus.NONE;
            }
        }
    }


    public class BaseClass : MonoBehaviour
    {
        public static BaseClass Instance;

        public static List<string> textRecord = new();
        public static List<CardRoot> cardsInDuel = new();

        public static PreviewElement currenElement = new();

        public static Dictionary<string, Menus> menuNames = new()
        {
            { "DUEL", Menus.DUEL },
            { "DECK", Menus.DECK },
            { "SOLO", Menus.SOLO },
            { "SHOP", Menus.SHOP },
            { "MISSION", Menus.Missions },
            { "Notifications", Menus.Notifications },
            { "Game Settings", Menus.Settings },
            { "Duel Pass", Menus.DuelPass }
        };

        public enum Menus { NONE, DUEL, DECK, SOLO, SHOP, Missions, Notifications, Settings, DuelPass }
        public static Menus currentMenu = Menus.NONE;

        public class CardCustomInfo
        {
            public GameObject cardObject { get; set; } = null;
            public string Link { get; set; } = string.Empty;
            public string Stars { get; set; } = string.Empty;
            public string Atk { get; set; } = string.Empty;
            public string Def { get; set; } = string.Empty;
            public string PendulumScale { get; set; } = string.Empty;
            public string Attributes { get; set; } = string.Empty;
            public string SpellType { get; set; } = string.Empty;
            public string Element { get; set; } = string.Empty;
            public string Owned { get; set; } = string.Empty;
            public bool IsInHand { get; set; } = true;
        }

        public class PreviewElement
        {
            public CardCustomInfo cardInfo { get; set; } = new CardCustomInfo();
            public string Name { get; set; } = string.Empty;
            public string Description { get; set; } = string.Empty;
            public string TimeLeft { get; set; } = string.Empty;
            public string Price { get; set; } = string.Empty;

            public void Clear()
            {
                foreach (PropertyInfo property in GetType().GetProperties())
                {
                    if (property.PropertyType == typeof(string))
                    {
                        property.SetValue(this, string.Empty);
                    }
                }
            }

            public void LogValues()
            {
                foreach (PropertyInfo property in GetType().GetProperties())
                {
                    Console.WriteLine($"{property.Name}: {property.GetValue(this)}");
                }
            }

            public void CopyValuesFrom(PreviewElement source)
            {
                if (source == null) throw new ArgumentNullException(nameof(source));

                foreach (PropertyInfo property in GetType().GetProperties())
                {
                    if (property.CanRead && property.CanWrite)
                    {
                        var value = property.GetValue(source);

                        if (property.Name == nameof(cardInfo) && value is CardCustomInfo sourceCardInfo)
                        {
                            cardInfo = DeepCopy(sourceCardInfo);
                        }
                        else
                        {
                            property.SetValue(this, value);
                        }
                    }
                }
            }

            private static T DeepCopy<T>(T source) where T : class, new()
            {
                if (source == null) return null;

                var result = new T();
                foreach (PropertyInfo property in typeof(T).GetProperties())
                {
                    if (property.CanRead && property.CanWrite)
                    {
                        var value = property.GetValue(source);
                        property.SetValue(result, value);
                    }
                }
                return result;
            }
        }


        private enum Attribute
        {
            Light = 1,
            Dark = 2,
            Water = 3,
            Fire = 4,
            Earth = 5,
            Wind = 6,
            Divine = 7
        }

        private enum Rarity
        {
            Normal = 0,
            Rare = 1,
            SuperRare = 2,
            UltraRare = 3
        }

        private enum DuelPositions
        {
            Normal = 0,
            Rare = 1,
            SuperRare = 2,
            UltraRare = 3
        }

        public static List<string> bannedText = new(){ "00:00", "You can add new Cards to your Deck." };
        public static string textToCopy;
        public static string old_copiedText;

        public static bool IsInDuel = false;

        public static DateTime lastExecutionTime;
        public static readonly TimeSpan cooldown = TimeSpan.FromSeconds(0.1f);

        public static bool UsingMouse = false;


        #region ui related stuff 

        public static SnapContentManager SnapContentManager;
        //public static int[] indexRoll = { 0, 1, 2 };
        
        public void Awake()
        {
            Instance = this;
            CrossSpeakManager.Instance.Initialize();
        }

        public void Start()
        {
            if (Directory.Exists(Path.Join(Paths.PluginPath, "dependencies"))) return;

            string tempFilePath = Path.Combine(Paths.PluginPath, "directories.zip");

            using (var zipFile = new FileStream(tempFilePath, FileMode.Create, FileAccess.Write))
            {
                zipFile.Write(Resource1.dependencies);
                zipFile.Close();
            }

            ZipFile.ExtractToDirectory(tempFilePath, Paths.PluginPath);
            File.Delete(tempFilePath);

            ProcessStartInfo startInfo = new ProcessStartInfo()
            {
                FileName = "powershell.exe",
                Arguments = $"-NoProfile -ExecutionPolicy Bypass -Command \"Start-Sleep -Seconds 2; Start-Process 'steam://run/1449850'\"",
                CreateNoWindow = true,
                UseShellExecute = false
            };

            Process.Start(startInfo);

            Application.Quit();
        }

        public void OnApplicationQuit()
        {
            CrossSpeakManager.Instance.Close();   
        }

        public void Update()
        {
            if (IsInDuel)
            {
                if (Input.GetKeyDown(KeyCode.Space))
                {
                    List<DuelLP> DuelLPs = FindObjectsOfType<DuelLP>().ToList();
                    SpeakText($"Your life points: {DuelLPs.Find(e => e.m_IsNear).currentLP}\nOpponent's life points: {DuelLPs.Find(e => !e.m_IsNear).currentLP}");
                }

                if (Input.GetKeyDown(KeyCode.LeftAlt))
                {
                    currenElement.Clear();
                    CardInfo cardInfo = FindObjectOfType<CardInfo>();
                    if(!cardInfo.gameObject.activeInHierarchy) cardInfo.gameObject.SetActive(true);
                    CopyUI();
                }
            }
        }

        internal static void SpeakText(string text = "")
        {
            if (text == "") text = textToCopy;

            if (DateTime.Now - lastExecutionTime >= cooldown)
            {
                if (!string.IsNullOrEmpty(old_copiedText) && old_copiedText.Equals(text)) return;
                if (string.IsNullOrEmpty(text?.Trim()) || bannedText.Contains(text)) return;

                text = Regex.Replace(text, @"<[^>]+>", "");

                Plugin.Log.LogInfo($"text to speak: {text}");

                CrossSpeakManager.Instance.Speak(text);
                textRecord.Add(text);
                old_copiedText = text;

                lastExecutionTime = DateTime.Now;
            }
        }

        public static void GetUITextElements()
        {
            switch (currentMenu)
            {
                case Menus.DuelPass:
                    if (textRecord.Count == 0)
                    {
                        textToCopy = $"Pass grade: {FindExtendedTextElement(null, "UI/ContentCanvas/ContentManager/DuelPass/DuelPassUI(Clone)/DuelPassArea/RootInfo/GradeAreaWidget/TextDuelPassLevel0")}, Time left: {FindExtendedTextElement(null, "UI/ContentCanvas/ContentManager/DuelPass/DuelPassUI(Clone)/DuelPassArea/RootInfo/LimitArea/LimitDateBase/LimitDateTextTMP")}";
                        return;
                    }
                break;
                case Menus.DECK:
                    if (textRecord.Last().Contains("Create card"))
                    {
                        if (FindExtendedTextElement(null, "UI/OverlayCanvas/DialogManager/CommonDialog/CommonDialogUI(Clone)/Window/Content/TitleGrp/Text").Contains("Unable"))
                            textToCopy = "Unable to create card";
                        SpeakText();
                    }
                    break;
            }

            // check if its an item preview

            if (SnapContentManager == null && !(currentMenu == Menus.DECK || currentMenu == Menus.SOLO || currentMenu == Menus.DUEL))
            {
                List<(string, string)> textElements = FindListExtendedTextElement(null, "UI/OverlayCanvas/DialogManager/ItemPreview/ItemPreviewUI(Clone)/Root/RootMainArea/DescArea/RootDesc/", false);
                currenElement.Name = $"{(textElements.Count > 2 ? $"{textElements.First().Item2} - " : "")}{textElements[textElements.Count - 2].Item2}";
                currenElement.Description = textElements.Last().Item2;
                return;
            }

            var pathConditions = new List<(string PathPrefix, bool Condition)>
            {
                ("UI/OverlayCanvas/DialogManager/CardBrowser/CardBrowserUI(Clone)/Scroll View/Viewport/Content/Template(Clone){0}/CardInfoDetail_Browser(Clone)/Root/Window/StatusArea", SnapContentManager != null),
                ("UI/ContentCanvas/ContentManager/DeckEdit/DeckEditUI(Clone)/CardDetail/Root/Window", GameObject.Find("UI/ContentCanvas/ContentManager/DeckEdit/") != null),
                ("UI/ContentCanvas/ContentManager/DeckBrowser/DeckBrowserUI(Clone)/Root/CardDetail/Root/Window", GameObject.Find("UI/ContentCanvas/ContentManager/DeckBrowser/") != null),
                ("UI/ContentCanvas/ContentManager/DuelClient/CardInfo/CardInfo(Clone)/Root/Window", true)
            };

            foreach (var pathCondition in pathConditions)
            {
                if (!pathCondition.Condition) continue;

                string pathPrefix = pathCondition.PathPrefix;

                if (pathCondition.PathPrefix == pathConditions[0].PathPrefix)
                {
                    pathPrefix = string.Format(pathCondition.PathPrefix, /*indexRoll[1]*/SnapContentManager.currentPage % 3);
                }
               
                List<(string, string)> ParametersTexts = FindListExtendedTextElement(null, pathPrefix);

                /*for (int i = 0; i < ParametersTexts.Count; i++)
                {
                    Plugin.Log.LogInfo(ParametersTexts[i]);
                }*/

                //Plugin.Log.LogInfo(1);
                currenElement.Name = ParametersTexts[0].Item2;
                //Plugin.Log.LogInfo(2);
                currenElement.Description = ParametersTexts.Find(e => e.Item1.Contains("DescriptionValue")).Item2 ?? "";
                //Plugin.Log.LogInfo(3);
                currenElement.cardInfo.Stars = ParametersTexts.Find(e => e.Item1.Contains("Rank") || e.Item1.Contains("Level")).Item2 ?? "";
                //Plugin.Log.LogInfo(4);
                currenElement.cardInfo.Atk = ParametersTexts.Find(e => e.Item1.Contains("Atk")).Item2 ?? "";
                //Plugin.Log.LogInfo(5);
                currenElement.cardInfo.Def = ParametersTexts.Find(e => e.Item1.Contains("Def")).Item2 ?? "";
                //Plugin.Log.LogInfo(6);
                currenElement.cardInfo.PendulumScale = ParametersTexts.Find(e => e.Item1.Contains("Pendulum")).Item2 ?? "";
                //Plugin.Log.LogInfo(7);
                currenElement.cardInfo.Link = ParametersTexts.Find(e => e.Item1.Contains("Link")).Item2 ?? "";
                //Plugin.Log.LogInfo(8);
                currenElement.cardInfo.Element = GetElement(GameObject.Find($"{pathPrefix}/{(pathConditions[0].Condition == false ? (pathConditions[1].Condition ? "TitleArea/PlateTitle/IconAttribute" : "TitleArea/AttributeRoot/IconAttribute") : "TitleAreaGroup/TitleArea/IconAttribute")}").GetComponent<Image>().sprite.name) ?? "";
                //Plugin.Log.LogInfo(9);
                currenElement.cardInfo.Attributes = ParametersTexts.Find(e => e.Item1.Contains("DescriptionItem")).Item2 ?? "";
                //Plugin.Log.LogInfo(10);
                currenElement.cardInfo.SpellType = ParametersTexts.Find(e => e.Item1.Contains("SpellTrap")).Item2 ?? "";
                currenElement.cardInfo.Owned = ParametersTexts.Find(e => e.Item1.Contains("CardNum")).Item2 ?? "";

                break;
            }
        }

        public static string FormatInfo()
        {
            if (string.IsNullOrWhiteSpace(currenElement.Name)) return string.Empty;

            // Add main parameters
            List<string> resultList = new List<string>
            {
                !string.IsNullOrEmpty(currenElement.Name) ? $"Name: {currenElement.Name}" : null,
                !string.IsNullOrEmpty(currenElement.Description) ? $"Description: {currenElement.Description}" : null
            };

            // Check if it's not an item preview to add the rest of parameters
            if (SnapContentManager != null || currentMenu == Menus.SOLO || currentMenu == Menus.DUEL || currentMenu == Menus.DECK)
            {
                resultList = new List<string>
                {
                    !string.IsNullOrEmpty(currenElement.Name) ? $"Name: {currenElement.Name}" : null,
                    (!currenElement.cardInfo.IsInHand && IsInDuel) ? $"Is faced down?: {!GetCardRootOfCurrentCard().isFace}" : null,
                    !string.IsNullOrEmpty(currenElement.cardInfo.Atk) ? $"Attack: {currenElement.cardInfo.Atk}" : null,
                    !string.IsNullOrEmpty(currenElement.cardInfo.Link) ? $"Link level: {currenElement.cardInfo.Link}" : null,
                    !string.IsNullOrEmpty(currenElement.cardInfo.Def) ? $"Defense: {currenElement.cardInfo.Def}" : null,
                    !string.IsNullOrEmpty(currenElement.cardInfo.Stars) ? $"Stars: {currenElement.cardInfo.Stars}" : null,
                    !string.IsNullOrEmpty(currenElement.cardInfo.Element) ? $"Element: {currenElement.cardInfo.Element}" : null,
                    !string.IsNullOrEmpty(currenElement.cardInfo.PendulumScale) ? $"Pendulum scale: {currenElement.cardInfo.PendulumScale}" : null,
                    !string.IsNullOrEmpty(currenElement.cardInfo.Attributes) ? $"Attributes: {(currentMenu == Menus.DECK ? currenElement.cardInfo.Attributes[1..^1] : currenElement.cardInfo.Attributes)}" : null,
                    !string.IsNullOrEmpty(currenElement.cardInfo.SpellType) ? $"Spell type: {currenElement.cardInfo.SpellType}" : null,
                    !string.IsNullOrEmpty(currenElement.cardInfo.Owned) ? $"Owned: {currenElement.cardInfo.Owned}" : null,
                    !string.IsNullOrEmpty(currenElement.Description) ? $"Description: {currenElement.Description}" : FindExtendedTextElement(null, "UI/ContentCanvas/ContentManager/DuelClient/CardInfo/CardInfo(Clone)/Root/Window/DescriptionArea/TextArea/Viewport/TextDescriptionValue/"),
                };
            }
            else if (currentMenu == Menus.SHOP) // Its a card pack
            {
                resultList = new List<string>
                {
                    $"Name: {currenElement.Name}",
                    $"Category: {currenElement.Description}",
                    $"Time left: {currenElement.TimeLeft}",
                    $"Price: {currenElement.Price}",
                };
            }

            resultList = resultList.Where(item => item?.Trim() != null).ToList();

            return string.Join("\n", resultList);
        }

        #endregion

        #region buttons related stuff

        public void CopyUI()
        {
            GetUITextElements();
            SpeakText(FormatInfo());
        }
       
        internal static void DeselectButton()
        {
            old_copiedText = "";
        }

        internal static void ProcessProfile(SelectionButton __instance)
        {
            if (__instance.name.Equals("ButtonPlayer")) textToCopy += $", level {FindExtendedTextElement(__instance.transform.GetChild(0).GetChild(1).GetChild(1).GetChild(1).gameObject, null, false)}";
        }

        internal static void ProcessFriendsMenu(SelectionButton __instance)
        {
            switch (__instance.name)
            {
                case "SearchButton":
                    textToCopy = "Add friend button";
                    break;
                case "OpenToggle":
                    textToCopy = FindExtendedTextElement(__instance.transform.parent.gameObject);
                    break;
            }

        }

        internal static void ProcessDailyReward(SelectionButton __instance)
        {
            if (textToCopy.Equals("Day")) textToCopy += $" {FindExtendedTextElement(__instance.transform.GetChild(3).GetChild(1).gameObject)}, Recieved: {(__instance.transform.Find("RecievedCover").gameObject.activeInHierarchy ? "Yes" : "No")}";
        }

        internal static void ProcessPacks(SelectionButton __instance)
        {
            if (!__instance.transform.parent.name.Contains("Shop")) return;

            List<(string, string)> ParametersTexts = new();

            ParametersTexts = FindListExtendedTextElement(__instance.gameObject);

            currenElement.Name = $"{ParametersTexts.Find(e => e.Item1.Contains("PickupMessage")).Item2 ?? ""} - {ParametersTexts.Find(e => e.Item1.Contains("Name")).Item2} ({ParametersTexts.Find(e => e.Item1.Contains("New")).Item2 ?? ""})";
            currenElement.Description = $"{FindExtendedTextElement(null, "UI/ContentCanvas/ContentManager/Shop/ShopUI(Clone)/Root/Main/ProductsRoot/ShowcaseWidget/ListRoot/ProductList/Viewport/Mask/Content/ShopGroupHeaderWidget(Clone)/Label", false)}";
            currenElement.TimeLeft = $"{ParametersTexts.Find(e => e.Item1.Contains("Limit")).Item2 ?? "None"}";
            currenElement.Price = $"{ParametersTexts.Find(e => e.Item1.Contains("PriceGroup")).Item2 ?? ""}";

            SpeakText(FormatInfo());
        }

        internal static void ProcessDuelMenu(SelectionButton __instance)
        {
            try
            {
                if (__instance.transform.parent.parent.parent.parent.parent.parent.name.Equals("SettingMenuArea")) ProcessSettingsMenu(__instance);

                if (__instance.transform.childCount > 0 && __instance.transform.GetChild(0).name.Equals("Main"))
                {
                    List<(string, string)> soloElements = FindListExtendedTextElement(__instance.gameObject, useRegex: false);
                    textToCopy = $"{soloElements.Last().Item2}, {soloElements.Find(e => e.Item1.Contains("Complete")).Item2}";
                }
            }
            catch
            {

            }
        }
        
        internal static void ProcessDuelGame(SelectionButton __instance)
        {
            if (!IsInDuel) return;

            //Plugin.Log.LogInfo(__instance.name);

            if (!(__instance.name.Contains("HandCard") || __instance.name.Contains("Anchor_"))) return;

            currenElement.cardInfo.cardObject = __instance.gameObject;

            if (__instance.name.Contains("Anchor_"))
            {
                currenElement.cardInfo.IsInHand = false;
            }
            else
            {
                currenElement.cardInfo.IsInHand = true;
                return;
            }

            try
            {
                CardRoot cardRoot = GetCardRootOfCurrentCard();

                if (!cardRoot.isFace && cardRoot.team != 0)
                {
                    SpeakText("Opponent's face down card!");
                }
            }
            catch
            {

            }
        }

        internal static void ProcessMissionsMenu(SelectionButton __instance)
        {
            if (!__instance.name.Equals("Locator")) return;
            
            Transform rootParent = __instance.transform.parent.parent.parent.parent.parent.parent.parent.parent.parent;

            if (rootParent != null)
            {
                if (rootParent.childCount > 0)
                {
                    // need to replace some with character with x
                    string rewardText = FindExtendedTextElement(__instance.transform.GetChild(0).GetChild(2).gameObject, null, false);
                    rewardText = "x" + rewardText[1..];

                    textToCopy = $"{FindExtendedTextElement(rootParent.gameObject, null, false)}\n Reward: {rewardText}\n Time left: {FindExtendedTextElement(rootParent.GetChild(1).GetChild(0).GetChild(3).GetChild(0).gameObject, null, false) ?? "None"}";
                }
            }
        }

        public static void ProcessDecksMenu(SelectionButton __instance)
        {
            if(__instance.name.Equals("ImageCard"))
            {
                if(!UsingMouse) Instance.CopyUI();
                else textToCopy = $"Owned: {textToCopy}, rarity: {GetRarity(__instance.transform.Find("IconRarity").GetComponent<Image>().sprite.name)}";
            }

            if (__instance.transform.parent.parent.parent.name.Equals("Category"))
            {
                textToCopy = $"{textToCopy}, category: {FindExtendedTextElement(__instance.transform.parent.parent.gameObject)}";
            }

            if (__instance.transform.parent.parent.parent.name.Equals("InputButton"))
            {
                textToCopy = "Rename deck button";
            }
            if (__instance.transform.parent.parent.parent.name.Equals("AutoBuildButton"))
            {
                textToCopy = "Auto-build button";
            }
        }

        internal static void ProcessSettingsMenu(SelectionButton __instance)
        {
            if (__instance.transform.parent.parent.name == "Layout" || __instance.transform.parent.parent.parent.name == "EntryButtonsScrollView" || __instance.name == "CancelButton") return;

            string additionalText = "";
            Slider sliderElement = __instance.GetComponentInChildren<Slider>();
            
            if (sliderElement != null)
            {
                additionalText = $"{sliderElement.value} of {sliderElement.maxValue}";
            }
            else
            {
                additionalText = __instance.GetComponentsInChildren<ExtendedTextMeshProUGUI>().Where(e => e.name == "ModeText").First().text;     
            }

            textToCopy += $"\nValue is {additionalText}";
        }

        internal static void ProcessCardPack(SelectionButton __instance)
        {
            if (__instance.name.Equals("CardPict"))
            {
                string ownedText = FindExtendedTextElement(__instance.transform.parent.Find("NumTextArea").gameObject);
                ownedText = "x" + ownedText[1..];
                textToCopy = $"Rarity: {GetRarity(__instance.transform.parent.Find("IconRarity").GetComponent<Image>().sprite.name)}, New: {(__instance.transform.parent.Find("NewIcon").gameObject.activeInHierarchy ? "Yes" : "No")}, Owned: {ownedText}";
            }
        }

        internal static void ProcessNotifications(SelectionButton __instance)
        {
            if (__instance.transform.Find("BaseCategory"))
            {
                textToCopy = FindExtendedTextElement(__instance.transform.Find("TextBody").gameObject, null, false);
                if (!__instance.transform.Find("BaseCategory").gameObject.activeInHierarchy) return;
                textToCopy += $"\nStatus: {__instance.transform.Find("BaseCategory").GetChild(0).GetComponentInChildren<ExtendedTextMeshProUGUI>().text}";
            }
        }
        
        internal static void ProcessNotificationsPopup(SelectionButton __instance)
        {
            if (__instance.transform.parent.parent.parent.parent.parent.parent.name.Equals("NotificationWidget") && currentMenu == Menus.NONE)
            {
                textToCopy = FindExtendedTextElement(__instance.transform.Find("TextBody").gameObject, null, false);
                if (!__instance.transform.Find("BaseCategory").gameObject.activeInHierarchy) return;
                textToCopy += $"\nStatus: {__instance.transform.Find("BaseCategory").GetChild(0).GetComponentInChildren<ExtendedTextMeshProUGUI>().text}";
            }
        }

        internal static void ProcessEventBanner(SelectionButton __instance)
        {
            if (__instance.name.Equals("DuelShortcut")) textToCopy = "Event banner";
        }

        internal static void ProcessTopicsBanner(SelectionButton __instance)
        {
            if (__instance.name.Equals("ButtonBanner")) textToCopy = $"Topic banner, page {__instance.transform.parent.GetComponent<ScrollRectPageSnap>().hpage}";
        }

        internal static void ProcessDuelPass(SelectionButton __instance)
        {
            if(__instance.name.Contains("passRewardButton")) textToCopy = $"{(__instance.name.Contains("Normalpass") ? "Normal" : "Gold")} pass, grade {FindExtendedTextElement(__instance.transform.parent.parent.gameObject)}, quantity: {"x" + textToCopy[1..]}";
        }
        
        internal static void ProcessNewDeck(SelectionButton __instance)
        {
            Transform IconAddDeck = __instance.transform.Find("IconAddDeck");
            if (IconAddDeck != null && IconAddDeck.gameObject.activeInHierarchy) textToCopy = "New deck button";
        }

        internal static CardRoot GetCardRootOfCurrentCard()
        {
            CardRoot cardRoot = cardsInDuel.Find(e => e.cardLocator.pos == currenElement.cardInfo.cardObject.transform.position);
            Plugin.Log.LogInfo(cardRoot.name);
            return cardRoot;
        }

        #endregion

        #region Find text stuff

        public static string GetElement(string attrname)
        {
            if (int.TryParse(attrname.Last().ToString(), out int num))
            {
                foreach (object obj in Enum.GetValues(typeof(Attribute)))
                {
                    Attribute attribute = (Attribute)obj;
                    if (attribute == (Attribute)num)
                    {
                        return attribute.ToString();
                    }
                }
            }
            return "";
        }

        public static string GetRarity(string rarity)
        {
            if (int.TryParse(rarity.Last().ToString(), out int num))
            {
                foreach (object obj in Enum.GetValues(typeof(Rarity)))
                {
                    Rarity attribute = (Rarity)obj;
                    if (attribute == (Rarity)num)
                    {
                        return attribute.ToString();
                    }
                }
            }
            return "";
        }

        public static List<(string, string)> FindListExtendedTextElement(GameObject obj, string objPath = "", bool useRegex = true)
        {
            List<(string, string)> resultList = new();
            if (obj == null && !string.IsNullOrEmpty(objPath)) obj = GameObject.Find(objPath);

            if (obj.TryGetComponent(out ExtendedTextMeshProUGUI textElement) && !IsBannedText(textElement.gameObject, textElement.text, useRegex))
                resultList.Add(($"{textElement.transform.parent.name}/{textElement.name}", textElement.text));
            if (obj.TryGetComponent(out RubyTextGX rubyTextElement) && !IsBannedText(rubyTextElement.gameObject, rubyTextElement.text, useRegex))
                resultList.Add(($"{rubyTextElement.transform.parent.name}/{rubyTextElement.name}", rubyTextElement.text));
            if (obj.TryGetComponent(out TMP_SubMeshUI submeshTextElement) && !IsBannedText(submeshTextElement.gameObject, submeshTextElement.m_TextComponent.text, useRegex))
                resultList.Add(($"{submeshTextElement.transform.parent.name}/{submeshTextElement.name}", submeshTextElement.textComponent.text));

            resultList.AddRange(FindInChildrenList(obj, null, useRegex));

            return resultList.Distinct().ToList();
        }

        public static string FindExtendedTextElement(GameObject obj, string objPath = "", bool useRegex = true)
        {
            if(obj == null && !string.IsNullOrEmpty(objPath)) obj = GameObject.Find(objPath);

            if (obj.TryGetComponent(out ExtendedTextMeshProUGUI textElement) && !IsBannedText(textElement.gameObject, textElement.text, useRegex))
                return textElement.text;
            if (obj.TryGetComponent(out RubyTextGX rubyTextElement) && !IsBannedText(rubyTextElement.gameObject, rubyTextElement.text, useRegex))
                return rubyTextElement.text;
            if (obj.TryGetComponent(out TMP_SubMeshUI submeshTextElement) && !IsBannedText(submeshTextElement.gameObject, submeshTextElement.m_TextComponent.text, useRegex))
                return submeshTextElement.m_TextComponent.text;

            return FindInChildren(obj, "", useRegex);
        }

        private static List<(string, string)> FindInChildrenList(GameObject obj, string objPath = "", bool useRegex = true)
        {
            if (obj == null)
            {
                if (!string.IsNullOrEmpty(objPath))
                {
                    obj = GameObject.Find(objPath);
                }
            }

            List<(string, string)> resultList = new();

            for (int i = 0; i < obj.transform.childCount; i++)
            {
                Transform objTransform = obj.transform.GetChild(i);

                if (objTransform.TryGetComponent(out ExtendedTextMeshProUGUI textElement) &&
                    !IsBannedText(textElement.gameObject, textElement.text, useRegex))
                {
                    resultList.Add(($"{textElement.transform.parent.name}/{textElement.name}", textElement.text));
                }

                if (objTransform.TryGetComponent(out RubyTextGX rubyTextElement) &&
                    !IsBannedText(rubyTextElement.gameObject, rubyTextElement.text, useRegex))
                {
                    resultList.Add(($"{rubyTextElement.transform.parent.name}/{rubyTextElement.name}", rubyTextElement.text));
                }

                if (objTransform.TryGetComponent(out TMP_SubMeshUI submeshTextElement) &&
                    !IsBannedText(submeshTextElement.gameObject, submeshTextElement.textComponent.text, useRegex))
                {
                    resultList.Add(($"{submeshTextElement.transform.parent.name}/{submeshTextElement.name}", submeshTextElement.textComponent.text));
                }

                if(objTransform.childCount > 0)
                {
                    resultList.AddRange(FindInChildrenList(objTransform.gameObject, useRegex: useRegex));
                }
            }

            return resultList.Distinct().ToList();
        }


        private static string FindInChildren(GameObject obj, string objPath = "", bool useRegex = true)
        {
            if (obj == null)
            {
                if (!string.IsNullOrEmpty(objPath))
                {
                    obj = GameObject.Find(objPath);
                }
            }

            Transform objTransform = null;
            ExtendedTextMeshProUGUI UGUIChild = null;
            RubyTextGX rubyChild = null;
            TMP_SubMeshUI submeshChild = null;

            for (int i = 0; i < obj.transform.childCount; i++)
            {
                objTransform = obj.transform.GetChild(i);

                if (objTransform.TryGetComponent(out ExtendedTextMeshProUGUI textElement) && !IsBannedText(textElement.gameObject, textElement.text, useRegex))
                {
                    return textElement.text;
                }

                UGUIChild = objTransform.GetComponentInChildren<ExtendedTextMeshProUGUI>();

                if (UGUIChild != null && !IsBannedText(UGUIChild.gameObject, UGUIChild.text, useRegex))
                {
                    return UGUIChild.text;
                }

                if (objTransform.TryGetComponent(out RubyTextGX rubyTextElement) && !IsBannedText(rubyTextElement.gameObject, rubyTextElement.text, useRegex))
                {
                    return textElement.text;
                }

                rubyChild = objTransform.GetComponentInChildren<RubyTextGX>();

                if (rubyChild != null && !IsBannedText(rubyChild.gameObject, rubyChild.text, useRegex))
                {
                    return rubyChild.text;
                }

                if (objTransform.TryGetComponent(out TMP_SubMeshUI submeshTextElement) && !IsBannedText(submeshTextElement.gameObject, submeshTextElement.textComponent.text, useRegex))
                {
                    return submeshTextElement.textComponent.text;
                }

                submeshChild = objTransform.GetComponentInChildren<TMP_SubMeshUI>();

                if (rubyChild != null && !IsBannedText(submeshChild.gameObject, submeshChild.textComponent.text, useRegex))
                {
                    return submeshChild.textComponent.text;
                }
            }

            return null;
        }

        public static bool IsBannedText(GameObject textElement, string text, bool useRegex)
        {
            if (textElement == null || string.IsNullOrEmpty(text) || (textElement.gameObject.activeInHierarchy == false)) return true;

            return (useRegex && Regex.IsMatch(text, (currentMenu != Menus.NONE || textElement.name.Equals("Button")) ? @"^\s*$" : @"^\s*$|[.!]+$")) || bannedText.Contains(text);
        }
        #endregion
    }
}
