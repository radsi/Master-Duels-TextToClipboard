using System;
using System.Reflection;

using YgomSystem.UI;
using YgomSystem.YGomTMPro;

using BepInEx;
using BepInEx.Logging;
using BepInEx.Unity.IL2CPP;

using HarmonyLib;

using Il2CppInterop.Runtime.Injection;

using UnityEngine;

using UniverseLib;

using System.Text.RegularExpressions;
using System.Collections.Generic;
using System.Linq;
using YgomGame.Duel;

namespace TextToClipboard
{
    [BepInPlugin("someone.texttoclipboard", "TextToClipboard", "1.0.0")]
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
                new Harmony("someone.texttoclipboard").PatchAll();

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
                Log.LogInfo($"Attempting to patch...");

                Harmony.CreateAndPatchAll(Assembly.GetExecutingAssembly());

                Log.LogInfo($"Successfully patched!");
            }
            catch (Exception e)
            {
                Log.LogError($"Error registering patch!");
                Log.LogError(e);
            }
        }
    }

    [HarmonyPatch(typeof(SelectionButton), nameof(SelectionButton.OnClick), MethodType.Normal)]
    class PatchOnClick
    {
        static Dictionary<string, string> menuNames = new Dictionary<string, string>
        {
            { "DUEL", "D" },
            { "DECK", "K" },
            { "SOLO", "S" },
            { "SHOP", "H" },
            { "Missions", "M" }
        };
        public enum Menus
        {
            NONE = 0,
            DUEL = 'D',
            DECK = 'K',
            SOLO = 'S',
            SHOP = 'H',
            Missions = 'M'
        }
        public static Menus currentMenu = Menus.NONE;

        public static string copyText = "";

        [HarmonyPostfix]
        static void Postfix(SelectionButton __instance)
        {
            if (__instance.name == "BackButton") return;

            try
            {
                copyText = BaseClass.FindExtendedTextElement(__instance.transform, true).text;

                if (menuNames.ContainsKey(copyText))
                {
                    currentMenu = (Menus)Enum.Parse(typeof(Menus), menuNames.Keys.ToList().Find(k => k == copyText));
                    Plugin.Log.LogInfo(currentMenu);
                }
            }
            catch
            {

            }

            copyText = "";

            switch (currentMenu)
            {
                case Menus.NONE:

                    break;
                case Menus.DUEL:
                    try
                    {
                        if (__instance.transform.GetChild(4).gameObject.activeSelf || !__instance.transform.parent.transform.name.Contains("Content"))
                        {
                            copyText = $"{(__instance.transform.parent.parent.name.Contains("View") ? "Chooice" : "Option")}: {BaseClass.FindExtendedTextElement(__instance.transform, true).text}";
                            copyText += $", {BaseClass.FindExtendedTextElement(__instance.transform.GetChild(3), true).text}";
                        }
                    }
                    catch
                    {
                        copyText = BaseClass.FindExtendedTextElement(__instance.transform.parent.parent.parent.parent.parent.parent.parent, true).text;
                    }
                    
                    break;

                case Menus.DECK:
                    if(__instance.name == "Button")
                    {
                        copyText = $"{BaseClass.FindExtendedTextElement(__instance.transform, true).text}\n{BaseClass.FindExtendedTextElement("UI/ContentCanvas/ContentManager/ProfileEdit/ProfileEditUI(Clone)/Root/ProfileArea/MainArea/RootView/ItemNameText").text}";
                    }

                    break;

                case Menus.SOLO:
                    
                    break;

                case Menus.SHOP:
                    BaseClass.searchShopDesc = true;
                    BaseClass.timer = 0f;
                    break;

                case Menus.Missions:

                    break;
            }

            Plugin.Log.LogInfo(copyText);
            if (Regex.IsMatch(copyText, BaseClass.SpecCharRegex, RegexOptions.IgnoreCase)) return;

            GUIUtility.systemCopyBuffer = copyText;
        }
    }

    [HarmonyPatch(typeof(SelectionButton), nameof(SelectionButton.OnSelected), MethodType.Normal)]
    class PatchOnSelected
    {
        static SelectionButton oldButton;
        public static GameObject BackButton;

        public static string copyText;

        [HarmonyPostfix]
        static void Postfix(SelectionButton __instance)
        {
            if (oldButton == __instance || !__instance.isSelected || __instance.name == "BackButton") return;

            if (!BackButton.activeSelf) PatchOnClick.currentMenu = PatchOnClick.Menus.NONE;

            switch (PatchOnClick.currentMenu)
            {
                case PatchOnClick.Menus.NONE:
                    try
                    {
                        if (__instance.name == "ButtonBanner") return;
                        copyText = BaseClass.FindExtendedTextElement(__instance.transform, true).text;
                    }
                    catch
                    {

                    }

                    break;
                case PatchOnClick.Menus.DUEL:
                    try
                    {
                        copyText = $"{(__instance.transform.parent.parent.name.Contains("View") ? "Chooice" : "Option")}: {BaseClass.FindExtendedTextElement(__instance.transform, true).text}";

                        if (__instance.transform.GetChild(4).gameObject.activeSelf || !__instance.transform.parent.transform.name.Contains("Content"))
                        {
                            copyText += $", {BaseClass.FindExtendedTextElement(__instance.transform.GetChild(3), true).text}";
                        }
                    }
                    catch
                    {
                        copyText = BaseClass.FindExtendedTextElement(__instance.transform, true).text;
                    }
                    break;

                case PatchOnClick.Menus.DECK:
                    try
                    {
                        if(__instance.transform.childCount >= 6)
                        {
                            if (__instance.transform.GetChild(6).gameObject.activeSelf && __instance.transform.GetChild(6).name.Contains("IconAddDeck")) return;
                        }
                        copyText = $"{(__instance.name == "Body" ? "Deck: " : "")}{BaseClass.FindExtendedTextElement(__instance.transform, true).text}";
                    }
                    catch
                    {
                        copyText = BaseClass.FindExtendedTextElement(__instance.transform.parent.parent.parent.parent.GetChild(0), true).text;
                    }

                    break;

                case PatchOnClick.Menus.SOLO:
                    try
                    {
                        copyText = BaseClass.FindExtendedTextElement(__instance.transform, true).text;
                    }
                    catch
                    {

                    }
                    break;

                case PatchOnClick.Menus.SHOP:
                    try
                    {
                        copyText = $"Shop item: {BaseClass.FindExtendedTextElement(__instance.transform.GetChild(2).GetChild(0).GetChild(0), true).text}";
                    }
                    catch
                    {
                        copyText = BaseClass.FindExtendedTextElement(__instance.transform, true).text;
                    }
                    break;
                case PatchOnClick.Menus.Missions:
                    try
                    {
                        copyText = $"{BaseClass.FindExtendedTextElement(__instance.transform.parent.parent.parent.parent.parent.parent.parent.parent.parent, true).text} - {BaseClass.FindExtendedTextElement(__instance.transform, true).text}";
                    }
                    catch
                    {
                        copyText = BaseClass.FindExtendedTextElement(__instance.transform, true).text;
                    }
                    break;
            }

            Plugin.Log.LogInfo(copyText);
            if (Regex.IsMatch(copyText, BaseClass.SpecCharRegex, RegexOptions.IgnoreCase)) return;

            GUIUtility.systemCopyBuffer = copyText;
            oldButton = __instance;
        }
    }

    [HarmonyPatch(typeof(DuelLP), nameof(DuelLP.ChangeLP), MethodType.Normal)]
    class PatchChangeLP
    {
        [HarmonyPostfix]
        static void Postfix(DuelLP __instance)
        {
            GUIUtility.systemCopyBuffer = $"{(__instance.name.Contains("Far") ? "Enemy" : "Your")} current life points: {__instance.currentLP}";
            Plugin.Log.LogInfo(GUIUtility.systemCopyBuffer);
        }
    }

    public class BaseClass : MonoBehaviour
    {
        SnapContentManager SnapContentManager;

        private RubyTextGX CardName;
        private ExtendedTextMeshProUGUI CardDescription;
        private ExtendedTextMeshProUGUI CardLink;
        private ExtendedTextMeshProUGUI CardStars;
        private ExtendedTextMeshProUGUI CardAtk;
        private ExtendedTextMeshProUGUI CardDef;
        private ExtendedTextMeshProUGUI CardPendulumScale;

        private string Owned;
        private string oldName;
        private string oldShopDesc;
        public static string SpecCharRegex = @"^\s*$|[^\w\s:\/'()"",&☆\-!]+\.?$";
        public static List<string> BanList = new() { "00:00", "NEW", "CANCEL", "COMPLETE!!", "CLEAR!", "OK" };

        private int oldPage = -1;
        private int clampedPage;

        public static float timer = 0f;

        private bool refreshCard;
        private bool cmExists;
        public static bool searchShopDesc;

        private void Update()
        {
            if(PatchOnSelected.BackButton == null)
            {
                PatchOnSelected.BackButton = GameObject.Find("UI/ContentCanvas/Header/HeaderUI(Clone)/Root/RootTop/SafeArea/BackButton/");
            }

            if (Input.GetKeyDown(KeyCode.E))
            {
                ResetVars();
            }

            if(SnapContentManager == null)
            {
                var _SnapContentManager = GameObject.Find("UI/OverlayCanvas/DialogManager/CardBrowser/CardBrowserUI(Clone)/Scroll View/");
                if (_SnapContentManager != null) { SnapContentManager = _SnapContentManager.GetComponent<SnapContentManager>(); cmExists = true; }
            }
            else
            {
                if (oldPage != SnapContentManager.currentPage) refreshCard = true;
            }

            if (CardName == null)
            {
                GetUITextElements();
            }
            else 
            {
                if (!GameObject.Find("UI/OverlayCanvas/DialogManager/CardBrowser/") && cmExists)
                {
                    cmExists = false;
                    SnapContentManager = null;
                    CardName = null;
                }
                if (oldName != CardName.text) refreshCard = true;
            }

            if (refreshCard)
            {
                RefreshActualCard();
                refreshCard = false;
            }

            if (searchShopDesc)
            {
                string copyText = "";

                timer += Time.deltaTime;

                if (timer < 2)
                {
                    GameObject Shop = GameObject.Find("UI/ContentCanvas/ContentManager/ShopBuy");

                    if (Shop != null)
                    {
                        GameObject DescTextGO = GameObject.Find("UI/ContentCanvas/ContentManager/ShopBuy/ShopBuyUI(Clone)/Root/Main/MenuArea/MenuAreaTop/");

                        ExtendedTextMeshProUGUI DescText = FindExtendedTextElementInChildren(DescTextGO.transform, false);

                        if (oldShopDesc != DescText.text)
                        {
                            copyText = $"{FindExtendedTextElement("UI/ContentCanvas/ContentManager/ShopBuy/ShopBuyUI(Clone)/Root/TitleSafeArea/TitleGroup/CategoryNameText").text} | {FindExtendedTextElement("UI/ContentCanvas/ContentManager/ShopBuy/ShopBuyUI(Clone)/Root/TitleSafeArea/TitleGroup/ProductNameGroup/ProductNameText").text}\n\n{DescText.text}";
                            oldShopDesc = DescText.text;
                        }
                    }
                }
                
                if(timer >= 2 || copyText != "")
                {
                    searchShopDesc = false;
                }

                if (String.IsNullOrWhiteSpace(copyText)) return;

                Plugin.Log.LogInfo(copyText);
                GUIUtility.systemCopyBuffer = copyText;
            }
        }

        private void RefreshActualCard()
        {
            try
            {
                GetUITextElements();
                string formattedText = FormatCardInfo();
                GUIUtility.systemCopyBuffer = formattedText;
                oldName = CardName.text;
                if (SnapContentManager != null) oldPage = SnapContentManager.currentPage;
                Plugin.Log.LogInfo(formattedText);
            }
            catch
            {
                ResetVars();
            }
        }

        private void ResetVars()
        {
            cmExists = false;
            refreshCard = false;
            searchShopDesc = false;
            timer = 0f;
            clampedPage = -1;
            oldPage = -1;
            oldName = "";
            oldShopDesc = "";
            Owned = "";
            SnapContentManager = null;
            CardName = null;
        }

        private void GetUITextElements()
        {
            if (SnapContentManager != null)
            {
                clampedPage = (SnapContentManager.currentPage % 3 + 3) % 3;

                Plugin.Log.LogInfo($"page {clampedPage}");

                CardName = FindUITextElement($"UI/OverlayCanvas/DialogManager/CardBrowser/CardBrowserUI(Clone)/Scroll View/Viewport/Content/Template(Clone){clampedPage}/CardInfoDetail_Browser(Clone)/Root/Window/StatusArea/TitleAreaGroup/TitleArea/NameArea/Viewport/TextCardName/");
                CardDescription = FindExtendedTextElement($"UI/OverlayCanvas/DialogManager/CardBrowser/CardBrowserUI(Clone)/Scroll View/Viewport/Content/Template(Clone){clampedPage}/CardInfoDetail_Browser(Clone)/Root/Window/StatusArea/DescriptionArea/TextArea/Viewport/TextDescriptionValue/");
                CardStars = FindExtendedTextElement($"UI/OverlayCanvas/DialogManager/CardBrowser/CardBrowserUI(Clone)/Scroll View/Viewport/Content/Template(Clone){clampedPage}/CardInfoDetail_Browser(Clone)/Root/Window/StatusArea/ParamatorArea/ParamatorAreaTop/TopLeftArea/IconLevel/TextLevel");
                CardAtk = FindExtendedTextElement($"UI/OverlayCanvas/DialogManager/CardBrowser/CardBrowserUI(Clone)/Scroll View/Viewport/Content/Template(Clone){clampedPage}/CardInfoDetail_Browser(Clone)/Root/Window/StatusArea/ParamatorArea/ParamatorAreaBottom/BottomLeftArea/IconAtk/TextAtk");
                CardDef = FindExtendedTextElement($"UI/OverlayCanvas/DialogManager/CardBrowser/CardBrowserUI(Clone)/Scroll View/Viewport/Content/Template(Clone){clampedPage}/CardInfoDetail_Browser(Clone)/Root/Window/StatusArea/ParamatorArea/ParamatorAreaBottom/BottomLeftArea/IconDef/TextDef");
                CardPendulumScale = FindExtendedTextElement($"UI/OverlayCanvas/DialogManager/CardBrowser/CardBrowserUI(Clone)/Scroll View/Viewport/Content/Template(Clone){clampedPage}/CardInfoDetail_Browser(Clone)/Root/Window/StatusArea/ParamatorArea/ParamatorAreaTop/TopLeftArea/IconPendulumScale/TextPendulumScale");
                CardLink = FindExtendedTextElement($"UI/OverlayCanvas/DialogManager/CardBrowser/CardBrowserUI(Clone)/Scroll View/Viewport/Content/Template(Clone){clampedPage}/CardInfoDetail_Browser(Clone)/Root/Window/StatusArea/ParamatorArea/ParamatorAreaTop/TopLeftArea/IconLink/TextLink");

                return;
            }

            if (GameObject.Find("UI/ContentCanvas/ContentManager/DeckEdit/"))
            {
                CardName = FindUITextElement("UI/ContentCanvas/ContentManager/DeckEdit/DeckEditUI(Clone)/CardDetail/Root/Window/TitleArea/PlateTitle/NameArea/Viewport/TextCardName/");
                CardDescription = FindExtendedTextElement("UI/ContentCanvas/ContentManager/DeckEdit/DeckEditUI(Clone)/CardDetail/Root/Window/DescriptionArea/TextArea/Viewport/TextDescriptionValueTMP/");
                CardStars = FindExtendedTextElement("UI/ContentCanvas/ContentManager/DeckEdit/DeckEditUI(Clone)/CardDetail/Root/Window/ParameterArea/IconLevel/TextLevelTMP");
                CardAtk = FindExtendedTextElement("UI/ContentCanvas/ContentManager/DeckEdit/DeckEditUI(Clone)/CardDetail/Root/Window/ParameterArea/IconAtk/TextAtkTMP");
                CardDef = FindExtendedTextElement("UI/ContentCanvas/ContentManager/DeckEdit/DeckEditUI(Clone)/CardDetail/Root/Window/ParameterArea/IconDef/TextDefTMP");
                CardPendulumScale = FindExtendedTextElement("UI/ContentCanvas/ContentManager/DeckEdit/DeckEditUI(Clone)/CardDetail/Root/Window/ParameterArea/IconPendulumScale/TextPendulumScaleTMP");
                CardLink = FindExtendedTextElement("UI/ContentCanvas/ContentManager/DeckEdit/DeckEditUI(Clone)/CardDetail/Root/Window/ParameterArea/IconLink/TextLinkTMP");

                Transform cardOwned = GameObject.Find("UI/ContentCanvas/ContentManager/DeckEdit/DeckEditUI(Clone)/CardDetail/Root/Window/ParameterArea/CardNumGroup/PremiumCardNumGroup/").transform;
                for (int i = 0; i < cardOwned.childCount; i++)
                {
                    if (i % 2 == 0) continue;
                    Owned += FindExtendedTextElement(cardOwned.GetChild(i), true).text + "/";
                }
            }
            else
            {
                CardName = FindUITextElement("UI/ContentCanvas/ContentManager/DuelClient/CardInfo/CardInfo(Clone)/Root/Window/TitleArea/NameArea/Viewport/TextCardName/");
                CardDescription = FindExtendedTextElement("UI/ContentCanvas/ContentManager/DuelClient/CardInfo/CardInfo(Clone)/Root/Window/DescriptionArea/TextArea/Viewport/TextDescriptionValue/");
                CardStars = FindExtendedTextElement("UI/ContentCanvas/ContentManager/DuelClient/CardInfo/CardInfo(Clone)/Root/Window/ParameterArea/IconLevel/TextLevel");
                CardAtk = FindExtendedTextElement("UI/ContentCanvas/ContentManager/DuelClient/CardInfo/CardInfo(Clone)/Root/Window/ParameterArea/IconAtk/TextAtk");
                CardDef = FindExtendedTextElement("UI/ContentCanvas/ContentManager/DuelClient/CardInfo/CardInfo(Clone)/Root/Window/ParameterArea/IconDef/TextDef");
                CardPendulumScale = FindExtendedTextElement("UI/ContentCanvas/ContentManager/DuelClient/CardInfo/CardInfo(Clone)/Root/Window/ParameterArea/PendulumScale/TextPendulumScale");
                CardLink = FindExtendedTextElement("UI/ContentCanvas/ContentManager/DuelClient/CardInfo/CardInfo(Clone)/Root/Window/ParameterArea/IconLink/TextLink");
            }
        }

        public static RubyTextGX FindUITextElement(string path)
        {
            return GameObject.Find(path)?.GetComponent<RubyTextGX>();
        }

        public static ExtendedTextMeshProUGUI FindExtendedTextElement(string path)
        {
            return GameObject.Find(path)?.GetComponent<ExtendedTextMeshProUGUI>();
        }

        public static RubyTextGX FindUITextElement(Transform obj, bool useRegex)
        {
            RubyTextGX textElement;

            if (obj.TryGetComponent(out textElement) && textElement != null)
            {
                if (!useRegex || (useRegex && !Regex.IsMatch(textElement.text, SpecCharRegex)) && textElement.gameObject.activeInHierarchy && !BanList.Contains(textElement.text))
                {
                    return textElement;
                }
            }

            textElement = FindUITextElementInChildren(obj, true);

            if (textElement != null)
            {
                return textElement;
            }

            Transform parent = obj.parent;
            while (parent != null)
            {
                if (parent.TryGetComponent(out textElement) && textElement != null)
                {
                    if (!useRegex || (useRegex && !Regex.IsMatch(textElement.text, SpecCharRegex)) && textElement.gameObject.activeInHierarchy && !BanList.Contains(textElement.text))
                    {
                        return textElement;
                    }
                }
                parent = parent.parent;
            }

            return null;
        }

        public static ExtendedTextMeshProUGUI FindExtendedTextElement(Transform obj, bool useRegex)
        {
            ExtendedTextMeshProUGUI textElement;

            if (obj.TryGetComponent(out textElement) && textElement != null)
            {
                if (!useRegex || (useRegex && !Regex.IsMatch(textElement.text, SpecCharRegex)) && textElement.gameObject.activeInHierarchy && !BanList.Contains(textElement.text))
                {
                    return textElement;
                }
            }

            textElement = FindExtendedTextElementInChildren(obj, useRegex);

            if (textElement != null)
            {
                return textElement;
            }

            Transform parent = obj.parent;
            while (parent != null)
            {
                if (parent.TryGetComponent(out textElement) && textElement != null)
                {
                    if (!useRegex || (useRegex && !Regex.IsMatch(textElement.text, SpecCharRegex)) && textElement.gameObject.activeInHierarchy && !BanList.Contains(textElement.text))
                    {
                        return textElement;
                    }
                }

                parent = parent.parent;
            }

            return null;
        }

        public static ExtendedTextMeshProUGUI FindExtendedTextElementInChildren(Transform obj, bool useRegex)
        {
            ExtendedTextMeshProUGUI textElement;

            for (int i = 0; i < obj.childCount; i++)
            {
                textElement = FindExtendedTextElement(obj.GetChild(i), useRegex);
                if (textElement != null)
                {
                    return textElement;
                }
            }

            return FindUITextElementInChildren(obj, useRegex);
        }

        public static RubyTextGX FindUITextElementInChildren(Transform obj, bool useRegex)
        {
            RubyTextGX textElement;

            for (int i = 0; i < obj.childCount; i++)
            {
                textElement = FindUITextElement(obj.GetChild(i), useRegex);
                if (textElement != null)
                {
                    return textElement;
                }
            }

            return null;
        }

        private string FormatCardInfo()
        {
            if (CardName.text.IsNullOrWhiteSpace()) return "";
            if (Owned.Length > 5) Owned = Owned.Substring(Owned.Length - 6);
            string cardNameText = $"Name: {CardName.text}";
            string cardOwnedText = Owned.Length > 2 ? $"Owned: {Owned.Remove(Owned.Length - 1)}" : "";
            string cardStarsText = CardStars != null & CardStars.transform.parent.gameObject.activeSelf ? $"Stars: {CardStars.text}" : "";
            string cardLinkText = CardLink != null & CardLink.transform.parent.gameObject.activeSelf ? $"Link level: {CardLink.text}" : "";
            string cardPendulumScaleText = CardPendulumScale != null & CardPendulumScale.transform.parent.gameObject.activeSelf ? $"Pendulum scale: {CardPendulumScale.text}" : "";
            string cardAtkText = CardAtk != null & CardAtk.transform.parent.gameObject.activeSelf ? $"Attack: {CardAtk.text}" : "";
            string cardDefText = CardDef != null & CardDef.transform.parent.gameObject.activeSelf ? $"Defense: {CardDef.text}" : "";
            string cardDescriptionText = CardDescription != null ? $"Description: {CardDescription.text}" : "";

            string formattedText = $"{cardNameText}\n{cardOwnedText}\n{cardStarsText}\n{cardLinkText}\n{cardPendulumScaleText}\n{cardAtkText}\n{cardDefText}\n{cardDescriptionText}";
            Owned = "";
            return Regex.Replace(formattedText, @"<[^>]+>|&nbsp;", "").Trim();
        }
    }
}
