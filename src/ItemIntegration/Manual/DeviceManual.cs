using System;
using System.IO;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using FluxForge.Events;

namespace FluxForge.Mod
{
    public sealed class DeviceManual : MonoBehaviour
    {
        private const string ModId = "fluxforge";
        private const string AssetBundlePath = "AssetBundles/fluxforge_device_manual";
        private const string PrefabName = "FlowDeviceBasicManual";

        private static AssetBundle assetBundle;
        private static GameObject prefab;
        private static DeviceManual current;

        private MultiPageUIManager manager;
        private GameObject[] pages;
        private Button previousButton;
        private Button nextButton;
        private TextMeshProUGUI pageNumber;
        private int currentPage;

        public static void Open()
        {
            MultiPageUIManager uiManager = MultiPageUIManager.Instance;
            uiManager.Reset();
            uiManager.panel.SetActive(false);

            if (current == null)
            {
                CreateBook(uiManager);
            }

            current.manager = uiManager;
            current.ShowPage(0);
            current.gameObject.SetActive(true);
            PaperUIManager.Instance.audioSourcePickup.Play();
            UIFamilyLayer.BringPanelsToFront();
        }

        private static void CreateBook(MultiPageUIManager uiManager)
        {
            if (assetBundle == null)
            {
                assetBundle = ModHelper.LoadModAssetBundle(ModId, AssetBundlePath);
                if (assetBundle == null)
                {
                    throw new FileNotFoundException(
                        "FluxForge 手册 AssetBundle 加载失败：" + AssetBundlePath);
                }

                prefab = assetBundle.LoadAsset<GameObject>(PrefabName);
                if (prefab == null)
                {
                    throw new InvalidDataException(
                        "FluxForge 手册 AssetBundle 中缺少 Prefab：" + PrefabName);
                }
            }

            GameObject sourceBook = uiManager.books[3];
            RectTransform sourceRect = (RectTransform)uiManager.panel.transform;
            Transform host = uiManager.panel.transform.parent;
            GameObject instance = Instantiate(prefab, host, false);
            instance.name = "FluxForgeDeviceGuide";

            RectTransform instanceRect = (RectTransform)instance.transform;
            instanceRect.anchorMin = sourceRect.anchorMin;
            instanceRect.anchorMax = sourceRect.anchorMax;
            instanceRect.pivot = sourceRect.pivot;
            instanceRect.sizeDelta = sourceRect.sizeDelta;
            instanceRect.anchoredPosition = sourceRect.anchoredPosition;
            instanceRect.localScale = sourceRect.localScale;

            DeviceManual view = instance.AddComponent<DeviceManual>();
            view.manager = uiManager;
            Transform pagesRoot = instance.transform.Find("Pages");
            view.pages = new[]
            {
                pagesRoot.Find("Page_overview").gameObject,
                pagesRoot.Find("Page_emitter").gameObject,
                pagesRoot.Find("Page_filter").gameObject,
                pagesRoot.Find("Page_coldtrap").gameObject
            };
            BindEmitterDemos(view.pages[1]);
            BindDemo(view.pages[2], "Demo_filter_loop", FlowEventApi.RunFilterLoop);
            BindDemo(view.pages[3], "Demo_cold_trap_loop", FlowEventApi.RunColdTrapLoop);
            view.previousButton = instance.transform.Find("PreviousButton").GetComponent<Button>();
            view.nextButton = instance.transform.Find("NextButton").GetComponent<Button>();
            view.pageNumber = instance.transform.Find("PageNumber").GetComponent<TextMeshProUGUI>();

            TMP_FontAsset gameFont = FontLoader.Instance.GetFontByRole(
                FontLoader.FontRole.Noto_Sans,
                UnityEngine.Localization.Settings.LocalizationSettings.SelectedLocale);
            foreach (TMP_Text text in instance.GetComponentsInChildren<TMP_Text>(true))
            {
                text.font = gameFont;
                if (text == view.pageNumber)
                {
                    continue;
                }

                text.text = ChineseManualText(LocalizationKey(text.gameObject.name));
            }

            view.previousButton.onClick.AddListener(view.FlipLeft);
            view.nextButton.onClick.AddListener(view.FlipRight);
            instance.transform.Find("CloseButton").GetComponent<Button>()
                .onClick.AddListener(uiManager.CloseUI);

            StoreUI storeUI = instance.AddComponent<StoreUI>();
            storeUI.isPaperUI = true;
            storeUI.zoomable = true;
            storeUI.identifier = "fluxforge_device_manual";
            instance.transform.Find("WindowButton").GetComponent<Button>()
                .onClick.AddListener(storeUI.ToggleZoom);
            instance.AddComponent<UIDragHandler>();

            current = view;
        }

