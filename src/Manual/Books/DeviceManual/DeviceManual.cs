using FluxForge.Mod;
using System;
using System.IO;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using FluxForge.Events;

namespace FluxForge.Manual
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
            GameObject coldTrapAdvanced = ColdTrapAdvancedPage.Bind(pagesRoot);
            GameObject coldTrapAutomation = ColdTrapAdvancedPage.BindAutomation(pagesRoot);
            view.pages = new[]
            {
                OverviewPage.Bind(pagesRoot),
                EmitterPage.Bind(pagesRoot),
                FilterPage.Bind(pagesRoot),
                ColdTrapPage.Bind(pagesRoot),
                coldTrapAdvanced,
                coldTrapAutomation,
                CentrifugePage.Bind(pagesRoot)
            };
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

                text.text = ModHelper.GetLocalized(ModId, LocalizationKey(text.gameObject.name));
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