        private static string LocalizationKey(string objectName)
        {
            return objectName switch
            {
                "account" => "manual.overview.recipe_a",
                "account (1)" => "manual.overview.recipe_b",
                _ => objectName
            };
        }

        private static string ChineseManualText(string key)
        {
            LoadedMod mod = ModLoader.Mods.Find(loaded => loaded.Manifest.ID == ModId);
            return mod.Localization["zh-CN"][key];
        }

        private static void BindDemo(
            GameObject page,
            string demoName,
            Func<GameGridInventory, bool> run)
        {
            Transform demo = page.transform.Find(demoName);
            ManualDemoPage demoPage = demo.gameObject.AddComponent<ManualDemoPage>();
            demoPage.Configure(
                demo.Find("GridArea").GetComponent<RectTransform>(),
                demo.Find("GridArea/manual.demo.placeholder").gameObject,
                run);
            demo.Find("PlayButton").GetComponent<Button>().onClick.AddListener(demoPage.Play);
        }

        private static void BindEmitterDemos(GameObject page)
        {
            Transform demo = page.transform.Find("Demo_boundary_emitter");
            ManualDemoPage demoPage = demo.gameObject.AddComponent<ManualDemoPage>();
            demoPage.Configure(
                demo.Find("GridArea").GetComponent<RectTransform>(),
                demo.Find("GridArea/manual.demo.placeholder").gameObject,
                FlowEventApi.RunBoundaryEmitter);
            demo.Find("PlayButton1").GetComponent<Button>()
                .onClick.AddListener(demoPage.PlayBoundaryEmitter);
            demo.Find("PlayButton2").GetComponent<Button>()
                .onClick.AddListener(demoPage.PlayTwoEmitterRelay);
        }

        private void ShowPage(int pageIndex)
        {
            currentPage = pageIndex;
            for (int i = 0; i < pages.Length; i++)
            {
                pages[i].SetActive(i == currentPage);
            }

            previousButton.gameObject.SetActive(currentPage > 0);
            nextButton.gameObject.SetActive(currentPage < pages.Length - 1);
            pageNumber.text = $"{currentPage + 1} / {pages.Length}";
        }

        private void FlipLeft()
        {
            EmporiumAudioPlayer.Instance.flip1.Play();
            ShowPage(currentPage - 1);
        }

        private void FlipRight()
        {
            EmporiumAudioPlayer.Instance.flip1.Play();
            ShowPage(currentPage + 1);
        }

        internal static void CloseCurrent()
        {
            if (current == null)
            {
                return;
            }

            DeviceManual closing = current;
            current = null;
            closing.gameObject.SetActive(false);
            Destroy(closing.gameObject);

            if (closing.manager.panel.activeSelf)
            {
                closing.manager.CloseUI();
            }
        }

        internal static void CloseForVanillaManual()
        {
            if (current == null)
            {
                return;
            }

            DeviceManual closing = current;
            closing.gameObject.SetActive(false);
            Destroy(closing.gameObject);
            current = null;
        }

        internal static void Shutdown()
        {
            CloseCurrent();
            assetBundle?.Unload(false);
            assetBundle = null;
            prefab = null;
        }
    }
}
